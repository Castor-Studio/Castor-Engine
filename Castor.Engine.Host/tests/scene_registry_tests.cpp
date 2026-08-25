#include "scene_registry.h"

#include <cstdint>
#include <iostream>
#include <memory>
#include <set>
#include <string>
#include <string_view>
#include <vector>

namespace
{
using castor::engine::detail::scene_registry_subsystem;
using castor::engine::detail::scene_backend;

class fake_scene_backend final : public scene_backend
{
  public:
    bool scene_creation_succeeds = true;
    bool display_source_available = true;
    bool display_source_creation_succeeds = true;
    bool add_succeeds = true;
    bool transition_type_available = true;
    bool transition_creation_succeeds = true;
    bool transition_start_succeeds = true;

    uint32_t scene_creations = 0;
    uint32_t scene_releases = 0;
    uint32_t rename_calls = 0;
    uint32_t display_source_creations = 0;
    uint32_t source_releases = 0;
    uint32_t add_attempts = 0;
    uint32_t item_removals = 0;
    uint32_t set_output_calls = 0;
    uint32_t transition_creations = 0;
    uint32_t transition_releases = 0;
    uint32_t swap_calls = 0;
    uint32_t seed_calls = 0;
    uint32_t start_transition_calls = 0;

    std::string requested_display_id;
    bool requested_capture_cursor = false;
    std::string last_renamed_to;
    castor_engine_scene_transition_type_t last_transition_type = CASTOR_ENGINE_SCENE_TRANSITION_CUT;
    uint32_t last_transition_duration = 0;
    void* last_transition_target = nullptr;
    std::vector<std::string> cleanup_events;

    void* create_scene(const char*) noexcept override
    {
        ++scene_creations;

        if (!scene_creation_succeeds)
        {
            return nullptr;
        }

        scenes_.push_back(std::make_unique<int>(0));
        return scenes_.back().get();
    }

    void release_scene(void* scene) noexcept override
    {
        ++scene_releases;
        cleanup_events.emplace_back("release_scene");
        released_.insert(scene);
    }

    void rename_scene(void*, const char* new_name) noexcept override
    {
        ++rename_calls;
        last_renamed_to = new_name;
    }

    void* get_scene_source(void* scene) noexcept override
    {
        return scene;
    }

    bool is_display_source_available() noexcept override
    {
        return display_source_available;
    }

    void* create_display_source(bool, const char* display_id, long long, bool capture_cursor) noexcept override
    {
        ++display_source_creations;
        requested_display_id = display_id;
        requested_capture_cursor = capture_cursor;

        if (!display_source_creation_succeeds)
        {
            return nullptr;
        }

        sources_.push_back(std::make_unique<int>(0));
        return sources_.back().get();
    }

    void release_source(void* source) noexcept override
    {
        ++source_releases;
        cleanup_events.emplace_back("release_source");
        released_.insert(source);
    }

    void* add_source_to_scene(void* scene, void* source) noexcept override
    {
        ++add_attempts;

        if (!add_succeeds || released_.count(scene) != 0)
        {
            return nullptr;
        }

        items_.push_back(std::make_unique<int>(0));
        return items_.back().get();
    }

    void remove_source_from_scene(void*) noexcept override
    {
        ++item_removals;
        cleanup_events.emplace_back("remove_item");
    }

    void set_output_source(void* source) noexcept override
    {
        ++set_output_calls;
        output_source_ = source;
    }

    void* get_output_source() noexcept override
    {
        return output_source_;
    }

    bool has_output_source() noexcept override
    {
        return output_source_ != nullptr;
    }

    void disconnect_output() noexcept override
    {
        output_source_ = nullptr;
        cleanup_events.emplace_back("disconnect_output");
    }

    bool is_transition_type_available(castor_engine_scene_transition_type_t) noexcept override
    {
        return transition_type_available;
    }

    void* create_transition(castor_engine_scene_transition_type_t type) noexcept override
    {
        ++transition_creations;
        last_transition_type = type;

        if (!transition_creation_succeeds)
        {
            return nullptr;
        }

        transitions_.push_back(std::make_unique<int>(0));
        return transitions_.back().get();
    }

