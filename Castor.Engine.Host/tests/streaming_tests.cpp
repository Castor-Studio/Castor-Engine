#include "streaming_configuration.h"
#include "streaming_subsystem.h"

#include <algorithm>
#include <cstring>
#include <iostream>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace
{
using namespace castor::engine::detail;

class fake_streaming_backend final : public streaming_backend
{
  public:
    bool has_service = true;
    bool has_output = true;
    bool service_creation_succeeds = true;
    bool output_creation_succeeds = true;
    bool attachment_succeeds = true;
    bool start_succeeds = true;
    bool emit_start_signals = true;
    bool output_active = false;
    uint64_t total = 120;
    uint64_t dropped = 3;
    std::string error;
    std::vector<std::string> events;
    streaming_event_callback callback = nullptr;
    void* callback_data = nullptr;

    bool service_available() noexcept override
    {
        return has_service;
    }
    bool output_available() noexcept override
    {
        return has_output;
    }
    void* create_service(const streaming_configuration&) noexcept override
    {
        events.emplace_back("create_service");
        return service_creation_succeeds ? &service_token : nullptr;
    }
    void release_service(void*) noexcept override
    {
        events.emplace_back("release_service");
    }
    void* create_output() noexcept override
    {
        events.emplace_back("create_output");
        return output_creation_succeeds ? &output_token : nullptr;
    }
    void release_output(void*) noexcept override
    {
        events.emplace_back("release_output");
    }
    bool attach(void*, void*, void*, void*) noexcept override
    {
        events.emplace_back("attach");
        return attachment_succeeds;
    }
    void set_reconnect_settings(void*, uint32_t, uint32_t) noexcept override
    {
        events.emplace_back("set_reconnect");
    }
    void connect_events(void*, streaming_event_callback event_callback, void* data) noexcept override
    {
        callback = event_callback;
        callback_data = data;
        events.emplace_back("connect_events");
    }
    void disconnect_events(void*) noexcept override
    {
        callback = nullptr;
        callback_data = nullptr;
        events.emplace_back("disconnect_events");
    }
    bool start(void*) noexcept override
    {
        events.emplace_back("start");
        if (start_succeeds)
        {
            output_active = true;
            if (emit_start_signals)
            {
                emit(streaming_event::starting);
                emit(streaming_event::started);
            }
        }
        return start_succeeds;
    }
    void stop(void*) noexcept override
    {
        events.emplace_back("stop");
        output_active = false;
        emit(streaming_event::stopped, streaming_stop_reason::success);
    }
    void force_stop(void*) noexcept override
    {
        events.emplace_back("force_stop");
        output_active = false;
        emit(streaming_event::stopped, streaming_stop_reason::success);
    }
    bool active(void*) noexcept override
    {
        return output_active;
    }
    uint64_t total_frames(void*) noexcept override
    {
        return total;
    }
    uint64_t dropped_frames(void*) noexcept override
    {
        return dropped;
    }
    std::string last_error(void*) noexcept override
    {
        return error;
    }

    void emit(streaming_event event, streaming_stop_reason reason = streaming_stop_reason::success)
    {
        if (callback != nullptr)
        {
            callback(callback_data, event, reason);
        }
    }

  private:
    int service_token = 0;
    int output_token = 0;
};

castor_engine_streaming_config_t config(const char* key = "sentinel-key", const char* password = "sentinel-password")
{
    castor_engine_streaming_config_t value{};
    value.struct_size = sizeof(value);
    value.server_url = "rtmp://127.0.0.1:1935/live";
    value.stream_key = key;
    value.use_authentication = 1;
    value.username = "castor";
    value.password = password;
    value.reconnect_retry_count = 20;
    value.reconnect_delay_seconds = 2;
    return value;
}

castor_engine_streaming_status_t status(streaming_subsystem& subsystem)
{
    castor_engine_streaming_status_t value{};
    value.struct_size = sizeof(value);
    subsystem.get_status(&value);
    return value;
}

bool expect(bool condition, std::string_view message)
{
    if (!condition)
    {
        std::cerr << "  expectation failed: " << message << '\n';
    }
    return condition;
}

bool validation_rejects_invalid_shapes_and_urls()
{
    auto valid = config();
    auto undersized = valid;
    undersized.struct_size = 1;
    auto malformed = valid;
    malformed.server_url = "https://example.test/live";
    auto missing_key = valid;
    missing_key.stream_key = "";
    auto missing_password = valid;
    missing_password.password = "";
    auto invalid_retry = valid;
    invalid_retry.reconnect_delay_seconds = 0;
    return expect(validate_streaming_config(nullptr).code == CASTOR_ENGINE_STREAMING_INVALID_CONFIGURATION,
                  "null is rejected") &&
           expect(validate_streaming_config(&undersized).code == CASTOR_ENGINE_STREAMING_INVALID_CONFIGURATION,
                  "undersized structures are rejected") &&
           expect(validate_streaming_config(&malformed).code == CASTOR_ENGINE_STREAMING_INVALID_CONFIGURATION,
                  "non-RTMP URLs are rejected") &&
           expect(validate_streaming_config(&missing_key).code == CASTOR_ENGINE_STREAMING_INVALID_CONFIGURATION,
                  "empty keys are rejected") &&
           expect(validate_streaming_config(&missing_password).code == CASTOR_ENGINE_STREAMING_INVALID_CONFIGURATION,
                  "incomplete authentication is rejected") &&
           expect(validate_streaming_config(&invalid_retry).code == CASTOR_ENGINE_STREAMING_INVALID_CONFIGURATION,
                  "zero retry delay is rejected") &&
           expect(validate_streaming_config(&valid).code == CASTOR_ENGINE_OK, "valid RTMP configuration is accepted");
}

bool configuration_checks_runtime_and_registrations()
{
    fake_streaming_backend backend;
    streaming_subsystem subsystem(backend);
    auto value = config();
    const auto no_runtime = subsystem.configure(&value, false);
    backend.has_service = false;
    const auto no_service = subsystem.configure(&value, true);
    backend.has_service = true;
    backend.has_output = false;
    const auto no_output = subsystem.configure(&value, true);
    return expect(no_runtime.code == CASTOR_ENGINE_NOT_INITIALIZED, "runtime is required") &&
           expect(no_service.code == CASTOR_ENGINE_STREAMING_SERVICE_UNAVAILABLE, "service registration is checked") &&
           expect(no_output.code == CASTOR_ENGINE_STREAMING_OUTPUT_UNAVAILABLE, "output registration is checked");
}

bool lifecycle_reports_states_health_and_cleanup_order()
{
    fake_streaming_backend backend;
    streaming_subsystem subsystem(backend);
    auto value = config();
    int video_encoder = 0;
    int audio_encoder = 0;
    if (subsystem.configure(&value, true).code != CASTOR_ENGINE_OK ||
        subsystem.start(true, true, true, true, false, &video_encoder, &audio_encoder).code != CASTOR_ENGINE_OK)
    {
        return false;
    }
    auto live = status(subsystem);
    castor_engine_streaming_health_t health{};
    health.struct_size = sizeof(health);
    const auto health_result = subsystem.get_health(&health);
    backend.emit(streaming_event::reconnecting);
    const auto reconnecting = status(subsystem);
    backend.emit(streaming_event::reconnected);
    const auto reconnected = status(subsystem);
    const auto stop_result = subsystem.stop();
    const auto idle = status(subsystem);
    const auto output_release = std::find(backend.events.begin(), backend.events.end(), "release_output");
    const auto service_release = std::find(backend.events.begin(), backend.events.end(), "release_service");
    const bool cleanup_order = output_release < service_release;
    const bool repeated =
        subsystem.start(true, true, true, true, false, &video_encoder, &audio_encoder).code == CASTOR_ENGINE_OK &&
        subsystem.stop().code == CASTOR_ENGINE_OK;
    return expect(live.state == CASTOR_ENGINE_STREAMING_LIVE, "start signal reports live") &&
           expect(health_result.code == CASTOR_ENGINE_OK && health.total_frames == 120 && health.dropped_frames == 3,
                  "health counters are returned") &&
           expect(reconnecting.state == CASTOR_ENGINE_STREAMING_RECONNECTING, "reconnect signal is observed") &&
           expect(reconnected.state == CASTOR_ENGINE_STREAMING_LIVE, "reconnect success returns live") &&
           expect(stop_result.code == CASTOR_ENGINE_OK && idle.state == CASTOR_ENGINE_STREAMING_IDLE,
                  "stop returns idle") &&
           expect(cleanup_order, "output is released before its service") &&
           expect(repeated, "the complete session can be repeated");
}

bool failures_are_mapped_and_secrets_redacted()
{
    const std::vector<std::pair<streaming_stop_reason, castor_engine_result_t>> mappings = {
        {streaming_stop_reason::connect_failed, CASTOR_ENGINE_STREAMING_CONNECTION_FAILED},
        {streaming_stop_reason::invalid_stream, CASTOR_ENGINE_STREAMING_STREAM_REJECTED},
        {streaming_stop_reason::disconnected, CASTOR_ENGINE_STREAMING_DISCONNECTED},
        {streaming_stop_reason::unsupported, CASTOR_ENGINE_STREAMING_UNSUPPORTED},
        {streaming_stop_reason::encode_error, CASTOR_ENGINE_STREAMING_ENCODER_ERROR},
        {streaming_stop_reason::bad_path, CASTOR_ENGINE_STREAMING_OUTPUT_ERROR},
        {streaming_stop_reason::no_space, CASTOR_ENGINE_STREAMING_OUTPUT_ERROR},
        {streaming_stop_reason::unknown, CASTOR_ENGINE_STREAMING_OUTPUT_ERROR},
        {streaming_stop_reason::error, CASTOR_ENGINE_STREAMING_OUTPUT_ERROR},
    };
    for (const auto& [reason, expected] : mappings)
    {
        fake_streaming_backend backend;
        streaming_subsystem subsystem(backend);
        auto value = config();
        int video_encoder = 0;
        int audio_encoder = 0;
        subsystem.configure(&value, true);
        subsystem.start(true, true, true, true, false, &video_encoder, &audio_encoder);
        backend.error = "failure includes sentinel-key and sentinel-password";
        backend.output_active = false;
        backend.emit(streaming_event::stopped, reason);
        const auto failed = status(subsystem);
        const std::string message = failed.last_failure_message;
        if (!expect(failed.state == CASTOR_ENGINE_STREAMING_FAILED && failed.last_failure_code == expected,
                    "stop reason maps to a distinct failure") ||
            !expect(message.find("sentinel-key") == std::string::npos &&
                        message.find("sentinel-password") == std::string::npos,
                    "secrets are redacted"))
        {
            return false;
        }
    }

    fake_streaming_backend reconnect_backend;
    streaming_subsystem reconnect_subsystem(reconnect_backend);
    auto value = config();
    int encoder = 0;
    reconnect_subsystem.configure(&value, true);
    reconnect_subsystem.start(true, true, true, true, false, &encoder, &encoder);
    reconnect_backend.emit(streaming_event::reconnecting);
    reconnect_backend.output_active = false;
    reconnect_backend.emit(streaming_event::stopped, streaming_stop_reason::connect_failed);
    return expect(status(reconnect_subsystem).last_failure_code == CASTOR_ENGINE_STREAMING_RECONNECT_EXHAUSTED,
                  "connection failure after reconnecting reports exhausted retries");
}

bool creation_attachment_and_start_failures_are_explicit()
{
    auto run = [](auto configure_backend, castor_engine_result_t expected)
    {
        fake_streaming_backend backend;
        configure_backend(backend);
        streaming_subsystem subsystem(backend);
        auto value = config();
        int encoder = 0;
        subsystem.configure(&value, true);
        const auto result = subsystem.start(true, true, true, true, false, &encoder, &encoder);
        return result.code == expected && status(subsystem).state == CASTOR_ENGINE_STREAMING_FAILED;
    };

    return expect(run([](auto& backend) { backend.service_creation_succeeds = false; },
                      CASTOR_ENGINE_STREAMING_SERVICE_CREATION_FAILED),
                  "service creation failure is explicit") &&
           expect(run([](auto& backend) { backend.output_creation_succeeds = false; },
                      CASTOR_ENGINE_STREAMING_OUTPUT_CREATION_FAILED),
                  "output creation failure is explicit") &&
           expect(run([](auto& backend) { backend.attachment_succeeds = false; },
                      CASTOR_ENGINE_STREAMING_ENCODERS_NOT_CONFIGURED),
                  "attachment failure is explicit") &&
           expect(run(
                      [](auto& backend)
                      {
                          backend.start_succeeds = false;
                          backend.error = "start failure includes sentinel-key";
                      },
                      CASTOR_ENGINE_STREAMING_START_FAILED),
                  "start failure is explicit");
}

bool invalid_lifecycle_operations_are_explicit()
{
    fake_streaming_backend backend;
    streaming_subsystem subsystem(backend);
    auto value = config();
    int encoder = 0;
    const auto no_configuration = subsystem.start(true, true, true, true, false, &encoder, &encoder);
    subsystem.configure(&value, true);
    const auto missing_video = subsystem.start(true, false, true, true, false, &encoder, &encoder);
    const auto missing_audio = subsystem.start(true, true, false, true, false, &encoder, &encoder);
    const auto missing_encoder = subsystem.start(true, true, true, true, false, nullptr, &encoder);
    const auto missing_scene = subsystem.start(true, true, true, false, false, &encoder, &encoder);
    const auto recording_conflict = subsystem.start(true, true, true, true, true, &encoder, &encoder);
    const auto inactive_stop = subsystem.stop();
    subsystem.start(true, true, true, true, false, &encoder, &encoder);
    const auto duplicate_start = subsystem.start(true, true, true, true, false, &encoder, &encoder);
    const auto identical_reconfigure = subsystem.configure(&value, true);
    auto replacement = config("replacement-key");
    const auto reconfigure = subsystem.configure(&replacement, true);
    subsystem.stop();
    return expect(no_configuration.code == CASTOR_ENGINE_STREAMING_NOT_CONFIGURED, "configuration is required") &&
           expect(missing_video.code == CASTOR_ENGINE_VIDEO_NOT_CONFIGURED, "video is required") &&
           expect(missing_audio.code == CASTOR_ENGINE_AUDIO_NOT_CONFIGURED, "audio is required") &&
           expect(missing_encoder.code == CASTOR_ENGINE_STREAMING_ENCODERS_NOT_CONFIGURED, "encoders are required") &&
           expect(missing_scene.code == CASTOR_ENGINE_STREAMING_NO_ACTIVE_SCENE, "scene is required") &&
           expect(recording_conflict.code == CASTOR_ENGINE_STREAMING_CONFLICTING_OUTPUT_ACTIVE,
                  "recording conflict is explicit") &&
           expect(inactive_stop.code == CASTOR_ENGINE_STREAMING_NOT_ACTIVE, "inactive stop is rejected") &&
           expect(duplicate_start.code == CASTOR_ENGINE_STREAMING_ALREADY_ACTIVE, "duplicate start is rejected") &&
           expect(identical_reconfigure.code == CASTOR_ENGINE_OK, "identical active configuration is idempotent") &&
           expect(reconfigure.code == CASTOR_ENGINE_STREAMING_RECONFIGURATION_WHILE_ACTIVE,
                  "active reconfiguration is rejected");
}

bool reset_stops_before_releasing_resources()
{
    fake_streaming_backend backend;
    streaming_subsystem subsystem(backend);
    auto value = config();
    int encoder = 0;
    subsystem.configure(&value, true);
    subsystem.start(true, true, true, true, false, &encoder, &encoder);

    const bool reset = subsystem.reset();
    const auto stop = std::find(backend.events.begin(), backend.events.end(), "stop");
    const auto output_release = std::find(backend.events.begin(), backend.events.end(), "release_output");
    const auto service_release = std::find(backend.events.begin(), backend.events.end(), "release_service");
    return expect(reset, "reset terminates the active output") &&
           expect(stop < output_release && output_release < service_release,
                  "reset stops, then releases output before service") &&
           expect(status(subsystem).state == CASTOR_ENGINE_STREAMING_IDLE, "reset returns to idle") &&
           expect(subsystem.start(true, true, true, true, false, &encoder, &encoder).code ==
                      CASTOR_ENGINE_STREAMING_NOT_CONFIGURED,
                  "reset clears the destination");
}
} // namespace

int main()
{
    const std::vector<std::pair<std::string_view, bool (*)()>> tests = {
        {"validation_rejects_invalid_shapes_and_urls", validation_rejects_invalid_shapes_and_urls},
        {"configuration_checks_runtime_and_registrations", configuration_checks_runtime_and_registrations},
        {"lifecycle_reports_states_health_and_cleanup_order", lifecycle_reports_states_health_and_cleanup_order},
        {"failures_are_mapped_and_secrets_redacted", failures_are_mapped_and_secrets_redacted},
        {"creation_attachment_and_start_failures_are_explicit", creation_attachment_and_start_failures_are_explicit},
        {"invalid_lifecycle_operations_are_explicit", invalid_lifecycle_operations_are_explicit},
        {"reset_stops_before_releasing_resources", reset_stops_before_releasing_resources},
    };
    bool passed = true;
    for (const auto& [name, test] : tests)
    {
        const bool result = test();
        std::cout << (result ? "PASS " : "FAIL ") << name << '\n';
        passed = passed && result;
    }
    return passed ? 0 : 1;
}
