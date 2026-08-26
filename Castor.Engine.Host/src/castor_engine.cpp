#include "castor_engine.h"

#include "audio_configuration.h"
#include "audio_encoder_configuration.h"
#include "audio_encoder_subsystem.h"
#include "audio_subsystem.h"
#include "display_capture_configuration.h"
#include "obs_display_enumeration.h"
#include "obs_scene_backend.h"
#include "obs_streaming_backend.h"
#include "recording_configuration.h"
#include "recording_subsystem.h"
#include "scene_registry.h"
#include "streaming_configuration.h"
#include "streaming_subsystem.h"
#include "video_configuration.h"
#include "video_encoder_configuration.h"
#include "video_encoder_enumeration.h"
#include "video_encoder_subsystem.h"

#include <algorithm>
#include <cstring>
#include <filesystem>
#include <mutex>
#include <obs.h>
#include <sstream>
#include <string>
#include <utility>

namespace
{
std::mutex lifecycle_mutex;
bool modules_loaded = false;
std::string registered_libobs_data_path;
thread_local std::string last_error;
std::vector<castor::engine::detail::display_descriptor> display_snapshot;
bool display_snapshot_valid = false;

castor::engine::detail::video_subsystem video;
castor::engine::detail::audio_subsystem audio;
castor::engine::detail::video_encoder_subsystem video_encoder;
castor::engine::detail::audio_encoder_subsystem audio_encoder;
castor::engine::detail::recording_subsystem recording;
castor::engine::detail::obs_streaming_backend streaming_backend;
castor::engine::detail::streaming_subsystem streaming(streaming_backend);
castor::engine::detail::obs_scene_backend scene_backend;
castor::engine::detail::scene_registry_subsystem scene_registry(scene_backend);

std::string path_to_utf8(const std::filesystem::path& path)
{
    const auto utf8_path = path.generic_u8string();

    return std::string(reinterpret_cast<const char*>(utf8_path.data()), utf8_path.size());
}

void set_last_error(std::string message)
{
    last_error = std::move(message);
}

void copy_to_fixed_buffer(const std::string& source, char* destination, size_t destination_size)
{
    std::memset(destination, 0, destination_size);
    const size_t copy_size = std::min(source.size(), destination_size - 1);
    std::memcpy(destination, source.data(), copy_size);
}

castor_engine_result_t refresh_display_snapshot()
{
    display_snapshot.clear();
    display_snapshot_valid = false;

    if (!obs_initialized() || !modules_loaded)
    {
        set_last_error("The engine and its OBS modules must be initialized before displays can be enumerated.");
        return CASTOR_ENGINE_NOT_INITIALIZED;
    }

    castor::engine::detail::display_enumeration_result result = castor::engine::detail::enumerate_obs_displays();

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
        return result.code;
    }

    display_snapshot = std::move(result.displays);
    display_snapshot_valid = true;
    return CASTOR_ENGINE_OK;
}

void unregister_libobs_data_path()
{
    if (registered_libobs_data_path.empty())
    {
        return;
    }

#if defined(_MSC_VER)
#pragma warning(push)
#pragma warning(disable : 4996)
#endif
    obs_remove_data_path(registered_libobs_data_path.c_str());
#if defined(_MSC_VER)
#pragma warning(pop)
#endif
    registered_libobs_data_path.clear();
}

void register_libobs_data_path(std::string path)
{
    if (registered_libobs_data_path == path)
    {
        return;
    }

    unregister_libobs_data_path();

#if defined(_MSC_VER)
#pragma warning(push)
#pragma warning(disable : 4996)
#endif
    obs_add_data_path(path.c_str());
#if defined(_MSC_VER)
#pragma warning(pop)
#endif
    registered_libobs_data_path = std::move(path);
}

void count_loaded_module(void* parameter, obs_module_t*)
{
    auto* count = static_cast<uint32_t*>(parameter);
    ++(*count);
}

uint32_t get_loaded_module_count()
{
    uint32_t count = 0;
    obs_enum_modules(count_loaded_module, &count);
    return count;
}

