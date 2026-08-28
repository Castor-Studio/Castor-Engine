#pragma once

#include "scene_registry.h"

namespace castor::engine::detail
{
class obs_scene_backend final : public scene_backend
{
  public:
    void* create_scene(const char* name) noexcept override;
    void release_scene(void* scene) noexcept override;
    void rename_scene(void* scene, const char* new_name) noexcept override;
    void* get_scene_source(void* scene) noexcept override;

    bool is_display_source_available() noexcept override;
    void* create_display_source(bool uses_string_selector, const char* obs_monitor_id, long long obs_monitor_index,
                                bool capture_cursor) noexcept override;
    void release_source(void* source) noexcept override;

    void* add_source_to_scene(void* scene, void* source) noexcept override;
    void remove_source_from_scene(void* scene_item) noexcept override;
    void get_scene_item_transform(void* scene_item,
                                  castor_engine_scene_item_transform_t& out_transform) noexcept override;
    void set_scene_item_transform(void* scene_item,
                                  const castor_engine_scene_item_transform_t& transform) noexcept override;

    void set_output_source(void* source) noexcept override;
    void* get_output_source() noexcept override;
    bool has_output_source() noexcept override;
    void disconnect_output() noexcept override;

    bool is_transition_type_available(castor_engine_scene_transition_type_t type) noexcept override;
    void* create_transition(castor_engine_scene_transition_type_t type) noexcept override;
    void release_transition(void* transition) noexcept override;
    void set_transition_size(void* transition, uint32_t width, uint32_t height) noexcept override;
    void seed_transition(void* transition, void* initial_source) noexcept override;
    bool start_transition(void* transition, void* target_source, uint32_t duration_ms) noexcept override;

    void wait_for_deferred_destruction() noexcept override;
};
} // namespace castor::engine::detail
