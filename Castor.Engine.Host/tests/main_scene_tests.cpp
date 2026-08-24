#include "main_scene.h"

#include <cstdint>
#include <iostream>
#include <string>
#include <string_view>
#include <vector>

namespace
{
using castor::engine::detail::main_scene_subsystem;
using castor::engine::detail::scene_backend;

class fake_scene_backend final : public scene_backend
{
  public:
    bool scene_creation_succeeds = true;
    bool color_source_available = true;
    bool display_source_available = true;
    bool source_creation_succeeds = true;
    bool display_source_creation_succeeds = true;
    bool add_succeeds = true;
    bool connect_succeeds = true;

    uint32_t scene_creations = 0;
    uint32_t scene_releases = 0;
    uint32_t source_creations = 0;
    uint32_t source_releases = 0;
    uint32_t display_source_creations = 0;
    uint32_t add_attempts = 0;
    uint32_t item_removals = 0;
    uint32_t connect_attempts = 0;
    uint32_t disconnect_attempts = 0;
    uint32_t requested_width = 0;
    uint32_t requested_height = 0;
    std::string requested_display_id;
    bool requested_capture_cursor = false;
    std::vector<std::string> cleanup_events;

    void* create_scene() noexcept override
    {
        ++scene_creations;
        return scene_creation_succeeds ? &scene_token_ : nullptr;
    }

    void release_scene(void* scene) noexcept override
    {
        if (scene == &scene_token_)
        {
            ++scene_releases;
            cleanup_events.emplace_back("release_scene");
        }
    }

    bool is_color_source_available() noexcept override
    {
        return color_source_available;
    }

    void* create_color_source(uint32_t width, uint32_t height) noexcept override
    {
        ++source_creations;
        requested_width = width;
        requested_height = height;
        return source_creation_succeeds ? &source_token_ : nullptr;
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
        return display_source_creation_succeeds ? &display_source_token_ : nullptr;
    }

    void release_source(void* source) noexcept override
    {
        if (source == &source_token_ || source == &display_source_token_)
        {
            ++source_releases;
            cleanup_events.emplace_back("release_source");
        }
    }

    void* add_source_to_scene(void* scene, void* source) noexcept override
    {
        ++add_attempts;
        if (!add_succeeds || scene != &scene_token_)
        {
            return nullptr;
        }

        if (source == &source_token_)
        {
            return &scene_item_token_;
        }

        return source == &display_source_token_ ? &display_scene_item_token_ : nullptr;
    }

    void remove_source_from_scene(void* scene_item) noexcept override
    {
        if (scene_item == &scene_item_token_ || scene_item == &display_scene_item_token_)
        {
            ++item_removals;
            cleanup_events.emplace_back("remove_item");
        }
    }

    bool connect_scene_to_output(void* scene) noexcept override
    {
        ++connect_attempts;
        connected_ = connect_succeeds && scene == &scene_token_;
        return connected_;
    }

    bool is_scene_connected_to_output(void* scene) noexcept override
    {
        return connected_ && scene == &scene_token_;
    }

    void disconnect_scene_from_output(void* scene) noexcept override
    {
        if (scene == &scene_token_)
        {
            ++disconnect_attempts;
            connected_ = false;
            cleanup_events.emplace_back("disconnect");
        }
    }

    void wait_for_deferred_destruction() noexcept override
    {
        cleanup_events.emplace_back("flush_destruction");
    }

  private:
    int scene_token_ = 0;
    int source_token_ = 0;
    int display_source_token_ = 0;
    int scene_item_token_ = 0;
    int display_scene_item_token_ = 0;
    bool connected_ = false;
};

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
    main_scene_subsystem scene(backend);
    const auto result = scene.create(false, false, 0, 0);

    return expect(result.code == CASTOR_ENGINE_NOT_INITIALIZED, "an uninitialized runtime returns an explicit error") &&
           expect(!result.message.empty(), "an uninitialized runtime provides diagnostics") &&
           expect(backend.scene_creations == 0, "no resource is created in an invalid runtime state");
}

bool create_requires_configured_video()
{
    fake_scene_backend backend;
    main_scene_subsystem scene(backend);
    const auto result = scene.create(true, false, 0, 0);

    return expect(result.code == CASTOR_ENGINE_VIDEO_NOT_CONFIGURED,
                  "an unconfigured video subsystem returns an explicit error") &&
           expect(!result.message.empty(), "an unconfigured video subsystem provides diagnostics") &&
           expect(backend.scene_creations == 0, "no resource is created before video is ready");
}

bool scene_creation_failure_is_reported()
{
    fake_scene_backend backend;
    backend.scene_creation_succeeds = false;
    main_scene_subsystem scene(backend);
    const auto result = scene.create(true, true, 1280, 720);

    return expect(result.code == CASTOR_ENGINE_SCENE_CREATION_FAILED, "scene creation failure is explicit") &&
           expect(!result.message.empty(), "scene creation failure provides diagnostics") &&
           expect(!scene.is_active(), "a failed scene is not active");
}

