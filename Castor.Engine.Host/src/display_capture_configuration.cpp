#include "display_capture_configuration.h"

#include <cstring>
#include <utility>

namespace castor::engine::detail
{
namespace
{
display_capture_configuration_result failure(std::string message)
{
    return {CASTOR_ENGINE_DISPLAY_INVALID_CONFIGURATION, std::move(message)};
}
} // namespace

display_capture_configuration_result validate_display_capture_config(
    const castor_engine_display_capture_config_t* config)
{
    if (config == nullptr)
    {
        return failure("The display capture configuration must not be null.");
    }

    if (config->struct_size < sizeof(castor_engine_display_capture_config_t))
    {
        return failure("The display capture configuration structure is too small. Expected at least " +
                       std::to_string(sizeof(castor_engine_display_capture_config_t)) + " bytes, received " +
                       std::to_string(config->struct_size) + ".");
    }

    const void* terminator = std::memchr(config->display_id, '\0', sizeof(config->display_id));

    if (terminator == nullptr)
    {
        return failure("The display identifier must be null-terminated UTF-8.");
    }

    if (config->display_id[0] == '\0')
    {
        return failure("The display identifier must not be empty.");
    }

    if (config->capture_cursor > 1U)
    {
        return failure("The display cursor setting must be either 0 or 1.");
    }

    return {CASTOR_ENGINE_OK, {}};
}
} // namespace castor::engine::detail
