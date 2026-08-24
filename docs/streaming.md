# RTMP streaming

Castor Engine can send the active main scene to one custom RTMP or RTMPS
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
EngineRuntime.CreateMainScene();
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
is rejected during a session. Recording and streaming are mutually exclusive in
this milestone.

## Local ingest endpoint

[MediaMTX](https://mediamtx.org/docs/kickoff/install) provides a local RTMP
listener without requiring a public streaming account:

```powershell
docker run --rm -it -p 1935:1935 bluenviron/mediamtx:1
```

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
