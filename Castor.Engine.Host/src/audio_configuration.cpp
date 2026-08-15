#include "audio_configuration.h"

#include <utility>

namespace castor::engine::detail
{
namespace
{
constexpr uint32_t default_sample_rate = 48000;
constexpr castor_engine_speaker_layout_t default_speaker_layout = CASTOR_ENGINE_SPEAKERS_STEREO;

audio_configuration_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}

bool is_supported_sample_rate(uint32_t sample_rate)
{
    return sample_rate == 44100 || sample_rate == 48000;
}

bool is_supported_speaker_layout(uint32_t speaker_layout)
{
    return speaker_layout == CASTOR_ENGINE_SPEAKERS_MONO || speaker_layout == CASTOR_ENGINE_SPEAKERS_STEREO;
}
} // namespace

audio_configuration_result validate_audio_config(const castor_engine_audio_config_t* config)
{
    if (config == nullptr)
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The audio configuration must not be null.");
    }

    if (config->struct_size < sizeof(castor_engine_audio_config_t))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT,
                       "The audio configuration structure is too small. Expected at least " +
                           std::to_string(sizeof(castor_engine_audio_config_t)) + " bytes, received " +
                           std::to_string(config->struct_size) + ".");
    }

    const uint32_t sample_rate = config->sample_rate == 0 ? default_sample_rate : config->sample_rate;

    if (!is_supported_sample_rate(sample_rate))
    {
        return failure(CASTOR_ENGINE_AUDIO_UNSUPPORTED_SAMPLE_RATE,
                       "Unsupported audio sample rate: " + std::to_string(sample_rate) +
                           " Hz. Supported sample rates are 44100 Hz and 48000 Hz.");
    }

    const uint32_t speaker_layout = config->speaker_layout == CASTOR_ENGINE_SPEAKERS_DEFAULT
                                        ? static_cast<uint32_t>(default_speaker_layout)
                                        : config->speaker_layout;

    if (!is_supported_speaker_layout(speaker_layout))
    {
        return failure(CASTOR_ENGINE_AUDIO_UNSUPPORTED_SPEAKER_LAYOUT,
                       "Unsupported audio speaker layout: " + std::to_string(speaker_layout) +
                           ". Supported speaker layouts are mono (" +
                           std::to_string(static_cast<uint32_t>(CASTOR_ENGINE_SPEAKERS_MONO)) + ") and stereo (" +
                           std::to_string(static_cast<uint32_t>(CASTOR_ENGINE_SPEAKERS_STEREO)) + ").");
    }

    return {CASTOR_ENGINE_OK, {}};
}
} // namespace castor::engine::detail