std::string describe_module_failures(const obs_module_failure_info& failure_info)
{
    std::ostringstream message;
    message << "Failed to load " << failure_info.count << " OBS module";

    if (failure_info.count != 1)
    {
        message << 's';
    }

    if (failure_info.count != 0)
    {
        message << ": ";
    }

    for (size_t index = 0; index < failure_info.count; ++index)
    {
        if (index != 0)
        {
            message << ", ";
        }

        const char* module_name = failure_info.failed_modules[index];
        message << (module_name != nullptr ? module_name : "<unknown>");
    }

    message << '.';
    return message.str();
}

void rollback_startup(bool started_here)
{
    modules_loaded = false;
    display_snapshot.clear();
    display_snapshot_valid = false;
    recording.reset();
    streaming.reset();
    scene_registry.reset();
    video_encoder.reset();
    audio_encoder.reset();
    video.reset();
    audio.reset();
    unregister_libobs_data_path();

    if (started_here && obs_initialized())
    {
        obs_shutdown();
    }
}

} // namespace

uint32_t castor_engine_get_abi_version(void)
{
    return CASTOR_ENGINE_ABI_VERSION;
}

const char* castor_engine_get_version(void)
{
    return CASTOR_ENGINE_VERSION;
}

const char* castor_engine_get_obs_version(void)
{
    return obs_get_version_string();
}

