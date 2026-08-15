namespace Castor.Engine
{
    /// <summary>
    /// Describes the audio speaker layout used by Castor Engine.
    /// </summary>
    public enum EngineSpeakerLayout
    {
        /// <summary>
        /// Resolves to <see cref="Stereo"/>.
        /// </summary>
        Default = 0,

        /// <summary>
        /// A single-channel layout.
        /// </summary>
        Mono = 1,

        /// <summary>
        /// A two-channel layout.
        /// </summary>
        Stereo = 2,
    }
}
