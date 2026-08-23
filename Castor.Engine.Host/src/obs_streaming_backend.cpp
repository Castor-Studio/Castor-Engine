#include "obs_streaming_backend.h"

#include <algorithm>
#include <cstdarg>
#include <cstdio>
#include <obs.h>
#include <vector>

namespace castor::engine::detail
{
namespace
{
constexpr const char* service_id = "rtmp_custom";
constexpr const char* output_id = "rtmp_output";

void forward_log(log_handler_t handler, void* parameter, int level, const char* format, ...)
{
    if (handler == nullptr)
    {
        return;
    }
    va_list arguments;
    va_start(arguments, format);
    handler(level, format, arguments, parameter);
    va_end(arguments);
}

void redact_value(std::string& message, const std::string& secret)
{
    if (secret.empty())
    {
        return;
    }
    size_t position = 0;
    while ((position = message.find(secret, position)) != std::string::npos)
    {
        message.replace(position, secret.size(), "[REDACTED]");
        position += 10;
    }
}
} // namespace

bool obs_streaming_backend::service_available() noexcept
{
    return obs_service_get_module(service_id) != nullptr;
}

bool obs_streaming_backend::output_available() noexcept
{
    return obs_get_output_flags(output_id) != 0;
}

void* obs_streaming_backend::create_service(const streaming_configuration& config) noexcept
{
    install_log_filter(config);
    obs_data_t* settings = obs_data_create();
    obs_data_set_string(settings, "server", config.server_url.c_str());
    obs_data_set_string(settings, "key", config.stream_key.c_str());
    obs_data_set_bool(settings, "use_auth", config.use_authentication);
    if (config.use_authentication)
    {
        obs_data_set_string(settings, "username", config.username.c_str());
        obs_data_set_string(settings, "password", config.password.c_str());
    }
    obs_service_t* service = obs_service_create(service_id, "castor-streaming-service", settings, nullptr);
    obs_data_release(settings);
    if (service == nullptr)
    {
        uninstall_log_filter();
    }
    return service;
}

void obs_streaming_backend::release_service(void* service) noexcept
{
    obs_service_release(static_cast<obs_service_t*>(service));
    uninstall_log_filter();
}

void* obs_streaming_backend::create_output() noexcept
{
    return obs_output_create(output_id, "castor-streaming-output", nullptr, nullptr);
}

void obs_streaming_backend::release_output(void* output) noexcept
{
    obs_output_release(static_cast<obs_output_t*>(output));
}

bool obs_streaming_backend::attach(void* output, void* service, void* video_encoder, void* audio_encoder) noexcept
{
    auto* native_output = static_cast<obs_output_t*>(output);
    auto* native_service = static_cast<obs_service_t*>(service);
    auto* native_video_encoder = static_cast<obs_encoder_t*>(video_encoder);
    auto* native_audio_encoder = static_cast<obs_encoder_t*>(audio_encoder);
    obs_output_set_service(native_output, native_service);
    obs_output_set_video_encoder(native_output, native_video_encoder);
    obs_output_set_audio_encoder(native_output, native_audio_encoder, 0);
    return obs_output_get_service(native_output) == native_service &&
           obs_output_get_video_encoder(native_output) == native_video_encoder &&
           obs_output_get_audio_encoder(native_output, 0) == native_audio_encoder;
}

void obs_streaming_backend::set_reconnect_settings(void* output, uint32_t retry_count, uint32_t delay_seconds) noexcept
{
    obs_output_set_reconnect_settings(static_cast<obs_output_t*>(output), static_cast<int>(retry_count),
                                      static_cast<int>(delay_seconds));
}

void obs_streaming_backend::connect_events(void* output, streaming_event_callback callback, void* data) noexcept
{
    auto* native_output = static_cast<obs_output_t*>(output);
    {
        std::scoped_lock lock(callback_mutex_);
        connected_output_ = output;
        callback_ = callback;
        callback_data_ = data;
    }
    signal_handler_t* handler = obs_output_get_signal_handler(native_output);
    signal_handler_connect(handler, "starting", on_starting, this);
    signal_handler_connect(handler, "start", on_start, this);
    signal_handler_connect(handler, "reconnect", on_reconnect, this);
    signal_handler_connect(handler, "reconnect_success", on_reconnect_success, this);
    signal_handler_connect(handler, "stop", on_stop, this);
}

void obs_streaming_backend::disconnect_events(void* output) noexcept
{
    {
        std::scoped_lock lock(callback_mutex_);
        if (output == nullptr || output != connected_output_)
        {
            return;
        }
    }
    signal_handler_t* handler = obs_output_get_signal_handler(static_cast<obs_output_t*>(output));
    signal_handler_disconnect(handler, "starting", on_starting, this);
    signal_handler_disconnect(handler, "start", on_start, this);
    signal_handler_disconnect(handler, "reconnect", on_reconnect, this);
    signal_handler_disconnect(handler, "reconnect_success", on_reconnect_success, this);
    signal_handler_disconnect(handler, "stop", on_stop, this);
    std::scoped_lock lock(callback_mutex_);
    connected_output_ = nullptr;
    callback_ = nullptr;
    callback_data_ = nullptr;
}

bool obs_streaming_backend::start(void* output) noexcept
{
    return obs_output_start(static_cast<obs_output_t*>(output));
}

void obs_streaming_backend::stop(void* output) noexcept
{
    obs_output_stop(static_cast<obs_output_t*>(output));
}

void obs_streaming_backend::force_stop(void* output) noexcept
{
    obs_output_force_stop(static_cast<obs_output_t*>(output));
}

bool obs_streaming_backend::active(void* output) noexcept
{
    return obs_output_active(static_cast<obs_output_t*>(output));
}

uint64_t obs_streaming_backend::total_frames(void* output) noexcept
{
    return static_cast<uint64_t>(std::max(obs_output_get_total_frames(static_cast<obs_output_t*>(output)), 0));
}

uint64_t obs_streaming_backend::dropped_frames(void* output) noexcept
{
    return static_cast<uint64_t>(std::max(obs_output_get_frames_dropped(static_cast<obs_output_t*>(output)), 0));
}

std::string obs_streaming_backend::last_error(void* output) noexcept
{
    const char* error = output == nullptr ? nullptr : obs_output_get_last_error(static_cast<obs_output_t*>(output));
    return error == nullptr ? std::string{} : std::string(error);
}

void obs_streaming_backend::on_starting(void* data, calldata_t*)
{
    static_cast<obs_streaming_backend*>(data)->emit(streaming_event::starting);
}

void obs_streaming_backend::on_start(void* data, calldata_t*)
{
    static_cast<obs_streaming_backend*>(data)->emit(streaming_event::started);
}

void obs_streaming_backend::on_reconnect(void* data, calldata_t*)
{
    static_cast<obs_streaming_backend*>(data)->emit(streaming_event::reconnecting);
}

void obs_streaming_backend::on_reconnect_success(void* data, calldata_t*)
{
    static_cast<obs_streaming_backend*>(data)->emit(streaming_event::reconnected);
}

void obs_streaming_backend::on_stop(void* data, calldata_t* calldata)
{
    const int code = static_cast<int>(calldata_int(calldata, "code"));
    static_cast<obs_streaming_backend*>(data)->emit(streaming_event::stopped, translate_stop_code(code));
}

streaming_stop_reason obs_streaming_backend::translate_stop_code(int code) noexcept
{
    switch (code)
    {
    case OBS_OUTPUT_SUCCESS:
        return streaming_stop_reason::success;
    case OBS_OUTPUT_BAD_PATH:
        return streaming_stop_reason::bad_path;
    case OBS_OUTPUT_CONNECT_FAILED:
        return streaming_stop_reason::connect_failed;
    case OBS_OUTPUT_INVALID_STREAM:
        return streaming_stop_reason::invalid_stream;
    case OBS_OUTPUT_ERROR:
        return streaming_stop_reason::error;
    case OBS_OUTPUT_DISCONNECTED:
        return streaming_stop_reason::disconnected;
    case OBS_OUTPUT_UNSUPPORTED:
        return streaming_stop_reason::unsupported;
    case OBS_OUTPUT_NO_SPACE:
        return streaming_stop_reason::no_space;
    case OBS_OUTPUT_ENCODE_ERROR:
        return streaming_stop_reason::encode_error;
    default:
        return streaming_stop_reason::unknown;
    }
}

void obs_streaming_backend::emit(streaming_event event, streaming_stop_reason reason) noexcept
{
    streaming_event_callback callback = nullptr;
    void* data = nullptr;
    {
        std::scoped_lock lock(callback_mutex_);
        callback = callback_;
        data = callback_data_;
    }
    if (callback != nullptr)
    {
        callback(data, event, reason);
    }
}

void obs_streaming_backend::on_log(int level, const char* format, va_list arguments, void* data)
{
    auto* backend = static_cast<obs_streaming_backend*>(data);
    va_list copy;
    va_copy(copy, arguments);
    const int required = std::vsnprintf(nullptr, 0, format, copy);
    va_end(copy);
    if (required < 0)
    {
        return;
    }
    std::vector<char> buffer(static_cast<size_t>(required) + 1);
    std::vsnprintf(buffer.data(), buffer.size(), format, arguments);
    std::string message(buffer.data(), static_cast<size_t>(required));

    log_handler_t previous = nullptr;
    void* parameter = nullptr;
    {
        std::scoped_lock lock(backend->log_mutex_);
        redact_value(message, backend->stream_key_);
        redact_value(message, backend->password_);
        previous = backend->previous_log_handler_;
        parameter = backend->previous_log_parameter_;
    }
    forward_log(previous, parameter, level, "%s", message.c_str());
}

void obs_streaming_backend::install_log_filter(const streaming_configuration& config) noexcept
{
    std::scoped_lock lock(log_mutex_);
    stream_key_ = config.stream_key;
    password_ = config.password;
    if (!log_filter_installed_)
    {
        base_get_log_handler(&previous_log_handler_, &previous_log_parameter_);
        base_set_log_handler(on_log, this);
        log_filter_installed_ = true;
    }
}

void obs_streaming_backend::uninstall_log_filter() noexcept
{
    std::scoped_lock lock(log_mutex_);
    if (log_filter_installed_)
    {
        base_set_log_handler(previous_log_handler_, previous_log_parameter_);
        log_filter_installed_ = false;
    }
    std::fill(stream_key_.begin(), stream_key_.end(), '\0');
    std::fill(password_.begin(), password_.end(), '\0');
    stream_key_.clear();
    password_.clear();
    previous_log_handler_ = nullptr;
    previous_log_parameter_ = nullptr;
}
} // namespace castor::engine::detail
