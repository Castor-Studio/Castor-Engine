#include "video_encoder_enumeration.h"

#include <cstring>
#include <obs.h>
#include <string>
#include <vector>

namespace castor::engine::detail
{
namespace
{
// Hardware encoder plugins (obs-nvenc, obs-qsv11, obs-amf) only register
// their encoder id once module loading has detected the corresponding
// hardware is present, so presence here already implies availability -
// enumeration never needs to instantiate an encoder just to check it.
std::vector<std::string> collect_video_encoder_ids()
{
    std::vector<std::string> ids;
    const char* id = nullptr;

    for (size_t index = 0; obs_enum_encoder_types(index, &id); ++index)
    {
        if (id != nullptr && obs_get_encoder_type(id) == OBS_ENCODER_VIDEO)
        {
            ids.emplace_back(id);
        }
    }

    return ids;
}

void copy_to_fixed_buffer(const char* source, char* destination, size_t destination_size)
{
    std::memset(destination, 0, destination_size);

    if (source != nullptr)
    {
        std::strncpy(destination, source, destination_size - 1);
    }
}
} // namespace

uint32_t get_video_encoder_count()
{
    return static_cast<uint32_t>(collect_video_encoder_ids().size());
}

bool get_video_encoder_at(uint32_t index, castor_engine_video_encoder_info_t& out_info)
{
    const std::vector<std::string> ids = collect_video_encoder_ids();

    if (index >= ids.size())
    {
        return false;
    }

    const std::string& id = ids[index];
    const char* display_name = obs_encoder_get_display_name(id.c_str());
    // OBS_ENCODER_CAP_PASS_TEXTURE means the encoder can consume a GPU
    // texture directly, which only the hardware encoder plugins set - x264
    // requires a CPU-side readback and never sets it.
    const uint32_t caps = obs_get_encoder_caps(id.c_str());

    out_info.struct_size = sizeof(castor_engine_video_encoder_info_t);
    copy_to_fixed_buffer(id.c_str(), out_info.id, sizeof(out_info.id));
    copy_to_fixed_buffer(display_name != nullptr ? display_name : id.c_str(), out_info.name, sizeof(out_info.name));
    out_info.is_hardware = (caps & OBS_ENCODER_CAP_PASS_TEXTURE) != 0 ? 1U : 0U;
    out_info.is_available = 1U;

    return true;
}
} // namespace castor::engine::detail
