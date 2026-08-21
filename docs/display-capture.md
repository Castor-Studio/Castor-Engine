# Display Capture

Castor Engine ABI version 10 can enumerate the displays exposed by OBS's
packaged `monitor_capture` source and replace the default scene's solid-color
source with one selected display. Castor owns the scene item and source for the
entire lifecycle; no OBS pointer or Windows display handle crosses the public
API.

## Managed API

Display enumeration is available after runtime initialization, because the
list comes from the properties registered by the loaded `win-capture` module:

```csharp
EngineRuntime.Initialize(
    new EngineRuntimeConfiguration(AppContext.BaseDirectory));

var displays = EngineRuntime.EnumerateDisplays();
```

An empty list is a valid result for a headless or non-interactive session. Each
`EngineDisplayInfo` contains an opaque identifier, the human-readable label
provided by OBS, and whether Windows identifies it as the primary display.

After configuring video and creating the main scene, the caller can select one
of those identifiers:

```csharp
EngineRuntime.ConfigureVideo(
    new EngineVideoConfiguration(1280, 720, 1280, 720, 30, 1));
EngineRuntime.CreateMainScene();

var selected = displays.First(display => display.IsPrimary);
EngineRuntime.ConfigureDisplayCapture(
    new EngineDisplayCaptureConfiguration(
        selected.Id,
        captureCursor: true));
```

The UI that presents this list and persists the user's choice belongs to the
frontend. The engine only provides enumeration, validation, configuration, and
lifecycle ownership.

## OBS integration

Castor does not maintain a second display enumeration. It asks libobs for the
properties of the latest registered `monitor_capture` source and copies the
entries exposed by `win-capture`.

OBS can expose one of two compatible contracts:

- modern D3D11 capture uses the string property `monitor_id`;
- legacy capture uses the integer property `monitor`.

Castor adapts both to one opaque public identifier and remembers the selector
needed to create the OBS source. The source is configured with that selector
plus `capture_cursor`; modern capture also receives OBS's automatic method and
`force_sdr = false` defaults.

Windows metadata is used only to attach a stable device identity and the primary
flag to entries already enumerated by OBS. This is required for the legacy OBS
integer selector, whose index can move after a topology change, and because OBS
embeds the primary status in a localized label rather than structured metadata.
OBS remains authoritative for which displays are capturable, their labels, and
the selector passed back to `monitor_capture`.

## Lifecycle

Display capture configuration requires, in order:

1. an initialized runtime with loaded OBS modules;
2. a configured video subsystem;
3. an active main scene;
4. a display identifier present in a fresh OBS enumeration.

The first successful configuration creates and adds the display source before
removing the solid-color source. Later replacements use the same transactional
sequence. If creation or insertion fails, the previous visual source remains
active.

Repeating the same display and cursor configuration is a no-op. A different
display or cursor preference is rejected while recording; dynamic source
replacement during recording is intentionally outside this feature.

If a selected display disconnects after configuration, the source remains
attached. OBS may render no content while its monitor is unavailable and will
retry its own lookup. A future configuration call re-enumerates displays and
rejects identifiers that are no longer available.

Shutdown stops any recording, removes and releases the visual source and scene,
waits for OBS deferred destruction, and then stops OBS. The full lifecycle can
be repeated in the same process.

## Native API

The C ABI exposes:

- `castor_engine_get_display_count`;
- `castor_engine_get_display_at`;
- `castor_engine_validate_display_capture_config`;
- `castor_engine_configure_display_capture`;
- `castor_engine_is_display_capture_active`.

`castor_engine_get_display_count` refreshes the enumeration snapshot. A zero
count with an empty last-error message means a valid headless environment; a
non-empty error reports an invalid lifecycle state or unavailable OBS source.

## Scope

This feature intentionally does not implement window capture, simultaneous
multi-display capture, scaling, cropping, preview rendering, hardware encoder
selection, or frontend display-selection controls.
