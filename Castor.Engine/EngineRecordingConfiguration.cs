namespace Castor.Engine
{
    /// <summary>
    /// Describes where Castor Engine should write an MKV recording. If
    /// <see cref="DestinationPath"/> already exists, it is overwritten -
    /// Castor Engine does not perform its own existence check or provide a
    /// no-overwrite mode.
    /// </summary>
    public sealed class EngineRecordingConfiguration
    {
        /// <summary>
        /// Initializes a new recording configuration.
        /// </summary>
        /// <param name="destinationPath">
        /// The UTF-8 destination path for the MKV file.
        /// </param>
        public EngineRecordingConfiguration(string destinationPath)
        {
            DestinationPath = destinationPath;
        }

        /// <summary>
        /// Gets the destination path for the MKV file.
        /// </summary>
        public string DestinationPath { get; }
    }
}
