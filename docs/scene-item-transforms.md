# Scene Item Transforms

Castor Engine exposes the stored transform of the single visual item owned by
each named scene. A caller can read or atomically replace the complete exposed
snapshot while the scene is active or in the background, including during
recording or streaming. The source and scene item are never recreated by a
transform update.

This feature is available starting with Castor Engine ABI version 13.

## Managed API

Configure a visual source before accessing its transform. An empty scene has no
scene item and reports `SceneItemNotFound`.

```csharp
EngineRuntime.CreateScene("closeup");
EngineRuntime.ConfigureDisplayCapture(
    new EngineDisplayCaptureConfiguration(
        "closeup",
        selectedDisplay.Id));

var transform = EngineRuntime.GetSceneItemTransform("closeup");
transform.PositionX = 320.0F;
transform.PositionY = 180.0F;
transform.ScaleX = 1.25F;
transform.ScaleY = 1.25F;
transform.RotationDegrees = 5.0F;
transform.BoundsMode = EngineSceneItemBoundsMode.ScaleOuter;
transform.BoundsWidth = 640.0F;
transform.BoundsHeight = 360.0F;
transform.CropLeft = 40;
transform.CropRight = 40;

EngineRuntime.SetSceneItemTransform("closeup", transform);
```

`SetSceneItemTransform` treats the object as a complete snapshot: every
exposed property is written on each call. The mutable object can be reused for
continuous smart-framing updates without allocating a new configuration for
every frame.

## Coordinate system and units

| Property | Unit and direction |
| --- | --- |
| `PositionX` / `PositionY` | Pixels in the configured base canvas. `(0, 0)` is the top-left corner; X increases rightward and Y downward. |
| `ScaleX` / `ScaleY` | Unitless multipliers relative to the source size after explicit crop. `1` preserves source size, negative values flip an axis, and `0` collapses that axis. |
| `RotationDegrees` | Degrees around the scene item's top-left alignment point. Positive values rotate clockwise in the canvas coordinate system. Values are stored without normalization. |
| `BoundsWidth` / `BoundsHeight` | Pixels in the base canvas. Both must be positive when bounds are enabled. |
| `CropLeft` / `CropTop` / `CropRight` / `CropBottom` | Non-negative pixels removed from the unscaled source edges before scale and bounds are applied. |

Castor-created scene items retain libobs's top-left item alignment and centered
bounds alignment. Those alignments and libobs's `crop_to_bounds` flag are not
part of this API and are preserved by every read-modify-write operation.

The getter returns the values stored by libobs, not derived dimensions such as
the final rendered width or the draw matrix. Castor creates scenes in libobs's
absolute-coordinate mode, making a written snapshot and its readback an exact
round trip for valid single-precision values.

## Bounds modes

- `None` disables the bounding box; explicit scale controls the rendered size.
- `Stretch` fills the bounds without preserving aspect ratio.
- `ScaleInner` fits the entire source inside the bounds.
- `ScaleOuter` fills the bounds while preserving aspect ratio.
- `ScaleToWidth` and `ScaleToHeight` fit the corresponding dimension.
- `MaxOnly` preserves aspect ratio and prevents the source from exceeding the
  bounds.

Bounds dimensions may be zero only in `None` mode. They are still stored when
bounds are disabled, so non-zero disabled dimensions also round-trip.

## Native API

The C ABI owns the enum and structure; no OBS handle or enum crosses the public
boundary:

- `castor_engine_get_scene_item_transform(scene_name, out_transform)`;
- `castor_engine_set_scene_item_transform(scene_name, transform)`;
- `castor_engine_scene_item_bounds_mode_t`;
- `castor_engine_scene_item_transform_t`.

The caller initializes `struct_size` before either operation. The setter rejects
non-finite floating-point values, unknown bounds modes, negative bounds,
non-positive enabled bounds, and crop values above `INT32_MAX`. Scale may be
zero or negative, and rotation may use any finite degree value.

The native lifecycle mutex serializes reads and writes. The OBS backend updates
the existing `obs_sceneitem_t` between `obs_sceneitem_defer_update_begin` and
`obs_sceneitem_defer_update_end`, so one complete snapshot produces one live
transform update without resource churn.

## Failure handling

| Native result | Managed result name | Meaning |
| --- | --- | --- |
| `CASTOR_ENGINE_NOT_INITIALIZED` | `NotInitialized` | Transform access was requested before engine initialization. |
| `CASTOR_ENGINE_SCENE_INVALID_NAME` | `SceneInvalidName` | The supplied scene name was null or empty. |
| `CASTOR_ENGINE_SCENE_NOT_FOUND` | `SceneNotFound` | The named scene does not exist. |
| `CASTOR_ENGINE_SCENE_ITEM_NOT_FOUND` | `SceneItemNotFound` | The scene exists but has no configured visual source. |
| `CASTOR_ENGINE_SCENE_ITEM_INVALID_TRANSFORM` | `SceneItemInvalidTransform` | A pointer, structure size, enum, dimension, crop, or floating-point value is invalid. |

Replacing a display capture creates a new scene item with OBS defaults. Apply
the desired snapshot again after replacement; preserving a transform across
source replacement is intentionally outside this API.

## Automated coverage

Native tests cover target resolution, every bounds mode, validation,
independence between scenes, rename behavior, exact round trips, and the absence
of item recreation. Managed tests cover the ABI and perform a real libobs round
trip when an interactive display is available.

```powershell
ctest --test-dir build_x64 --build-config RelWithDebInfo --output-on-failure
dotnet test Castor.Engine.Tests/Castor.Engine.Tests.csproj --configuration Release
```
