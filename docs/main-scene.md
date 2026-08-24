# Default OBS Main Scene

Castor Engine owns a single default OBS scene that provides deterministic video
content before a capture source is selected. The scene initially contains one
opaque-black color source and is connected to the primary OBS video output. A
successful display-capture configuration replaces that color source while
keeping the scene and output connection alive.

This feature is available starting with Castor Engine ABI version 4.

## Managed API

Initialize the runtime and configure video before creating the scene:

```csharp
var runtimeConfiguration = new EngineRuntimeConfiguration(
    AppContext.BaseDirectory);
var videoConfiguration = new EngineVideoConfiguration(
    baseWidth: 1280,
    baseHeight: 720,
    outputWidth: 1280,
    outputHeight: 720,
    framesPerSecondNumerator: 30,
    framesPerSecondDenominator: 1);

try
{
    EngineRuntime.Initialize(runtimeConfiguration);
    EngineRuntime.ConfigureVideo(videoConfiguration);
    EngineRuntime.CreateMainScene();

    if (!EngineRuntime.HasActiveScene)
    {
        throw new InvalidOperationException("The main scene is not active.");
    }
}
finally
{
    EngineRuntime.Shutdown();
}
```

`EngineRuntime.CreateMainScene()` is idempotent while the engine-owned scene is
active. Repeated calls reuse the existing scene, current visual source, scene
item, and output connection.

`EngineRuntime.HasActiveScene` is `true` only when all owned resources exist and
the scene source is still connected to OBS output channel zero. It returns
`false` before creation and after shutdown.

## Public contract

The public C ABI exposes only these scene operations:

- `castor_engine_create_main_scene()` creates and activates the default scene;
- `castor_engine_has_active_scene()` reports whether that scene is active.

The managed wrapper exposes the same contract through `CreateMainScene()` and
`HasActiveScene`. Neither API exposes `obs_scene_t`, `obs_source_t`,
`obs_sceneitem_t`, or another OBS-owned handle.

## Native implementation

The implementation is split into three layers:

| Component | Responsibility |
| --- | --- |
| `main_scene_subsystem` | Owns the scene and its replaceable visual source, defines idempotence, and rolls back partial creation. |
| `scene_backend` | Abstracts the OBS operations so lifecycle failures can be tested without a running OBS instance. |
| `obs_scene_backend` | Creates the real OBS resources, connects output channel zero, and synchronizes deferred destruction. |

The creation sequence is:

1. Confirm that OBS, its modules, and the video subsystem are initialized.
2. Create the `Castor Main Scene` scene.
3. Resolve the latest input type whose unversioned ID is `color_source`.
4. Create `Castor Main Color Source` at the configured base resolution, using
   opaque black (`0xFF000000`).
5. Add the source to the scene and retain the returned scene item.
6. Connect the scene source to OBS output channel zero.
7. Read the output source back and verify that the expected scene is connected.

The source implementation comes from the packaged OBS `image-source` module.
Castor does not fall back to a built-in or synthetic source when that module is
missing, because module availability is part of the runtime contract.

## Failure handling and diagnostics

Creation failures use the existing engine diagnostic channel. The native result
is returned by `castor_engine_create_main_scene()`, and the descriptive message
is available through `castor_engine_get_last_error()`. The managed API combines
both values in an `InvalidOperationException`.

| Native result | Managed result name | Meaning |
| --- | --- | --- |
| `CASTOR_ENGINE_NOT_INITIALIZED` | `NotInitialized` | OBS or its packaged modules are not initialized. |
| `CASTOR_ENGINE_VIDEO_NOT_CONFIGURED` | `VideoNotConfigured` | Video configuration has not completed. |
| `CASTOR_ENGINE_SCENE_CREATION_FAILED` | `SceneCreationFailed` | OBS could not create the scene. |
| `CASTOR_ENGINE_SCENE_SOURCE_UNAVAILABLE` | `SceneSourceUnavailable` | No loaded module provides `color_source`. |
| `CASTOR_ENGINE_SCENE_SOURCE_CREATION_FAILED` | `SceneSourceCreationFailed` | OBS could not create the color source. |
| `CASTOR_ENGINE_SCENE_SOURCE_ADD_FAILED` | `SceneSourceAddFailed` | OBS could not add the source to the scene. |
| `CASTOR_ENGINE_SCENE_ACTIVATION_FAILED` | `SceneActivationFailed` | The scene could not be verified on output channel zero. |

Every failure after scene creation runs the same rollback path as shutdown. A
failed operation therefore leaves `HasActiveScene` false and does not retain a
partially created scene or source.

## Shutdown and restart

Shutdown performs resource cleanup before stopping OBS:

1. Disconnect the scene if it still owns output channel zero.
2. Wait for queued graphics work to cross a synchronization barrier.
3. Remove the scene item.
4. Release the retained color or display-capture source reference.
5. Release the retained scene reference.
6. Wait for the OBS destroy queue to process the released resources.
7. Reset video and shut down OBS.

The explicit barriers prevent source destruction from racing with graphics or
OBS shutdown. After cleanup, the complete `initialize -> configure video ->
create scene -> shutdown` sequence can run again in the same process.

## Automated coverage

Native lifecycle tests inject failures at scene creation, source discovery,
source creation, scene insertion, and output activation. They also verify
cleanup ordering and idempotence. Managed integration tests exercise the real
packaged OBS runtime, including repeated creation, shutdown, and restart.

After building and installing the native runtime, run both suites with:

```powershell
ctest --test-dir build_x64 --build-config RelWithDebInfo --output-on-failure
dotnet test Castor.Engine.Tests/Castor.Engine.Tests.csproj --configuration Release
```

The Windows CI workflows run the native tests before installing the runtime and
then execute the managed integration suite.
