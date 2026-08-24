namespace Castor.Engine
{
    /// <summary>
    /// Selects one display for the main scene and controls cursor capture.
    /// </summary>
    public sealed class EngineDisplayCaptureConfiguration
    {
        /// <summary>Initializes a display capture request.</summary>
        public EngineDisplayCaptureConfiguration(string displayId, bool captureCursor = true)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayId);
            DisplayId = displayId;
            CaptureCursor = captureCursor;
        }

        /// <summary>Gets the opaque identifier returned by display enumeration.</summary>
        public string DisplayId { get; }

        /// <summary>Gets whether the mouse cursor is included in the capture.</summary>
        public bool CaptureCursor { get; }
    }
}
