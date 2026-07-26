#include "castor_engine.h"

uint32_t castor_engine_get_abi_version(void)
{
    return CASTOR_ENGINE_ABI_VERSION;
}

const char* castor_engine_get_version(void)
{
    return CASTOR_ENGINE_VERSION;
}
