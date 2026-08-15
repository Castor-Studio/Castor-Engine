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

#define CASTOR_ENGINE_ABI_VERSION 5
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
        CASTOR_ENGINE_NOT_INITIALIZED = 5,

        CASTOR_ENGINE_VIDEO_NOT_SUPPORTED = 6,
        CASTOR_ENGINE_VIDEO_INVALID_CONFIGURATION = 7,
        CASTOR_ENGINE_VIDEO_CURRENTLY_ACTIVE = 8,
        CASTOR_ENGINE_VIDEO_MODULE_NOT_FOUND = 9,
        CASTOR_ENGINE_VIDEO_CONFIGURATION_FAILED = 10,
        CASTOR_ENGINE_VIDEO_NOT_CONFIGURED = 11,

        CASTOR_ENGINE_AUDIO_UNSUPPORTED_SAMPLE_RATE = 12,
        CASTOR_ENGINE_AUDIO_UNSUPPORTED_SPEAKER_LAYOUT = 13,
        CASTOR_ENGINE_AUDIO_ALREADY_CONFIGURED = 14,
        CASTOR_ENGINE_AUDIO_CONFIGURATION_FAILED = 15,

        CASTOR_ENGINE_SCENE_CREATION_FAILED = 16,
        CASTOR_ENGINE_SCENE_SOURCE_UNAVAILABLE = 17,
        CASTOR_ENGINE_SCENE_SOURCE_CREATION_FAILED = 18,
        CASTOR_ENGINE_SCENE_SOURCE_ADD_FAILED = 19,
        CASTOR_ENGINE_SCENE_ACTIVATION_FAILED = 20,
    } castor_engine_result_t;

    typedef enum castor_engine_speaker_layout
    {
        CASTOR_ENGINE_SPEAKERS_DEFAULT = 0,
        CASTOR_ENGINE_SPEAKERS_MONO = 1,
        CASTOR_ENGINE_SPEAKERS_STEREO = 2,
    } castor_engine_speaker_layout_t;

    typedef struct castor_engine_config
    {
        uint32_t struct_size;
        const char* runtime_root;
        const char* locale;
    } castor_engine_config_t;

    typedef struct castor_engine_video_config
    {
        uint32_t struct_size;
        uint32_t base_width;
        uint32_t base_height;
        uint32_t output_width;
        uint32_t output_height;
        uint32_t fps_numerator;
        uint32_t fps_denominator;
    } castor_engine_video_config_t;

    /**
     * A sample_rate of 0 resolves to the default of 48000 Hz. A speaker_layout
     * of CASTOR_ENGINE_SPEAKERS_DEFAULT resolves to CASTOR_ENGINE_SPEAKERS_STEREO.
     * The initial implementation supports 44100 Hz and 48000 Hz sample rates,
     * and mono/stereo speaker layouts.
     */
    typedef struct castor_engine_audio_config
    {
        uint32_t struct_size;
        uint32_t sample_rate;
        uint32_t speaker_layout;
    } castor_engine_audio_config_t;

    CASTOR_ENGINE_API castor_engine_result_t castor_engine_initialize(const castor_engine_config_t* config);

    CASTOR_ENGINE_API const char* castor_engine_get_last_error(void);

    CASTOR_ENGINE_API uint32_t castor_engine_get_loaded_module_count(void);

    CASTOR_ENGINE_API uint8_t castor_engine_is_module_loaded(const char* module_name);

    CASTOR_ENGINE_API castor_engine_result_t castor_engine_configure_video(const castor_engine_video_config_t* config);

    CASTOR_ENGINE_API uint8_t castor_engine_is_video_configured(void);

    /**
     * Validates an audio configuration in isolation. This does not require the
     * engine or OBS to be initialized and does not apply the configuration.
     */
    CASTOR_ENGINE_API castor_engine_result_t
    castor_engine_validate_audio_config(const castor_engine_audio_config_t* config);

    /**
     * Initializes or re-applies the OBS audio subsystem from a validated
     * configuration. Requires no physical playback or capture device.
     *
     * Repeating the same effective configuration is a no-op. OBS does not
     * support runtime audio reconfiguration, so requesting different values
     * while the subsystem is already configured is rejected with
     * CASTOR_ENGINE_AUDIO_ALREADY_CONFIGURED; shut down the engine first to
     * apply different audio settings.
     */
    CASTOR_ENGINE_API castor_engine_result_t castor_engine_configure_audio(const castor_engine_audio_config_t* config);

    CASTOR_ENGINE_API uint8_t castor_engine_is_audio_configured(void);

    /**
     * Retrieves the engine-owned effective audio configuration. The caller
     * must set struct_size before calling. Returns 0 when the audio
     * subsystem is not configured, the pointer is null, or struct_size is
     * too small.
     */
    CASTOR_ENGINE_API uint8_t castor_engine_get_audio_config(castor_engine_audio_config_t* out_config);
    CASTOR_ENGINE_API castor_engine_result_t castor_engine_create_main_scene(void);

    CASTOR_ENGINE_API uint8_t castor_engine_has_active_scene(void);

    CASTOR_ENGINE_API void castor_engine_shutdown(void);

    CASTOR_ENGINE_API uint8_t castor_engine_is_initialized(void);

#ifdef __cplusplus
}
#endif
