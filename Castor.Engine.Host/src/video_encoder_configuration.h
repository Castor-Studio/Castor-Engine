#pragma once

#include "castor_engine.h"

#include <string>

namespace castor::engine::detail
{
struct video_encoder_configuration_result
{
    castor_engine_result_t code;
    std::string message;
};

video_encoder_configuration_result validate_video_encoder_config(const castor_engine_video_encoder_config_t* config);
} // namespace castor::engine::detail
