# Native Audio Configuration

Castor Engine defines a versioned native audio configuration contract,
`castor_engine_audio_config_t`, and a standalone validation entry point,
`castor_engine_validate_audio_config`.

Validation is independent of engine and OBS initialization: it can be called
and tested without an initialized engine or OBS instance, and does not apply
the configuration to OBS. Applying a validated configuration to the OBS audio
subsystem is tracked separately.

```c
castor_engine_audio_config_t config = {0};
config.struct_size = sizeof(config);
config.sample_rate = 48000;
config.speaker_layout = CASTOR_ENGINE_SPEAKERS_STEREO;

castor_engine_result_t result = castor_engine_validate_audio_config(&config);
```

## Fields

- `sample_rate`: the audio sample rate, in Hz. A value of `0` resolves to the
  default of 48000 Hz. Supported sample rates are 44100 Hz and 48000 Hz.
- `speaker_layout`: the speaker layout, one of the
  `castor_engine_speaker_layout_t` values. `CASTOR_ENGINE_SPEAKERS_DEFAULT`
  (`0`) resolves to `CASTOR_ENGINE_SPEAKERS_STEREO`. Supported layouts are
  `CASTOR_ENGINE_SPEAKERS_MONO` and `CASTOR_ENGINE_SPEAKERS_STEREO`.

## Validation

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
