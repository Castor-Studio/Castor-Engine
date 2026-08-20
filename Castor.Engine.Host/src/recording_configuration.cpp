#include "recording_configuration.h"

#include <utility>

namespace castor::engine::detail
{
namespace
{
recording_configuration_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}
} // namespace

recording_configuration_result validate_recording_config(const castor_engine_recording_config_t* config)
{
    if (config == nullptr)
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The recording configuration must not be null.");
    }

    if (config->struct_size < sizeof(castor_engine_recording_config_t))
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT,
                       "The recording configuration structure is too small. Expected at least " +
                           std::to_string(sizeof(castor_engine_recording_config_t)) + " bytes, received " +
                           std::to_string(config->struct_size) + ".");
    }

    if (config->destination_path == nullptr || config->destination_path[0] == '\0')
    {
        return failure(CASTOR_ENGINE_INVALID_ARGUMENT, "The recording destination path must not be null or empty.");
    }

    return {CASTOR_ENGINE_OK, {}};
}
} // namespace castor::engine::detail
