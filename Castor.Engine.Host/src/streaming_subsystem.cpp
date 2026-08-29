#include "streaming_subsystem.h"

#include <algorithm>
#include <chrono>
#include <cstring>
#include <utility>

namespace castor::engine::detail
{
namespace
{
constexpr std::chrono::seconds graceful_stop_timeout{10};
constexpr std::chrono::seconds forced_stop_timeout{2};

streaming_lifecycle_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}

bool active_state(castor_engine_streaming_state_t state)
{
    return state == CASTOR_ENGINE_STREAMING_CONNECTING || state == CASTOR_ENGINE_STREAMING_LIVE ||
           state == CASTOR_ENGINE_STREAMING_RECONNECTING || state == CASTOR_ENGINE_STREAMING_STOPPING;
}

void replace_all(std::string& value, const std::string& secret)
{
    if (secret.empty())
    {
        return;
    }
    size_t position = 0;
    while ((position = value.find(secret, position)) != std::string::npos)
    {
        value.replace(position, secret.size(), "[REDACTED]");
        position += 10;
    }
}
} // namespace

streaming_subsystem::streaming_subsystem(streaming_backend& backend) noexcept : backend_(backend)
{
}

streaming_subsystem::~streaming_subsystem()
{
    reset();
}

streaming_lifecycle_result streaming_subsystem::configure(const castor_engine_streaming_config_t* config,
                                                          bool runtime_ready)
{
    const streaming_configuration_result validation = validate_streaming_config(config);
    if (validation.code != CASTOR_ENGINE_OK)
    {
        return {validation.code, validation.message};
    }
    if (!runtime_ready)
    {
        return failure(CASTOR_ENGINE_NOT_INITIALIZED,
                       "The engine and its OBS modules must be initialized before streaming can be configured.");
    }
    if (!backend_.service_available())
    {
        return failure(CASTOR_ENGINE_STREAMING_SERVICE_UNAVAILABLE,
                       "The packaged OBS custom RTMP service is not available.");
    }
    if (!backend_.output_available())
    {
        return failure(CASTOR_ENGINE_STREAMING_OUTPUT_UNAVAILABLE, "The packaged OBS RTMP output is not available.");
    }

    streaming_configuration requested = copy_streaming_config(*config);
    {
        std::scoped_lock lock(mutex_);
        if (configured_ && streaming_configs_match(config_, requested) && state_ != CASTOR_ENGINE_STREAMING_FAILED)
        {
            return {CASTOR_ENGINE_OK, {}};
        }
        if (active_state(state_))
        {
            return failure(CASTOR_ENGINE_STREAMING_RECONFIGURATION_WHILE_ACTIVE,
                           "The streaming destination cannot be changed while streaming is active.");
        }
    }

    release_resources();
    {
        std::scoped_lock lock(mutex_);
        if (output_ != nullptr)
        {
            return failure(CASTOR_ENGINE_STREAMING_STOP_TIMEOUT,
                           "The previous RTMP output is still terminating and cannot be reconfigured yet.");
        }
    }
    std::scoped_lock lock(mutex_);
    clear_secret(config_.stream_key);
    clear_secret(config_.password);
    config_ = std::move(requested);
    configured_ = true;
    state_ = CASTOR_ENGINE_STREAMING_IDLE;
    failure_code_ = CASTOR_ENGINE_OK;
    failure_message_.clear();
    return {CASTOR_ENGINE_OK, {}};
}

