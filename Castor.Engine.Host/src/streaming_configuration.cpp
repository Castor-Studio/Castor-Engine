#include "streaming_configuration.h"

#include <algorithm>
#include <cctype>
#include <climits>
#include <string_view>
#include <utility>

namespace castor::engine::detail
{
namespace
{
streaming_configuration_result failure(std::string message)
{
    return {CASTOR_ENGINE_STREAMING_INVALID_CONFIGURATION, std::move(message)};
}

bool starts_with_case_insensitive(std::string_view value, std::string_view prefix)
{
    return value.size() >= prefix.size() && std::equal(prefix.begin(), prefix.end(), value.begin(),
                                                       [](char left, char right)
                                                       {
                                                           return std::tolower(static_cast<unsigned char>(left)) ==
                                                                  std::tolower(static_cast<unsigned char>(right));
                                                       });
}

bool valid_server_url(std::string_view url)
{
    size_t scheme_length = 0;
    if (starts_with_case_insensitive(url, "rtmp://"))
    {
        scheme_length = 7;
    }
    else if (starts_with_case_insensitive(url, "rtmps://"))
    {
        scheme_length = 8;
    }
    else
    {
        return false;
    }

    if (url.find_first_of("\t\r\n ?#@") != std::string_view::npos)
    {
        return false;
    }

    const size_t authority_end = url.find('/', scheme_length);
    const std::string_view authority = url.substr(scheme_length, authority_end - scheme_length);
    if (authority.empty())
    {
        return false;
    }

    if (authority.front() == '[')
    {
        const size_t closing_bracket = authority.find(']');
        if (closing_bracket == std::string_view::npos || closing_bracket == 1)
        {
            return false;
        }
        if (closing_bracket + 1 == authority.size())
        {
            return true;
        }
        if (authority[closing_bracket + 1] != ':')
        {
            return false;
        }

        const std::string_view port = authority.substr(closing_bracket + 2);
        if (port.empty() || !std::all_of(port.begin(), port.end(),
                                         [](char value) { return std::isdigit(static_cast<unsigned char>(value)); }))
        {
            return false;
        }
        try
        {
            return std::stoul(std::string(port)) <= 65535;
        }
        catch (...)
        {
            return false;
        }
    }

    const size_t port_separator = authority.rfind(':');
    if (port_separator == std::string_view::npos)
    {
        return authority.front() != ':';
    }

    const std::string_view host = authority.substr(0, port_separator);
    const std::string_view port = authority.substr(port_separator + 1);
    if (host.empty() || port.empty() ||
        !std::all_of(port.begin(), port.end(),
                     [](char value) { return std::isdigit(static_cast<unsigned char>(value)); }))
    {
        return false;
    }

    try
    {
        return std::stoul(std::string(port)) <= 65535;
    }
    catch (...)
    {
        return false;
    }
}
} // namespace

streaming_configuration_result validate_streaming_config(const castor_engine_streaming_config_t* config)
{
    if (config == nullptr)
    {
        return failure("The streaming configuration must not be null.");
    }
    if (config->struct_size < sizeof(castor_engine_streaming_config_t))
    {
        return failure("The streaming configuration structure is too small. Expected at least " +
                       std::to_string(sizeof(castor_engine_streaming_config_t)) + " bytes, received " +
                       std::to_string(config->struct_size) + ".");
    }
    if (config->server_url == nullptr || config->server_url[0] == '\0' || !valid_server_url(config->server_url))
    {
        return failure("The streaming server URL must be an absolute rtmp:// or rtmps:// URL with a valid host.");
    }
    if (config->stream_key == nullptr || config->stream_key[0] == '\0')
    {
        return failure("The streaming key must not be null or empty.");
    }
    if (config->use_authentication > 1)
    {
        return failure("The streaming authentication flag must be zero or one.");
    }
    if (config->use_authentication != 0 && (config->username == nullptr || config->username[0] == '\0' ||
                                            config->password == nullptr || config->password[0] == '\0'))
    {
        return failure("Streaming authentication requires a non-empty username and password.");
    }
    if (config->reconnect_retry_count > static_cast<uint32_t>(INT_MAX) ||
        config->reconnect_delay_seconds > static_cast<uint32_t>(INT_MAX))
    {
        return failure("Streaming reconnection values exceed the range supported by OBS.");
    }
    if (config->reconnect_retry_count != 0 && config->reconnect_delay_seconds == 0)
    {
        return failure("The streaming reconnect delay must be at least one second when retries are enabled.");
    }

    return {CASTOR_ENGINE_OK, {}};
}

streaming_configuration copy_streaming_config(const castor_engine_streaming_config_t& config)
{
    streaming_configuration result;
    result.server_url = config.server_url;
    const size_t scheme_end = result.server_url.find(':');
    std::transform(result.server_url.begin(), result.server_url.begin() + static_cast<std::ptrdiff_t>(scheme_end),
                   result.server_url.begin(),
                   [](char value) { return static_cast<char>(std::tolower(static_cast<unsigned char>(value))); });
    result.stream_key = config.stream_key;
    result.use_authentication = config.use_authentication != 0;
    result.username = result.use_authentication ? config.username : "";
    result.password = result.use_authentication ? config.password : "";
    result.reconnect_retry_count = config.reconnect_retry_count;
    result.reconnect_delay_seconds = config.reconnect_delay_seconds;
    return result;
}

bool streaming_configs_match(const streaming_configuration& left, const streaming_configuration& right) noexcept
{
    return left.server_url == right.server_url && left.stream_key == right.stream_key &&
           left.use_authentication == right.use_authentication && left.username == right.username &&
           left.password == right.password && left.reconnect_retry_count == right.reconnect_retry_count &&
           left.reconnect_delay_seconds == right.reconnect_delay_seconds;
}
} // namespace castor::engine::detail