    void release_transition(void* transition) noexcept override
    {
        ++transition_releases;
        cleanup_events.emplace_back("release_transition");
        released_.insert(transition);
    }

    void set_transition_size(void*, uint32_t, uint32_t) noexcept override
    {
    }

    void swap_transition(void* transition, void*) noexcept override
    {
        ++swap_calls;
        output_source_ = transition;
    }

    void seed_transition(void* transition, void*) noexcept override
    {
        ++seed_calls;
        output_source_ = transition;
    }

    bool start_transition(void* transition, void* target_source, uint32_t duration_ms) noexcept override
    {
        ++start_transition_calls;
        last_transition_duration = duration_ms;
        last_transition_target = target_source;

        if (!transition_start_succeeds)
        {
            return false;
        }

        output_source_ = transition;
        return true;
    }

    void wait_for_deferred_destruction() noexcept override
    {
        cleanup_events.emplace_back("flush_destruction");
    }

  private:
    std::vector<std::unique_ptr<int>> scenes_;
    std::vector<std::unique_ptr<int>> sources_;
    std::vector<std::unique_ptr<int>> items_;
    std::vector<std::unique_ptr<int>> transitions_;
    std::set<void*> released_;
    void* output_source_ = nullptr;
};

castor_engine_scene_transition_config_t make_transition(castor_engine_scene_transition_type_t type,
                                                         uint32_t duration_ms)
{
    return {static_cast<uint32_t>(sizeof(castor_engine_scene_transition_config_t)), static_cast<uint32_t>(type),
            duration_ms};
}

bool expect(bool condition, std::string_view message)
{
    if (!condition)
    {
        std::cerr << "  expectation failed: " << message << '\n';
    }

    return condition;
}

bool create_requires_initialized_runtime()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    const auto result = registry.create_scene("wide", false);

    return expect(result.code == CASTOR_ENGINE_NOT_INITIALIZED, "an uninitialized runtime returns an explicit error") &&
           expect(backend.scene_creations == 0, "no resource is created in an invalid runtime state");
}

bool create_requires_nonempty_name()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    const auto result = registry.create_scene("", true);

    return expect(result.code == CASTOR_ENGINE_SCENE_INVALID_NAME, "a blank name is rejected") &&
           expect(backend.scene_creations == 0, "no resource is created for an invalid name");
}

bool create_rejects_duplicate_name_and_lists_scenes()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.create_scene("halftime", true);
    const auto duplicate = registry.create_scene("wide", true);

    std::string first;
    std::string second;
    std::string third;

    return expect(duplicate.code == CASTOR_ENGINE_SCENE_ALREADY_EXISTS, "a duplicate name is rejected") &&
           expect(registry.scene_count() == 3, "the registry lists exactly the created scenes") &&
           expect(registry.scene_name_at(0, first) && first == "wide", "the first scene name is preserved") &&
           expect(registry.scene_name_at(1, second) && second == "closeup", "the second scene name is preserved") &&
           expect(registry.scene_name_at(2, third) && third == "halftime", "the third scene name is preserved") &&
           expect(!registry.scene_name_at(3, first), "an out-of-range index reports no scene");
}

bool create_failure_is_reported()
{
    fake_scene_backend backend;
    backend.scene_creation_succeeds = false;
    scene_registry_subsystem registry(backend);
    const auto result = registry.create_scene("wide", true);

    return expect(result.code == CASTOR_ENGINE_SCENE_CREATION_FAILED, "scene creation failure is explicit") &&
           expect(registry.scene_count() == 0, "a failed creation is not registered");
}

bool delete_unknown_scene_is_not_found()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    const auto result = registry.delete_scene("ghost");

    return expect(result.code == CASTOR_ENGINE_SCENE_NOT_FOUND, "deleting an unknown scene is explicit");
}

bool delete_active_scene_is_rejected()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    const auto result = registry.delete_scene("wide");

    return expect(result.code == CASTOR_ENGINE_SCENE_DELETE_ACTIVE_SCENE, "deleting the active scene is rejected") &&
           expect(registry.scene_count() == 1, "the active scene is not removed");
}

