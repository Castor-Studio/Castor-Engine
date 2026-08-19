# Video Encoder Configuration

Castor Engine creates and configures the video encoder only after the video
subsystem has been configured (see [OBS Video Configuration](video-configuration.md)).

The managed API accepts Castor-owned values and does not expose OBS or
platform-native pointers:

```csharp
EngineRuntime.Initialize(
    new EngineRuntimeConfiguration(AppContext.BaseDirectory));

EngineRuntime.ConfigureVideo(
    new EngineVideoConfiguration(
        baseWidth: 1280,
        baseHeight: 720,
        outputWidth: 1280,
        outputHeight: 720,
        framesPerSecondNumerator: 30,
        framesPerSecondDenominator: 1));

IReadOnlyList<EngineVideoEncoderInfo> encoders =
    EngineRuntime.EnumerateVideoEncoders();

EngineRuntime.ConfigureVideoEncoder(
    new EngineVideoEncoderConfiguration(
        selectionMode: EngineVideoEncoderSelectionMode.HardwarePreferred,
        bitrate: 6000,
        rateControl: EngineVideoEncoderRateControl.ConstantBitrate,
        keyframeIntervalSeconds: 2));

bool isConfigured = EngineRuntime.IsVideoEncoderConfigured;
EngineVideoEncoderInfo selected = EngineRuntime.GetSelectedVideoEncoder();
string fallbackNotice = EngineRuntime.VideoEncoderFallbackNotice;
```

`EngineRuntime.EnumerateVideoEncoders`, `ConfigureVideoEncoder`,
`IsVideoEncoderConfigured`, `GetVideoEncoderConfiguration`,
`GetSelectedVideoEncoder`, and `VideoEncoderFallbackNotice` mirror
`castor_engine_get_video_encoder_count`/`_at`,
`castor_engine_configure_video_encoder`,
`castor_engine_is_video_encoder_configured`,
`castor_engine_get_video_encoder_config`,
`castor_engine_get_selected_video_encoder`, and
`castor_engine_get_video_encoder_fallback_notice` respectively, translating
native result codes and diagnostics into `InvalidOperationException`, and an
incompatible ABI into `NotSupportedException`.

## Native Contract

Castor Engine defines a versioned native video encoder configuration
contract, `castor_engine_video_encoder_config_t`, and a standalone
validation entry point, `castor_engine_validate_video_encoder_config`.

Validation is independent of engine, OBS, and video initialization: it can
be called and tested without an initialized engine, OBS instance, or
configured video subsystem, and does not select or create an encoder.

```c
castor_engine_video_encoder_config_t config = {0};
config.struct_size = sizeof(config);
config.selection_mode = CASTOR_ENGINE_VIDEO_ENCODER_SOFTWARE_FORCED;
config.bitrate = 2500;
config.rate_control = CASTOR_ENGINE_VIDEO_ENCODER_RATE_CONTROL_CBR;

castor_engine_result_t result = castor_engine_validate_video_encoder_config(&config);
```

### Fields

- `selection_mode`: one of the `castor_engine_video_encoder_selection_mode_t`
  values - `AUTOMATIC` (`0`) or `HARDWARE_PREFERRED` (`1`) both try an
  available hardware encoder first, falling back to the software encoder
  when none is available; `SOFTWARE_FORCED` (`2`) always uses the software
  encoder. Ignored when `encoder_id` is set.
- `encoder_id`: an engine-owned video encoder identifier, as returned by
  `castor_engine_get_video_encoder_at`. Empty lets `selection_mode` decide;
  set, it is used directly with no fallback, and fails if it does not name
  an available video encoder.
- `bitrate`: the video bitrate, in kbps. Must be greater than zero.
- `rate_control`: one of the `castor_engine_video_encoder_rate_control_t`
  values - CBR (`0`), VBR (`1`), CQP (`2`), CRF (`3`).
- `keyframe_interval_seconds`: the keyframe interval, in seconds. `0` lets
  the encoder apply its own default.
- `preset`, `profile`: encoder-specific setting names. Empty leaves the
  encoder's own default in place.
- `audio_bitrate`, `audio_track_index`: reserved for the audio encoder
  introduced in a follow-up feature. Validated only for shape in this
  contract and have no effect on the video encoder.

### Validation

`castor_engine_validate_video_encoder_config` rejects:

