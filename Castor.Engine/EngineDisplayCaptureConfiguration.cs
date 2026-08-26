namespace Castor.Engine
{
    /// <summary>
    /// Selects one display for a named scene and controls cursor capture.
    /// </summary>
    public sealed class EngineDisplayCaptureConfiguration
    {
        /// <summary>Initializes a display capture request.</summary>
        public EngineDisplayCaptureConfiguration(string sceneName, string displayId, bool captureCursor = true)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
            ArgumentException.ThrowIfNullOrWhiteSpace(displayId);
            SceneName = sceneName;
            DisplayId = displayId;
            CaptureCursor = captureCursor;
        }

        /// <summary>Gets the name of the scene the capture is applied to.</summary>
        public string SceneName { get; }

        /// <summary>Gets the opaque identifier returned by display enumeration.</summary>
        public string DisplayId { get; }

        /// <summary>Gets whether the mouse cursor is included in the capture.</summary>
        public bool CaptureCursor { get; }
    }
}
