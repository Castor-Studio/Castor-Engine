namespace Castor.Engine
{
    /// <summary>
    /// Describes how Castor Engine selects a video encoder when no explicit
    /// encoder identifier is requested.
    /// </summary>
    public enum EngineVideoEncoderSelectionMode
    {
        /// <summary>
        /// Prefers an available hardware encoder, falling back to the
        /// software encoder when none is available.
        /// </summary>
        Automatic = 0,

        /// <summary>
        /// Prefers an available hardware encoder, falling back to the
        /// software encoder when none is available. Identical to
        /// <see cref="Automatic"/>; the distinct value exists to make the
        /// caller's intent explicit.
        /// </summary>
        HardwarePreferred = 1,

        /// <summary>
        /// Always uses the software encoder, even when a hardware encoder
        /// is available.
        /// </summary>
        SoftwareForced = 2,
    }
}
