using System.Runtime.InteropServices;

namespace Castor.Engine.Tests.Interop
{
    internal enum NativeAudioResult
    {
        Ok = 0,
        InvalidArgument = 1,
        NotInitialized = 5,
        AudioUnsupportedSampleRate = 11,
        AudioUnsupportedSpeakerLayout = 12,
        AudioAlreadyConfigured = 13,
        AudioConfigurationFailed = 14,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeAudioConfig
    {
        internal uint StructSize;
        internal uint SampleRate;
        internal uint SpeakerLayout;
    }

    /// <summary>
    /// Bindings for the native audio contract, declared directly against
    /// Castor.Engine.Host to exercise it in isolation from the managed
    /// audio API, which does not exist yet.
    /// </summary>
    internal static class NativeAudioMethods
    {
        private const string LibraryName = "Castor.Engine.Host";

        internal const uint DefaultSpeakerLayout = 0;
        internal const uint MonoSpeakerLayout = 1;
        internal const uint StereoSpeakerLayout = 2;

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_last_error",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint GetLastError();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_audio_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeAudioResult ValidateAudioConfig(in NativeAudioConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_audio_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeAudioResult ValidateAudioConfigRaw(nint config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_configure_audio",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern NativeAudioResult ConfigureAudio(in NativeAudioConfig config);

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_is_audio_configured",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte IsAudioConfigured();

        [DllImport(
            LibraryName,
            EntryPoint = "castor_engine_get_audio_config",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte GetAudioConfig(ref NativeAudioConfig config);

        internal static string? GetLastErrorMessage()
        {
            var pointer = GetLastError();
            return pointer == nint.Zero ? null : Marshal.PtrToStringUTF8(pointer);
        }

        internal static NativeAudioConfig CreateConfig(uint sampleRate = 0, uint speakerLayout = 0)
        {
            return new NativeAudioConfig
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeAudioConfig>()),
                SampleRate = sampleRate,
                SpeakerLayout = speakerLayout,
            };
        }
    }
}
