#include "obs_scene_backend.h"

#include <chrono>
#include <condition_variable>
#include <cstring>
#include <mutex>
#include <obs.h>
#include <thread>

namespace castor::engine::detail
{
namespace
{
constexpr const char* display_source_unversioned_id = "monitor_capture";
constexpr const char* display_source_name = "Castor Main Display Capture";
constexpr const char* transition_name = "Castor Scene Transition";
constexpr uint32_t main_output_channel = 0;

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

const char* transition_type_id(castor_engine_scene_transition_type_t type) noexcept
{
    switch (type)
    {
    case CASTOR_ENGINE_SCENE_TRANSITION_CUT:
        return "cut_transition";
    case CASTOR_ENGINE_SCENE_TRANSITION_FADE:
        return "fade_transition";
    case CASTOR_ENGINE_SCENE_TRANSITION_SLIDE:
        return "slide_transition";
    case CASTOR_ENGINE_SCENE_TRANSITION_SWIPE:
        return "swipe_transition";
    default:
        return nullptr;
    }
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

constexpr std::chrono::milliseconds transition_poll_interval{10};
} // namespace

void* obs_scene_backend::create_scene(const char* name) noexcept
{
    return obs_scene_create(name);
}

void obs_scene_backend::release_scene(void* scene) noexcept
{
    obs_scene_release(as_scene(scene));
}

void obs_scene_backend::rename_scene(void* scene, const char* new_name) noexcept
{
    obs_source_set_name(obs_scene_get_source(as_scene(scene)), new_name);
}

void* obs_scene_backend::get_scene_source(void* scene) noexcept
{
    return obs_scene_get_source(as_scene(scene));
}

bool obs_scene_backend::is_display_source_available() noexcept
{
    return obs_get_latest_input_type_id(display_source_unversioned_id) != nullptr;
}

void* obs_scene_backend::create_display_source(bool uses_string_selector, const char* obs_monitor_id,
                                               long long obs_monitor_index, bool capture_cursor) noexcept
{
    const char* source_id = obs_get_latest_input_type_id(display_source_unversioned_id);

    if (source_id == nullptr)
    {
        return nullptr;
    }

    obs_data_t* settings = obs_data_create();

    if (settings == nullptr)
    {
        return nullptr;
    }

    if (uses_string_selector)
    {
        obs_data_set_string(settings, "monitor_id", obs_monitor_id);
    }
    else
    {
        obs_data_set_int(settings, "monitor", obs_monitor_index);
    }
    obs_data_set_bool(settings, "capture_cursor", capture_cursor);
    obs_data_set_int(settings, "method", 0);
    obs_data_set_bool(settings, "force_sdr", false);

    obs_source_t* source = obs_source_create(source_id, display_source_name, settings, nullptr);
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

void obs_scene_backend::set_output_source(void* source) noexcept
{
    obs_set_output_source(main_output_channel, as_source(source));
}

void* obs_scene_backend::get_output_source() noexcept
{
    obs_source_t* source = obs_get_output_source(main_output_channel);

    if (source != nullptr)
    {
        obs_source_release(source);
    }

    return source;
}

bool obs_scene_backend::has_output_source() noexcept
{
    return get_output_source() != nullptr;
}

void obs_scene_backend::disconnect_output() noexcept
{
    obs_set_output_source(main_output_channel, nullptr);
    obs_queue_task(OBS_TASK_GRAPHICS, signal_graphics_barrier, nullptr, true);
}

bool obs_scene_backend::is_transition_type_available(castor_engine_scene_transition_type_t type) noexcept
{
    const char* target_id = transition_type_id(type);

    if (target_id == nullptr)
    {
        return false;
    }

    for (size_t index = 0;; ++index)
    {
        const char* transition_id = nullptr;

        if (!obs_enum_transition_types(index, &transition_id))
        {
            return false;
        }

        if (transition_id != nullptr && std::strcmp(transition_id, target_id) == 0)
        {
            return true;
        }
    }
}

void* obs_scene_backend::create_transition(castor_engine_scene_transition_type_t type) noexcept
{
    const char* id = transition_type_id(type);

    if (id == nullptr)
    {
        return nullptr;
    }

    return obs_source_create(id, transition_name, nullptr, nullptr);
}

void obs_scene_backend::release_transition(void* transition) noexcept
{
    obs_source_release(as_source(transition));
}

void obs_scene_backend::set_transition_size(void* transition, uint32_t width, uint32_t height) noexcept
{
    obs_transition_set_size(as_source(transition), width, height);
    obs_transition_set_scale_type(as_source(transition), OBS_TRANSITION_SCALE_ASPECT);
}

void obs_scene_backend::seed_transition(void* transition, void* initial_source) noexcept
{
    obs_source_t* new_transition = as_source(transition);

    obs_transition_set(new_transition, as_source(initial_source));
    obs_set_output_source(main_output_channel, new_transition);
}

bool obs_scene_backend::start_transition(void* transition, void* target_source, uint32_t duration_ms) noexcept
{
    obs_source_t* source = as_source(transition);

    if (!obs_transition_start(source, OBS_TRANSITION_MODE_AUTO, duration_ms, as_source(target_source)))
    {
        return false;
    }

    // obs_transition_start only kicks the animation off; there is no public
    // completion signal for transitions (transition_stop in obs-source.h is
    // a plugin-private callback, never proxied to the source's signal
    // handler), and obs_transition_is_active never clears once a transition
    // has run at all. The video thread advances the normalized transition
    // time to 1.0 as it ticks, so poll that instead.
    while (obs_transition_get_time(source) < 1.0F)
    {
        std::this_thread::sleep_for(transition_poll_interval);
    }

    return true;
}

void obs_scene_backend::wait_for_deferred_destruction() noexcept
{
    destruction_wait_state state;
    obs_queue_task(OBS_TASK_DESTROY, signal_destruction_barrier, &state, false);

    std::unique_lock lock(state.mutex);
    state.completed_condition.wait(lock, [&state] { return state.completed; });
}
} // namespace castor::engine::detail
