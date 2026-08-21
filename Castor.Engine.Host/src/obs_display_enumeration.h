#pragma once

#include "castor_engine.h"

#include <string>
#include <vector>

namespace castor::engine::detail
{
struct display_descriptor
{
    std::string id;
    std::string name;
    bool is_primary = false;
    bool uses_string_selector = false;
    std::string obs_monitor_id;
    long long obs_monitor_index = 0;
};

struct display_enumeration_result
{
    castor_engine_result_t code;
    std::string message;
    std::vector<display_descriptor> displays;
};

display_enumeration_result enumerate_obs_displays();
} // namespace castor::engine::detail