bool unavailable_source_releases_scene()
{
    fake_scene_backend backend;
    backend.color_source_available = false;
    main_scene_subsystem scene(backend);
    const auto result = scene.create(true, true, 1280, 720);
    const std::vector<std::string> expected_cleanup{"disconnect", "release_scene", "flush_destruction"};

    return expect(result.code == CASTOR_ENGINE_SCENE_SOURCE_UNAVAILABLE, "source unavailability is explicit") &&
           expect(backend.scene_releases == 1, "the partially created scene is released") &&
           expect(backend.source_creations == 0, "an unavailable source is not created") &&
           expect(backend.cleanup_events == expected_cleanup,
                  "source unavailability flushes deferred scene destruction") &&
           expect(!scene.is_active(), "the partial scene is not active");
}

bool source_creation_failure_releases_scene()
{
    fake_scene_backend backend;
    backend.source_creation_succeeds = false;
    main_scene_subsystem scene(backend);
    const auto result = scene.create(true, true, 1280, 720);
    const std::vector<std::string> expected_cleanup{"disconnect", "release_scene", "flush_destruction"};

    return expect(result.code == CASTOR_ENGINE_SCENE_SOURCE_CREATION_FAILED, "source creation failure is explicit") &&
           expect(backend.scene_releases == 1, "the scene is released after source creation fails") &&
           expect(backend.source_releases == 0, "no null source is released") &&
           expect(backend.cleanup_events == expected_cleanup,
                  "source creation failure flushes deferred scene destruction") &&
           expect(!scene.is_active(), "the partial scene is not active");
}

bool add_failure_releases_every_resource()
{
    fake_scene_backend backend;
    backend.add_succeeds = false;
    main_scene_subsystem scene(backend);
    const auto result = scene.create(true, true, 1280, 720);
    const std::vector<std::string> expected_cleanup{"disconnect", "release_source", "release_scene",
                                                    "flush_destruction"};

    return expect(result.code == CASTOR_ENGINE_SCENE_SOURCE_ADD_FAILED, "source add failure is explicit") &&
           expect(backend.cleanup_events == expected_cleanup, "add failure cleans resources in lifecycle order") &&
           expect(!scene.is_active(), "the partial scene is not active");
}

bool activation_failure_releases_every_resource()
{
    fake_scene_backend backend;
    backend.connect_succeeds = false;
    main_scene_subsystem scene(backend);
    const auto result = scene.create(true, true, 1280, 720);
    const std::vector<std::string> expected_cleanup{"disconnect", "remove_item", "release_source", "release_scene",
                                                    "flush_destruction"};

    return expect(result.code == CASTOR_ENGINE_SCENE_ACTIVATION_FAILED, "activation failure is explicit") &&
           expect(backend.cleanup_events == expected_cleanup,
                  "activation failure cleans resources in lifecycle order") &&
           expect(!scene.is_active(), "the disconnected scene is not active");
}

bool create_is_idempotent_and_reset_is_complete()
{
    fake_scene_backend backend;
    main_scene_subsystem scene(backend);
    const auto first_result = scene.create(true, true, 1280, 720);
    const auto second_result = scene.create(true, true, 1280, 720);

    bool passed = expect(first_result.code == CASTOR_ENGINE_OK, "the first scene creation succeeds") &&
                  expect(second_result.code == CASTOR_ENGINE_OK, "repeated scene creation succeeds") &&
                  expect(scene.is_active(), "the scene reports active after creation") &&
                  expect(backend.scene_creations == 1, "repeated creation does not create another scene") &&
                  expect(backend.source_creations == 1, "repeated creation does not create another source") &&
                  expect(backend.add_attempts == 1, "repeated creation does not add another scene item") &&
                  expect(backend.connect_attempts == 1, "repeated creation does not reconnect the scene") &&
                  expect(backend.requested_width == 1280 && backend.requested_height == 720,
                         "the color source uses the configured base dimensions");

    scene.reset();
    const std::vector<std::string> expected_cleanup{"disconnect", "remove_item", "release_source", "release_scene",
                                                    "flush_destruction"};
    passed = expect(backend.cleanup_events == expected_cleanup, "reset cleans resources in lifecycle order") && passed;
    passed = expect(!scene.is_active(), "reset clears the active state") && passed;

    scene.reset();
    passed = expect(backend.cleanup_events == expected_cleanup, "repeated reset is a no-op") && passed;
    return passed;
}

