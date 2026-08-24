#include "obs_display_enumeration.h"

#include <algorithm>
#include <cstring>
#include <obs-properties.h>
#include <obs.h>
#include <string>
#include <utility>

#if defined(_WIN32)
#include <Windows.h>
#endif

namespace castor::engine::detail
{
namespace
{
constexpr const char* display_source_unversioned_id = "monitor_capture";
constexpr const char* monitor_property_name = "monitor_id";
constexpr const char* legacy_monitor_property_name = "monitor";

struct platform_display_identity
{
    std::string id;
    bool is_primary = false;
};

#if defined(_WIN32)
BOOL CALLBACK collect_display_identity(HMONITOR monitor, HDC, LPRECT, LPARAM parameter)
{
    auto* identities = reinterpret_cast<std::vector<platform_display_identity>*>(parameter);
    MONITORINFOEXA monitor_info{};
    monitor_info.cbSize = sizeof(monitor_info);

    if (!GetMonitorInfoA(monitor, &monitor_info))
    {
        return TRUE;
    }

    DISPLAY_DEVICEA display_device{};
    display_device.cb = sizeof(display_device);

    std::string id;

    if (EnumDisplayDevicesA(monitor_info.szDevice, 0, &display_device, EDD_GET_DEVICE_INTERFACE_NAME) &&
        display_device.DeviceID[0] != '\0')
    {
        id = display_device.DeviceID;
    }
    else
    {
        id = monitor_info.szDevice;
    }

    identities->push_back({std::move(id), (monitor_info.dwFlags & MONITORINFOF_PRIMARY) != 0});
    return TRUE;
}

std::vector<platform_display_identity> get_platform_display_identities()
{
    std::vector<platform_display_identity> identities;
    EnumDisplayMonitors(nullptr, nullptr, collect_display_identity, reinterpret_cast<LPARAM>(&identities));
    return identities;
}
#else
std::vector<platform_display_identity> get_platform_display_identities()
{
    return {};
}
#endif
} // namespace

display_enumeration_result enumerate_obs_displays()
{
    const char* source_id = obs_get_latest_input_type_id(display_source_unversioned_id);

    if (source_id == nullptr)
    {
        return {CASTOR_ENGINE_DISPLAY_SOURCE_UNAVAILABLE,
                "The loaded OBS modules do not provide the 'monitor_capture' video source.",
                {}};
    }

    obs_properties_t* properties = obs_get_source_properties(source_id);

    if (properties == nullptr)
    {
        return {CASTOR_ENGINE_DISPLAY_SOURCE_UNAVAILABLE,
                "OBS did not expose properties for the 'monitor_capture' video source.",
                {}};
    }

    obs_property_t* monitors = obs_properties_get(properties, monitor_property_name);
    bool uses_string_selector = monitors != nullptr && obs_property_get_type(monitors) == OBS_PROPERTY_LIST &&
                                obs_property_list_format(monitors) == OBS_COMBO_FORMAT_STRING;

    if (!uses_string_selector)
    {
        monitors = obs_properties_get(properties, legacy_monitor_property_name);
    }

    const bool uses_integer_selector = monitors != nullptr && obs_property_get_type(monitors) == OBS_PROPERTY_LIST &&
                                       obs_property_list_format(monitors) == OBS_COMBO_FORMAT_INT;

    if (!uses_string_selector && !uses_integer_selector)
    {
        obs_properties_destroy(properties);
        return {CASTOR_ENGINE_DISPLAY_SOURCE_UNAVAILABLE,
                "The OBS 'monitor_capture' source did not expose an expected 'monitor_id' or 'monitor' property.",
                {}};
    }

    const std::vector<platform_display_identity> platform_displays = get_platform_display_identities();
    const size_t count = obs_property_list_item_count(monitors);
    std::vector<display_descriptor> displays;
    displays.reserve(count);

    for (size_t index = 0; index < count; ++index)
    {
        const char* name = obs_property_list_item_name(monitors, index);

        if (uses_string_selector)
        {
            const char* id = obs_property_list_item_string(monitors, index);

            if (id == nullptr || id[0] == '\0')
            {
                continue;
            }

            const auto platform_display = std::find_if(platform_displays.begin(), platform_displays.end(),
                                                       [id](const auto& display) { return display.id == id; });
            displays.push_back({id, name != nullptr && name[0] != '\0' ? name : id,
                                platform_display != platform_displays.end() && platform_display->is_primary, true, id,
                                0});
        }
        else
        {
            const long long monitor_index = obs_property_list_item_int(monitors, index);
            const bool has_platform_identity =
                monitor_index >= 0 && static_cast<size_t>(monitor_index) < platform_displays.size();
            const std::string engine_id = has_platform_identity
                                              ? platform_displays[static_cast<size_t>(monitor_index)].id
                                              : "obs-monitor-index:" + std::to_string(monitor_index);
            displays.push_back(
                {engine_id,
                 name != nullptr && name[0] != '\0' ? name : engine_id,
                 has_platform_identity && platform_displays[static_cast<size_t>(monitor_index)].is_primary,
                 false,
                 {},
                 monitor_index});
        }
    }

    obs_properties_destroy(properties);
    return {CASTOR_ENGINE_OK, {}, std::move(displays)};
}
} // namespace castor::engine::detail
