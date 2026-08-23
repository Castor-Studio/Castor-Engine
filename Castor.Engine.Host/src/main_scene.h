#pragma once

#include "castor_engine.h"

#include <cstdint>
#include <string>

namespace castor::engine::detail
{
struct main_scene_result
{
    castor_engine_result_t code;
    std::string message;
};

class scene_backend
{
  public:
    virtual ~scene_backend() = default;

    virtual void* create_scene() noexcept = 0;
    virtual void release_scene(void* scene) noexcept = 0;

    virtual bool is_color_source_available() noexcept = 0;
    virtual void* create_color_source(uint32_t width, uint32_t height) noexcept = 0;
    virtual bool is_display_source_available() noexcept = 0;
    virtual void* create_display_source(bool uses_string_selector, const char* obs_monitor_id,
                                        long long obs_monitor_index, bool capture_cursor) noexcept = 0;
    virtual void release_source(void* source) noexcept = 0;

    virtual void* add_source_to_scene(void* scene, void* source) noexcept = 0;
    virtual void remove_source_from_scene(void* scene_item) noexcept = 0;
    virtual bool connect_scene_to_output(void* scene) noexcept = 0;
    virtual bool is_scene_connected_to_output(void* scene) noexcept = 0;
    virtual void disconnect_scene_from_output(void* scene) noexcept = 0;
    virtual void wait_for_deferred_destruction() noexcept = 0;
};

class main_scene_subsystem final
{
  public:
    explicit main_scene_subsystem(scene_backend& backend) noexcept;
    ~main_scene_subsystem();

    main_scene_subsystem(const main_scene_subsystem&) = delete;
    main_scene_subsystem& operator=(const main_scene_subsystem&) = delete;

    main_scene_result create(bool runtime_ready, bool video_ready, uint32_t width, uint32_t height);

    main_scene_result configure_display_capture(const char* display_id, bool uses_string_selector,
                                                const char* obs_monitor_id, long long obs_monitor_index,
                                                bool capture_cursor, bool recording_active, bool streaming_active);

    bool is_active() noexcept;

    bool is_display_capture_active() noexcept;

    void reset() noexcept;

  private:
    scene_backend& backend_;
    void* scene_ = nullptr;
    void* visual_source_ = nullptr;
    void* scene_item_ = nullptr;
    std::string display_id_;
    bool capture_cursor_ = false;
    bool display_capture_active_ = false;
};
} // namespace castor::engine::detail