bool delete_scene_releases_resources()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);

    const auto result = registry.delete_scene("closeup");

    return expect(result.code == CASTOR_ENGINE_OK, "deleting an inactive scene succeeds") &&
           expect(registry.scene_count() == 1, "the deleted scene is removed from the registry") &&
           expect(backend.scene_releases == 1, "the deleted scene's OBS resource is released") &&
           expect(backend.cleanup_events.back() == "flush_destruction", "deletion flushes deferred destruction");
}

bool rename_unknown_scene_is_not_found()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    const auto result = registry.rename_scene("ghost", "renamed");

    return expect(result.code == CASTOR_ENGINE_SCENE_NOT_FOUND, "renaming an unknown scene is explicit");
}

bool rename_to_blank_name_is_invalid()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    const auto result = registry.rename_scene("wide", "");

    return expect(result.code == CASTOR_ENGINE_SCENE_INVALID_NAME, "a blank new name is rejected");
}

bool rename_to_existing_name_is_rejected()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    const auto result = registry.rename_scene("wide", "closeup");

    return expect(result.code == CASTOR_ENGINE_SCENE_ALREADY_EXISTS, "renaming onto an existing name is rejected");
}

bool rename_updates_active_scene_tracking()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    const auto result = registry.rename_scene("wide", "main-camera");
    std::string active;

    return expect(result.code == CASTOR_ENGINE_OK, "renaming an existing scene succeeds") &&
           expect(backend.rename_calls == 1, "the backend performs the rename") &&
           expect(backend.last_renamed_to == "main-camera", "the requested new name reaches the backend") &&
           expect(registry.active_scene_name(active) && active == "main-camera",
                  "the active scene name follows the rename") &&
           expect(registry.delete_scene("main-camera").code == CASTOR_ENGINE_SCENE_DELETE_ACTIVE_SCENE,
                  "the renamed scene is still tracked as active");
}

bool first_switch_binds_directly_without_transition()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);

    const auto result = registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 500), true,
                                              1280, 720);
    std::string active;

    return expect(result.code == CASTOR_ENGINE_OK, "the first switch succeeds") &&
           expect(backend.set_output_calls == 1, "the first switch binds output directly") &&
           expect(backend.transition_creations == 0, "the first switch never creates a transition") &&
           expect(backend.start_transition_calls == 0, "the first switch never starts a transition") &&
           expect(registry.active_scene_name(active) && active == "wide", "the active scene is tracked");
}

bool switch_to_unknown_scene_is_not_found_and_state_unchanged()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    const auto result =
        registry.switch_scene("ghost", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);
    std::string active;

    return expect(result.code == CASTOR_ENGINE_SCENE_NOT_FOUND, "switching to an unknown scene is explicit") &&
           expect(registry.active_scene_name(active) && active == "wide", "the active scene is unchanged");
}

bool switch_to_active_scene_is_noop()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);
    const uint32_t calls_before = backend.set_output_calls;

    const auto result = registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 500), true,
                                              1280, 720);

    return expect(result.code == CASTOR_ENGINE_OK, "switching to the active scene is a no-op") &&
           expect(backend.set_output_calls == calls_before, "no additional backend call is made") &&
           expect(backend.transition_creations == 0, "no transition is created for a no-op switch");
}

bool cut_switch_always_binds_directly()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    const auto result =
        registry.switch_scene("closeup", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);
    std::string active;

    return expect(result.code == CASTOR_ENGINE_OK, "a cut switch succeeds") &&
           expect(backend.transition_creations == 0, "a cut switch never creates a transition") &&
           expect(backend.start_transition_calls == 0, "a cut switch never starts a transition") &&
           expect(registry.active_scene_name(active) && active == "closeup", "the active scene updates");
}

bool fade_switch_uses_transition_backend()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    const auto result = registry.switch_scene("closeup", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 750),
                                              true, 1280, 720);
    std::string active;

    return expect(result.code == CASTOR_ENGINE_OK, "a fade switch succeeds") &&
           expect(backend.transition_creations == 1, "a fade switch creates a transition") &&
           expect(backend.seed_calls == 1,
                  "switching from a directly-bound scene seeds the new transition instead of swapping") &&
           expect(backend.swap_calls == 0, "no swap happens when there was no prior transition") &&
           expect(backend.start_transition_calls == 1, "a fade switch starts the transition") &&
           expect(backend.last_transition_type == CASTOR_ENGINE_SCENE_TRANSITION_FADE,
                  "the requested transition type reaches the backend") &&
           expect(backend.last_transition_duration == 750, "the requested duration reaches the backend") &&
           expect(registry.active_scene_name(active) && active == "closeup", "the active scene updates");
}