bool display_capture_replaces_color_source_and_is_idempotent()
{
    fake_scene_backend backend;
    main_scene_subsystem scene(backend);
    const auto create_result = scene.create(true, true, 1280, 720);
    const auto first_result = scene.configure_display_capture("display-1", true, "display-1", 0, true, false, false);
    const auto second_result = scene.configure_display_capture("display-1", true, "display-1", 0, true, true, false);

    return expect(create_result.code == CASTOR_ENGINE_OK, "the initial scene is created") &&
           expect(first_result.code == CASTOR_ENGINE_OK, "display capture replaces the color source") &&
           expect(second_result.code == CASTOR_ENGINE_OK, "the same display configuration is idempotent") &&
           expect(scene.is_display_capture_active(), "display capture reports active") &&
           expect(backend.display_source_creations == 1, "an idempotent call does not recreate the display source") &&
           expect(backend.requested_display_id == "display-1", "the selected display id reaches the backend") &&
           expect(backend.requested_capture_cursor, "the cursor preference reaches the backend") &&
           expect(backend.item_removals == 1, "the color scene item is removed") &&
           expect(backend.source_releases == 1, "the color source is released");
}

bool display_capture_replacement_is_transactional()
{
    fake_scene_backend backend;
    main_scene_subsystem scene(backend);
    scene.create(true, true, 1280, 720);
    scene.configure_display_capture("display-1", true, "display-1", 0, true, false, false);
    backend.display_source_creation_succeeds = false;

    const auto result = scene.configure_display_capture("display-2", true, "display-2", 0, false, false, false);

    return expect(result.code == CASTOR_ENGINE_DISPLAY_SOURCE_CREATION_FAILED,
                  "replacement creation failure is explicit") &&
           expect(scene.is_display_capture_active(), "the previous display source remains active") &&
           expect(backend.item_removals == 1, "the previous display item is not removed after failure") &&
           expect(backend.source_releases == 1, "the previous display source is not released after failure");
}

bool display_capture_add_failure_releases_only_the_replacement()
{
    fake_scene_backend backend;
    main_scene_subsystem scene(backend);
    scene.create(true, true, 1280, 720);
    scene.configure_display_capture("display-1", true, "display-1", 0, true, false, false);
    backend.add_succeeds = false;

    const auto result = scene.configure_display_capture("display-2", true, "display-2", 0, false, false, false);

    return expect(result.code == CASTOR_ENGINE_DISPLAY_SOURCE_ADD_FAILED, "replacement add failure is explicit") &&
           expect(scene.is_display_capture_active(), "the previous display remains active after add failure") &&
           expect(backend.item_removals == 1, "the previous item is not removed after add failure") &&
           expect(backend.source_releases == 2, "only the failed replacement is additionally released");
}

bool display_capture_replacement_is_rejected_while_recording()
{
    fake_scene_backend backend;
    main_scene_subsystem scene(backend);
    scene.create(true, true, 1280, 720);
    scene.configure_display_capture("display-1", true, "display-1", 0, true, false, false);

    const auto result = scene.configure_display_capture("display-2", true, "display-2", 0, true, true, false);

    return expect(result.code == CASTOR_ENGINE_DISPLAY_RECONFIGURATION_WHILE_RECORDING,
                  "replacement while recording is explicit") &&
           expect(backend.display_source_creations == 1, "no replacement source is created while recording") &&
           expect(scene.is_display_capture_active(), "the existing display source remains active");
}

bool display_capture_replacement_is_rejected_while_streaming()
{
    fake_scene_backend backend;
    main_scene_subsystem scene(backend);
    scene.create(true, true, 1280, 720);
    scene.configure_display_capture("display-1", true, "display-1", 0, true, false, false);

    const auto result = scene.configure_display_capture("display-2", true, "display-2", 0, true, false, true);

    return expect(result.code == CASTOR_ENGINE_DISPLAY_RECONFIGURATION_WHILE_STREAMING,
                  "replacement while streaming is explicit") &&
           expect(backend.display_source_creations == 1, "no replacement source is created while streaming") &&
           expect(scene.is_display_capture_active(), "the existing display source remains active");
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
        {"create_requires_configured_video", create_requires_configured_video},
        {"scene_creation_failure_is_reported", scene_creation_failure_is_reported},
        {"unavailable_source_releases_scene", unavailable_source_releases_scene},
        {"source_creation_failure_releases_scene", source_creation_failure_releases_scene},
        {"add_failure_releases_every_resource", add_failure_releases_every_resource},
        {"activation_failure_releases_every_resource", activation_failure_releases_every_resource},
        {"create_is_idempotent_and_reset_is_complete", create_is_idempotent_and_reset_is_complete},
        {"display_capture_replaces_color_source_and_is_idempotent",
         display_capture_replaces_color_source_and_is_idempotent},
        {"display_capture_replacement_is_transactional", display_capture_replacement_is_transactional},
        {"display_capture_add_failure_releases_only_the_replacement",
         display_capture_add_failure_releases_only_the_replacement},
        {"display_capture_replacement_is_rejected_while_recording",
         display_capture_replacement_is_rejected_while_recording},
        {"display_capture_replacement_is_rejected_while_streaming",
         display_capture_replacement_is_rejected_while_streaming},
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
        std::cerr << failures << " native main-scene test(s) failed.\n";
        return 1;
    }

    std::cout << "All native main-scene tests passed.\n";
    return 0;
}