castor_engine_result_t castor_engine_initialize(const castor_engine_config_t* config)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();
    bool started_here = false;

    if (config == nullptr)
    {
        set_last_error("The engine configuration must not be null.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    if (config->struct_size < sizeof(castor_engine_config_t))
    {
        set_last_error("The engine configuration structure is too small. Expected at least " +
                       std::to_string(sizeof(castor_engine_config_t)) + " bytes, received " +
                       std::to_string(config->struct_size) + ".");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    if (config->runtime_root == nullptr || config->runtime_root[0] == '\0')
    {
        set_last_error("The Castor runtime root must not be null or empty.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    try
    {
        const auto runtime_root = std::filesystem::u8path(config->runtime_root);
        const auto plugin_binary_directory = runtime_root / "obs-plugins" / "64bit";
        const auto plugin_data_directory = runtime_root / "data" / "obs-plugins";
        const auto libobs_data_directory = runtime_root / "data" / "libobs";

        std::error_code filesystem_error;

        if (!std::filesystem::is_directory(runtime_root, filesystem_error))
        {
            set_last_error("The Castor runtime root is not a valid directory: " + path_to_utf8(runtime_root) + ".");
            return CASTOR_ENGINE_INVALID_RUNTIME;
        }

        filesystem_error.clear();

        if (!std::filesystem::is_directory(plugin_binary_directory, filesystem_error))
        {
            set_last_error("The OBS plugin binary directory is missing: " + path_to_utf8(plugin_binary_directory) +
                           ".");
            return CASTOR_ENGINE_INVALID_RUNTIME;
        }

        filesystem_error.clear();

        if (!std::filesystem::is_directory(plugin_data_directory, filesystem_error))
        {
            set_last_error("The OBS plugin data directory is missing: " + path_to_utf8(plugin_data_directory) + ".");
            return CASTOR_ENGINE_INVALID_RUNTIME;
        }

        filesystem_error.clear();

        if (!std::filesystem::is_directory(libobs_data_directory, filesystem_error))
        {
            set_last_error("The libobs data directory is missing: " + path_to_utf8(libobs_data_directory) + ".");
            return CASTOR_ENGINE_INVALID_RUNTIME;
        }

        if (modules_loaded && obs_initialized())
        {
            return CASTOR_ENGINE_OK;
        }

        modules_loaded = false;

        started_here = !obs_initialized();
        const char* locale = config->locale != nullptr && config->locale[0] != '\0' ? config->locale : "en-US";

        if (started_here && !obs_startup(locale, nullptr, nullptr))
        {
            set_last_error("OBS failed to start.");
            return CASTOR_ENGINE_OBS_STARTUP_FAILED;
        }

        const std::string plugin_binary_path = path_to_utf8(plugin_binary_directory);
        const std::string plugin_data_path = path_to_utf8(plugin_data_directory / "%module%");
        const std::string libobs_data_path = path_to_utf8(libobs_data_directory) + "/";

        register_libobs_data_path(libobs_data_path);

        obs_add_module_path(plugin_binary_path.c_str(), plugin_data_path.c_str());

        obs_module_failure_info failure_info{};
        obs_load_all_modules2(&failure_info);

        if (failure_info.count != 0)
        {
            const std::string failure_message = describe_module_failures(failure_info);
            obs_module_failure_info_free(&failure_info);
            rollback_startup(started_here);
            set_last_error(failure_message);
            return CASTOR_ENGINE_MODULE_LOAD_FAILED;
        }

        obs_module_failure_info_free(&failure_info);

        if (get_loaded_module_count() == 0)
        {
            rollback_startup(started_here);
            set_last_error("No OBS modules were loaded from: " + plugin_binary_path + ".");
            return CASTOR_ENGINE_MODULE_LOAD_FAILED;
        }

        obs_post_load_modules();
        modules_loaded = true;
        return CASTOR_ENGINE_OK;
    }
    catch (const std::filesystem::filesystem_error& exception)
    {
        rollback_startup(started_here);
        set_last_error(std::string("Failed to process the Castor runtime path: ") + exception.what());
        return CASTOR_ENGINE_INVALID_RUNTIME;
    }
    catch (const std::exception& exception)
    {
        rollback_startup(started_here);
        set_last_error(std::string("Unexpected native initialization failure: ") + exception.what());
        return CASTOR_ENGINE_MODULE_LOAD_FAILED;
    }
    catch (...)
    {
        rollback_startup(started_here);
        set_last_error("Unexpected native initialization failure.");
        return CASTOR_ENGINE_MODULE_LOAD_FAILED;
    }
}

const char* castor_engine_get_last_error(void)
{
    return last_error.c_str();
}

uint32_t castor_engine_get_loaded_module_count(void)
{
    std::scoped_lock lock(lifecycle_mutex);

    if (!obs_initialized() || !modules_loaded)
    {
        return 0;
    }

    return get_loaded_module_count();
}

uint8_t castor_engine_is_module_loaded(const char* module_name)
{
    if (module_name == nullptr || module_name[0] == '\0')
    {
        return 0U;
    }

    std::scoped_lock lock(lifecycle_mutex);

    if (!obs_initialized() || !modules_loaded)
    {
        return 0U;
    }

    return obs_get_module(module_name) != nullptr ? 1U : 0U;
}

uint32_t castor_engine_get_display_count(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    if (refresh_display_snapshot() != CASTOR_ENGINE_OK)
    {
        return 0;
    }

    return static_cast<uint32_t>(display_snapshot.size());
}

uint8_t castor_engine_get_display_at(uint32_t index, castor_engine_display_info_t* out_info)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    if (out_info == nullptr)
    {
        set_last_error("The output display information pointer must not be null.");
        return 0U;
    }

    if (out_info->struct_size < sizeof(castor_engine_display_info_t))
    {
        set_last_error("The output display information structure is too small. Expected at least " +
                       std::to_string(sizeof(castor_engine_display_info_t)) + " bytes, received " +
                       std::to_string(out_info->struct_size) + ".");
        return 0U;
    }

    if (!display_snapshot_valid && refresh_display_snapshot() != CASTOR_ENGINE_OK)
    {
        return 0U;
    }

    if (index >= display_snapshot.size())
    {
        set_last_error("No display exists at index " + std::to_string(index) + ".");
        return 0U;
    }

    const castor::engine::detail::display_descriptor& display = display_snapshot[index];
    out_info->struct_size = sizeof(castor_engine_display_info_t);
    copy_to_fixed_buffer(display.id, out_info->id, sizeof(out_info->id));
    copy_to_fixed_buffer(display.name, out_info->name, sizeof(out_info->name));
    out_info->is_primary = display.is_primary ? 1U : 0U;
    return 1U;
}

castor_engine_result_t castor_engine_validate_display_capture_config(
    const castor_engine_display_capture_config_t* config)
{
    last_error.clear();
    castor::engine::detail::display_capture_configuration_result result =
        castor::engine::detail::validate_display_capture_config(config);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

castor_engine_result_t castor_engine_configure_display_capture(const castor_engine_display_capture_config_t* config)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    castor::engine::detail::display_capture_configuration_result validation =
        castor::engine::detail::validate_display_capture_config(config);

    if (validation.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(validation.message));
        return validation.code;
    }

    if (!obs_initialized() || !modules_loaded)
    {
        set_last_error("The engine and its OBS modules must be initialized before display capture can be configured.");
        return CASTOR_ENGINE_NOT_INITIALIZED;
    }

    if (!video.is_configured())
    {
        set_last_error("The video subsystem must be configured before display capture can be configured.");
        return CASTOR_ENGINE_VIDEO_NOT_CONFIGURED;
    }

    if (!scene_registry.scene_exists(config->scene_name))
    {
        set_last_error("No scene named '" + std::string(config->scene_name) + "' exists.");
        return CASTOR_ENGINE_SCENE_NOT_FOUND;
    }

    castor::engine::detail::display_enumeration_result enumeration = castor::engine::detail::enumerate_obs_displays();

    if (enumeration.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(enumeration.message));
        return enumeration.code;
    }

    const auto selected = std::find_if(enumeration.displays.begin(), enumeration.displays.end(),
                                       [config](const auto& display) { return display.id == config->display_id; });

    if (selected == enumeration.displays.end())
    {
        if (enumeration.displays.empty())
        {
            set_last_error("No interactive display is currently available for capture.");
        }
        else
        {
            set_last_error("The selected display '" + std::string(config->display_id) +
                           "' is not currently available for capture.");
        }

        return CASTOR_ENGINE_DISPLAY_NOT_FOUND;
    }

    castor::engine::detail::scene_registry_result result = scene_registry.configure_display_capture(
        config->scene_name, config->display_id, selected->uses_string_selector, selected->obs_monitor_id.c_str(),
        selected->obs_monitor_index, config->capture_cursor != 0, recording.is_active(), streaming.is_active());

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

uint8_t castor_engine_is_display_capture_active(const char* scene_name)
{
    std::scoped_lock lock(lifecycle_mutex);

    if (!obs_initialized() || !modules_loaded)
    {
        return 0U;
    }

    return scene_registry.is_display_capture_active(scene_name) ? 1U : 0U;
}

castor_engine_result_t castor_engine_configure_video(const castor_engine_video_config_t* config)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    castor::engine::detail::video_configuration_result result =
        video.configure(config, obs_initialized() && modules_loaded);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

uint8_t castor_engine_is_video_configured(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return video.is_configured() ? 1U : 0U;
}

castor_engine_result_t castor_engine_validate_audio_config(const castor_engine_audio_config_t* config)
{
    last_error.clear();

    castor::engine::detail::audio_configuration_result result = castor::engine::detail::validate_audio_config(config);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

castor_engine_result_t castor_engine_configure_audio(const castor_engine_audio_config_t* config)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    castor::engine::detail::audio_lifecycle_result result =
        audio.configure(config, obs_initialized() && modules_loaded);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

uint8_t castor_engine_is_audio_configured(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return audio.is_configured() ? 1U : 0U;
}

uint8_t castor_engine_get_audio_config(castor_engine_audio_config_t* out_config)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    if (out_config == nullptr)
    {
        set_last_error("The output audio configuration pointer must not be null.");
        return 0U;
    }

    if (out_config->struct_size < sizeof(castor_engine_audio_config_t))
    {
        set_last_error("The output audio configuration structure is too small. Expected at least " +
                       std::to_string(sizeof(castor_engine_audio_config_t)) + " bytes, received " +
                       std::to_string(out_config->struct_size) + ".");
        return 0U;
    }

    if (!audio.get_effective_config(out_config))
    {
        set_last_error("The audio subsystem is not configured.");
        return 0U;
    }

    return 1U;
}

uint32_t castor_engine_get_video_encoder_count(void)
{
    std::scoped_lock lock(lifecycle_mutex);

    if (!obs_initialized() || !modules_loaded)
    {
        return 0;
    }

    return castor::engine::detail::get_video_encoder_count();
}

uint8_t castor_engine_get_video_encoder_at(uint32_t index, castor_engine_video_encoder_info_t* out_info)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    if (out_info == nullptr)
    {
        set_last_error("The output video encoder info pointer must not be null.");
        return 0U;
    }

    if (out_info->struct_size < sizeof(castor_engine_video_encoder_info_t))
    {
        set_last_error("The output video encoder info structure is too small. Expected at least " +
                       std::to_string(sizeof(castor_engine_video_encoder_info_t)) + " bytes, received " +
                       std::to_string(out_info->struct_size) + ".");
        return 0U;
    }

    if (!obs_initialized() || !modules_loaded)
    {
        set_last_error("The engine must be initialized before video encoders can be enumerated.");
        return 0U;
    }

    if (!castor::engine::detail::get_video_encoder_at(index, *out_info))
    {
        set_last_error("No video encoder exists at index " + std::to_string(index) + ".");
        return 0U;
    }

    return 1U;
}

castor_engine_result_t castor_engine_validate_video_encoder_config(const castor_engine_video_encoder_config_t* config)
{
    last_error.clear();

    castor::engine::detail::video_encoder_configuration_result result =
        castor::engine::detail::validate_video_encoder_config(config);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

castor_engine_result_t castor_engine_configure_video_encoder(const castor_engine_video_encoder_config_t* config)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    castor::engine::detail::video_encoder_lifecycle_result result =
        video_encoder.configure(config, obs_initialized() && modules_loaded, video.is_configured());

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

uint8_t castor_engine_is_video_encoder_configured(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return video_encoder.is_configured() ? 1U : 0U;
}

uint8_t castor_engine_get_video_encoder_config(castor_engine_video_encoder_config_t* out_config)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    if (out_config == nullptr)
    {
        set_last_error("The output video encoder configuration pointer must not be null.");
        return 0U;
    }

    if (out_config->struct_size < sizeof(castor_engine_video_encoder_config_t))
    {
        set_last_error("The output video encoder configuration structure is too small. Expected at least " +
                       std::to_string(sizeof(castor_engine_video_encoder_config_t)) + " bytes, received " +
                       std::to_string(out_config->struct_size) + ".");
        return 0U;
    }

    if (!video_encoder.get_effective_config(out_config))
    {
        set_last_error("The video encoder is not configured.");
        return 0U;
    }

    return 1U;
}

uint8_t castor_engine_get_selected_video_encoder(castor_engine_video_encoder_info_t* out_info)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    if (out_info == nullptr)
    {
        set_last_error("The output video encoder info pointer must not be null.");
        return 0U;
    }

    if (out_info->struct_size < sizeof(castor_engine_video_encoder_info_t))
    {
        set_last_error("The output video encoder info structure is too small. Expected at least " +
                       std::to_string(sizeof(castor_engine_video_encoder_info_t)) + " bytes, received " +
                       std::to_string(out_info->struct_size) + ".");
        return 0U;
    }

    if (!video_encoder.get_selected_encoder_info(out_info))
    {
        set_last_error("The video encoder is not configured.");
        return 0U;
    }

    return 1U;
}

