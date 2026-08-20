#pragma once

#include "castor_engine.h"

#include <string>

namespace castor::engine::detail
{
struct recording_lifecycle_result
{
    castor_engine_result_t code;
    std::string message;
};

class recording_subsystem final
{
  public:
    recording_lifecycle_result start(const castor_engine_recording_config_t* config, bool runtime_ready,
                                     bool video_ready, bool scene_active, void* video_encoder_handle,
                                     void* audio_encoder_handle);

    recording_lifecycle_result stop();

    bool is_active() noexcept;

    void reset() noexcept;

  private:
    // Opaque obs_output_t*, kept as void* so this header never needs to
    // include obs.h, matching the encoder subsystems.
    void* output_ = nullptr;
    bool active_ = false;
};
} // namespace castor::engine::detail
