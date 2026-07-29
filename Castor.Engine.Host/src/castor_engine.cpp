#include "castor_engine.h"

#include <obs.h>

uint32_t castor_engine_get_abi_version(void)
{
    return CASTOR_ENGINE_ABI_VERSION;
}

const char* castor_engine_get_version(void)
{
    return CASTOR_ENGINE_VERSION;
}

const char* castor_engine_get_obs_version(void)
{
    return obs_get_version_string();
}

uint8_t castor_engine_initialize(void)
{
    if (obs_initialized())
    {
        return 1U;
    }

    return obs_startup("en-US", nullptr, nullptr) ? 1U : 0U;
}

void castor_engine_shutdown(void)
{
    if (obs_initialized())
    {
        obs_shutdown();
    }
}

uint8_t castor_engine_is_initialized(void)
{
    return obs_initialized() ? 1U : 0U;
}
