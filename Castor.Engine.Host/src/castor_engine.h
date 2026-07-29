#pragma once

#include <stdint.h>

#if defined(_WIN32)
#if defined(CASTOR_ENGINE_HOST_EXPORTS)
#define CASTOR_ENGINE_API __declspec(dllexport)
#else
#define CASTOR_ENGINE_API __declspec(dllimport)
#endif
#else
#define CASTOR_ENGINE_API
#endif

#ifdef __cplusplus
extern "C"
{
#endif

#define CASTOR_ENGINE_ABI_VERSION 1
#define CASTOR_ENGINE_VERSION "0.1.0-alpha.1"

    CASTOR_ENGINE_API uint32_t castor_engine_get_abi_version(void);

    CASTOR_ENGINE_API const char* castor_engine_get_obs_version(void);

    CASTOR_ENGINE_API const char* castor_engine_get_version(void);

    CASTOR_ENGINE_API uint8_t castor_engine_initialize(void);

    CASTOR_ENGINE_API void castor_engine_shutdown(void);

    CASTOR_ENGINE_API uint8_t castor_engine_is_initialized(void);

#ifdef __cplusplus
}
#endif