bool switching_same_type_twice_reuses_transition()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.create_scene("halftime", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);
    registry.switch_scene("closeup", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 500), true, 1280, 720);

    registry.switch_scene("halftime", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 300), true, 1280, 720);

    return expect(backend.transition_creations == 1, "repeating the same transition type reuses the object") &&
           expect(backend.start_transition_calls == 2, "each switch still starts the transition");
}

bool cut_between_same_type_transitions_reseeds_instead_of_reusing_directly()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.create_scene("halftime", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);
    registry.switch_scene("closeup", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 500), true, 1280, 720);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    const auto result = registry.switch_scene("halftime", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 500),
                                              true, 1280, 720);

    return expect(result.code == CASTOR_ENGINE_OK, "fading again after an intervening cut succeeds") &&
           expect(backend.transition_creations == 1, "the same-type transition object is reused, not recreated") &&
           expect(backend.seed_calls == 2,
                  "the cached transition is reseeded each time a cut detaches it from the output");
}

bool switching_between_different_types_swaps_transition()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.create_scene("halftime", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);
    registry.switch_scene("closeup", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 500), true, 1280, 720);

    registry.switch_scene("halftime", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_SLIDE, 300), true, 1280, 720);

    return expect(backend.transition_creations == 2, "a different transition type creates a new transition object") &&
           expect(backend.transition_releases == 1, "the previous transition object is released") &&
           expect(backend.seed_calls == 1, "the first fade after a cut seeds rather than swaps") &&
           expect(backend.swap_calls == 1,
                  "switching between two active transition types swaps the transition object") &&
           expect(backend.last_transition_type == CASTOR_ENGINE_SCENE_TRANSITION_SLIDE,
                  "the newly requested type reaches the backend");
}

bool transition_unavailable_is_reported()
{
    fake_scene_backend backend;
    backend.transition_type_available = false;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    const auto result = registry.switch_scene("closeup", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_SWIPE, 500),
                                              true, 1280, 720);
    std::string active;

    return expect(result.code == CASTOR_ENGINE_SCENE_TRANSITION_UNAVAILABLE, "an unavailable type is explicit") &&
           expect(registry.active_scene_name(active) && active == "wide", "the active scene is unchanged");
}

bool transition_creation_failure_is_reported()
{
    fake_scene_backend backend;
    backend.transition_creation_succeeds = false;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    const auto result = registry.switch_scene("closeup", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 500),
                                              true, 1280, 720);
    std::string active;

    return expect(result.code == CASTOR_ENGINE_SCENE_TRANSITION_CREATION_FAILED,
                  "a transition creation failure is explicit") &&
           expect(registry.active_scene_name(active) && active == "wide", "the active scene is unchanged");
}

bool transition_start_failure_is_reported_and_state_unchanged()
{
    fake_scene_backend backend;
    backend.transition_start_succeeds = false;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    const auto result = registry.switch_scene("closeup", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 500),
                                              true, 1280, 720);
    std::string active;

    return expect(result.code == CASTOR_ENGINE_SCENE_TRANSITION_START_FAILED,
                  "a transition start failure is explicit") &&
           expect(registry.active_scene_name(active) && active == "wide",
                  "the active scene is unchanged after a failed transition");
}

bool configure_display_capture_targets_specific_scene_independent_of_active()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    const auto result =
        registry.configure_display_capture("closeup", "display-1", true, "display-1", 0, true, false, false);

    return expect(result.code == CASTOR_ENGINE_OK, "configuring a backgrounded scene succeeds") &&
           expect(registry.is_display_capture_active("closeup"), "the backgrounded scene reports capture active") &&
           expect(!registry.is_display_capture_active("wide"), "the active scene is unaffected");
}

bool configure_display_capture_unknown_scene_is_not_found()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    const auto result =
        registry.configure_display_capture("ghost", "display-1", true, "display-1", 0, true, false, false);

    return expect(result.code == CASTOR_ENGINE_SCENE_NOT_FOUND, "configuring an unknown scene is explicit");
}

