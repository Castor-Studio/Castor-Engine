#pragma once

#include "castor_engine.h"

#include <string>

namespace castor::engine::detail
{
struct audio_configuration_result
{
    castor_engine_result_t code;
    std::string message;
};

audio_configuration_result validate_audio_config(const castor_engine_audio_config_t* config);
} // namespace castor::engine::detail