const char* castor_engine_get_video_encoder_fallback_notice(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return video_encoder.get_fallback_notice();
}

castor_engine_result_t castor_engine_validate_audio_encoder_config(const castor_engine_video_encoder_config_t* config)
{
    last_error.clear();

    castor::engine::detail::audio_encoder_configuration_result result =
        castor::engine::detail::validate_audio_encoder_config(config);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

castor_engine_result_t castor_engine_configure_audio_encoder(const castor_engine_video_encoder_config_t* config)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    castor::engine::detail::audio_encoder_lifecycle_result result =
        audio_encoder.configure(config, obs_initialized() && modules_loaded, audio.is_configured());

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

uint8_t castor_engine_is_audio_encoder_configured(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return audio_encoder.is_configured() ? 1U : 0U;
}

uint8_t castor_engine_get_selected_audio_encoder(castor_engine_video_encoder_info_t* out_info)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    if (out_info == nullptr)
    {
        set_last_error("The output audio encoder info pointer must not be null.");
        return 0U;
    }

    if (out_info->struct_size < sizeof(castor_engine_video_encoder_info_t))
    {
        set_last_error("The output audio encoder info structure is too small. Expected at least " +
                       std::to_string(sizeof(castor_engine_video_encoder_info_t)) + " bytes, received " +
                       std::to_string(out_info->struct_size) + ".");
        return 0U;
    }

    if (!audio_encoder.get_selected_encoder_info(out_info))
    {
        set_last_error("The audio encoder is not configured.");
        return 0U;
    }

    return 1U;
}