streaming_lifecycle_result streaming_subsystem::start(bool runtime_ready, bool video_ready, bool audio_ready,
                                                      bool scene_active, void* video_encoder, void* audio_encoder)
{
    {
        std::scoped_lock lock(mutex_);
        if (active_state(state_))
        {
            return failure(CASTOR_ENGINE_STREAMING_ALREADY_ACTIVE,
                           "Streaming is already active or is currently stopping.");
        }
        if (!configured_)
        {
            return failure(CASTOR_ENGINE_STREAMING_NOT_CONFIGURED,
                           "Configure a streaming destination before starting streaming.");
        }
    }
    if (!runtime_ready)
    {
        return failure(CASTOR_ENGINE_NOT_INITIALIZED, "The engine must be initialized before streaming can start.");
    }
    if (!video_ready)
    {
        return failure(CASTOR_ENGINE_VIDEO_NOT_CONFIGURED,
                       "The video subsystem must be configured before streaming can start.");
    }
    if (!audio_ready)
    {
        return failure(CASTOR_ENGINE_AUDIO_NOT_CONFIGURED,
                       "The audio subsystem must be configured before streaming can start.");
    }
    if (video_encoder == nullptr || audio_encoder == nullptr)
    {
        return failure(CASTOR_ENGINE_STREAMING_ENCODERS_NOT_CONFIGURED,
                       "The shared video and audio encoders must be configured before streaming can start.");
    }
    if (!scene_active)
    {
        return failure(CASTOR_ENGINE_STREAMING_NO_ACTIVE_SCENE, "A scene must be active before streaming can start.");
    }

    release_resources();
    streaming_configuration snapshot;
    {
        std::scoped_lock lock(mutex_);
        if (output_ != nullptr)
        {
            return failure(CASTOR_ENGINE_STREAMING_STOP_TIMEOUT,
                           "The previous RTMP output is still terminating and cannot be restarted yet.");
        }
        snapshot = config_;
        state_ = CASTOR_ENGINE_STREAMING_CONNECTING;
        failure_code_ = CASTOR_ENGINE_OK;
        failure_message_.clear();
        reconnect_seen_ = false;
    }

    service_ = backend_.create_service(snapshot);
    if (service_ == nullptr)
    {
        set_failure(CASTOR_ENGINE_STREAMING_SERVICE_CREATION_FAILED, "OBS failed to create the custom RTMP service.");
        return failure(CASTOR_ENGINE_STREAMING_SERVICE_CREATION_FAILED,
                       "OBS failed to create the custom RTMP service.");
    }
    output_ = backend_.create_output();
    if (output_ == nullptr)
    {
        set_failure(CASTOR_ENGINE_STREAMING_OUTPUT_CREATION_FAILED, "OBS failed to create the RTMP output.");
        release_resources();
        return failure(CASTOR_ENGINE_STREAMING_OUTPUT_CREATION_FAILED, "OBS failed to create the RTMP output.");
    }
    if (!backend_.attach(output_, service_, video_encoder, audio_encoder))
    {
        set_failure(CASTOR_ENGINE_STREAMING_ENCODERS_NOT_CONFIGURED,
                    "OBS failed to attach the RTMP service or shared encoders to the output.");
        release_resources();
        return failure(CASTOR_ENGINE_STREAMING_ENCODERS_NOT_CONFIGURED,
                       "OBS failed to attach the RTMP service or shared encoders to the output.");
    }

    backend_.set_reconnect_settings(output_, snapshot.reconnect_retry_count, snapshot.reconnect_delay_seconds);
    backend_.connect_events(output_, handle_event, this);
    if (!backend_.start(output_))
    {
        std::string message = "OBS failed to start the RTMP output.";
        const std::string obs_error = redact(backend_.last_error(output_));
        if (!obs_error.empty())
        {
            message += " OBS reported: " + obs_error;
        }
        set_failure(CASTOR_ENGINE_STREAMING_START_FAILED, message);
        release_resources();
        return failure(CASTOR_ENGINE_STREAMING_START_FAILED, std::move(message));
    }

    return {CASTOR_ENGINE_OK, {}};
}

streaming_lifecycle_result streaming_subsystem::stop()
{
    void* output = nullptr;
    {
        std::scoped_lock lock(mutex_);
        if (!active_state(state_) || state_ == CASTOR_ENGINE_STREAMING_STOPPING)
        {
            return failure(CASTOR_ENGINE_STREAMING_NOT_ACTIVE, "No streaming session is active.");
        }
        state_ = CASTOR_ENGINE_STREAMING_STOPPING;
        output = output_;
    }

    backend_.stop(output);
    {
        std::unique_lock lock(mutex_);
        stopped_condition_.wait_for(lock, graceful_stop_timeout, [this] { return !active_state(state_); });
    }
    if (backend_.active(output))
    {
        backend_.force_stop(output);
        std::unique_lock lock(mutex_);
        stopped_condition_.wait_for(lock, forced_stop_timeout, [this] { return !active_state(state_); });
    }
    if (backend_.active(output))
    {
        const std::string message = "The RTMP output did not terminate after a forced stop request.";
        set_failure(CASTOR_ENGINE_STREAMING_STOP_TIMEOUT, message);
        return failure(CASTOR_ENGINE_STREAMING_STOP_TIMEOUT, message);
    }

    release_resources();
    return {CASTOR_ENGINE_OK, {}};
}

streaming_lifecycle_result streaming_subsystem::get_status(castor_engine_streaming_status_t* out_status) noexcept
{
    if (out_status == nullptr || out_status->struct_size < sizeof(castor_engine_streaming_status_t))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT,
                       "The streaming status pointer must be non-null and its structure large enough.");
    }
    std::scoped_lock lock(mutex_);
    out_status->struct_size = sizeof(*out_status);
    out_status->state = state_;
    out_status->last_failure_code = failure_code_;
    std::memset(out_status->last_failure_message, 0, sizeof(out_status->last_failure_message));
    const size_t size = std::min(failure_message_.size(), sizeof(out_status->last_failure_message) - 1);
    std::memcpy(out_status->last_failure_message, failure_message_.data(), size);
    return {CASTOR_ENGINE_OK, {}};
}

streaming_lifecycle_result streaming_subsystem::get_health(castor_engine_streaming_health_t* out_health) noexcept
{
    if (out_health == nullptr || out_health->struct_size < sizeof(castor_engine_streaming_health_t))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT,
                       "The streaming health pointer must be non-null and its structure large enough.");
    }
    void* output = nullptr;
    {
        std::scoped_lock lock(mutex_);
        if (!active_state(state_) || output_ == nullptr)
        {
            return failure(CASTOR_ENGINE_STREAMING_NOT_ACTIVE,
                           "Streaming health is only available during an active session.");
        }
        output = output_;
    }
    out_health->struct_size = sizeof(*out_health);
    out_health->total_frames = backend_.total_frames(output);
    out_health->dropped_frames = backend_.dropped_frames(output);
    return {CASTOR_ENGINE_OK, {}};
}

