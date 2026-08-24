#pragma once

#include "castor_engine.h"

#include <string>

namespace castor::engine::detail
{
struct display_capture_configuration_result
{
    castor_engine_result_t code;
    std::string message;
};

display_capture_configuration_result validate_display_capture_config(
    const castor_engine_display_capture_config_t* config);
} // namespace castor::engine::detail
