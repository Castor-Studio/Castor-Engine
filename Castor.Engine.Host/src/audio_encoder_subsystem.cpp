#include "audio_encoder_subsystem.h"

#include "audio_encoder_configuration.h"

#include <cstring>
#include <obs.h>
#include <optional>
#include <utility>

namespace castor::engine::detail
{
namespace
{
audio_encoder_lifecycle_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}

// Same principle as the video encoder's H.264 codec check: verify through
// OBS's own obs_get_encoder_codec rather than assuming a hardcoded id like
// "ffmpeg_aac", which module load order or an SDK update could change.
// Comparison is case-insensitive since the exact casing OBS uses for audio
// codec names wasn't confirmed against the vendored headers the way
// "h264" was for video.
std::optional<std::string> find_aac_encoder_id()
{
    const char* id = nullptr;

    for (size_t index = 0; obs_enum_encoder_types(index, &id); ++index)
    {
        if (id == nullptr || obs_get_encoder_type(id) != OBS_ENCODER_AUDIO)
        {
            continue;
        }

        const char* codec = obs_get_encoder_codec(id);

        if (codec != nullptr && _stricmp(codec, "aac") == 0)
        {
            return std::string(id);
        }
    }

    return std::nullopt;
}

void copy_to_fixed_buffer(const char* source, char* destination, size_t destination_size)
{
    std::memset(destination, 0, destination_size);

    if (source != nullptr)
    {
        std::strncpy(destination, source, destination_size - 1);
    }
}
} // namespace

audio_encoder_lifecycle_result audio_encoder_subsystem::configure(const castor_engine_video_encoder_config_t* config,
                                                                    bool runtime_ready, bool audio_ready)
{
    const audio_encoder_configuration_result validation_result = validate_audio_encoder_config(config);

    if (validation_result.code != CASTOR_ENGINE_OK)
    {
        return {validation_result.code, validation_result.message};
    }

    if (!runtime_ready)
    {
        return failure(CASTOR_ENGINE_NOT_INITIALIZED,
                       "The engine must be initialized before the audio encoder can be configured.");
    }

    if (!audio_ready)
    {
        return failure(CASTOR_ENGINE_AUDIO_NOT_CONFIGURED,
                       "The audio subsystem must be configured before the audio encoder can be configured.");
    }

    if (configured_)
    {
        if (current_audio_bitrate_ == config->audio_bitrate && current_audio_track_index_ == config->audio_track_index)
        {
            return {CASTOR_ENGINE_OK, {}};
        }

        return failure(CASTOR_ENGINE_AUDIO_ENCODER_ALREADY_CONFIGURED,
                       "The audio encoder is already configured. Shut down the engine before applying a "
                       "different audio encoder configuration.");
    }

    const std::optional<std::string> aac_id = find_aac_encoder_id();

    if (!aac_id.has_value())
    {
        return failure(CASTOR_ENGINE_AUDIO_ENCODER_UNAVAILABLE,
                       "No AAC audio encoder is available in the current OBS runtime.");
    }

    obs_data_t* settings = obs_data_create();
    obs_data_set_int(settings, "bitrate", config->audio_bitrate);

    obs_encoder_t* encoder =
        obs_audio_encoder_create(aac_id->c_str(), "castor-audio-encoder", settings, config->audio_track_index, nullptr);
    obs_data_release(settings);

    if (encoder == nullptr)
    {
        return failure(CASTOR_ENGINE_AUDIO_ENCODER_CREATION_FAILED,
                       std::string("OBS failed to create the audio encoder '") + *aac_id + "'.");
    }

    obs_encoder_set_audio(encoder, obs_get_audio());

    encoder_ = encoder;
    configured_ = true;
    current_audio_bitrate_ = config->audio_bitrate;
    current_audio_track_index_ = config->audio_track_index;

    selected_info_.struct_size = sizeof(castor_engine_video_encoder_info_t);
    copy_to_fixed_buffer(aac_id->c_str(), selected_info_.id, sizeof(selected_info_.id));
    copy_to_fixed_buffer(obs_encoder_get_display_name(aac_id->c_str()), selected_info_.name,
                          sizeof(selected_info_.name));
    selected_info_.is_hardware = 0U;
    selected_info_.is_available = 1U;

    return {CASTOR_ENGINE_OK, {}};
}

bool audio_encoder_subsystem::is_configured() noexcept
{
    return configured_;
}

bool audio_encoder_subsystem::get_selected_encoder_info(castor_engine_video_encoder_info_t* out_info)
{
    if (out_info == nullptr || !configured_)
    {
        return false;
    }

    *out_info = selected_info_;
    return true;
}

void* audio_encoder_subsystem::get_native_encoder() const noexcept
{
    return configured_ ? encoder_ : nullptr;
}

void audio_encoder_subsystem::reset() noexcept
{
    if (encoder_ != nullptr)
    {
        obs_encoder_release(static_cast<obs_encoder_t*>(encoder_));
        encoder_ = nullptr;
    }

    configured_ = false;
    current_audio_bitrate_ = 0;
    current_audio_track_index_ = 0;
    selected_info_ = {};
}
} // namespace castor::engine::detail
