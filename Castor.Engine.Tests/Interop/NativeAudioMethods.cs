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
    internal static partial class NativeAudioMethods
    {
        private const string LibraryName = "Castor.Engine.Host";

        internal const uint DefaultSpeakerLayout = 0;
        internal const uint MonoSpeakerLayout = 1;
        internal const uint StereoSpeakerLayout = 2;

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_last_error")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial nint GetLastError();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_audio_config")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeAudioResult ValidateAudioConfig(in NativeAudioConfig config);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_audio_config")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeAudioResult ValidateAudioConfigRaw(nint config);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_configure_audio")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeAudioResult ConfigureAudio(in NativeAudioConfig config);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_is_audio_configured")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte IsAudioConfigured();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_audio_config")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte GetAudioConfig(ref NativeAudioConfig config);

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
