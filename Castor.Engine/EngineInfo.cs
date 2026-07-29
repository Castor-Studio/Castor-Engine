using System.Runtime.InteropServices;
using Castor.Engine.Interop;

namespace Castor.Engine
{
    public static class EngineInfo
    {
        public const uint SupportedAbiVersion = 1;

        public static uint AbiVersion =>
            NativeMethods.GetAbiVersion();

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
