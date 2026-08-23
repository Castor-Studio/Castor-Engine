using System.Runtime.InteropServices;

namespace Castor.Engine.Tests.Interop
{
    internal enum NativeStreamingResult
    {
        Ok = 0,
        InvalidArgument = 1,
        NotInitialized = 5,
        StreamingInvalidConfiguration = 44,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeStreamingConfig
    {
        internal uint StructSize;
        internal nint ServerUrl;
        internal nint StreamKey;
        internal byte UseAuthentication;
        internal nint Username;
        internal nint Password;
        internal uint ReconnectRetryCount;
        internal uint ReconnectDelaySeconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct NativeStreamingStatus
    {
        internal uint StructSize;
        internal uint State;
        internal uint LastFailureCode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        internal byte[] LastFailureMessage;
    }

    internal static class NativeStreamingMethods
    {
        private const string LibraryName = "Castor.Engine.Host";

        [DllImport(LibraryName, EntryPoint = "castor_engine_validate_streaming_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeStreamingResult Validate(in NativeStreamingConfig config);

        [DllImport(LibraryName, EntryPoint = "castor_engine_validate_streaming_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeStreamingResult ValidateRaw(nint config);

        [DllImport(LibraryName, EntryPoint = "castor_engine_get_streaming_status",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeStreamingResult GetStatus(ref NativeStreamingStatus status);

        internal static NativeStreamingConfig Create(
            string? server = "rtmp://127.0.0.1:1935/live",
            string? key = "test-key",
            bool useAuthentication = false,
            string? username = "",
            string? password = "",
            uint retries = 20,
            uint delay = 2)
        {
            return new NativeStreamingConfig
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeStreamingConfig>()),
                ServerUrl = Allocate(server),
                StreamKey = Allocate(key),
                UseAuthentication = useAuthentication ? (byte)1 : (byte)0,
                Username = Allocate(username),
                Password = Allocate(password),
                ReconnectRetryCount = retries,
                ReconnectDelaySeconds = delay,
            };
        }

        internal static void Free(NativeStreamingConfig config)
        {
            Marshal.FreeCoTaskMem(config.ServerUrl);
            Marshal.FreeCoTaskMem(config.StreamKey);
            Marshal.FreeCoTaskMem(config.Username);
            Marshal.FreeCoTaskMem(config.Password);
        }

        internal static NativeStreamingStatus CreateStatus() => new()
        {
            StructSize = checked((uint)Marshal.SizeOf<NativeStreamingStatus>()),
            LastFailureMessage = new byte[512],
        };

        private static nint Allocate(string? value) =>
            value is null ? nint.Zero : Marshal.StringToCoTaskMemUTF8(value);
    }
}
