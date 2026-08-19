#include "video_encoder_subsystem.h"

#include "video_encoder_configuration.h"
#include "video_encoder_enumeration.h"

#include <cstring>
#include <obs.h>
#include <optional>
#include <utility>
#include <vector>

namespace castor::engine::detail
{
namespace
{
video_encoder_lifecycle_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}

bool video_encoder_configs_match(const castor_engine_video_encoder_config_t& left,
                                  const castor_engine_video_encoder_config_t& right)
{
    return left.selection_mode == right.selection_mode && std::strcmp(left.encoder_id, right.encoder_id) == 0 &&
           left.bitrate == right.bitrate && left.rate_control == right.rate_control &&
           left.keyframe_interval_seconds == right.keyframe_interval_seconds &&
           std::strcmp(left.preset, right.preset) == 0 && std::strcmp(left.profile, right.profile) == 0 &&
           left.audio_bitrate == right.audio_bitrate && left.audio_track_index == right.audio_track_index;
}

std::vector<castor_engine_video_encoder_info_t> enumerate_all()
{
    std::vector<castor_engine_video_encoder_info_t> infos;
    const uint32_t count = get_video_encoder_count();

    for (uint32_t index = 0; index < count; ++index)
    {
        castor_engine_video_encoder_info_t info{};
        info.struct_size = sizeof(info);

        if (get_video_encoder_at(index, info))
        {
            infos.push_back(info);
        }
    }

    return infos;
}

// Selection only ever targets H.264: the config carries no codec field, and
// the recording path this feeds (issue #14) muxes H.264 video with AAC
// audio, matching what the packaged software encoder ("x264") produces.
// Checking the codec through OBS rather than a hardcoded id keeps this
// correct even if a machine has multiple hardware or software encoders
// registered for other codecs (e.g. AV1).
bool is_h264(const char* id)
{
    const char* codec = obs_get_encoder_codec(id);
    return codec != nullptr && std::strcmp(codec, "h264") == 0;
}

std::optional<castor_engine_video_encoder_info_t> find_by_id(
    const std::vector<castor_engine_video_encoder_info_t>& infos, const char* id)
{
    for (const auto& info : infos)
    {
        if (std::strcmp(id, info.id) == 0)
        {
            return info;
        }
    }

    return std::nullopt;
}

std::optional<castor_engine_video_encoder_info_t> find_first_hardware_h264(
    const std::vector<castor_engine_video_encoder_info_t>& infos)
{
    for (const auto& info : infos)
    {
        if (info.is_hardware != 0 && info.is_available != 0 && is_h264(info.id))
        {
            return info;
        }
    }

    return std::nullopt;
}

// Some hardware encoder plugins (e.g. obs-qsv11) also register their own
// software fallback encoder id, which would otherwise tie with the
// packaged x264 encoder as "a software H.264 encoder" depending on module
// load order. The issue this feature implements names x264 specifically
// as the software encoder, so prefer it by id - still verified against
// the live enumeration rather than assumed - and only fall back to
// whichever other software H.264 encoder is available if x264 is somehow
// not registered.
std::optional<castor_engine_video_encoder_info_t> find_software_h264(
    const std::vector<castor_engine_video_encoder_info_t>& infos)
{
    constexpr const char* preferred_software_encoder_id = "obs_x264";

    if (std::optional<castor_engine_video_encoder_info_t> preferred = find_by_id(infos, preferred_software_encoder_id);
        preferred.has_value() && preferred->is_hardware == 0 && preferred->is_available != 0)
    {
        return preferred;
    }

    for (const auto& info : infos)
    {
        if (info.is_hardware == 0 && is_h264(info.id))
        {
            return info;
        }
    }

    return std::nullopt;
}

const char* rate_control_string(uint32_t rate_control)
{
    switch (rate_control)
    {
    case CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_VBR:
        return "VBR";
    case CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CQP:
        return "CQP";
    case CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CRF:
        return "CRF";
    case CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CBR:
    default:
        return "CBR";
    }
}
} // namespace

