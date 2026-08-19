#pragma once

#include "castor_engine.h"

#include <string>

namespace castor::engine::detail
{
struct video_encoder_lifecycle_result
{
    castor_engine_result_t code;
    std::string message;
};

class video_encoder_subsystem final
{
  public:
    video_encoder_lifecycle_result configure(const castor_engine_video_encoder_config_t* config, bool runtime_ready,
                                              bool video_ready);

    bool is_configured() noexcept;

    bool get_effective_config(castor_engine_video_encoder_config_t* out_config);

    bool get_selected_encoder_info(castor_engine_video_encoder_info_t* out_info);

    const char* get_fallback_notice() const noexcept;

    void reset() noexcept;

  private:
    // Opaque obs_encoder_t*, kept as void* so this header never needs to
    // include obs.h, matching how main_scene_subsystem hides its OBS handles.
    void* encoder_ = nullptr;
    bool configured_ = false;
    castor_engine_video_encoder_config_t current_config_{};
    castor_engine_video_encoder_info_t selected_info_{};
    std::string fallback_notice_;
};
} // namespace castor::engine::detail