void* castor_engine_get_video_encoder_handle(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return video_encoder.get_native_encoder();
}

void* castor_engine_get_audio_encoder_handle(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return audio_encoder.get_native_encoder();
}

castor_engine_result_t castor_engine_validate_recording_config(const castor_engine_recording_config_t* config)
{
    last_error.clear();

    castor::engine::detail::recording_configuration_result result =
        castor::engine::detail::validate_recording_config(config);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

castor_engine_result_t castor_engine_start_recording(const castor_engine_recording_config_t* config)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    castor::engine::detail::recording_configuration_result validation_result =
        castor::engine::detail::validate_recording_config(config);

    if (validation_result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(validation_result.message));
        return validation_result.code;
    }

    if (streaming.is_active())
    {
        set_last_error("Recording cannot start while streaming is active.");
        return CASTOR_ENGINE_STREAMING_CONFLICTING_OUTPUT_ACTIVE;
    }

    const bool runtime_ready = obs_initialized() && modules_loaded;
    const bool video_ready = runtime_ready && video.is_configured();

    if (runtime_ready && video_ready && !video_encoder.is_configured())
    {
        castor_engine_video_encoder_config_t default_encoder_config{};
        default_encoder_config.struct_size = sizeof(default_encoder_config);
        default_encoder_config.selection_mode = CASTOR_ENGINE_VIDEO_ENCODER_SOFTWARE_FORCED;
        default_encoder_config.bitrate = 2500;
        default_encoder_config.rate_control = CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CBR;

        castor::engine::detail::video_encoder_lifecycle_result auto_configure_result =
            video_encoder.configure(&default_encoder_config, runtime_ready, video_ready);

        if (auto_configure_result.code != CASTOR_ENGINE_OK)
        {
            set_last_error(std::move(auto_configure_result.message));
            return auto_configure_result.code;
        }
    }

    if (video_encoder.is_configured())
    {
        castor_engine_video_encoder_info_t selected_info{};
        selected_info.struct_size = sizeof(selected_info);

        if (video_encoder.get_selected_encoder_info(&selected_info) && selected_info.is_hardware != 0)
        {
            set_last_error(
                "Recording requires a software video encoder, but the configured video encoder is a hardware "
                "encoder. Shut down the engine and let recording auto-configure a software encoder, or "
                "configure a software video encoder explicitly before starting a recording.");
            return CASTOR_ENGINE_RECORDING_HARDWARE_ENCODER_NOT_ALLOWED;
        }
    }

    // OBS's ffmpeg_muxer output (the one this feature uses, and the one
    // named explicitly in this issue) refuses to start without an audio
    // encoder bound, even for a video-only recording - obs_output_start
    // fails immediately, with no error text, for an encoded AV-capable
    // output missing its audio encoder. obs_output_set_mixers cannot work
    // around this either ("Tried to use obs_output_set_mixers on an
    // encoded output" is rejected by OBS itself). So recording always
    // auto-configures the audio subsystem and the AAC audio encoder too,
    // the same way it already does for the video encoder, rather than
    // leaving recording permanently broken.
    bool audio_ready = runtime_ready && audio.is_configured();

    if (runtime_ready && !audio_ready)
    {
        castor_engine_audio_config_t default_audio_config{};
        default_audio_config.struct_size = sizeof(default_audio_config);

        castor::engine::detail::audio_lifecycle_result audio_result =
            audio.configure(&default_audio_config, runtime_ready);

        if (audio_result.code != CASTOR_ENGINE_OK)
        {
            set_last_error(std::move(audio_result.message));
            return audio_result.code;
        }

        audio_ready = true;
    }

    if (runtime_ready && audio_ready && !audio_encoder.is_configured())
    {
        castor_engine_video_encoder_config_t default_audio_encoder_config{};
        default_audio_encoder_config.struct_size = sizeof(default_audio_encoder_config);
        default_audio_encoder_config.audio_bitrate = 128;
        default_audio_encoder_config.audio_track_index = 0;

        castor::engine::detail::audio_encoder_lifecycle_result audio_encoder_result =
            audio_encoder.configure(&default_audio_encoder_config, runtime_ready, audio_ready);

        if (audio_encoder_result.code != CASTOR_ENGINE_OK)
        {
            set_last_error(std::move(audio_encoder_result.message));
            return audio_encoder_result.code;
        }
    }

    const bool scene_active = runtime_ready && scene_registry.has_active_scene();
    void* video_encoder_handle = video_encoder.get_native_encoder();
    void* audio_encoder_handle = audio_encoder.get_native_encoder();

    castor::engine::detail::recording_lifecycle_result result =
        recording.start(config, runtime_ready, video_ready, scene_active, video_encoder_handle, audio_encoder_handle);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