video_encoder_lifecycle_result video_encoder_subsystem::configure(const castor_engine_video_encoder_config_t* config,
                                                                    bool runtime_ready, bool video_ready)
{
    const video_encoder_configuration_result validation_result = validate_video_encoder_config(config);

    if (validation_result.code != CASTOR_ENGINE_OK)
    {
        return {validation_result.code, validation_result.message};
    }

    if (!runtime_ready)
    {
        return failure(CASTOR_ENGINE_NOT_INITIALIZED,
                       "The engine must be initialized before the video encoder can be configured.");
    }

    if (!video_ready)
    {
        return failure(CASTOR_ENGINE_VIDEO_NOT_CONFIGURED,
                       "The video subsystem must be configured before the video encoder can be configured.");
    }

    if (configured_)
    {
        if (video_encoder_configs_match(current_config_, *config))
        {
            return {CASTOR_ENGINE_OK, {}};
        }

        return failure(CASTOR_ENGINE_VIDEO_ENCODER_ALREADY_CONFIGURED,
                       "The video encoder is already configured. Shut down the engine before applying a "
                       "different video encoder configuration.");
    }

    const std::vector<castor_engine_video_encoder_info_t> encoders = enumerate_all();
    std::string fallback_notice;
    std::optional<castor_engine_video_encoder_info_t> selected;
    const bool has_explicit_id = config->encoder_id[0] != '\0';

    if (has_explicit_id)
    {
        selected = find_by_id(encoders, config->encoder_id);

        if (!selected.has_value())
        {
            return failure(CASTOR_ENGINE_VIDEO_ENCODER_UNKNOWN_ID,
                           std::string("No video encoder with identifier '") + config->encoder_id +
                               "' is available in the current OBS runtime.");
        }

        if (selected->is_available == 0)
        {
            return failure(CASTOR_ENGINE_VIDEO_ENCODER_UNAVAILABLE,
                           std::string("The requested video encoder '") + config->encoder_id +
                               "' is not currently available.");
        }
    }
    else if (config->selection_mode == CASTOR_ENGINE_VIDEO_ENCODER_SOFTWARE_FORCED)
    {
        selected = find_software_h264(encoders);

        if (!selected.has_value())
        {
            return failure(CASTOR_ENGINE_VIDEO_ENCODER_UNAVAILABLE,
                           "No software H.264 video encoder is available in the current OBS runtime.");
        }
    }
    else
    {
        // AUTOMATIC and HARDWARE_PREFERRED both try hardware first and fall
        // back to software - they only differ in intent, not behavior, per
        // the issue's fallback order.
        selected = find_first_hardware_h264(encoders);

        if (!selected.has_value())
        {
            selected = find_software_h264(encoders);

            if (!selected.has_value())
            {
                return failure(CASTOR_ENGINE_VIDEO_ENCODER_UNAVAILABLE,
                               "No hardware or software H.264 video encoder is available in the current OBS "
                               "runtime.");
            }

            fallback_notice = "Hardware-preferred video encoding was requested, but no hardware video encoder "
                               "is currently available; falling back to the software encoder '" +
                               std::string(selected->id) + "'.";
        }
    }

    obs_data_t* settings = obs_data_create();
    obs_data_set_int(settings, "bitrate", config->bitrate);
    obs_data_set_string(settings, "rate_control", rate_control_string(config->rate_control));

    if (config->keyframe_interval_seconds != 0)
    {
        obs_data_set_int(settings, "keyint_sec", config->keyframe_interval_seconds);
    }

    if (config->preset[0] != '\0')
    {
        obs_data_set_string(settings, "preset", config->preset);
    }

    if (config->profile[0] != '\0')
    {
        obs_data_set_string(settings, "profile", config->profile);
    }

    obs_encoder_t* encoder = obs_video_encoder_create(selected->id, "castor-video-encoder", settings, nullptr);
    obs_data_release(settings);

    if (encoder == nullptr)
    {
        return failure(CASTOR_ENGINE_VIDEO_ENCODER_CREATION_FAILED,
                       std::string("OBS failed to create the video encoder '") + selected->id + "'.");
    }

    obs_encoder_set_video(encoder, obs_get_video());

    encoder_ = encoder;
    configured_ = true;
    current_config_ = *config;
    current_config_.struct_size = sizeof(castor_engine_video_encoder_config_t);
    selected_info_ = *selected;
    fallback_notice_ = std::move(fallback_notice);

    return {CASTOR_ENGINE_OK, {}};
}

bool video_encoder_subsystem::is_configured() noexcept
{
    return configured_;
}

bool video_encoder_subsystem::get_effective_config(castor_engine_video_encoder_config_t* out_config)
{
    if (out_config == nullptr || !configured_)
    {
        return false;
    }

    *out_config = current_config_;
    return true;
}

bool video_encoder_subsystem::get_selected_encoder_info(castor_engine_video_encoder_info_t* out_info)
{
    if (out_info == nullptr || !configured_)
    {
        return false;
    }

    *out_info = selected_info_;
    return true;
}

const char* video_encoder_subsystem::get_fallback_notice() const noexcept
{
    return fallback_notice_.c_str();
}

void* video_encoder_subsystem::get_native_encoder() const noexcept
{
    return configured_ ? encoder_ : nullptr;
}

void video_encoder_subsystem::reset() noexcept
{
    if (encoder_ != nullptr)
    {
        obs_encoder_release(static_cast<obs_encoder_t*>(encoder_));
        encoder_ = nullptr;
    }

    configured_ = false;
    current_config_ = {};
    selected_info_ = {};
    fallback_notice_.clear();
}
} // namespace castor::engine::detail
