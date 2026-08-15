#include "audio_subsystem.h"

#include "audio_configuration.h"

#include <obs.h>
#include <string>
#include <utility>

namespace castor::engine::detail
{
namespace
{
audio_lifecycle_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}

bool audio_configs_match(const castor_engine_audio_config_t& left, const castor_engine_audio_config_t& right)
{
    return left.sample_rate == right.sample_rate && left.speaker_layout == right.speaker_layout;
}

castor_engine_audio_config_t resolve_effective_config(const castor_engine_audio_config_t& config)
{
    castor_engine_audio_config_t effective = config;
    effective.struct_size = sizeof(castor_engine_audio_config_t);

    if (effective.sample_rate == 0)
    {
        effective.sample_rate = 48000;
    }

    if (effective.speaker_layout == CASTOR_ENGINE_SPEAKERS_DEFAULT)
    {
        effective.speaker_layout = CASTOR_ENGINE_SPEAKERS_STEREO;
    }

    return effective;
}
} // namespace

// OBS does not support runtime audio reconfiguration: obs_reset_audio()
// silently no-ops (still returns true) when the audio subsystem is already
// active, regardless of the parameters passed in. Forwarding a differing
// reconfiguration request to OBS would therefore make the engine believe it
// applied new settings while OBS silently kept the old ones running under an
// active recording. To keep the engine-owned state truthful, a differing
// reconfiguration request is rejected here before ever reaching OBS.
audio_lifecycle_result audio_subsystem::configure(const castor_engine_audio_config_t* config, bool runtime_ready)
{
    const audio_configuration_result validation_result = validate_audio_config(config);

    if (validation_result.code != CASTOR_ENGINE_OK)
    {
        return {validation_result.code, validation_result.message};
    }

    if (!runtime_ready)
    {
        return failure(CASTOR_ENGINE_NOT_INITIALIZED, "The engine must be initialized before audio can be configured.");
    }

    const castor_engine_audio_config_t effective_config = resolve_effective_config(*config);

    if (configured_)
    {
        if (audio_configs_match(current_config_, effective_config))
        {
            return {CASTOR_ENGINE_OK, {}};
        }

        return failure(CASTOR_ENGINE_AUDIO_ALREADY_CONFIGURED,
                       "The audio subsystem is already configured at " + std::to_string(current_config_.sample_rate) +
                           " Hz with speaker layout " + std::to_string(current_config_.speaker_layout) +
                           ". OBS does not support runtime audio reconfiguration; shut down the engine before "
                           "applying a different audio configuration.");
    }

    obs_audio_info audio_info{};
    audio_info.samples_per_sec = effective_config.sample_rate;
    audio_info.speakers = static_cast<speaker_layout>(effective_config.speaker_layout);

    if (!obs_reset_audio(&audio_info))
    {
        return failure(CASTOR_ENGINE_AUDIO_CONFIGURATION_FAILED, "OBS failed to initialize the audio subsystem.");
    }

    current_config_ = effective_config;
    configured_ = true;
    return {CASTOR_ENGINE_OK, {}};
}

// Unlike the video subsystem, audio does not re-verify its state against
// OBS on every query. Castor is the sole owner of the OBS audio lifecycle
// (obs_reset_audio/obs_shutdown are only ever called from this file and
// castor_engine_shutdown), so configured_ cannot drift from OBS's actual
// state without our own knowledge. Querying OBS here was also observed to
// race with obs_reset_audio's internal audio thread startup when called in
// quick succession, causing an access violation inside obs.dll.
bool audio_subsystem::is_configured()
{
    return obs_initialized() && configured_;
}

bool audio_subsystem::get_effective_config(castor_engine_audio_config_t* out_config)
{
    if (out_config == nullptr || !is_configured())
    {
        return false;
    }

    *out_config = current_config_;
    return true;
}

void audio_subsystem::reset() noexcept
{
    configured_ = false;
    current_config_ = {};
}
} // namespace castor::engine::detail