castor_engine_result_t castor_engine_stop_recording(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    castor::engine::detail::recording_lifecycle_result result = recording.stop();

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

uint8_t castor_engine_is_recording_active(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return recording.is_active() ? 1U : 0U;
}

castor_engine_result_t castor_engine_validate_streaming_config(const castor_engine_streaming_config_t* config)
{
    last_error.clear();
    castor::engine::detail::streaming_configuration_result result =
        castor::engine::detail::validate_streaming_config(config);
    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }
    return result.code;
}

castor_engine_result_t castor_engine_configure_streaming(const castor_engine_streaming_config_t* config)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();
    castor::engine::detail::streaming_lifecycle_result result =
        streaming.configure(config, obs_initialized() && modules_loaded);
    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }
    return result.code;
}

castor_engine_result_t castor_engine_start_streaming(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();
    const bool runtime_ready = obs_initialized() && modules_loaded;
    castor::engine::detail::streaming_lifecycle_result result =
        streaming.start(runtime_ready, runtime_ready && video.is_configured(), runtime_ready && audio.is_configured(),
                        runtime_ready && scene_registry.has_active_scene(), recording.is_active(),
                        video_encoder.get_native_encoder(), audio_encoder.get_native_encoder());
    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }
    return result.code;
}

castor_engine_result_t castor_engine_stop_streaming(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();
    castor::engine::detail::streaming_lifecycle_result result = streaming.stop();
    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }
    return result.code;
}