- a null configuration pointer, with `CASTOR_ENGINE_INVALID_ARGUMENT`;
- a `struct_size` smaller than `sizeof(castor_engine_video_encoder_config_t)`,
  with `CASTOR_ENGINE_INVALID_ARGUMENT`;
- an unsupported `selection_mode` or `rate_control`, a zero `bitrate`, or a
  non-null-terminated `encoder_id`/`preset`/`profile`, all with
  `CASTOR_ENGINE_INVALID_ARGUMENT`.

Each rejection sets a descriptive message retrievable through
`castor_engine_get_last_error`.

## Enumeration

`castor_engine_get_video_encoder_count` and `castor_engine_get_video_encoder_at`
enumerate the video encoders available in the current OBS runtime, once the
engine is initialized and its modules are loaded:

```c
uint32_t count = castor_engine_get_video_encoder_count();

for (uint32_t index = 0; index < count; ++index)
{
    castor_engine_video_encoder_info_t info = {0};
    info.struct_size = sizeof(info);
    castor_engine_get_video_encoder_at(index, &info);
    // info.id, info.name, info.is_hardware, info.is_available
}
```

Encoders are classified as hardware or software using OBS's own
`OBS_ENCODER_CAP_PASS_TEXTURE` capability flag, which the hardware encoder
plugins (NVENC, QSV, AMF) set and the software encoder does not - not by
guessing from the encoder id. Because those plugins only register their
encoder id once module loading has already detected the corresponding
hardware is present, enumeration presence already implies availability.

## Selection, Creation, and Binding

`castor_engine_configure_video_encoder` selects a video encoder id, creates
it through `obs_video_encoder_create` with settings applied from the
configuration, and binds it to the video pipeline:

```c
EngineRuntime.Initialize(...);      // castor_engine_initialize
EngineRuntime.ConfigureVideo(...);  // castor_engine_configure_video

castor_engine_video_encoder_config_t config = {0};
config.struct_size = sizeof(config);
config.selection_mode = CASTOR_ENGINE_VIDEO_ENCODER_HARDWARE_PREFERRED;
config.bitrate = 6000;

castor_engine_result_t result = castor_engine_configure_video_encoder(&config);

uint8_t is_configured = castor_engine_is_video_encoder_configured();

castor_engine_video_encoder_info_t selected = {0};
selected.struct_size = sizeof(selected);
castor_engine_get_selected_video_encoder(&selected);

const char* fallback_notice = castor_engine_get_video_encoder_fallback_notice();
```

Selection is scoped to H.264, verified through `obs_get_encoder_codec`
rather than assumed from an id:

- an explicit `encoder_id` is used directly if it names an available video
  encoder, with no fallback;
- `SOFTWARE_FORCED` selects the packaged x264 encoder;
- `AUTOMATIC`/`HARDWARE_PREFERRED` selects the first available hardware
  H.264 encoder, falling back to x264 when none is available.

A hardware-preferred or automatic fallback to software never fails or
happens silently: `castor_engine_get_video_encoder_fallback_notice` returns
a non-empty diagnostic naming the reason whenever the last successful
configuration fell back, and an empty string otherwise.

#### Lifecycle rules

- Configuring before the engine is initialized returns
  `CASTOR_ENGINE_NOT_INITIALIZED`.
- Configuring before the video subsystem is configured returns
  `CASTOR_ENGINE_VIDEO_NOT_CONFIGURED`.
- An explicit `encoder_id` that does not name an available video encoder
  returns `CASTOR_ENGINE_VIDEO_ENCODER_UNKNOWN_ID`.
- Repeating the same effective configuration is a no-op that returns
  `CASTOR_ENGINE_OK`.
- Requesting different settings while already configured is rejected with
  `CASTOR_ENGINE_VIDEO_ENCODER_ALREADY_CONFIGURED`, since no output exists
  yet to make a more permissive rule meaningful - shut down the engine to
  change the video encoder configuration.
- If OBS fails to create the encoder, `castor_engine_configure_video_encoder`
  returns `CASTOR_ENGINE_VIDEO_ENCODER_CREATION_FAILED`.
- `castor_engine_shutdown` releases the video encoder before OBS itself
  shuts down and clears the engine's video encoder state, so the full
  initialize/configure/shutdown lifecycle can run again.
