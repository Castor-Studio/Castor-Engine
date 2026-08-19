#include "castor_engine.h"

#include "audio_configuration.h"
#include "audio_encoder_configuration.h"
#include "audio_encoder_subsystem.h"
#include "audio_subsystem.h"
#include "main_scene.h"
#include "obs_scene_backend.h"
#include "video_configuration.h"
#include "video_encoder_configuration.h"
#include "video_encoder_enumeration.h"
#include "video_encoder_subsystem.h"

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

castor::engine::detail::video_subsystem video;
castor::engine::detail::audio_subsystem audio;
castor::engine::detail::video_encoder_subsystem video_encoder;
castor::engine::detail::audio_encoder_subsystem audio_encoder;
castor::engine::detail::obs_scene_backend scene_backend;
castor::engine::detail::main_scene_subsystem main_scene(scene_backend);

std::string path_to_utf8(const std::filesystem::path& path)
{
    const auto utf8_path = path.generic_u8string();

    return std::string(reinterpret_cast<const char*>(utf8_path.data()), utf8_path.size());
}

void set_last_error(std::string message)
{
    last_error = std::move(message);
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
    main_scene.reset();
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

castor_engine_result_t castor_engine_create_main_scene(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    last_error.clear();

    const bool runtime_ready = obs_initialized() && modules_loaded;
    uint32_t base_width = 0;
    uint32_t base_height = 0;
    const bool video_ready = runtime_ready && video.try_get_base_size(base_width, base_height);

    try
    {
        castor::engine::detail::main_scene_result result =
            main_scene.create(runtime_ready, video_ready, base_width, base_height);

        if (result.code != CASTOR_ENGINE_OK)
        {
            set_last_error(std::move(result.message));
        }

        return result.code;
    }
    catch (const std::exception& exception)
    {
        main_scene.reset();
        set_last_error(std::string("Unexpected native main-scene failure: ") + exception.what());
        return CASTOR_ENGINE_SCENE_CREATION_FAILED;
    }
    catch (...)
    {
        main_scene.reset();
        set_last_error("Unexpected native main-scene failure.");
        return CASTOR_ENGINE_SCENE_CREATION_FAILED;
    }
}

uint8_t castor_engine_has_active_scene(void)
{
    std::scoped_lock lock(lifecycle_mutex);

    if (!obs_initialized() || !modules_loaded)
    {
        return 0U;
    }

    return main_scene.is_active() ? 1U : 0U;
}

void castor_engine_shutdown(void)
{
    std::scoped_lock lock(lifecycle_mutex);

    main_scene.reset();
    video_encoder.reset();
    audio_encoder.reset();
    video.reset();

    if (obs_initialized())
    {
        obs_shutdown();
    }

    modules_loaded = false;
    audio.reset();

    unregister_libobs_data_path();
    last_error.clear();
}

uint8_t castor_engine_is_initialized(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return obs_initialized() && modules_loaded ? 1U : 0U;
}
