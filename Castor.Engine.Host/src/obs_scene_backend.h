#pragma once

#include "main_scene.h"

namespace castor::engine::detail
{
class obs_scene_backend final : public scene_backend
{
  public:
    void* create_scene() noexcept override;
    void release_scene(void* scene) noexcept override;

    bool is_color_source_available() noexcept override;
    void* create_color_source(uint32_t width, uint32_t height) noexcept override;
    void release_source(void* source) noexcept override;

    void* add_source_to_scene(void* scene, void* source) noexcept override;
    void remove_source_from_scene(void* scene_item) noexcept override;
    bool connect_scene_to_output(void* scene) noexcept override;
    bool is_scene_connected_to_output(void* scene) noexcept override;
    void disconnect_scene_from_output(void* scene) noexcept override;
    void wait_for_deferred_destruction() noexcept override;
};
} // namespace castor::engine::detail
