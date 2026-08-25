#pragma once

#include "castor_engine.h"

#include <cstdint>
#include <string>
#include <vector>

namespace castor::engine::detail
{
struct scene_registry_result
{
    castor_engine_result_t code;
    std::string message;
};

class scene_backend
{
  public:
    virtual ~scene_backend() = default;

    virtual void* create_scene(const char* name) noexcept = 0;
    virtual void release_scene(void* scene) noexcept = 0;
    virtual void rename_scene(void* scene, const char* new_name) noexcept = 0;
    virtual void* get_scene_source(void* scene) noexcept = 0;

    virtual bool is_display_source_available() noexcept = 0;
    virtual void* create_display_source(bool uses_string_selector, const char* obs_monitor_id,
                                        long long obs_monitor_index, bool capture_cursor) noexcept = 0;
    virtual void release_source(void* source) noexcept = 0;

    virtual void* add_source_to_scene(void* scene, void* source) noexcept = 0;
    virtual void remove_source_from_scene(void* scene_item) noexcept = 0;

    virtual void set_output_source(void* source) noexcept = 0;
    virtual void* get_output_source() noexcept = 0;
    virtual bool has_output_source() noexcept = 0;
    virtual void disconnect_output() noexcept = 0;

    virtual bool is_transition_type_available(castor_engine_scene_transition_type_t type) noexcept = 0;
    virtual void* create_transition(castor_engine_scene_transition_type_t type) noexcept = 0;
    virtual void release_transition(void* transition) noexcept = 0;
    virtual void set_transition_size(void* transition, uint32_t width, uint32_t height) noexcept = 0;
    virtual void swap_transition(void* transition, void* previous_output_source) noexcept = 0;
    virtual bool start_transition(void* transition, void* target_source, uint32_t duration_ms) noexcept = 0;

    virtual void wait_for_deferred_destruction() noexcept = 0;
};

class scene_registry_subsystem final
{
  public:
    explicit scene_registry_subsystem(scene_backend& backend) noexcept;
    ~scene_registry_subsystem();

    scene_registry_subsystem(const scene_registry_subsystem&) = delete;
    scene_registry_subsystem& operator=(const scene_registry_subsystem&) = delete;

    scene_registry_result create_scene(const char* name, bool runtime_ready);
    scene_registry_result delete_scene(const char* name);
    scene_registry_result rename_scene(const char* old_name, const char* new_name);

    uint32_t scene_count() const noexcept;
    bool scene_name_at(uint32_t index, std::string& out_name) const;

    scene_registry_result configure_display_capture(const char* scene_name, const char* display_id,
                                                     bool uses_string_selector, const char* obs_monitor_id,
                                                     long long obs_monitor_index, bool capture_cursor,
                                                     bool recording_active, bool streaming_active);
    bool is_display_capture_active(const char* scene_name) const noexcept;

    scene_registry_result switch_scene(const char* name, const castor_engine_scene_transition_config_t& transition,
                                       bool video_ready, uint32_t width, uint32_t height);

    bool has_active_scene() noexcept;
    bool active_scene_name(std::string& out_name) const noexcept;

    void reset() noexcept;

  private:
    struct scene_entry
    {
        std::string name;
        void* scene = nullptr;
        void* visual_source = nullptr;
        void* scene_item = nullptr;
        std::string display_id;
        bool capture_cursor = false;
        bool display_capture_active = false;
    };

    scene_backend& backend_;
    std::vector<scene_entry> scenes_;
    std::string active_scene_name_;
    void* current_transition_ = nullptr;
    castor_engine_scene_transition_type_t current_transition_type_ = CASTOR_ENGINE_SCENE_TRANSITION_CUT;
    bool has_transition_ = false;

    scene_entry* find(const char* name) noexcept;
    const scene_entry* find(const char* name) const noexcept;
    void teardown_entry(scene_entry& entry) noexcept;
    void release_source_from_entry(scene_entry& entry) noexcept;
};
} // namespace castor::engine::detail
