#include "main_scene.h"

#include <utility>

namespace castor::engine::detail
{
namespace
{
main_scene_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}
} // namespace

main_scene_subsystem::main_scene_subsystem(scene_backend& backend) noexcept : backend_(backend)
{
}

main_scene_subsystem::~main_scene_subsystem()
{
    reset();
}

main_scene_result main_scene_subsystem::create(bool runtime_ready, bool video_ready, uint32_t width, uint32_t height)
{
    if (is_active())
    {
        return {CASTOR_ENGINE_OK, {}};
    }

    if (scene_ != nullptr || color_source_ != nullptr || scene_item_ != nullptr)
    {
        reset();
    }

    if (!runtime_ready)
    {
        return failure(CASTOR_ENGINE_NOT_INITIALIZED,
                       "The engine must be initialized before the main scene can be created.");
    }

    if (!video_ready || width == 0 || height == 0)
    {
        return failure(CASTOR_ENGINE_VIDEO_NOT_CONFIGURED,
                       "The video subsystem must be configured before the main scene can be created.");
    }

    scene_ = backend_.create_scene();

    if (scene_ == nullptr)
    {
        return failure(CASTOR_ENGINE_SCENE_CREATION_FAILED, "OBS failed to create the main scene.");
    }

    if (!backend_.is_color_source_available())
    {
        reset();
        return failure(CASTOR_ENGINE_SCENE_SOURCE_UNAVAILABLE,
                       "The loaded OBS modules do not provide the 'color_source' video source.");
    }

    color_source_ = backend_.create_color_source(width, height);

    if (color_source_ == nullptr)
    {
        reset();
        return failure(CASTOR_ENGINE_SCENE_SOURCE_CREATION_FAILED,
                       "OBS failed to create the solid-color video source for the main scene.");
    }

    scene_item_ = backend_.add_source_to_scene(scene_, color_source_);

    if (scene_item_ == nullptr)
    {
        reset();
        return failure(CASTOR_ENGINE_SCENE_SOURCE_ADD_FAILED,
                       "OBS failed to add the solid-color video source to the main scene.");
    }

    if (!backend_.connect_scene_to_output(scene_) || !is_active())
    {
        reset();
        return failure(CASTOR_ENGINE_SCENE_ACTIVATION_FAILED,
                       "OBS failed to connect the main scene to the primary video output channel.");
    }

    return {CASTOR_ENGINE_OK, {}};
}

bool main_scene_subsystem::is_active() noexcept
{
    return scene_ != nullptr && color_source_ != nullptr && scene_item_ != nullptr &&
           backend_.is_scene_connected_to_output(scene_);
}

void main_scene_subsystem::reset() noexcept
{
    bool released_resources = false;

    if (scene_ != nullptr)
    {
        backend_.disconnect_scene_from_output(scene_);
    }

    if (scene_item_ != nullptr)
    {
        backend_.remove_source_from_scene(scene_item_);
        scene_item_ = nullptr;
    }

    if (color_source_ != nullptr)
    {
        backend_.release_source(color_source_);
        color_source_ = nullptr;
        released_resources = true;
    }

    if (scene_ != nullptr)
    {
        backend_.release_scene(scene_);
        scene_ = nullptr;
        released_resources = true;
    }

    if (released_resources)
    {
        backend_.wait_for_deferred_destruction();
    }
}
} // namespace castor::engine::detail
