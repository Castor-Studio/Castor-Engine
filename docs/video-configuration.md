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

## Default main scene

After video configuration, the engine can create its default scene and connect
it to the primary OBS video output:

```csharp
EngineRuntime.CreateMainScene();

if (!EngineRuntime.HasActiveScene)
{
    throw new InvalidOperationException("The main scene is not active.");
}
```

The scene and its source remain native implementation details. The public API
does not expose `obs_scene_t`, `obs_source_t`, or any OBS-owned handle. The
engine resolves the latest `color_source` implementation supplied by the
packaged `image-source` module, creates an opaque black source at the configured
base resolution, adds it to the scene, and verifies that the scene is connected
to output channel zero.

`CreateMainScene` is idempotent while that scene remains active. Calling it
before runtime initialization or video configuration produces a diagnostic
`InvalidOperationException`. Failures during scene, source, item, or output
creation are rolled back before the diagnostic is returned.

Shutdown first disconnects the scene, then releases the retained source and
scene references before stopping OBS. The complete initialize, configure,
create, and shutdown lifecycle can therefore be repeated in the same process.
