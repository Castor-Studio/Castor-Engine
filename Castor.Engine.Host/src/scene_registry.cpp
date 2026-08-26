#include "scene_registry.h"

#include <algorithm>
#include <utility>

namespace castor::engine::detail
{
namespace
{
scene_registry_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}

bool is_blank(const char* value) noexcept
{
    return value == nullptr || value[0] == '\0';
}
} // namespace

scene_registry_subsystem::scene_registry_subsystem(scene_backend& backend) noexcept : backend_(backend)
{
}

scene_registry_subsystem::~scene_registry_subsystem()
{
    reset();
}

scene_registry_subsystem::scene_entry* scene_registry_subsystem::find(const char* name) noexcept
{
    if (name == nullptr)
    {
        return nullptr;
    }

    auto it =
        std::find_if(scenes_.begin(), scenes_.end(), [name](const scene_entry& entry) { return entry.name == name; });
    return it == scenes_.end() ? nullptr : &(*it);
}

const scene_registry_subsystem::scene_entry* scene_registry_subsystem::find(const char* name) const noexcept
{
    if (name == nullptr)
    {
        return nullptr;
    }

    auto it =
        std::find_if(scenes_.begin(), scenes_.end(), [name](const scene_entry& entry) { return entry.name == name; });
    return it == scenes_.end() ? nullptr : &(*it);
}

void scene_registry_subsystem::release_source_from_entry(scene_entry& entry) noexcept
{
    if (entry.scene_item != nullptr)
    {
        backend_.remove_source_from_scene(entry.scene_item);
        entry.scene_item = nullptr;
    }

    if (entry.visual_source != nullptr)
    {
        backend_.release_source(entry.visual_source);
        entry.visual_source = nullptr;
    }

    entry.display_id.clear();
    entry.capture_cursor = false;
    entry.display_capture_active = false;
}

void scene_registry_subsystem::teardown_entry(scene_entry& entry) noexcept
{
    release_source_from_entry(entry);

    if (entry.scene != nullptr)
    {
        backend_.release_scene(entry.scene);
        entry.scene = nullptr;
    }
}

scene_registry_result scene_registry_subsystem::create_scene(const char* name, bool runtime_ready)
{
    if (is_blank(name))
    {
        return failure(CASTOR_ENGINE_SCENE_INVALID_NAME, "A scene name must be a non-empty string.");
    }

    if (!runtime_ready)
    {
        return failure(CASTOR_ENGINE_NOT_INITIALIZED, "The engine must be initialized before a scene can be created.");
    }

    if (find(name) != nullptr)
    {
        return failure(CASTOR_ENGINE_SCENE_ALREADY_EXISTS, "A scene named '" + std::string(name) + "' already exists.");
    }

    void* scene = backend_.create_scene(name);

    if (scene == nullptr)
    {
        return failure(CASTOR_ENGINE_SCENE_CREATION_FAILED, "OBS failed to create scene '" + std::string(name) + "'.");
    }

    scene_entry entry;
    entry.name = name;
    entry.scene = scene;
    scenes_.push_back(std::move(entry));
    return {CASTOR_ENGINE_OK, {}};
}

scene_registry_result scene_registry_subsystem::delete_scene(const char* name)
{
    scene_entry* entry = find(name);

    if (entry == nullptr)
    {
        return failure(CASTOR_ENGINE_SCENE_NOT_FOUND, "No scene named '" + std::string(name ? name : "") + "' exists.");
    }

    if (active_scene_name_ == entry->name)
    {
        return failure(CASTOR_ENGINE_SCENE_DELETE_ACTIVE_SCENE,
                       "Scene '" + entry->name + "' cannot be deleted while it is the active scene.");
    }

    teardown_entry(*entry);
    backend_.wait_for_deferred_destruction();

    scenes_.erase(std::remove_if(scenes_.begin(), scenes_.end(),
                                 [name](const scene_entry& candidate) { return candidate.name == name; }),
                  scenes_.end());
    return {CASTOR_ENGINE_OK, {}};
}