bool streaming_subsystem::is_active() noexcept
{
    std::scoped_lock lock(mutex_);
    return active_state(state_);
}

bool streaming_subsystem::reset() noexcept
{
    if (is_active())
    {
        stop();
    }
    {
        std::scoped_lock lock(mutex_);
        if (output_ != nullptr && backend_.active(output_))
        {
            return false;
        }
    }
    release_resources();
    std::scoped_lock lock(mutex_);
    clear_secret(config_.stream_key);
    clear_secret(config_.password);
    config_ = {};
    configured_ = false;
    state_ = CASTOR_ENGINE_STREAMING_IDLE;
    failure_code_ = CASTOR_ENGINE_OK;
    failure_message_.clear();
    reconnect_seen_ = false;
    return true;
}

void streaming_subsystem::handle_event(void* data, streaming_event event, streaming_stop_reason reason)
{
    static_cast<streaming_subsystem*>(data)->on_event(event, reason);
}

void streaming_subsystem::on_event(streaming_event event, streaming_stop_reason reason)
{
    std::scoped_lock lock(mutex_);
    switch (event)
    {
    case streaming_event::starting:
        state_ = CASTOR_ENGINE_STREAMING_CONNECTING;
        break;
    case streaming_event::started:
        state_ = CASTOR_ENGINE_STREAMING_LIVE;
        break;
    case streaming_event::reconnecting:
        state_ = CASTOR_ENGINE_STREAMING_RECONNECTING;
        reconnect_seen_ = true;
        break;
    case streaming_event::reconnected:
        state_ = CASTOR_ENGINE_STREAMING_LIVE;
        break;
    case streaming_event::stopped:
        if (reason == streaming_stop_reason::success && state_ == CASTOR_ENGINE_STREAMING_STOPPING)
        {
            state_ = CASTOR_ENGINE_STREAMING_IDLE;
        }
        else if (reason == streaming_stop_reason::success)
        {
            state_ = CASTOR_ENGINE_STREAMING_IDLE;
        }
        else
        {
            castor_engine_result_t code = CASTOR_ENGINE_STREAMING_OUTPUT_ERROR;
            switch (reason)
            {
            case streaming_stop_reason::connect_failed:
                code = reconnect_seen_ ? CASTOR_ENGINE_STREAMING_RECONNECT_EXHAUSTED
                                       : CASTOR_ENGINE_STREAMING_CONNECTION_FAILED;
                break;
            case streaming_stop_reason::invalid_stream:
                code = CASTOR_ENGINE_STREAMING_STREAM_REJECTED;
                break;
            case streaming_stop_reason::disconnected:
                code = reconnect_seen_ ? CASTOR_ENGINE_STREAMING_RECONNECT_EXHAUSTED
                                       : CASTOR_ENGINE_STREAMING_DISCONNECTED;
                break;
            case streaming_stop_reason::unsupported:
                code = CASTOR_ENGINE_STREAMING_UNSUPPORTED;
                break;
            case streaming_stop_reason::encode_error:
                code = CASTOR_ENGINE_STREAMING_ENCODER_ERROR;
                break;
            default:
                code = CASTOR_ENGINE_STREAMING_OUTPUT_ERROR;
                break;
            }
            failure_code_ = code;
            failure_message_ = redact(backend_.last_error(output_));
            if (failure_message_.empty())
            {
                failure_message_ = "The RTMP output stopped unexpectedly.";
            }
            state_ = CASTOR_ENGINE_STREAMING_FAILED;
        }
        stopped_condition_.notify_all();
        break;
    }
}

void streaming_subsystem::release_resources() noexcept
{
    void* output = nullptr;
    void* service = nullptr;
    {
        std::scoped_lock lock(mutex_);
        output = output_;
        service = service_;
        output_ = nullptr;
        service_ = nullptr;
    }
    if (output != nullptr)
    {
        if (backend_.active(output))
        {
            std::scoped_lock lock(mutex_);
            output_ = output;
            service_ = service;
            return;
        }
        backend_.disconnect_events(output);
        backend_.release_output(output);
    }
    if (service != nullptr)
    {
        backend_.release_service(service);
    }
}

void streaming_subsystem::clear_secret(std::string& value) noexcept
{
    std::fill(value.begin(), value.end(), '\0');
    value.clear();
}

std::string streaming_subsystem::redact(std::string message) const
{
    replace_all(message, config_.stream_key);
    replace_all(message, config_.password);
    return message;
}

void streaming_subsystem::set_failure(castor_engine_result_t code, std::string message)
{
    std::scoped_lock lock(mutex_);
    failure_code_ = code;
    failure_message_ = redact(std::move(message));
    state_ = CASTOR_ENGINE_STREAMING_FAILED;
}
} // namespace castor::engine::detail
