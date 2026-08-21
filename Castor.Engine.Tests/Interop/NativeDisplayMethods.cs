using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Castor.Engine.Tests.Interop
{
    internal enum NativeDisplayResult
    {
        Ok = 0,
        NotInitialized = 5,
        VideoNotConfigured = 11,
        DisplayInvalidConfiguration = 37,
        DisplayNotFound = 38,
        DisplaySourceUnavailable = 39,
        DisplaySourceCreationFailed = 40,
        DisplaySourceAddFailed = 41,
        DisplayNoActiveScene = 42,
        DisplayReconfigurationWhileRecording = 43,
    }

    [InlineArray(256)]
    internal struct NativeDisplayBuffer256
    {
        private byte _element0;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeDisplayInfo
    {
        internal uint StructSize;
        internal NativeDisplayBuffer256 Id;
        internal NativeDisplayBuffer256 Name;
        internal byte IsPrimary;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeDisplayCaptureConfig
    {
        internal uint StructSize;
        internal NativeDisplayBuffer256 DisplayId;
        internal byte CaptureCursor;
    }

    internal static class NativeDisplayMethods
    {
        private const string LibraryName = "Castor.Engine.Host";

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_last_error",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint GetLastError();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_display_count",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint GetDisplayCount();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_display_at",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte GetDisplayAt(uint index, ref NativeDisplayInfo info);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_display_at",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte GetDisplayAtRaw(uint index, nint info);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_display_capture_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeDisplayResult ValidateDisplayCaptureConfig(in NativeDisplayCaptureConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_display_capture_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeDisplayResult ValidateDisplayCaptureConfigRaw(nint config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_configure_display_capture",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeDisplayResult ConfigureDisplayCapture(in NativeDisplayCaptureConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_is_display_capture_active",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte IsDisplayCaptureActive();

        internal static NativeDisplayCaptureConfig CreateConfig(string displayId, bool captureCursor = true)
        {
            var config = new NativeDisplayCaptureConfig
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeDisplayCaptureConfig>()),
                CaptureCursor = captureCursor ? (byte)1 : (byte)0,
            };
            Encode(displayId, config.DisplayId);
            return config;
        }

        internal static NativeDisplayInfo CreateInfo()
        {
            return new NativeDisplayInfo
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeDisplayInfo>()),
            };
        }

        internal static string Decode(ReadOnlySpan<byte> buffer)
        {
            var terminator = buffer.IndexOf((byte)0);
            return Encoding.UTF8.GetString(buffer[..(terminator < 0 ? buffer.Length : terminator)]);
        }

        internal static string? GetLastErrorMessage()
        {
            var pointer = GetLastError();
            return pointer == nint.Zero ? null : Marshal.PtrToStringUTF8(pointer);
        }

        private static void Encode(string value, Span<byte> destination)
        {
            destination.Clear();
            Encoding.UTF8.GetBytes(value, destination);
        }
    }
}