scene_registry_result scene_registry_subsystem::rename_scene(const char* old_name, const char* new_name)
{
    scene_entry* entry = find(old_name);

    if (entry == nullptr)
    {
        return failure(CASTOR_ENGINE_SCENE_NOT_FOUND,
                       "No scene named '" + std::string(old_name ? old_name : "") + "' exists.");
    }

    if (is_blank(new_name))
    {
        return failure(CASTOR_ENGINE_SCENE_INVALID_NAME, "A scene name must be a non-empty string.");
    }

    if (entry->name == new_name)
    {
        return {CASTOR_ENGINE_OK, {}};
    }

    const scene_entry* collision = find(new_name);

    if (collision != nullptr)
    {
        return failure(CASTOR_ENGINE_SCENE_ALREADY_EXISTS,
                       "A scene named '" + std::string(new_name) + "' already exists.");
    }

    backend_.rename_scene(entry->scene, new_name);

    if (active_scene_name_ == entry->name)
    {
        active_scene_name_ = new_name;
    }

    entry->name = new_name;
    return {CASTOR_ENGINE_OK, {}};
}

uint32_t scene_registry_subsystem::scene_count() const noexcept
{
    return static_cast<uint32_t>(scenes_.size());
}

bool scene_registry_subsystem::scene_name_at(uint32_t index, std::string& out_name) const
{
    if (index >= scenes_.size())
    {
        return false;
    }

    out_name = scenes_[index].name;
    return true;
}

bool scene_registry_subsystem::scene_exists(const char* name) const noexcept
{
    return find(name) != nullptr;
}

scene_registry_result scene_registry_subsystem::configure_display_capture(
    const char* scene_name, const char* display_id, bool uses_string_selector, const char* obs_monitor_id,
    long long obs_monitor_index, bool capture_cursor, bool recording_active, bool streaming_active)
{
    scene_entry* entry = find(scene_name);

    if (entry == nullptr)
    {
        return failure(CASTOR_ENGINE_SCENE_NOT_FOUND,
                       "No scene named '" + std::string(scene_name ? scene_name : "") + "' exists.");
    }

    if (entry->display_capture_active && entry->display_id == display_id && entry->capture_cursor == capture_cursor)
    {
        return {CASTOR_ENGINE_OK, {}};
    }

    if (recording_active)
    {
        return failure(CASTOR_ENGINE_DISPLAY_RECONFIGURATION_WHILE_RECORDING,
                       "The display capture cannot be replaced while a recording is active.");
    }

    if (streaming_active)
    {
        return failure(CASTOR_ENGINE_DISPLAY_RECONFIGURATION_WHILE_STREAMING,
                       "The display capture cannot be replaced while streaming is active.");
    }

    if (!backend_.is_display_source_available())
    {
        return failure(CASTOR_ENGINE_DISPLAY_SOURCE_UNAVAILABLE,
                       "The loaded OBS modules do not provide the 'monitor_capture' video source.");
    }

    void* replacement_source =
        backend_.create_display_source(uses_string_selector, obs_monitor_id, obs_monitor_index, capture_cursor);

    if (replacement_source == nullptr)
    {
        return failure(CASTOR_ENGINE_DISPLAY_SOURCE_CREATION_FAILED,
                       "OBS failed to create the display capture source for display '" + std::string(display_id) +
                           "'.");
    }

    void* replacement_item = backend_.add_source_to_scene(entry->scene, replacement_source);

    if (replacement_item == nullptr)
    {
        backend_.release_source(replacement_source);
        backend_.wait_for_deferred_destruction();
        return failure(CASTOR_ENGINE_DISPLAY_SOURCE_ADD_FAILED,
                       "OBS failed to add the display capture source to scene '" + entry->name + "'.");
    }

    release_source_from_entry(*entry);

    entry->visual_source = replacement_source;
    entry->scene_item = replacement_item;
    entry->display_id = display_id;
    entry->capture_cursor = capture_cursor;
    entry->display_capture_active = true;
    backend_.wait_for_deferred_destruction();
    return {CASTOR_ENGINE_OK, {}};
}

