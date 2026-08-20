# MKV Recording

Castor Engine records the active main scene (see [Default OBS Main Scene](main-scene.md))
to a finalized MKV file, encoded through the [video and audio encoding layer](video-encoder-configuration.md).

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

EngineRuntime.CreateMainScene();

EngineRuntime.StartRecording(
    new EngineRecordingConfiguration(@"C:\recordings\output.mkv"));

bool isRecording = EngineRuntime.IsRecordingActive;

EngineRuntime.StopRecording();
```

`EngineRuntime.StartRecording`, `StopRecording`, and `IsRecordingActive`
mirror `castor_engine_start_recording`, `castor_engine_stop_recording`, and
`castor_engine_is_recording_active` respectively, translating native result
codes and diagnostics into `InvalidOperationException`, and an incompatible
ABI into `NotSupportedException`.

## Encoders are configured automatically

`StartRecording` does not require the video or audio encoder to be
configured first. If the video encoder isn't configured, it is created
automatically in forced-software mode with baseline settings (the same
behavior [issue #14](https://github.com/Castor-Studio/Castor-Engine/issues/14)
originally specified as its own encoder-creation logic, now delivered
through the shared encoding layer instead of duplicating it). If an already-configured
video encoder turns out to be a hardware encoder, recording is rejected -
this recording path never uses a hardware encoder, silently or otherwise.

**The recording always includes an audio track.** OBS's `ffmpeg_muxer`
output - the one this feature uses - refuses to start without an audio
encoder bound, even for a nominally video-only recording: it declares both
video and audio capability, and `obs_output_start` fails immediately (with
no error text) if no audio encoder is attached. There is no supported way
to make it accept a video-only stream (`obs_output_set_mixers`, which
controls active audio tracks on raw/unencoded outputs, does not apply to
encoded outputs like this one and is rejected by OBS if attempted). So if
the audio subsystem or the AAC audio encoder aren't configured yet,
`StartRecording` configures both automatically, the same way it does for
the video encoder.

## Native contract

Castor Engine defines a versioned native recording configuration contract,
`castor_engine_recording_config_t`, and a standalone validation entry
point, `castor_engine_validate_recording_config`.

```c
castor_engine_recording_config_t config = {0};
config.struct_size = sizeof(config);
config.destination_path = "C:\\recordings\\output.mkv";

castor_engine_result_t result = castor_engine_validate_recording_config(&config);
```

### Fields

- `destination_path`: the UTF-8 destination path for the MKV file. Required.
  If the file already exists, it is overwritten - Castor Engine does not
  perform its own existence check or provide a no-overwrite mode.

### Validation

`castor_engine_validate_recording_config` rejects a null configuration
pointer, a `struct_size` smaller than `sizeof(castor_engine_recording_config_t)`,
or a null/empty `destination_path`, all with `CASTOR_ENGINE_INVALID_ARGUMENT`.
`castor_engine_start_recording` additionally rejects a `destination_path`
whose parent directory does not exist, with `CASTOR_ENGINE_RECORDING_INVALID_DESTINATION` -
a deterministic, engine-side check performed before OBS is ever involved.

## Starting, stopping, and finalization

`castor_engine_start_recording` creates an engine-owned `ffmpeg_muxer`
output targeting `destination_path`, binds the video and audio encoders
retrieved through the [encoder retrieval API](video-encoder-configuration.md#encoder-retrieval),
and starts it, including the underlying OBS error in the diagnostic when
`obs_output_start` fails.

`castor_engine_stop_recording` requests the stop and then **blocks until
OBS has actually finalized the MKV container** before releasing the output -
`obs_output_stop` alone only requests a stop and returns immediately, so
`castor_engine_stop_recording` connects to the output's own `"stop"`
signal and waits for it (bounded by an internal timeout as a defensive
fallback) before returning.

#### Lifecycle rules

- Starting before the engine is initialized returns `CASTOR_ENGINE_NOT_INITIALIZED`.
- Starting before the video subsystem is configured returns `CASTOR_ENGINE_VIDEO_NOT_CONFIGURED`.
- Starting before the main scene is active returns `CASTOR_ENGINE_RECORDING_NO_ACTIVE_SCENE`.
- Starting while already recording returns `CASTOR_ENGINE_RECORDING_ALREADY_ACTIVE`.
- Stopping while not recording returns `CASTOR_ENGINE_RECORDING_NOT_ACTIVE`.
- A second recording can be started once the first has been stopped;
  each `StartRecording`/`StopRecording` pair is independent.
- `castor_engine_shutdown` stops an active recording (waiting for
  finalization the same way `castor_engine_stop_recording` does) and
  releases the output *before* releasing the video and audio encoders it
  references - the release-ordering contract the
  [encoder retrieval API](video-encoder-configuration.md#encoder-retrieval)
  documents, exercised here for the first time with a real output.
