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

#define CASTOR_ENGINE_ABI_VERSION 6
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

        CASTOR_ENGINE_VIDEO_ENCODER_UNKNOWN_ID = 21,
        CASTOR_ENGINE_VIDEO_ENCODER_UNAVAILABLE = 22,
        CASTOR_ENGINE_VIDEO_ENCODER_ALREADY_CONFIGURED = 23,
        CASTOR_ENGINE_VIDEO_ENCODER_CREATION_FAILED = 24,

        CASTOR_ENGINE_AUDIO_NOT_CONFIGURED = 25,
        CASTOR_ENGINE_AUDIO_ENCODER_ALREADY_CONFIGURED = 26,
        CASTOR_ENGINE_AUDIO_ENCODER_UNAVAILABLE = 27,
        CASTOR_ENGINE_AUDIO_ENCODER_CREATION_FAILED = 28,
    } castor_engine_result_t;

    typedef enum castor_engine_speaker_layout
    {
        CASTOR_ENGINE_SPEAKERS_DEFAULT = 0,
        CASTOR_ENGINE_SPEAKERS_MONO = 1,
        CASTOR_ENGINE_SPEAKERS_STEREO = 2,
    } castor_engine_speaker_layout_t;

    typedef enum castor_engine_video_encoder_selection_mode
    {
        CASTOR_ENGINE_VIDEO_ENCODER_AUTOMATIC = 0,
        CASTOR_ENGINE_VIDEO_ENCODER_HARDWARE_PREFERRED = 1,
        CASTOR_ENGINE_VIDEO_ENCODER_SOFTWARE_FORCED = 2,
    } castor_engine_video_encoder_selection_mode_t;

    typedef enum castor_engine_video_encoder_rate_control
    {
        CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CBR = 0,
        CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_VBR = 1,
        CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CQP = 2,
        CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CRF = 3,
    } castor_engine_video_encoder_rate_control_t;

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

    /**
     * A versioned description of how the engine should select, create, and
     * apply settings to the video encoder, plus the audio settings the
     * audio encoder will use once it is introduced. audio_bitrate and
     * audio_track_index are reserved for that follow-up work and have no
     * effect on the video encoder created from this configuration.
     *
     * An empty encoder_id lets selection_mode decide the encoder; a
     * non-empty encoder_id is used directly, with no fallback, and fails if
     * it does not name an available video encoder. An empty preset or
     * profile leaves that setting unset, letting OBS apply its own default.
     */
    typedef struct castor_engine_video_encoder_config
    {
        uint32_t struct_size;
        uint32_t selection_mode;
        char encoder_id[64];
        uint32_t bitrate;
        uint32_t rate_control;
        uint32_t keyframe_interval_seconds;
        char preset[32];
        char profile[32];
        uint32_t audio_bitrate;
        uint32_t audio_track_index;
    } castor_engine_video_encoder_config_t;

    /**
     * Engine-owned metadata describing an available video encoder. Never
     * carries an OBS or platform-native handle.
     */
    typedef struct castor_engine_video_encoder_info
    {
        uint32_t struct_size;
        char id[64];
        char name[128];
        uint8_t is_hardware;
        uint8_t is_available;
    } castor_engine_video_encoder_info_t;

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

    /**
     * Gets the number of video encoders available in the current OBS
     * runtime. Requires the engine to be initialized and its modules loaded.
     */
    CASTOR_ENGINE_API uint32_t castor_engine_get_video_encoder_count(void);

    /**
     * Retrieves metadata for the video encoder at the given index, in the
     * same order and count as castor_engine_get_video_encoder_count. The
     * caller must set struct_size before calling. Returns 0 when the index
     * is out of range, the pointer is null, or struct_size is too small.
     */
    CASTOR_ENGINE_API uint8_t castor_engine_get_video_encoder_at(uint32_t index,
                                                                  castor_engine_video_encoder_info_t* out_info);

    /**
     * Validates a video encoder configuration in isolation. This does not
     * require the engine or OBS to be initialized and does not select or
     * create an encoder.
     */
    CASTOR_ENGINE_API castor_engine_result_t
    castor_engine_validate_video_encoder_config(const castor_engine_video_encoder_config_t* config);

    /**
     * Selects, creates, and binds the video encoder to the OBS video
     * pipeline from a validated configuration. Requires the video subsystem
     * to already be configured.
     *
     * Repeating the same effective configuration is a no-op. Requesting
     * different settings while the video encoder is already created is
     * rejected with CASTOR_ENGINE_VIDEO_ENCODER_ALREADY_CONFIGURED; shut
     * down the engine first to apply a different configuration.
     *
     * A hardware-preferred or automatic selection that falls back to
     * software never fails silently: check
     * castor_engine_get_video_encoder_fallback_notice after a successful
     * call.
     */
    CASTOR_ENGINE_API castor_engine_result_t
    castor_engine_configure_video_encoder(const castor_engine_video_encoder_config_t* config);

    CASTOR_ENGINE_API uint8_t castor_engine_is_video_encoder_configured(void);

    /**
     * Retrieves the engine-owned effective video encoder configuration. The
     * caller must set struct_size before calling. Returns 0 when the video
     * encoder is not configured, the pointer is null, or struct_size is too
     * small.
     */
    CASTOR_ENGINE_API uint8_t castor_engine_get_video_encoder_config(castor_engine_video_encoder_config_t* out_config);

    /**
     * Retrieves metadata for the video encoder actually selected by the
     * last successful castor_engine_configure_video_encoder call. Returns 0
     * when no video encoder is configured.
     */
    CASTOR_ENGINE_API uint8_t
    castor_engine_get_selected_video_encoder(castor_engine_video_encoder_info_t* out_info);

    /**
     * Describes why the video encoder fell back from hardware to software,
     * when castor_engine_configure_video_encoder did so. Returns an empty
     * string when the current configuration did not fall back.
     */
    CASTOR_ENGINE_API const char* castor_engine_get_video_encoder_fallback_notice(void);

    /**
     * Validates the audio_bitrate and audio_track_index fields of a video
     * encoder configuration in isolation, for use with
     * castor_engine_configure_audio_encoder. This does not require the
     * engine, OBS, or the audio subsystem to be initialized, and does not
     * inspect any other field in the configuration.
     */
    CASTOR_ENGINE_API castor_engine_result_t
    castor_engine_validate_audio_encoder_config(const castor_engine_video_encoder_config_t* config);

    /**
     * Creates the AAC audio encoder from audio_bitrate and binds it to the
     * OBS audio pipeline on the mixer identified by audio_track_index.
     * Requires the OBS audio subsystem (see castor_engine_configure_audio)
     * to already be configured. Independent of the video encoder: neither
     * requires the other to be configured first.
     *
     * Repeating the same effective configuration is a no-op. Requesting
     * different settings while the audio encoder is already created is
     * rejected with CASTOR_ENGINE_AUDIO_ENCODER_ALREADY_CONFIGURED; shut
     * down the engine first to apply a different configuration.
     */
    CASTOR_ENGINE_API castor_engine_result_t
    castor_engine_configure_audio_encoder(const castor_engine_video_encoder_config_t* config);

    CASTOR_ENGINE_API uint8_t castor_engine_is_audio_encoder_configured(void);

    /**
     * Retrieves metadata for the audio encoder actually selected by the
     * last successful castor_engine_configure_audio_encoder call.
     * is_hardware is always 0. Returns 0 when no audio encoder is
     * configured.
     */
    CASTOR_ENGINE_API uint8_t
    castor_engine_get_selected_audio_encoder(castor_engine_video_encoder_info_t* out_info);

    CASTOR_ENGINE_API castor_engine_result_t castor_engine_create_main_scene(void);

    CASTOR_ENGINE_API uint8_t castor_engine_has_active_scene(void);

    CASTOR_ENGINE_API void castor_engine_shutdown(void);

    CASTOR_ENGINE_API uint8_t castor_engine_is_initialized(void);

#ifdef __cplusplus
}
#endif
