# Audio Configuration

The managed API accepts Castor-owned values and does not expose native or
OBS types:

```csharp
EngineRuntime.Initialize(
    new EngineRuntimeConfiguration(AppContext.BaseDirectory));

EngineRuntime.ConfigureAudio(
    new EngineAudioConfiguration(
        sampleRate: 48000,
        speakerLayout: EngineSpeakerLayout.Stereo));

bool isConfigured = EngineRuntime.IsAudioConfigured;

EngineAudioConfiguration effective = EngineRuntime.GetAudioConfiguration();
```

`EngineRuntime.ConfigureAudio`, `EngineRuntime.IsAudioConfigured`, and
`EngineRuntime.GetAudioConfiguration` mirror `castor_engine_configure_audio`,
`castor_engine_is_audio_configured`, and `castor_engine_get_audio_config`
respectively, translating native result codes and diagnostics into
`InvalidOperationException`, and an incompatible ABI into
`NotSupportedException`. `EngineAudioConfiguration.SampleRate` left at `0`
and `EngineAudioConfiguration.SpeakerLayout` left at
`EngineSpeakerLayout.Default` resolve to the documented defaults (48 kHz,
stereo), matching the native behavior described below.

## Native Contract

Castor Engine defines a versioned native audio configuration contract,
`castor_engine_audio_config_t`, and a standalone validation entry point,
`castor_engine_validate_audio_config`.

Validation is independent of engine and OBS initialization: it can be called
and tested without an initialized engine or OBS instance, and does not apply
the configuration to OBS.

```c
castor_engine_audio_config_t config = {0};
config.struct_size = sizeof(config);
config.sample_rate = 48000;
config.speaker_layout = CASTOR_ENGINE_SPEAKERS_STEREO;

castor_engine_result_t result = castor_engine_validate_audio_config(&config);
```

### Fields

- `sample_rate`: the audio sample rate, in Hz. A value of `0` resolves to the
  default of 48000 Hz. Supported sample rates are 44100 Hz and 48000 Hz.
- `speaker_layout`: the speaker layout, one of the
  `castor_engine_speaker_layout_t` values. `CASTOR_ENGINE_SPEAKERS_DEFAULT`
  (`0`) resolves to `CASTOR_ENGINE_SPEAKERS_STEREO`. Supported layouts are
  `CASTOR_ENGINE_SPEAKERS_MONO` and `CASTOR_ENGINE_SPEAKERS_STEREO`.

### Validation

`castor_engine_validate_audio_config` rejects:

- a null configuration pointer, with `CASTOR_ENGINE_INVALID_ARGUMENT`;
- a `struct_size` smaller than `sizeof(castor_engine_audio_config_t)`, with
  `CASTOR_ENGINE_INVALID_ARGUMENT`;
- an unsupported sample rate, with `CASTOR_ENGINE_AUDIO_UNSUPPORTED_SAMPLE_RATE`;
- an unsupported speaker layout, with
  `CASTOR_ENGINE_AUDIO_UNSUPPORTED_SPEAKER_LAYOUT`.

Each rejection sets a descriptive message retrievable through
`castor_engine_get_last_error`, naming the offending field, the value
received, and the supported values. A configuration with `sample_rate` and
`speaker_layout` left at `0` validates successfully and resolves to the
documented defaults (48 kHz, stereo).

### OBS Audio Subsystem Lifecycle

`castor_engine_configure_audio` initializes the OBS audio subsystem from a
validated configuration, once the engine has been initialized. It requires
no physical playback or capture device: OBS audio is a software mixer, and
no audio source is created.

```c
EngineRuntime.Initialize(...); // castor_engine_initialize

castor_engine_audio_config_t config = {0};
config.struct_size = sizeof(config);
config.sample_rate = 48000;
config.speaker_layout = CASTOR_ENGINE_SPEAKERS_STEREO;

castor_engine_result_t result = castor_engine_configure_audio(&config);

uint8_t is_configured = castor_engine_is_audio_configured();

castor_engine_audio_config_t effective = {0};
effective.struct_size = sizeof(effective);
uint8_t has_config = castor_engine_get_audio_config(&effective);
```

`castor_engine_get_audio_config` copies the engine-owned effective
configuration into a caller-provided buffer; it never hands back an OBS
type or a pointer owned by the engine. The caller must set `struct_size`
before calling.

#### Lifecycle rules

- Configuring before the engine is initialized returns
  `CASTOR_ENGINE_NOT_INITIALIZED`.
- Repeating the same effective configuration is a no-op that returns
  `CASTOR_ENGINE_OK`.
- OBS does not support runtime audio reconfiguration: once configured,
  `obs_reset_audio` silently keeps the existing settings active instead of
  reporting a failure. To avoid the engine believing new settings were
  applied while OBS silently kept the old ones running, requesting
  different `sample_rate`/`speaker_layout` values while already configured
  is rejected with `CASTOR_ENGINE_AUDIO_ALREADY_CONFIGURED` — including
  while a recording is active, since no runtime reconfiguration path
  exists. Shut down the engine to change the audio configuration.
- If OBS fails to initialize the audio subsystem, `castor_engine_configure_audio`
  returns `CASTOR_ENGINE_AUDIO_CONFIGURATION_FAILED`.
- `castor_engine_shutdown` releases the OBS audio subsystem through the
  normal OBS shutdown and clears the engine's audio state, so the full
  initialize/configure/shutdown lifecycle can run again.
