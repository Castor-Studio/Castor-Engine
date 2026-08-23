#pragma once

#include "streaming_subsystem.h"

#include <mutex>
#include <obs.h>
#include <util/base.h>

namespace castor::engine::detail
{
class obs_streaming_backend final : public streaming_backend
{
  public:
    bool service_available() noexcept override;
    bool output_available() noexcept override;
    void* create_service(const streaming_configuration& config) noexcept override;
    void release_service(void* service) noexcept override;
    void* create_output() noexcept override;
    void release_output(void* output) noexcept override;
    bool attach(void* output, void* service, void* video_encoder, void* audio_encoder) noexcept override;
    void set_reconnect_settings(void* output, uint32_t retry_count, uint32_t delay_seconds) noexcept override;
    void connect_events(void* output, streaming_event_callback callback, void* data) noexcept override;
    void disconnect_events(void* output) noexcept override;
    bool start(void* output) noexcept override;
    void stop(void* output) noexcept override;
    void force_stop(void* output) noexcept override;
    bool active(void* output) noexcept override;
    uint64_t total_frames(void* output) noexcept override;
    uint64_t dropped_frames(void* output) noexcept override;
    std::string last_error(void* output) noexcept override;

  private:
    static void on_starting(void* data, calldata_t* calldata);
    static void on_start(void* data, calldata_t* calldata);
    static void on_reconnect(void* data, calldata_t* calldata);
    static void on_reconnect_success(void* data, calldata_t* calldata);
    static void on_stop(void* data, calldata_t* calldata);
    static void on_log(int level, const char* format, va_list arguments, void* data);
    static streaming_stop_reason translate_stop_code(int code) noexcept;
    void emit(streaming_event event, streaming_stop_reason reason = streaming_stop_reason::success) noexcept;
    void install_log_filter(const streaming_configuration& config) noexcept;
    void uninstall_log_filter() noexcept;

    streaming_event_callback callback_ = nullptr;
    void* callback_data_ = nullptr;
    void* connected_output_ = nullptr;
    std::mutex callback_mutex_;
    std::mutex log_mutex_;
    log_handler_t previous_log_handler_ = nullptr;
    void* previous_log_parameter_ = nullptr;
    std::string stream_key_;
    std::string password_;
    bool log_filter_installed_ = false;
};
} // namespace castor::engine::detail
