#include "obs_scene_backend.h"

#include <condition_variable>
#include <cstring>
#include <mutex>
#include <obs.h>

namespace castor::engine::detail
{
namespace
{
constexpr const char* main_scene_name = "Castor Main Scene";
constexpr const char* color_source_unversioned_id = "color_source";
constexpr const char* color_source_name = "Castor Main Color Source";
constexpr uint32_t main_output_channel = 0;
constexpr long long opaque_black = 0xFF000000LL;

obs_scene_t* as_scene(void* scene) noexcept
{
    return static_cast<obs_scene_t*>(scene);
}

obs_source_t* as_source(void* source) noexcept
{
    return static_cast<obs_source_t*>(source);
}

obs_sceneitem_t* as_scene_item(void* scene_item) noexcept
{
    return static_cast<obs_sceneitem_t*>(scene_item);
}

obs_source_t* get_scene_source(void* scene) noexcept
{
    return obs_scene_get_source(as_scene(scene));
}

struct destruction_wait_state
{
    std::mutex mutex;
    std::condition_variable completed_condition;
    bool completed = false;
};

void signal_destruction_barrier(void* parameter)
{
    auto* state = static_cast<destruction_wait_state*>(parameter);

    {
        std::scoped_lock lock(state->mutex);
        state->completed = true;
    }

    state->completed_condition.notify_one();
}

void signal_graphics_barrier(void*)
{
}
} // namespace

void* obs_scene_backend::create_scene() noexcept
{
    return obs_scene_create(main_scene_name);
}

void obs_scene_backend::release_scene(void* scene) noexcept
{
    obs_scene_release(as_scene(scene));
}

bool obs_scene_backend::is_color_source_available() noexcept
{
    for (size_t index = 0;; ++index)
    {
        const char* source_id = nullptr;
        const char* unversioned_id = nullptr;

        if (!obs_enum_input_types2(index, &source_id, &unversioned_id))
        {
            return false;
        }

        if (source_id != nullptr && unversioned_id != nullptr &&
            std::strcmp(unversioned_id, color_source_unversioned_id) == 0)
        {
            return true;
        }
    }
}

void* obs_scene_backend::create_color_source(uint32_t width, uint32_t height) noexcept
{
    const char* source_id = obs_get_latest_input_type_id(color_source_unversioned_id);

    if (source_id == nullptr)
    {
        return nullptr;
    }

    obs_data_t* settings = obs_data_create();

    if (settings == nullptr)
    {
        return nullptr;
    }

    obs_data_set_int(settings, "color", opaque_black);
    obs_data_set_int(settings, "width", width);
    obs_data_set_int(settings, "height", height);

    obs_source_t* source = obs_source_create(source_id, color_source_name, settings, nullptr);
    obs_data_release(settings);
    return source;
}

void obs_scene_backend::release_source(void* source) noexcept
{
    obs_source_release(as_source(source));
}

void* obs_scene_backend::add_source_to_scene(void* scene, void* source) noexcept
{
    return obs_scene_add(as_scene(scene), as_source(source));
}

void obs_scene_backend::remove_source_from_scene(void* scene_item) noexcept
{
    obs_sceneitem_remove(as_scene_item(scene_item));
}

bool obs_scene_backend::connect_scene_to_output(void* scene) noexcept
{
    obs_set_output_source(main_output_channel, get_scene_source(scene));
    return is_scene_connected_to_output(scene);
}

bool obs_scene_backend::is_scene_connected_to_output(void* scene) noexcept
{
    obs_source_t* output_source = obs_get_output_source(main_output_channel);
    const bool is_connected = output_source != nullptr && output_source == get_scene_source(scene);

    if (output_source != nullptr)
    {
        obs_source_release(output_source);
    }

    return is_connected;
}

void obs_scene_backend::disconnect_scene_from_output(void* scene) noexcept
{
    obs_source_t* output_source = obs_get_output_source(main_output_channel);

    if (output_source != nullptr && output_source == get_scene_source(scene))
    {
        obs_set_output_source(main_output_channel, nullptr);
    }

    if (output_source != nullptr)
    {
        obs_source_release(output_source);
    }

    obs_queue_task(OBS_TASK_GRAPHICS, signal_graphics_barrier, nullptr, true);
}

void obs_scene_backend::wait_for_deferred_destruction() noexcept
{
    destruction_wait_state state;
    obs_queue_task(OBS_TASK_DESTROY, signal_destruction_barrier, &state, false);

    std::unique_lock lock(state.mutex);
    state.completed_condition.wait(lock, [&state] { return state.completed; });
}
} // namespace castor::engine::detail
