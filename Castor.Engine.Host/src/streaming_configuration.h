#pragma once

#include "castor_engine.h"

#include <string>

namespace castor::engine::detail
{
struct streaming_configuration_result
{
    castor_engine_result_t code;
    std::string message;
};

struct streaming_configuration
{
    std::string server_url;
    std::string stream_key;
    bool use_authentication = false;
    std::string username;
    std::string password;
    uint32_t reconnect_retry_count = 0;
    uint32_t reconnect_delay_seconds = 0;
};

streaming_configuration_result validate_streaming_config(const castor_engine_streaming_config_t* config);
streaming_configuration copy_streaming_config(const castor_engine_streaming_config_t& config);
bool streaming_configs_match(const streaming_configuration& left, const streaming_configuration& right) noexcept;
} // namespace castor::engine::detail