castor_engine_result_t castor_engine_get_streaming_status(castor_engine_streaming_status_t* out_status)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();
    castor::engine::detail::streaming_lifecycle_result result = streaming.get_status(out_status);
    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }
    return result.code;
}

castor_engine_result_t castor_engine_get_streaming_health(castor_engine_streaming_health_t* out_health)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();
    castor::engine::detail::streaming_lifecycle_result result = streaming.get_health(out_health);
    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }
    return result.code;
}

castor_engine_result_t castor_engine_create_scene(const char* scene_name)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    const bool runtime_ready = obs_initialized() && modules_loaded;
    castor::engine::detail::scene_registry_result result = scene_registry.create_scene(scene_name, runtime_ready);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

castor_engine_result_t castor_engine_delete_scene(const char* scene_name)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    castor::engine::detail::scene_registry_result result = scene_registry.delete_scene(scene_name);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

castor_engine_result_t castor_engine_rename_scene(const char* old_name, const char* new_name)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    castor::engine::detail::scene_registry_result result = scene_registry.rename_scene(old_name, new_name);

    if (result.code != CASTOR_ENGINE_OK)
    {
        set_last_error(std::move(result.message));
    }

    return result.code;
}

uint32_t castor_engine_get_scene_count(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return scene_registry.scene_count();
}

