#pragma once

#include "castor_engine.h"

#include <string>

namespace castor::engine::detail
{
struct recording_configuration_result
{
    castor_engine_result_t code;
    std::string message;
};

recording_configuration_result validate_recording_config(const castor_engine_recording_config_t* config);
} // namespace castor::engine::detail
