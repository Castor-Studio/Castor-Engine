#include "audio_encoder_configuration.h"

#include <obs.h>
#include <utility>

namespace castor::engine::detail
{
namespace
{
audio_encoder_configuration_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}
} // namespace

audio_encoder_configuration_result validate_audio_encoder_config(const castor_engine_video_encoder_config_t* config)
{
    if (config == nullptr)
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The audio encoder configuration must not be null.");
    }

    if (config->struct_size < sizeof(castor_engine_video_encoder_config_t))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT,
                       "The audio encoder configuration structure is too small. Expected at least " +
                           std::to_string(sizeof(castor_engine_video_encoder_config_t)) + " bytes, received " +
                           std::to_string(config->struct_size) + ".");
    }

    if (config->audio_bitrate == 0)
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The audio bitrate must be greater than zero.");
    }

    if (config->audio_track_index >= MAX_AUDIO_MIXES)
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The audio track index must be less than " +
                                                           std::to_string(MAX_AUDIO_MIXES) + ", received " +
                                                           std::to_string(config->audio_track_index) + ".");
    }

    return {CASTOR_ENGINE_OK, {}};
}
} // namespace castor::engine::detail
