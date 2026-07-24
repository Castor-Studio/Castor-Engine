#include "castor_engine.h"
#include <obs.h>

uint32_t castor_engine_get_abi_version(void) {
  return CASTOR_ENGINE_ABI_VERSION;
}

const char *castor_engine_get_version(void) { return CASTOR_ENGINE_VERSION; }

const char *castor_engine_get_obs_version(void) {
  return obs_get_version_string();
}
