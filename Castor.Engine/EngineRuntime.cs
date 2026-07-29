using Castor.Engine.Interop;

namespace Castor.Engine
{
    /// <summary>
    /// Manages the lifecycle of the OBS runtime.
    /// </summary>
    public static class EngineRuntime
    {
        /// <summary>
        /// Gets whether the OBS runtime is initialized.
        /// </summary>
        public static bool IsInitialized =>
            NativeMethods.IsInitialized() != 0;

        /// <summary>
        /// Initializes the OBS runtime.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the OBS runtime cannot be initialized.
        /// </exception>
        public static void Initialize()
        {
            if (NativeMethods.Initialize() == 0)
            {
                throw new InvalidOperationException(
                    "Failed to initialize the OBS runtime.");
            }
        }

        /// <summary>
        /// Shuts down the OBS runtime.
        /// </summary>
        public static void Shutdown()
        {
            NativeMethods.Shutdown();
        }
    }
}
