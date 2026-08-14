#include "video_configuration.h"

#include <obs.h>
#include <string>
#include <utility>

namespace castor::engine::detail
{
namespace
{
constexpr uint32_t maximum_video_dimension = 16384;
constexpr const char* windows_graphics_module = "libobs-d3d11";

video_configuration_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
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

video_configuration_result validate_video_config(const castor_engine_video_config_t* config, bool runtime_ready)
{
    if (config == nullptr)
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The video configuration must not be null.");
    }

    if (config->struct_size < sizeof(castor_engine_video_config_t))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT,
                       "The video configuration structure is too small. Expected at least " +
                           std::to_string(sizeof(castor_engine_video_config_t)) + " bytes, received " +
                           std::to_string(config->struct_size) + ".");
    }

    if (!runtime_ready)
    {
        return failure(CASTOR_ENGINE_NOT_INITIALIZED, "The engine must be initialized before video can be configured.");
    }

    if (config->fps_numerator == 0 || config->fps_denominator == 0)
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT,
                       "The video FPS numerator and denominator must both be non-zero.");
    }

    if (!is_supported_video_dimension(config->base_width))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The base video width must be even and between 2 and " +
                                                           std::to_string(maximum_video_dimension) + " pixels.");
    }

    if (!is_supported_video_dimension(config->base_height))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The base video height must be even and between 2 and " +
                                                           std::to_string(maximum_video_dimension) + " pixels.");
    }

    if (!is_supported_video_dimension(config->output_width))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The output video width must be even and between 2 and " +
                                                           std::to_string(maximum_video_dimension) + " pixels.");
    }

    if (!is_supported_video_dimension(config->output_height))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The output video height must be even and between 2 and " +
                                                           std::to_string(maximum_video_dimension) + " pixels.");
    }

    return {CASTOR_ENGINE_OK, {}};
}

video_configuration_result translate_video_result(int result)
{
    switch (result)
    {
    case OBS_VIDEO_SUCCESS:
        return {CASTOR_ENGINE_OK, {}};
    case OBS_VIDEO_NOT_SUPPORTED:
        return failure(CASTOR_ENGINE_VIDEO_NOT_SUPPORTED,
                       "OBS does not support the requested video configuration on the selected graphics adapter.");
    case OBS_VIDEO_INVALID_PARAM:
        return failure(CASTOR_ENGINE_VIDEO_INVALID_CONFIGURATION,
                       "OBS rejected one or more parameters in the requested video configuration.");
    case OBS_VIDEO_CURRENTLY_ACTIVE:
        return failure(CASTOR_ENGINE_VIDEO_CURRENTLY_ACTIVE,
                       "OBS video cannot be reconfigured while a video output is active.");
    case OBS_VIDEO_MODULE_NOT_FOUND:
        return failure(CASTOR_ENGINE_VIDEO_MODULE_NOT_FOUND,
                       "OBS could not load the packaged Windows graphics module 'libobs-d3d11'.");
    case OBS_VIDEO_FAIL:
        return failure(CASTOR_ENGINE_VIDEO_CONFIGURATION_FAILED, "OBS failed to configure the video subsystem.");
    default:
        return failure(CASTOR_ENGINE_VIDEO_CONFIGURATION_FAILED,
                       "OBS returned an unknown video configuration result: " + std::to_string(result) + ".");
    }
}
} // namespace

video_configuration_result video_subsystem::configure(const castor_engine_video_config_t* config, bool runtime_ready)
{
    const video_configuration_result validation_result = validate_video_config(config, runtime_ready);

    if (validation_result.code != CASTOR_ENGINE_OK)
    {
        return validation_result;
    }

    obs_video_info active_video_info{};

    if (configured_ && video_configs_match(current_config_, *config) && obs_get_video_info(&active_video_info))
    {
        return {CASTOR_ENGINE_OK, {}};
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

    video_configuration_result result = translate_video_result(obs_reset_video(&video_info));

    if (result.code == CASTOR_ENGINE_OK)
    {
        current_config_ = *config;
        configured_ = true;
    }
    else if (!obs_get_video_info(&active_video_info))
    {
        reset();
    }

    return result;
}

bool video_subsystem::is_configured()
{
    if (!obs_initialized() || !configured_)
    {
        return false;
    }

    obs_video_info video_info{};
    configured_ = obs_get_video_info(&video_info);
    return configured_;
}

void video_subsystem::reset() noexcept
{
    configured_ = false;
    current_config_ = {};
}
} // namespace castor::engine::detail