bool scene_registry_subsystem::is_display_capture_active(const char* scene_name) const noexcept
{
    const scene_entry* entry = find(scene_name);
    return entry != nullptr && entry->display_capture_active;
}

scene_registry_result scene_registry_subsystem::switch_scene(const char* name,
                                                             const castor_engine_scene_transition_config_t& transition,
                                                             bool video_ready, uint32_t width, uint32_t height)
{
    if (is_blank(name))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "A scene name must be a non-empty string.");
    }

    scene_entry* target = find(name);

    if (target == nullptr)
    {
        return failure(CASTOR_ENGINE_SCENE_NOT_FOUND, "No scene named '" + std::string(name) + "' exists.");
    }

    if (active_scene_name_ == target->name)
    {
        return {CASTOR_ENGINE_OK, {}};
    }

    if (!video_ready || width == 0 || height == 0)
    {
        return failure(CASTOR_ENGINE_VIDEO_NOT_CONFIGURED,
                       "The video subsystem must be configured before scenes can be switched.");
    }

    void* target_source = backend_.get_scene_source(target->scene);
    const auto transition_type = static_cast<castor_engine_scene_transition_type_t>(transition.type);

    if (active_scene_name_.empty() || transition_type == CASTOR_ENGINE_SCENE_TRANSITION_CUT)
    {
        backend_.set_output_source(target_source);
        active_scene_name_ = target->name;
        output_is_transition_ = false;
        return {CASTOR_ENGINE_OK, {}};
    }

    if (!backend_.is_transition_type_available(transition_type))
    {
        return failure(CASTOR_ENGINE_SCENE_TRANSITION_UNAVAILABLE,
                       "The requested transition type is not available in the loaded OBS modules.");
    }

    if (!has_transition_ || current_transition_type_ != transition_type)
    {
        void* new_transition = backend_.create_transition(transition_type);

        if (new_transition == nullptr)
        {
            return failure(CASTOR_ENGINE_SCENE_TRANSITION_CREATION_FAILED,
                           "OBS failed to create the requested transition.");
        }

        backend_.set_transition_size(new_transition, width, height);

        // Seed the new transition with whatever is currently on the output
        // channel - a scene (the first transition switch, or the switch
        // right after a cut) or a different-typed transition (every prior
        // switch fully completes before the next one starts, so there is
        // never an in-flight animation to preserve) - and attach it.
        backend_.seed_transition(new_transition, backend_.get_output_source());

        if (has_transition_)
        {
            backend_.release_transition(current_transition_);
        }

        current_transition_ = new_transition;
        current_transition_type_ = transition_type;
        has_transition_ = true;
    }
    else if (!output_is_transition_)
    {
        // Same type as last time, but a cut switch since then detached it
        // from the output: reseed and reattach the cached transition.
        backend_.seed_transition(current_transition_, backend_.get_output_source());
    }

    if (!backend_.start_transition(current_transition_, target_source, transition.duration_ms))
    {
        return failure(CASTOR_ENGINE_SCENE_TRANSITION_START_FAILED, "OBS failed to start or complete the transition.");
    }

    active_scene_name_ = target->name;
    output_is_transition_ = true;
    return {CASTOR_ENGINE_OK, {}};
}

bool scene_registry_subsystem::has_active_scene() noexcept
{
    return !active_scene_name_.empty() && backend_.has_output_source();
}

bool scene_registry_subsystem::active_scene_name(std::string& out_name) const noexcept
{
    if (active_scene_name_.empty())
    {
        return false;
    }

    out_name = active_scene_name_;
    return true;
}

void scene_registry_subsystem::reset() noexcept
{
    const bool released_resources = !scenes_.empty() || has_transition_;

    for (scene_entry& entry : scenes_)
    {
        teardown_entry(entry);
    }

    scenes_.clear();

    if (has_transition_)
    {
        backend_.release_transition(current_transition_);
        current_transition_ = nullptr;
        has_transition_ = false;
    }

    if (released_resources)
    {
        backend_.disconnect_output();
        backend_.wait_for_deferred_destruction();
    }

    active_scene_name_.clear();
    output_is_transition_ = false;
}
} // namespace castor::engine::detail
