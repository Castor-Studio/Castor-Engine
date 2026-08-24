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

#define CASTOR_ENGINE_ABI_VERSION 10
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

        CASTOR_ENGINE_RECORDING_NO_ACTIVE_SCENE = 29,
        CASTOR_ENGINE_RECORDING_HARDWARE_ENCODER_NOT_ALLOWED = 30,
        CASTOR_ENGINE_RECORDING_ALREADY_ACTIVE = 31,
        CASTOR_ENGINE_RECORDING_NOT_ACTIVE = 32,
        CASTOR_ENGINE_RECORDING_INVALID_DESTINATION = 33,
        CASTOR_ENGINE_RECORDING_OUTPUT_UNAVAILABLE = 34,
        CASTOR_ENGINE_RECORDING_OUTPUT_CREATION_FAILED = 35,
        CASTOR_ENGINE_RECORDING_START_FAILED = 36,

        CASTOR_ENGINE_DISPLAY_INVALID_CONFIGURATION = 37,
        CASTOR_ENGINE_DISPLAY_NOT_FOUND = 38,
        CASTOR_ENGINE_DISPLAY_SOURCE_UNAVAILABLE = 39,
        CASTOR_ENGINE_DISPLAY_SOURCE_CREATION_FAILED = 40,
        CASTOR_ENGINE_DISPLAY_SOURCE_ADD_FAILED = 41,
        CASTOR_ENGINE_DISPLAY_NO_ACTIVE_SCENE = 42,
        CASTOR_ENGINE_DISPLAY_RECONFIGURATION_WHILE_RECORDING = 43,
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

    /**
     * Engine-owned metadata describing a display exposed by the loaded OBS
     * display-capture source. Never
     * carries an OBS or platform-native handle.
     */
    typedef struct castor_engine_display_info
    {
        uint32_t struct_size;
        char id[256];
        char name[256];
        uint8_t is_primary;
    } castor_engine_display_info_t;

    /**
     * A versioned request to replace the main scene's current visual source
     * with a capture of the
     * display identified by display_id.
     */
    typedef struct castor_engine_display_capture_config
    {
        uint32_t struct_size;
        char display_id[256];
        uint8_t capture_cursor;
    } castor_engine_display_capture_config_t;

    CASTOR_ENGINE_API castor_engine_result_t castor_engine_initialize(const castor_engine_config_t* config);

    CASTOR_ENGINE_API const char* castor_engine_get_last_error(void);

    CASTOR_ENGINE_API uint32_t castor_engine_get_loaded_module_count(void);

    CASTOR_ENGINE_API uint8_t castor_engine_is_module_loaded(const char* module_name);

    /**
     * Refreshes the engine-owned display snapshot from the properties exposed
     * by OBS's registered
     * monitor_capture source and returns its size. Returns
     * 0 both for a valid headless environment and for an
     * error; consult
     * castor_engine_get_last_error to distinguish those cases.
     */
    CASTOR_ENGINE_API uint32_t castor_engine_get_display_count(void);

    /**
     * Retrieves one entry from the latest display snapshot. The caller must
     * set struct_size before
     * calling. If no snapshot exists yet, one is
     * refreshed first.
     */
    CASTOR_ENGINE_API uint8_t castor_engine_get_display_at(uint32_t index, castor_engine_display_info_t* out_info);

    /** Validates only the versioned configuration shape and values. */
    CASTOR_ENGINE_API castor_engine_result_t
    castor_engine_validate_display_capture_config(const castor_engine_display_capture_config_t* config);

    /**
     * Replaces the main scene's current visual source with monitor_capture.
     * An identical effective
     * configuration is a no-op. A different
     * configuration is rejected while recording.
     */
    CASTOR_ENGINE_API castor_engine_result_t
    castor_engine_configure_display_capture(const castor_engine_display_capture_config_t* config);

    CASTOR_ENGINE_API uint8_t castor_engine_is_display_capture_active(void);

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
    CASTOR_ENGINE_API uint8_t castor_engine_get_selected_video_encoder(castor_engine_video_encoder_info_t* out_info);

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
    CASTOR_ENGINE_API uint8_t castor_engine_get_selected_audio_encoder(castor_engine_video_encoder_info_t* out_info);

    /**
     * Retrieves an opaque, engine-owned handle to the configured video
     * encoder, for a native output implementation to bind (e.g. through
     * obs_output_set_video_encoder). Never a struct or a documented OBS
     * type - callers must not interpret or dereference it themselves.
     *
     * The handle is non-owning: the engine still owns and releases the
     * encoder. The same handle may be retrieved and attached to more than
     * one output. Returns NULL when the video encoder is not configured.
     *
     * The handle becomes invalid when the engine shuts down or the video
     * encoder is reconfigured. The engine does not track which outputs
     * hold an outstanding handle, so any output using one must stop and
     * release itself before engine shutdown reaches the point where the
     * encoder itself is released.
     */
    CASTOR_ENGINE_API void* castor_engine_get_video_encoder_handle(void);

    /**
     * Retrieves an opaque, engine-owned handle to the configured audio
     * encoder, for a native output implementation to bind (e.g. through
     * obs_output_set_audio_encoder). Same contract as
     * castor_engine_get_video_encoder_handle: non-owning, attachable to
     * more than one output, invalid after shutdown or reconfiguration, and
     * NULL when the audio encoder is not configured.
     */
    CASTOR_ENGINE_API void* castor_engine_get_audio_encoder_handle(void);

    /**
     * A versioned description of an MKV recording destination. If
     * destination_path already exists, it is overwritten - Castor Engine
     * does not perform its own existence check or provide a no-overwrite
     * mode.
     */
    typedef struct castor_engine_recording_config
    {
        uint32_t struct_size;
        const char* destination_path;
    } castor_engine_recording_config_t;

    /**
     * Validates a recording configuration in isolation. This does not
     * require the engine or OBS to be initialized and does not start a
     * recording.
     */
    CASTOR_ENGINE_API castor_engine_result_t
    castor_engine_validate_recording_config(const castor_engine_recording_config_t* config);

    /**
     * Starts recording the active main scene to destination_path as an
     * MKV file, encoded with the video encoder.
     *
     * If no video encoder is configured yet, one is created automatically
     * in forced-software mode with deterministic baseline settings. If a
     * video encoder is already configured, it must be a software encoder
     * - a configured hardware encoder is rejected with
     * CASTOR_ENGINE_RECORDING_HARDWARE_ENCODER_NOT_ALLOWED, since this
     * recording path never uses a hardware encoder, silently or
     * otherwise.
     *
     * Requires the engine to be initialized, the video subsystem to be
     * configured, and the main scene to be active. Starting while already
     * recording is rejected with CASTOR_ENGINE_RECORDING_ALREADY_ACTIVE.
     */
    CASTOR_ENGINE_API castor_engine_result_t
    castor_engine_start_recording(const castor_engine_recording_config_t* config);

    /**
     * Stops the active recording and blocks until OBS has finalized the
     * MKV container before returning. Returns
     * CASTOR_ENGINE_RECORDING_NOT_ACTIVE when no recording is active.
     */
    CASTOR_ENGINE_API castor_engine_result_t castor_engine_stop_recording(void);

    CASTOR_ENGINE_API uint8_t castor_engine_is_recording_active(void);

    CASTOR_ENGINE_API castor_engine_result_t castor_engine_create_main_scene(void);

    CASTOR_ENGINE_API uint8_t castor_engine_has_active_scene(void);

    CASTOR_ENGINE_API void castor_engine_shutdown(void);

    CASTOR_ENGINE_API uint8_t castor_engine_is_initialized(void);

#ifdef __cplusplus
}
#endif
