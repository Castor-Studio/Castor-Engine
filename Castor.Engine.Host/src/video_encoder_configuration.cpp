#include "video_encoder_configuration.h"

#include <cstring>
#include <utility>

namespace castor::engine::detail
{
namespace
{
video_encoder_configuration_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}

bool is_supported_selection_mode(uint32_t selection_mode)
{
    return selection_mode == CASTOR_ENGINE_VIDEO_ENCODER_AUTOMATIC ||
           selection_mode == CASTOR_ENGINE_VIDEO_ENCODER_HARDWARE_PREFERRED ||
           selection_mode == CASTOR_ENGINE_VIDEO_ENCODER_SOFTWARE_FORCED;
}

bool is_supported_rate_control(uint32_t rate_control)
{
    return rate_control == CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CBR ||
           rate_control == CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_VBR ||
           rate_control == CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CQP ||
           rate_control == CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CRF;
}

bool is_null_terminated(const char* buffer, size_t buffer_size)
{
    return strnlen(buffer, buffer_size) < buffer_size;
}
} // namespace

video_encoder_configuration_result validate_video_encoder_config(const castor_engine_video_encoder_config_t* config)
{
    if (config == nullptr)
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The video encoder configuration must not be null.");
    }

    if (config->struct_size < sizeof(castor_engine_video_encoder_config_t))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT,
                       "The video encoder configuration structure is too small. Expected at least " +
                           std::to_string(sizeof(castor_engine_video_encoder_config_t)) + " bytes, received " +
                           std::to_string(config->struct_size) + ".");
    }

    if (!is_supported_selection_mode(config->selection_mode))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT,
                       "Unsupported video encoder selection mode: " + std::to_string(config->selection_mode) +
                           ". Supported modes are automatic (0), hardware preferred (1), and software forced (2).");
    }

    if (!is_supported_rate_control(config->rate_control))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT,
                       "Unsupported video encoder rate control mode: " + std::to_string(config->rate_control) +
                           ". Supported modes are CBR (0), VBR (1), CQP (2), and CRF (3).");
    }

    if (config->bitrate == 0)
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The video encoder bitrate must be greater than zero.");
    }

    if (!is_null_terminated(config->encoder_id, sizeof(config->encoder_id)))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The video encoder identifier must be null-terminated within " +
                                                           std::to_string(sizeof(config->encoder_id)) + " bytes.");
    }

    if (!is_null_terminated(config->preset, sizeof(config->preset)))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The video encoder preset must be null-terminated within " +
                                                           std::to_string(sizeof(config->preset)) + " bytes.");
    }

    if (!is_null_terminated(config->profile, sizeof(config->profile)))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The video encoder profile must be null-terminated within " +
                                                           std::to_string(sizeof(config->profile)) + " bytes.");
    }

    return {CASTOR_ENGINE_OK, {}};
}
} // namespace castor::engine::detail
