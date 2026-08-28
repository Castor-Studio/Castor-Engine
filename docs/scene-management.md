# Scene Management

Castor Engine owns a dynamic registry of independently named OBS scenes
instead of a single default scene. A live operator can create, list, rename,
and delete scenes at any time, and switch the active scene with a single
call that applies a configurable transition instead of a hard cut. This
supports driving match coverage - wide view, close-up, halftime card -
entirely programmatically.

This feature is available starting with Castor Engine ABI version 12, which
replaced the earlier single-scene API (`castor_engine_create_main_scene`).

There is no dedicated "main" scene: the registry starts empty, and any
created scene can be the first one switched to. Scene creation has no fixed
count limit, bounded only by memory and OBS itself.

## Managed API

```csharp
var runtimeConfiguration = new EngineRuntimeConfiguration(AppContext.BaseDirectory);
var videoConfiguration = new EngineVideoConfiguration(
    baseWidth: 1280, baseHeight: 720,
    outputWidth: 1280, outputHeight: 720,
    framesPerSecondNumerator: 30, framesPerSecondDenominator: 1);

EngineRuntime.Initialize(runtimeConfiguration);
EngineRuntime.ConfigureVideo(videoConfiguration);

EngineRuntime.CreateScene("wide");
EngineRuntime.CreateScene("closeup");
EngineRuntime.CreateScene("halftime");

// The first switch after startup applies instantly - there is nothing to
// transition away from yet.
EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));

// Later switches animate over the requested duration.
EngineRuntime.SwitchScene(
    "closeup", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Fade, durationMilliseconds: 300));

Console.WriteLine(EngineRuntime.ActiveSceneName); // "closeup"
```

`EngineRuntime.CreateScene` only creates an empty scene container; it does
not activate it. Attach a visual source to it with
[`ConfigureDisplayCapture`](display-capture.md) (before or after creation,
active or not), then bring it on air with `SwitchScene`.

Once a visual source is configured, its position, scale, rotation, bounds, and
crop can be read or updated through the
[`Scene Item Transforms`](scene-item-transforms.md) API without activating the
scene or replacing its source.

`EngineRuntime.SwitchScene` is synchronous: for any transition type other
than `Cut`, the call blocks until OBS finishes animating it. Switching to the
scene that is already active is a no-op. Switching to an unknown scene name
throws with `SceneNotFound` and leaves the active scene unchanged.

`EngineRuntime.DeleteScene` rejects the currently active scene with
`SceneDeleteActiveScene` - switch away first. `EngineRuntime.RenameScene`
keeps `ActiveSceneName` in sync when the renamed scene is the active one.

## Public contract

- `castor_engine_create_scene(scene_name)`
- `castor_engine_delete_scene(scene_name)`
- `castor_engine_rename_scene(old_name, new_name)`
- `castor_engine_get_scene_count()`
- `castor_engine_get_scene_name_at(index, out_name, out_name_size)`
- `castor_engine_get_active_scene_name(out_name, out_name_size)`
- `castor_engine_switch_scene(scene_name, transition)`
- `castor_engine_has_active_scene()`

The managed wrapper exposes the same contract through `EngineRuntime`'s
`CreateScene`, `DeleteScene`, `RenameScene`, `GetSceneNames`,
`ActiveSceneName`, `SwitchScene`, and `HasActiveScene`. Neither API exposes
`obs_scene_t`, `obs_source_t`, or another OBS-owned handle.

`castor_engine_scene_transition_config_t` carries a
`castor_engine_scene_transition_type_t` (`Cut`, `Fade`, `Slide`, or `Swipe`)
and a `duration_ms`, ignored for `Cut`.

Transform operations are documented separately because they target the single
visual item owned by each scene rather than the scene container itself.

## Native implementation

The implementation is split into three layers, following the same shape as
every other OBS-backed subsystem in this engine:

| Component | Responsibility |
| --- | --- |
| `scene_registry_subsystem` | Owns a dynamic, unbounded collection of named scenes, the active scene name, and the cached transition object; defines idempotence and rejects unsafe operations. |
| `scene_backend` | Abstracts the OBS scene and transition operations so lifecycle and switching logic can be tested without a running OBS instance. |
| `obs_scene_backend` | Creates the real OBS scenes and transition sources and drives the primary output channel. |

### Switching mechanics

Output channel zero always holds either a scene's source directly, or a
transition source wrapping the handoff between scenes:

- The **first switch** after startup, and every switch requesting
  `Cut`, binds the target scene's source to the output channel directly -
  there is no transition object involved, so correctness never depends on
  a transition's own completion signal.
- A **Fade, Slide, or Swipe** switch uses a real `obs_transition_t`. The
  transition object for the requested type is created once and cached;
  later switches reuse it as long as the requested type is unchanged and it
  is still attached to the output.
  - Whenever a (re)attach is needed - the first transition switch, the
    switch right after a `Cut`, or a change of transition type - the new
    or cached transition is seeded with whatever is currently on the
    output channel (`obs_transition_set`) and attached
    (`obs_set_output_source`). A transition switching every prior switch
    always fully completes before the next one starts, so there is never
    an in-flight animation to preserve, which is also why this engine does
    not use `obs_transition_swap_begin`/`_end`: that pair snaps the
    transition straight to its resting state instead of leaving it ready
    to animate, so a subsequent `obs_transition_start` on it is rejected.
  - `obs_transition_start` then animates to the target scene, and the call
    blocks by polling `obs_transition_get_time` until it reaches `1.0`
    (fully complete) - transitions have no public completion signal (the
    `transition_stop` field in `obs_source_info` is a plugin-private
    callback, never proxied to the source's signal handler) and
    `obs_transition_is_active` never clears once a transition has run.

## Failure handling and diagnostics

| Native result | Managed result name | Meaning |
| --- | --- | --- |
| `CASTOR_ENGINE_SCENE_INVALID_NAME` | `SceneInvalidName` | A scene name was null, empty, or whitespace. |
| `CASTOR_ENGINE_SCENE_ALREADY_EXISTS` | `SceneAlreadyExists` | `CreateScene` or `RenameScene` collided with an existing scene name. |
| `CASTOR_ENGINE_SCENE_NOT_FOUND` | `SceneNotFound` | The named scene does not exist (delete, rename, switch, or configure display capture). |
| `CASTOR_ENGINE_SCENE_DELETE_ACTIVE_SCENE` | `SceneDeleteActiveScene` | `DeleteScene` targeted the currently active scene. |
| `CASTOR_ENGINE_SCENE_TRANSITION_UNAVAILABLE` | `SceneTransitionUnavailable` | The requested transition type is not provided by the loaded OBS modules. |
| `CASTOR_ENGINE_SCENE_TRANSITION_CREATION_FAILED` | `SceneTransitionCreationFailed` | OBS failed to create the transition source. |
| `CASTOR_ENGINE_SCENE_TRANSITION_START_FAILED` | `SceneTransitionStartFailed` | OBS failed to start or complete the transition; the active scene is left unchanged. |

## Automated coverage

Native lifecycle tests use a fake backend (no OBS required) to cover scene
creation, listing, deletion, renaming, the direct-bind vs. seed switching
paths for every transition type, injected transition failures, and full
teardown/restart. Managed integration tests exercise the real packaged
OBS runtime: creating several scenes, switching between them with every
transition type, and querying the active scene.

```powershell
ctest --test-dir build_x64 --build-config RelWithDebInfo --output-on-failure
dotnet test Castor.Engine.Tests/Castor.Engine.Tests.csproj --configuration Release
```
