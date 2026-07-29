using System.Runtime.InteropServices;
using Castor.Engine.Interop;

namespace Castor.Engine
{
    /// <summary>
    /// Provides version and compatibility information for Castor Engine
    /// and its native dependencies.
    /// </summary>
    public static class EngineInfo
    {
        /// <summary>
        /// Gets the ABI version supported by this managed wrapper.
        /// </summary>
        public const uint SupportedAbiVersion = 1;

        /// <summary>
        /// Gets the ABI version exposed by the native Castor Engine library.
        /// </summary>
        public static uint AbiVersion =>
            NativeMethods.GetAbiVersion();

        /// <summary>
        /// Gets the version of the native Castor Engine library.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the native engine returns an invalid pointer or UTF-8 string.
        /// </exception>
        public static string Version
        {
            get
            {
                var pointer = NativeMethods.GetVersion();

                if (pointer == nint.Zero)
                {
                    throw new InvalidOperationException(
                        "The native engine returned an invalid version.");
                }

                return Marshal.PtrToStringUTF8(pointer)
                    ?? throw new InvalidOperationException(
                        "The native engine returned an invalid UTF-8 string.");
            }
        }

        /// <summary>
        /// Gets the version of the OBS library used by Castor Engine.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when OBS returns an invalid pointer or UTF-8 string.
        /// </exception>
        public static string ObsVersion
        {
            get
            {
                var pointer = NativeMethods.GetObsVersion();

                if (pointer == nint.Zero)
                {
                    throw new InvalidOperationException(
                        "OBS returned an invalid version.");
                }

                return Marshal.PtrToStringUTF8(pointer)
                    ?? throw new InvalidOperationException(
                        "OBS returned an invalid UTF-8 string.");
            }
        }

        /// <summary>
        /// Ensures that the native engine ABI is compatible with this managed wrapper.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native engine exposes an unsupported ABI version.
        /// </exception>
        public static void ValidateCompatibility()
        {
            var actualVersion = AbiVersion;

            if (actualVersion != SupportedAbiVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported Castor Engine ABI. " +
                    $"Expected {SupportedAbiVersion}, received {actualVersion}.");
            }
        }
    }
}
