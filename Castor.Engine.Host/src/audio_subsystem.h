#pragma once

#include "castor_engine.h"

#include <string>

namespace castor::engine::detail
{
struct audio_lifecycle_result
{
    castor_engine_result_t code;
    std::string message;
};

class audio_subsystem final
{
  public:
    audio_lifecycle_result configure(const castor_engine_audio_config_t* config, bool runtime_ready);

    bool is_configured();

    bool get_effective_config(castor_engine_audio_config_t* out_config);

    void reset() noexcept;

  private:
    bool configured_ = false;
    castor_engine_audio_config_t current_config_{};
};
} // namespace castor::engine::detail
