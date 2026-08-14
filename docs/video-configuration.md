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

All dimensions must be even and between 2 and 16384 pixels. The FPS numerator
and denominator must both be non-zero. Repeating the same configuration is a
no-op, while OBS failures are returned through Castor result codes and surfaced
as descriptive managed exceptions.

Calling `EngineRuntime.Shutdown()` clears the configured-video state along
with the rest of the OBS runtime.
