# RTMP streaming

Castor Engine can send the active scene (see
[Scene Management](scene-management.md)) to one custom RTMP or RTMPS
destination. The output reuses the engine-owned video and audio encoders; it
never creates a separate encoder pass.

## Managed API

Keep credentials outside source control and inject them through the process
environment:

Use `CASTOR_RTMP_USERNAME` and `CASTOR_RTMP_PASSWORD` in the same way when the
ingest server requires separate authentication; pass them to
`EngineStreamingConfiguration` with `useAuthentication: true`.

```csharp
var server = Environment.GetEnvironmentVariable("CASTOR_RTMP_SERVER")
    ?? throw new InvalidOperationException("CASTOR_RTMP_SERVER is required.");
var key = Environment.GetEnvironmentVariable("CASTOR_RTMP_STREAM_KEY")
    ?? throw new InvalidOperationException("CASTOR_RTMP_STREAM_KEY is required.");

EngineRuntime.Initialize(new EngineRuntimeConfiguration(AppContext.BaseDirectory));
EngineRuntime.ConfigureVideo(new EngineVideoConfiguration(1280, 720, 1280, 720, 30, 1));
EngineRuntime.ConfigureAudio(new EngineAudioConfiguration());
EngineRuntime.CreateScene("wide");
EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
EngineRuntime.ConfigureVideoEncoder(new EngineVideoEncoderConfiguration(
    selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced));
EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);
EngineRuntime.ConfigureStreaming(new EngineStreamingConfiguration(server, key));

EngineRuntime.StartStreaming();
EngineStreamingStatus status = EngineRuntime.GetStreamingStatus();
EngineStreamingHealth health = EngineRuntime.GetStreamingHealth();
EngineRuntime.StopStreaming();
```

`StartStreaming` returns once OBS accepts the request. Poll the status until it
becomes `Live` or `Failed`. A failure snapshot retains a distinct reason and a
sanitized diagnostic. `GetStreamingHealth` is valid only while the session is
connecting, live, reconnecting, or stopping.

The destination can be configured before the video, audio, encoders, and scene,
but only after the engine has initialized and loaded its modules. Reconfiguration
is rejected during a session.

## Simultaneous recording

Streaming and [recording](recording.md) can run at the same time, each
independently started and stopped. Whichever one starts first uses (and, for
recording, auto-configures) the primary video/audio encoders described above;
whichever one starts *second*, while the other is already active, instead
gets its own isolated pair, auto-configured with the same forced-software
defaults recording uses. This isolation exists because binding the same live
`obs_encoder_t` to two started outputs is unsafe to tear down independently -
even with a confirmed, successful OBS "stop" signal, `obs_output_active()`
can keep reporting the output as active indefinitely while another encoded
output remains bound to the shared video pipeline. `castor_engine_stop_streaming`
therefore trusts the "stop" signal itself rather than re-polling
`obs_output_active()`, which was found to be unreliable in exactly this
scenario while validating simultaneous recording and streaming under load.

The isolated pair is released back as soon as the output that claimed it
stops; it does not require any extra managed API to configure, and it never
appears in `GetVideoEncoderHandle`/`GetAudioEncoderHandle` or the other
encoder-configuration accessors, which only ever reflect the primary pair.

## Render statistics

`EngineRuntime.GetRenderStats()` returns engine-wide render/encode pipeline
counters (`TotalFrames`, `LaggedFrames`, `LaggedFrameRatio`), independent of
any single output - unlike `GetStreamingHealth`, it is available whenever the
engine is initialized, whether or not streaming or recording is active. It
mirrors OBS's own `obs_get_total_frames`/`obs_get_lagged_frames`, the same
render-loop figures OBS Studio's stats window shows. Use it alongside
`GetStreamingHealth` to distinguish local render-loop lag (frames the render
loop couldn't produce on schedule) from RTMP delivery loss (frames dropped
over the network).

## Local ingest endpoint

[MediaMTX](https://mediamtx.org/docs/kickoff/install) provides a local RTMP
listener without requiring a public streaming account:

```powershell
docker run --rm -it -p 1935:1935 bluenviron/mediamtx:1
```

Without Docker, `ffmpeg -listen 1 -i rtmp://127.0.0.1:1935/live/<key> -c copy -f mp4 out.mp4`
also receives a single RTMP publish and writes it to a file - useful for a
one-off manual validation run, but it accepts exactly one connection and then
exits, so it cannot serve `StreamingIntegrationTests`'s own reachability probe
(a bare TCP connect/disconnect) followed by the real publish; that test still
needs MediaMTX or an equivalent always-listening endpoint.

Set the integration-test variables in the same shell, without writing the key
to a tracked file:

```powershell
$env:CASTOR_RTMP_TEST_SERVER = "rtmp://127.0.0.1:1935/live"
$env:CASTOR_RTMP_TEST_KEY = "castor-test"
dotnet test Castor.Engine.Tests/Castor.Engine.Tests.csproj --configuration Release
```

The test skips with an explicit reason when either variable is absent or port
1935 is unreachable. While the test runs, the resulting stream is readable at
`rtmp://127.0.0.1:1935/live/castor-test`, for example with:

```powershell
ffplay rtmp://127.0.0.1:1935/live/castor-test
```

## Failure and reconnection behavior

- Initial connection failure, rejected stream, disconnection, exhausted
  reconnection attempts, unsupported format, and encoder errors have distinct
  failure values.
- The default is 20 reconnect attempts with an initial two-second delay.
- A dropped uplink moves the state to `Reconnecting`; successful recovery moves
  it back to `Live`.
- Shutdown stops and releases the output and service before releasing either
  shared encoder.
- Stream keys and authentication passwords are filtered from native diagnostics
  and must never be printed by callers.

Before the stabilization milestone, repeat the same flow against one real
platform ingest endpoint and verify playback, timestamps, audio/video metadata,
reconnection, and rejected-key diagnostics. Do not add that credential to a
test fixture, command transcript, issue, or commit.
