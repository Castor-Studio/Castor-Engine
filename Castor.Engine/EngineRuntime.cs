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
        /// Gets whether the OBS video subsystem is configured.
        /// </summary>
        public static bool IsVideoConfigured =>
            NativeMethods.IsVideoConfigured() != 0;

        /// <summary>
        /// Gets whether the engine-owned main scene exists and is connected
        /// to the primary OBS video output.
        /// </summary>
        public static bool HasActiveScene =>
            NativeMethods.HasActiveScene() != 0;

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
                    throw CreateNativeOperationException(
                        "initialize the OBS runtime",
                        result);
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
        /// Configures the OBS video subsystem for rendering and encoding.
        /// </summary>
        /// <param name="configuration">
        /// The base resolution, output resolution, and frame rate.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configuration"/> is null.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine is not initialized or OBS cannot configure
        /// video with the requested settings.
        /// </exception>
        public static void ConfigureVideo(
            EngineVideoConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            EngineInfo.ValidateCompatibility();

            var nativeConfiguration = new NativeEngineVideoConfiguration
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineVideoConfiguration>()),
                BaseWidth = configuration.BaseWidth,
                BaseHeight = configuration.BaseHeight,
                OutputWidth = configuration.OutputWidth,
                OutputHeight = configuration.OutputHeight,
                FramesPerSecondNumerator =
                    configuration.FramesPerSecondNumerator,
                FramesPerSecondDenominator =
                    configuration.FramesPerSecondDenominator,
            };

            var result = NativeMethods.ConfigureVideo(in nativeConfiguration);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    "configure OBS video",
                    result);
            }
        }

        /// <summary>
        /// Creates the engine-owned main scene, adds a solid-color source,
        /// and connects it to the primary OBS video output.
        /// </summary>
        /// <remarks>
        /// Repeated calls are idempotent while the main scene remains active.
        /// </remarks>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine or video subsystem is not initialized, or
        /// when OBS cannot create and activate the main scene.
        /// </exception>
        public static void CreateMainScene()
        {
            EngineInfo.ValidateCompatibility();

            var result = NativeMethods.CreateMainScene();

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    "create and activate the main OBS scene",
                    result);
            }
        }

        /// <summary>
        /// Shuts down the OBS runtime.
        /// </summary>
        public static void Shutdown()
        {
            NativeMethods.Shutdown();
        }

        private static InvalidOperationException CreateNativeOperationException(
            string operation,
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
                $"Failed to {operation} ({result}): {detail}");
        }
    }
}
