using System.Runtime.InteropServices;
using System.Text;

namespace Castor.Engine.Tests.Interop
{
    internal enum NativeVideoEncoderResult
    {
        Ok = 0,
        InvalidArgument = 1,
        NotInitialized = 5,
        VideoNotConfigured = 11,
        VideoEncoderUnknownId = 21,
        VideoEncoderUnavailable = 22,
        VideoEncoderAlreadyConfigured = 23,
        VideoEncoderCreationFailed = 24,
        AudioNotConfigured = 25,
        AudioEncoderAlreadyConfigured = 26,
        AudioEncoderUnavailable = 27,
        AudioEncoderCreationFailed = 28,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeVideoEncoderConfig
    {
        internal uint StructSize;
        internal uint SelectionMode;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        internal byte[] EncoderId;

        internal uint Bitrate;
        internal uint RateControl;
        internal uint KeyframeIntervalSeconds;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        internal byte[] Preset;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        internal byte[] Profile;

        internal uint AudioBitrate;
        internal uint AudioTrackIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeVideoEncoderInfo
    {
        internal uint StructSize;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        internal byte[] Id;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        internal byte[] Name;

        internal byte IsHardware;
        internal byte IsAvailable;
    }

    /// <summary>
    /// Bindings for the native video encoder contract, declared directly
    /// against Castor.Engine.Host to exercise it in isolation from the
    /// managed video encoder API, which does not exist yet.
    /// </summary>
    internal static class NativeVideoEncoderMethods
    {
        private const string LibraryName = "Castor.Engine.Host";

        internal const uint AutomaticSelectionMode = 0;
        internal const uint HardwarePreferredSelectionMode = 1;
        internal const uint SoftwareForcedSelectionMode = 2;

        internal const uint ConstantBitrateRateControl = 0;
        internal const uint VariableBitrateRateControl = 1;
        internal const uint ConstantQpRateControl = 2;
        internal const uint ConstantRateFactorRateControl = 3;

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_last_error",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint GetLastError();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_video_encoder_count",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint GetVideoEncoderCount();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_video_encoder_at",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte GetVideoEncoderAt(uint index, ref NativeVideoEncoderInfo info);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_video_encoder_at",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte GetVideoEncoderAtRaw(uint index, nint info);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_video_encoder_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeVideoEncoderResult ValidateVideoEncoderConfig(in NativeVideoEncoderConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_configure_video_encoder",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeVideoEncoderResult ConfigureVideoEncoder(in NativeVideoEncoderConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_is_video_encoder_configured",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte IsVideoEncoderConfigured();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_video_encoder_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte GetVideoEncoderConfig(ref NativeVideoEncoderConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_selected_video_encoder",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte GetSelectedVideoEncoder(ref NativeVideoEncoderInfo info);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_video_encoder_fallback_notice",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint GetVideoEncoderFallbackNotice();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_video_encoder_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeVideoEncoderResult ValidateVideoEncoderConfigRaw(nint config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_audio_encoder_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeVideoEncoderResult ValidateAudioEncoderConfig(in NativeVideoEncoderConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_audio_encoder_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeVideoEncoderResult ValidateAudioEncoderConfigRaw(nint config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_configure_audio_encoder",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeVideoEncoderResult ConfigureAudioEncoder(in NativeVideoEncoderConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_is_audio_encoder_configured",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte IsAudioEncoderConfigured();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_selected_audio_encoder",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte GetSelectedAudioEncoder(ref NativeVideoEncoderInfo info);

        internal static string? GetLastErrorMessage()
        {
            var pointer = GetLastError();
            return pointer == nint.Zero ? null : Marshal.PtrToStringUTF8(pointer);
        }

        internal static string GetVideoEncoderFallbackNoticeMessage()
        {
            var pointer = GetVideoEncoderFallbackNotice();
            return pointer == nint.Zero ? string.Empty : Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
        }

        internal static NativeVideoEncoderConfig CreateConfig(
            uint selectionMode = SoftwareForcedSelectionMode,
            string encoderId = "",
            uint bitrate = 2500,
            uint rateControl = ConstantBitrateRateControl,
            uint keyframeIntervalSeconds = 2,
            string preset = "",
            string profile = "",
            uint audioBitrate = 0,
            uint audioTrackIndex = 0)
        {
            return new NativeVideoEncoderConfig
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeVideoEncoderConfig>()),
                SelectionMode = selectionMode,
                EncoderId = ToFixedBuffer(encoderId, 64),
                Bitrate = bitrate,
                RateControl = rateControl,
                KeyframeIntervalSeconds = keyframeIntervalSeconds,
                Preset = ToFixedBuffer(preset, 32),
                Profile = ToFixedBuffer(profile, 32),
                AudioBitrate = audioBitrate,
                AudioTrackIndex = audioTrackIndex,
            };
        }

        internal static NativeVideoEncoderConfig CreateAudioEncoderConfig(
            uint audioBitrate = 128,
            uint audioTrackIndex = 0)
        {
            return CreateConfig(audioBitrate: audioBitrate, audioTrackIndex: audioTrackIndex);
        }

        internal static byte[] ToFixedBuffer(string value, int size)
        {
            var buffer = new byte[size];
            var encoded = Encoding.UTF8.GetBytes(value);

            if (encoded.Length >= size)
            {
                throw new ArgumentException(
                    $"'{value}' does not fit in a {size}-byte null-terminated buffer.",
                    nameof(value));
            }

            Array.Copy(encoded, buffer, encoded.Length);
            return buffer;
        }

        internal static NativeVideoEncoderInfo CreateInfo()
        {
            return new NativeVideoEncoderInfo
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeVideoEncoderInfo>()),
                Id = new byte[64],
                Name = new byte[128],
            };
        }

        internal static string FromFixedBuffer(byte[] buffer)
        {
            var terminator = Array.IndexOf(buffer, (byte)0);
            var length = terminator < 0 ? buffer.Length : terminator;
            return Encoding.UTF8.GetString(buffer, 0, length);
        }
    }
}
