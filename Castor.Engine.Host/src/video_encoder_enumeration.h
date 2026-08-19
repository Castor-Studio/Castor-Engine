#pragma once

#include "castor_engine.h"

#include <cstdint>

namespace castor::engine::detail
{
uint32_t get_video_encoder_count();

bool get_video_encoder_at(uint32_t index, castor_engine_video_encoder_info_t& out_info);
} // namespace castor::engine::detail
