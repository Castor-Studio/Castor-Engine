#include "castor_engine.h"

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
bool video_configured = false;
castor_engine_video_config_t current_video_config{};
std::string registered_libobs_data_path;
thread_local std::string last_error;

constexpr uint32_t maximum_video_dimension = 16384;
constexpr const char* windows_graphics_module = "libobs-d3d11";

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
    video_configured = false;
    current_video_config = {};
    unregister_libobs_data_path();

    if (started_here && obs_initialized())
    {
        obs_shutdown();
    }
}

bool video_configs_match(const castor_engine_video_config_t& left, const castor_engine_video_config_t& right)
{
    return left.base_width == right.base_width && left.base_height == right.base_height &&
           left.output_width == right.output_width && left.output_height == right.output_height &&
           left.fps_numerator == right.fps_numerator && left.fps_denominator == right.fps_denominator;
}

bool is_supported_video_dimension(uint32_t dimension)
{
    return dimension != 0 && dimension <= maximum_video_dimension && dimension % 2 == 0;
}

castor_engine_result_t validate_video_config(const castor_engine_video_config_t* config)
{
    if (config == nullptr)
    {
        set_last_error("The video configuration must not be null.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    if (config->struct_size < sizeof(castor_engine_video_config_t))
    {
        set_last_error("The video configuration structure is too small. Expected at least " +
                       std::to_string(sizeof(castor_engine_video_config_t)) + " bytes, received " +
                       std::to_string(config->struct_size) + ".");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    if (!obs_initialized() || !modules_loaded)
    {
        set_last_error("The engine must be initialized before video can be configured.");
        return CASTOR_ENGINE_NOT_INITIALIZED;
    }

    if (config->fps_numerator == 0 || config->fps_denominator == 0)
    {
        set_last_error("The video FPS numerator and denominator must both be non-zero.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    if (!is_supported_video_dimension(config->base_width))
    {
        set_last_error("The base video width must be even and between 2 and " +
                       std::to_string(maximum_video_dimension) + " pixels.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    if (!is_supported_video_dimension(config->base_height))
    {
        set_last_error("The base video height must be even and between 2 and " +
                       std::to_string(maximum_video_dimension) + " pixels.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    if (!is_supported_video_dimension(config->output_width))
    {
        set_last_error("The output video width must be even and between 2 and " +
                       std::to_string(maximum_video_dimension) + " pixels.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    if (!is_supported_video_dimension(config->output_height))
    {
        set_last_error("The output video height must be even and between 2 and " +
                       std::to_string(maximum_video_dimension) + " pixels.");
        return CASTOR_ENGINE_INVALID_ARGUMENT;
    }

    return CASTOR_ENGINE_OK;
}

castor_engine_result_t translate_video_result(int result)
{
    switch (result)
    {
    case OBS_VIDEO_SUCCESS:
        return CASTOR_ENGINE_OK;
    case OBS_VIDEO_NOT_SUPPORTED:
        set_last_error("OBS does not support the requested video configuration on the selected graphics adapter.");
        return CASTOR_ENGINE_VIDEO_NOT_SUPPORTED;
    case OBS_VIDEO_INVALID_PARAM:
        set_last_error("OBS rejected one or more parameters in the requested video configuration.");
        return CASTOR_ENGINE_VIDEO_INVALID_CONFIGURATION;
    case OBS_VIDEO_CURRENTLY_ACTIVE:
        set_last_error("OBS video cannot be reconfigured while a video output is active.");
        return CASTOR_ENGINE_VIDEO_CURRENTLY_ACTIVE;
    case OBS_VIDEO_MODULE_NOT_FOUND:
        set_last_error("OBS could not load the packaged Windows graphics module 'libobs-d3d11'.");
        return CASTOR_ENGINE_VIDEO_MODULE_NOT_FOUND;
    case OBS_VIDEO_FAIL:
        set_last_error("OBS failed to configure the video subsystem.");
        return CASTOR_ENGINE_VIDEO_CONFIGURATION_FAILED;
    default:
        set_last_error("OBS returned an unknown video configuration result: " + std::to_string(result) + ".");
        return CASTOR_ENGINE_VIDEO_CONFIGURATION_FAILED;
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

    const castor_engine_result_t validation_result = validate_video_config(config);

    if (validation_result != CASTOR_ENGINE_OK)
    {
        return validation_result;
    }

    obs_video_info active_video_info{};

    if (video_configured && video_configs_match(current_video_config, *config) &&
        obs_get_video_info(&active_video_info))
    {
        return CASTOR_ENGINE_OK;
    }

    obs_video_info video_info{};
    video_info.graphics_module = windows_graphics_module;
    video_info.fps_num = config->fps_numerator;
    video_info.fps_den = config->fps_denominator;
    video_info.base_width = config->base_width;
    video_info.base_height = config->base_height;
    video_info.output_width = config->output_width;
    video_info.output_height = config->output_height;
    video_info.output_format = VIDEO_FORMAT_NV12;
    video_info.adapter = 0;
    video_info.gpu_conversion = true;
    video_info.colorspace = VIDEO_CS_709;
    video_info.range = VIDEO_RANGE_PARTIAL;
    video_info.scale_type = OBS_SCALE_BICUBIC;

    const castor_engine_result_t result = translate_video_result(obs_reset_video(&video_info));

    if (result == CASTOR_ENGINE_OK)
    {
        current_video_config = *config;
        video_configured = true;
    }
    else if (!obs_get_video_info(&active_video_info))
    {
        current_video_config = {};
        video_configured = false;
    }

    return result;
}

uint8_t castor_engine_is_video_configured(void)
{
    std::scoped_lock lock(lifecycle_mutex);

    if (!obs_initialized() || !video_configured)
    {
        return 0U;
    }

    obs_video_info video_info{};
    video_configured = obs_get_video_info(&video_info);
    return video_configured ? 1U : 0U;
}

void castor_engine_shutdown(void)
{
    std::scoped_lock lock(lifecycle_mutex);

    if (obs_initialized())
    {
        obs_shutdown();
    }

    modules_loaded = false;
    video_configured = false;
    current_video_config = {};
    unregister_libobs_data_path();
    last_error.clear();
}

uint8_t castor_engine_is_initialized(void)
{
    std::scoped_lock lock(lifecycle_mutex);
    return obs_initialized() && modules_loaded ? 1U : 0U;
}
