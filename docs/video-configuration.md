# OBS Video Configuration

Castor Engine configures the OBS video subsystem only after the packaged OBS
runtime has been initialized and its modules have been loaded.

The managed API accepts Castor-owned values and does not expose OBS pointers,
structures, formats, or enums:

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
```

On Windows, the native host registers `data/libobs` from the packaged runtime
and uses the relative graphics-module name `libobs-d3d11`. It applies
deterministic internal defaults: NV12 output, Rec. 709, partial range, bicubic
scaling, adapter zero, and GPU color conversion.

All dimensions must be even and between 2 and 16384 pixels. OBS aligns the
effective output width down to a multiple of four, so a requested width of 854
remains unchanged in the caller-owned configuration while the video runtime
uses 852. Configurations with the same effective width are treated as
equivalent. The FPS numerator and denominator must both be non-zero. Repeating
the same effective configuration is a no-op, while OBS failures are returned
through Castor result codes and surfaced as descriptive managed exceptions.

Calling `EngineRuntime.Shutdown()` clears the configured-video state along
with the rest of the OBS runtime.

## Scenes

After video configuration, the engine can create named scenes and switch the
active one, connecting it to the primary OBS video output:

```csharp
EngineRuntime.CreateScene("wide");
EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));

if (!EngineRuntime.HasActiveScene)
{
    throw new InvalidOperationException("No scene is active.");
}
```

Scenes and their sources remain native implementation details. The public API
does not expose `obs_scene_t`, `obs_source_t`, or any OBS-owned handle.

See [Scene Management](scene-management.md) for the public contract, native
resource lifecycle, diagnostics, and automated coverage.
