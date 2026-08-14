#pragma once

#include "castor_engine.h"

#include <string>

namespace castor::engine::detail
{
struct video_configuration_result
{
    castor_engine_result_t code;
    std::string message;
};

class video_subsystem final
{
  public:
    video_configuration_result configure(const castor_engine_video_config_t* config, bool runtime_ready);

    bool is_configured();

    void reset() noexcept;

  private:
    bool configured_ = false;
    castor_engine_video_config_t current_config_{};
};
} // namespace castor::engine::detail