uint8_t castor_engine_get_scene_name_at(uint32_t index, char* out_name, uint32_t out_name_size)
{
    std::scoped_lock lock(lifecycle_mutex);

    if (out_name == nullptr)
    {
        return 0U;
    }

    std::string name;

    if (!scene_registry.scene_name_at(index, name) || out_name_size < name.size() + 1)
    {
        return 0U;
    }

    copy_to_fixed_buffer(name, out_name, out_name_size);
    return 1U;
}

uint8_t castor_engine_get_active_scene_name(char* out_name, uint32_t out_name_size)
{
    std::scoped_lock lock(lifecycle_mutex);

    if (out_name == nullptr)
    {
        return 0U;
    }

    std::string name;

    if (!scene_registry.active_scene_name(name) || out_name_size < name.size() + 1)
    {
        return 0U;
    }

    copy_to_fixed_buffer(name, out_name, out_name_size);
    return 1U;
}

castor_engine_result_t castor_engine_switch_scene(const char* scene_name,
                                                  const castor_engine_scene_transition_config_t* transition)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    if (transition == nullptr)
    {
        set_last_error("The scene transition configuration must not be null.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    if (transition->struct_size < sizeof(castor_engine_scene_transition_config_t))
    {
        set_last_error("The scene transition configuration structure is too small.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    if (transition->type > CASTOR_ENGINE_SCENE_TRANSITION_SWIPE)
    {
        set_last_error("The scene transition type is not recognized.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    const bool runtime_ready = obs_initialized() && modules_loaded;
    uint32_t base_width = 0;
    uint32_t base_height = 0;
    const bool video_ready = runtime_ready && video.try_get_base_size(base_width, base_height);

    if (!runtime_ready)
    {
        set_last_error("The engine must be initialized before scenes can be switched.");
        return CASTOR_ENGINE_NOT_INITIALIZED;
    }

    try
    {
        castor::engine::detail::scene_registry_result result =
            scene_registry.switch_scene(scene_name, *transition, video_ready, base_width, base_height);

        if (result.code != CASTOR_ENGINE_OK)
        {
            set_last_error(std::move(result.message));
        }

        return result.code;
    }
    catch (const std::exception& exception)
    {
        set_last_error(std::string("Unexpected native scene-switch failure: ") + exception.what());
        return CASTOR_ENGINE_SCENE_TRANSITION_START_FAILED;
    }
    catch (...)
    {
        set_last_error("Unexpected native scene-switch failure.");
        return CASTOR_ENGINE_SCENE_TRANSITION_START_FAILED;
    }
}

uint8_t castor_engine_has_active_scene(void)
{
    std::scoped_lock lock(lifecycle_mutex);

    if (!obs_initialized() || !modules_loaded)
    {
        return 0U;
    }

    return scene_registry.has_active_scene() ? 1U : 0U;
}

void castor_engine_shutdown(void)
{
    std::scoped_lock lock(lifecycle_mutex);

    if (!streaming.reset())
    {
        set_last_error(
            "The active streaming output did not terminate; shutdown was deferred to preserve encoder ownership.");
        return;
    }
    recording.reset();
    scene_registry.reset();
    video_encoder.reset();
    audio_encoder.reset();
    video.reset();

    if (obs_initialized())
    {
        obs_shutdown();
    }

    modules_loaded = false;
    display_snapshot.clear();
    display_snapshot_valid = false;
    audio.reset();

    unregister_libobs_data_path();
    last_error.clear();
}

uint8_t castor_engine_is_initialized(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return obs_initialized() && modules_loaded ? 1U : 0U;
}
