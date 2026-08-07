namespace Castor.Engine
{
    /// <summary>
    /// Configures the native OBS runtime used by Castor Engine.
    /// </summary>
    public sealed class EngineRuntimeConfiguration
    {
        /// <summary>
        /// Initializes a new engine runtime configuration.
        /// </summary>
        /// <param name="runtimeRoot">
        /// The root directory of the packaged Castor runtime.
        /// </param>
        public EngineRuntimeConfiguration(string runtimeRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRoot);
            RuntimeRoot = runtimeRoot;
        }

        /// <summary>
        /// Gets the root directory of the packaged Castor runtime.
        /// </summary>
        public string RuntimeRoot { get; }

        /// <summary>
        /// Gets or initializes the locale used by OBS modules.
        /// </summary>
        public string Locale { get; init; } = "en-US";
    }
}
