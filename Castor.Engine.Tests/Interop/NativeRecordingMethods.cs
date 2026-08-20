using System.Runtime.InteropServices;

namespace Castor.Engine.Tests.Interop
{
    internal enum NativeRecordingResult
    {
        Ok = 0,
        InvalidArgument = 1,
        NotInitialized = 5,
        VideoNotConfigured = 11,
        RecordingNoActiveScene = 29,
        RecordingHardwareEncoderNotAllowed = 30,
        RecordingAlreadyActive = 31,
        RecordingNotActive = 32,
        RecordingInvalidDestination = 33,
        RecordingOutputUnavailable = 34,
        RecordingOutputCreationFailed = 35,
        RecordingStartFailed = 36,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRecordingConfig
    {
        internal uint StructSize;
        internal nint DestinationPath;
    }

    /// <summary>
    /// Bindings for the native recording contract, declared directly
    /// against Castor.Engine.Host to exercise it in isolation from the
    /// managed recording API, which does not exist yet.
    /// </summary>
    internal static class NativeRecordingMethods
    {
        private const string LibraryName = "Castor.Engine.Host";

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_last_error",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint GetLastError();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_recording_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeRecordingResult ValidateRecordingConfig(in NativeRecordingConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_recording_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeRecordingResult ValidateRecordingConfigRaw(nint config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_start_recording",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeRecordingResult StartRecording(in NativeRecordingConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_stop_recording",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeRecordingResult StopRecording();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_is_recording_active",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte IsRecordingActive();

        internal static string? GetLastErrorMessage()
        {
            var pointer = GetLastError();
            return pointer == nint.Zero ? null : Marshal.PtrToStringUTF8(pointer);
        }

        internal static NativeRecordingConfig CreateConfig(string? destinationPath)
        {
            return new NativeRecordingConfig
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeRecordingConfig>()),
                DestinationPath = destinationPath is null
                    ? nint.Zero
                    : Marshal.StringToCoTaskMemUTF8(destinationPath),
            };
        }

        internal static void FreeConfig(NativeRecordingConfig config)
        {
            if (config.DestinationPath != nint.Zero)
            {
                Marshal.FreeCoTaskMem(config.DestinationPath);
            }
        }
    }
}