bool reset_tears_down_everything_and_permits_restart()
{
    fake_scene_backend backend;
    scene_registry_subsystem registry(backend);
    registry.create_scene("wide", true);
    registry.create_scene("closeup", true);
    registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);
    registry.switch_scene("closeup", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_FADE, 500), true, 1280, 720);

    registry.reset();

    bool passed = expect(registry.scene_count() == 0, "reset clears the registry") &&
                  expect(!registry.has_active_scene(), "reset clears the active scene") &&
                  expect(backend.scene_releases == 2, "reset releases every scene") &&
                  expect(backend.transition_releases == 1, "reset releases the cached transition") &&
                  expect(!backend.has_output_source(), "reset disconnects the output");

    const auto create_result = registry.create_scene("wide", true);
    const auto switch_result =
        registry.switch_scene("wide", make_transition(CASTOR_ENGINE_SCENE_TRANSITION_CUT, 0), true, 1280, 720);

    passed = expect(create_result.code == CASTOR_ENGINE_OK, "a fresh scene can be created after reset") && passed;
    passed = expect(switch_result.code == CASTOR_ENGINE_OK, "a fresh switch succeeds after reset") && passed;
    return passed;
}

struct test_case
{
    std::string_view name;
    bool (*run)();
};
} // namespace

int main()
{
    const std::vector<test_case> tests{
        {"create_requires_initialized_runtime", create_requires_initialized_runtime},
        {"create_requires_nonempty_name", create_requires_nonempty_name},
        {"create_rejects_duplicate_name_and_lists_scenes", create_rejects_duplicate_name_and_lists_scenes},
        {"create_failure_is_reported", create_failure_is_reported},
        {"delete_unknown_scene_is_not_found", delete_unknown_scene_is_not_found},
        {"delete_active_scene_is_rejected", delete_active_scene_is_rejected},
        {"delete_scene_releases_resources", delete_scene_releases_resources},
        {"rename_unknown_scene_is_not_found", rename_unknown_scene_is_not_found},
        {"rename_to_blank_name_is_invalid", rename_to_blank_name_is_invalid},
        {"rename_to_existing_name_is_rejected", rename_to_existing_name_is_rejected},
        {"rename_updates_active_scene_tracking", rename_updates_active_scene_tracking},
        {"first_switch_binds_directly_without_transition", first_switch_binds_directly_without_transition},
        {"switch_to_unknown_scene_is_not_found_and_state_unchanged",
         switch_to_unknown_scene_is_not_found_and_state_unchanged},
        {"switch_to_active_scene_is_noop", switch_to_active_scene_is_noop},
        {"cut_switch_always_binds_directly", cut_switch_always_binds_directly},
        {"fade_switch_uses_transition_backend", fade_switch_uses_transition_backend},
        {"switching_same_type_twice_reuses_transition", switching_same_type_twice_reuses_transition},
        {"cut_between_same_type_transitions_reseeds_instead_of_reusing_directly",
         cut_between_same_type_transitions_reseeds_instead_of_reusing_directly},
        {"switching_between_different_types_swaps_transition", switching_between_different_types_swaps_transition},
        {"transition_unavailable_is_reported", transition_unavailable_is_reported},
        {"transition_creation_failure_is_reported", transition_creation_failure_is_reported},
        {"transition_start_failure_is_reported_and_state_unchanged",
         transition_start_failure_is_reported_and_state_unchanged},
        {"configure_display_capture_targets_specific_scene_independent_of_active",
         configure_display_capture_targets_specific_scene_independent_of_active},
        {"configure_display_capture_unknown_scene_is_not_found", configure_display_capture_unknown_scene_is_not_found},
        {"reset_tears_down_everything_and_permits_restart", reset_tears_down_everything_and_permits_restart},
    };

    uint32_t failures = 0;

    for (const test_case& test : tests)
    {
        std::cout << test.name << '\n';

        if (!test.run())
        {
            ++failures;
        }
    }

    if (failures != 0)
    {
        std::cerr << failures << " native scene-registry test(s) failed.\n";
        return 1;
    }

    std::cout << "All native scene-registry tests passed.\n";
    return 0;
}
