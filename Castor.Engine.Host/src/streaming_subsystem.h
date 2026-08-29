#pragma once

#include "castor_engine.h"
#include "streaming_configuration.h"

#include <condition_variable>
#include <cstdint>
#include <mutex>
#include <string>

namespace castor::engine::detail
{
struct streaming_lifecycle_result
{
    castor_engine_result_t code;
    std::string message;
};

enum class streaming_event
{
    starting,
    started,
    reconnecting,
    reconnected,
    stopped,
};

enum class streaming_stop_reason
{
    success,
    bad_path,
    connect_failed,
    invalid_stream,
    error,
    disconnected,
    unsupported,
    no_space,
    encode_error,
    unknown,
};

using streaming_event_callback = void (*)(void*, streaming_event, streaming_stop_reason);

class streaming_backend
{
  public:
    virtual ~streaming_backend() = default;
    virtual bool service_available() noexcept = 0;
    virtual bool output_available() noexcept = 0;
    virtual void* create_service(const streaming_configuration& config) noexcept = 0;
    virtual void release_service(void* service) noexcept = 0;
    virtual void* create_output() noexcept = 0;
    virtual void release_output(void* output) noexcept = 0;
    virtual bool attach(void* output, void* service, void* video_encoder, void* audio_encoder) noexcept = 0;
    virtual void set_reconnect_settings(void* output, uint32_t retry_count, uint32_t delay_seconds) noexcept = 0;
    virtual void connect_events(void* output, streaming_event_callback callback, void* data) noexcept = 0;
    virtual void disconnect_events(void* output) noexcept = 0;
    virtual bool start(void* output) noexcept = 0;
    virtual void stop(void* output) noexcept = 0;
    virtual void force_stop(void* output) noexcept = 0;
    virtual bool active(void* output) noexcept = 0;
    virtual uint64_t total_frames(void* output) noexcept = 0;
    virtual uint64_t dropped_frames(void* output) noexcept = 0;
    virtual std::string last_error(void* output) noexcept = 0;
};

class streaming_subsystem final
{
  public:
    explicit streaming_subsystem(streaming_backend& backend) noexcept;
    ~streaming_subsystem();

    streaming_subsystem(const streaming_subsystem&) = delete;
    streaming_subsystem& operator=(const streaming_subsystem&) = delete;

    streaming_lifecycle_result configure(const castor_engine_streaming_config_t* config, bool runtime_ready);
    streaming_lifecycle_result start(bool runtime_ready, bool video_ready, bool audio_ready, bool scene_active,
                                     void* video_encoder, void* audio_encoder);
    streaming_lifecycle_result stop();
    streaming_lifecycle_result get_status(castor_engine_streaming_status_t* out_status) noexcept;
    streaming_lifecycle_result get_health(castor_engine_streaming_health_t* out_health) noexcept;
    bool is_active() noexcept;
    bool reset() noexcept;

  private:
    static void handle_event(void* data, streaming_event event, streaming_stop_reason reason);
    void on_event(streaming_event event, streaming_stop_reason reason);
    void release_resources() noexcept;
    void clear_secret(std::string& value) noexcept;
    std::string redact(std::string message) const;
    void set_failure(castor_engine_result_t code, std::string message);

    streaming_backend& backend_;
    mutable std::mutex mutex_;
    std::condition_variable stopped_condition_;
    streaming_configuration config_;
    bool configured_ = false;
    void* service_ = nullptr;
    void* output_ = nullptr;
    castor_engine_streaming_state_t state_ = CASTOR_ENGINE_STREAMING_IDLE;
    castor_engine_result_t failure_code_ = CASTOR_ENGINE_OK;
    std::string failure_message_;
    bool reconnect_seen_ = false;
};
} // namespace castor::engine::detail
