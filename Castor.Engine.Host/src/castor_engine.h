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

#define CASTOR_ENGINE_ABI_VERSION 2
#define CASTOR_ENGINE_VERSION "0.1.0-alpha.1"

    CASTOR_ENGINE_API uint32_t castor_engine_get_abi_version(void);

    CASTOR_ENGINE_API const char* castor_engine_get_obs_version(void);

    CASTOR_ENGINE_API const char* castor_engine_get_version(void);

    typedef enum castor_engine_result
    {
        CASTOR_ENGINE_OK = 0,
        CASTOR_ENGINE_INVALID_ARGUMENT = 1,
        CASTOR_ENGINE_INVALID_RUNTIME = 2,
        CASTOR_ENGINE_OBS_STARTUP_FAILED = 3,
        CASTOR_ENGINE_MODULE_LOAD_FAILED = 4,
    } castor_engine_result_t;

    typedef struct castor_engine_config
    {
        uint32_t struct_size;
        const char* runtime_root;
        const char* locale;
    } castor_engine_config_t;

    CASTOR_ENGINE_API castor_engine_result_t castor_engine_initialize(const castor_engine_config_t* config);

    CASTOR_ENGINE_API const char* castor_engine_get_last_error(void);

    CASTOR_ENGINE_API uint32_t castor_engine_get_loaded_module_count(void);

    CASTOR_ENGINE_API uint8_t castor_engine_is_module_loaded(const char* module_name);

    CASTOR_ENGINE_API void castor_engine_shutdown(void);

    CASTOR_ENGINE_API uint8_t castor_engine_is_initialized(void);

#ifdef __cplusplus
}
#endif
