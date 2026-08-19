#pragma once

#include "castor_engine.h"

#include <string>

namespace castor::engine::detail
{
struct audio_encoder_lifecycle_result
{
    castor_engine_result_t code;
    std::string message;
};

class audio_encoder_subsystem final
{
  public:
    audio_encoder_lifecycle_result configure(const castor_engine_video_encoder_config_t* config, bool runtime_ready,
                                              bool audio_ready);

    bool is_configured() noexcept;

    bool get_selected_encoder_info(castor_engine_video_encoder_info_t* out_info);

    void reset() noexcept;

  private:
    // Opaque obs_encoder_t*, kept as void* so this header never needs to
    // include obs.h, matching video_encoder_subsystem.
    void* encoder_ = nullptr;
    bool configured_ = false;
    uint32_t current_audio_bitrate_ = 0;
    uint32_t current_audio_track_index_ = 0;
    castor_engine_video_encoder_info_t selected_info_{};
};
} // namespace castor::engine::detail
