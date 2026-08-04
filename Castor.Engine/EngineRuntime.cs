using System.Runtime.InteropServices;
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
        /// Gets the number of OBS modules loaded by Castor Engine.
        /// </summary>
        public static uint LoadedModuleCount =>
            NativeMethods.GetLoadedModuleCount();

        /// <summary>
        /// Initializes the OBS runtime and loads the packaged OBS modules.
        /// </summary>
        /// <param name="configuration">
        /// The packaged runtime location and OBS locale.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configuration"/> is null.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the OBS runtime or its modules cannot be initialized.
        /// </exception>
        public static void Initialize(EngineRuntimeConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            EngineInfo.ValidateCompatibility();

            var runtimeRoot = Path.GetFullPath(configuration.RuntimeRoot);
            var locale = string.IsNullOrWhiteSpace(configuration.Locale)
                ? "en-US"
                : configuration.Locale;

            nint runtimeRootPointer = nint.Zero;
            nint localePointer = nint.Zero;

            try
            {
                runtimeRootPointer = Marshal.StringToCoTaskMemUTF8(runtimeRoot);
                localePointer = Marshal.StringToCoTaskMemUTF8(locale);

                var nativeConfiguration = new NativeEngineConfiguration
                {
                    StructSize = checked(
                        (uint)Marshal.SizeOf<NativeEngineConfiguration>()),
                    RuntimeRoot = runtimeRootPointer,
                    Locale = localePointer,
                };

                var result = NativeMethods.Initialize(in nativeConfiguration);

                if (result != NativeEngineResult.Ok)
                {
                    throw CreateInitializationException(result);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(localePointer);
                Marshal.FreeCoTaskMem(runtimeRootPointer);
            }
        }

        /// <summary>
        /// Gets whether an OBS module with the specified name is loaded.
        /// </summary>
        /// <param name="moduleName">The OBS module name.</param>
        /// <returns>
        /// <see langword="true"/> when the module is loaded; otherwise,
        /// <see langword="false"/>.
        /// </returns>
        public static bool IsModuleLoaded(string moduleName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
            return NativeMethods.IsModuleLoaded(moduleName) != 0;
        }

        /// <summary>
        /// Shuts down the OBS runtime.
        /// </summary>
        public static void Shutdown()
        {
            NativeMethods.Shutdown();
        }

        private static InvalidOperationException CreateInitializationException(
            NativeEngineResult result)
        {
            var errorPointer = NativeMethods.GetLastError();
            var nativeMessage = errorPointer == nint.Zero
                ? null
                : Marshal.PtrToStringUTF8(errorPointer);

            var detail = string.IsNullOrWhiteSpace(nativeMessage)
                ? "The native engine did not provide additional diagnostics."
                : nativeMessage;

            return new InvalidOperationException(
                $"Failed to initialize the OBS runtime ({result}): {detail}");
        }
    }
}
