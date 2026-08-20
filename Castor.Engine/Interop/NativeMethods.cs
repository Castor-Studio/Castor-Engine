using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Castor.Engine.Interop
{
    internal enum NativeEngineResult
    {
        Ok = 0,
        InvalidArgument = 1,
        InvalidRuntime = 2,
        ObsStartupFailed = 3,
        ModuleLoadFailed = 4,
        NotInitialized = 5,

        VideoNotSupported = 6,
        VideoInvalidConfiguration = 7,
        VideoCurrentlyActive = 8,
        VideoModuleNotFound = 9,
        VideoConfigurationFailed = 10,
        VideoNotConfigured = 11,

        AudioUnsupportedSampleRate = 12,
        AudioUnsupportedSpeakerLayout = 13,
        AudioAlreadyConfigured = 14,
        AudioConfigurationFailed = 15,

        SceneCreationFailed = 16,
        SceneSourceUnavailable = 17,
        SceneSourceCreationFailed = 18,
        SceneSourceAddFailed = 19,
        SceneActivationFailed = 20,

        VideoEncoderUnknownId = 21,
        VideoEncoderUnavailable = 22,
        VideoEncoderAlreadyConfigured = 23,
        VideoEncoderCreationFailed = 24,

        AudioNotConfigured = 25,
        AudioEncoderAlreadyConfigured = 26,
        AudioEncoderUnavailable = 27,
        AudioEncoderCreationFailed = 28,

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
    internal struct NativeEngineConfiguration
    {
        internal uint StructSize;
        internal nint RuntimeRoot;
        internal nint Locale;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeEngineVideoConfiguration
    {
        internal uint StructSize;
        internal uint BaseWidth;
        internal uint BaseHeight;
        internal uint OutputWidth;
        internal uint OutputHeight;
        internal uint FramesPerSecondNumerator;
        internal uint FramesPerSecondDenominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeEngineAudioConfiguration
    {
        internal uint StructSize;
        internal uint SampleRate;
        internal uint SpeakerLayout;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeEngineVideoEncoderConfiguration
    {
        internal uint StructSize;
        internal uint SelectionMode;
        internal FixedBuffer64 EncoderId;
        internal uint Bitrate;
        internal uint RateControl;
        internal uint KeyframeIntervalSeconds;
        internal FixedBuffer32 Preset;
        internal FixedBuffer32 Profile;
        internal uint AudioBitrate;
        internal uint AudioTrackIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeEngineVideoEncoderInfo
    {
        internal uint StructSize;
        internal FixedBuffer64 Id;
        internal FixedBuffer128 Name;
        internal byte IsHardware;
        internal byte IsAvailable;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeEngineRecordingConfiguration
    {
        internal uint StructSize;
        internal nint DestinationPath;
    }

    internal static partial class NativeMethods
    {
        private const string LibraryName = "Castor.Engine.Host";

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_abi_version")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial uint GetAbiVersion();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_version")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial nint GetVersion();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_obs_version")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial nint GetObsVersion();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_initialize")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeEngineResult Initialize(
            in NativeEngineConfiguration configuration);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_last_error")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial nint GetLastError();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_loaded_module_count")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial uint GetLoadedModuleCount();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_is_module_loaded",
            StringMarshalling = StringMarshalling.Utf8)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte IsModuleLoaded(string moduleName);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_configure_video")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeEngineResult ConfigureVideo(
            in NativeEngineVideoConfiguration configuration);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_is_video_configured")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte IsVideoConfigured();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_configure_audio")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeEngineResult ConfigureAudio(
            in NativeEngineAudioConfiguration configuration);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_is_audio_configured")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte IsAudioConfigured();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_audio_config")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte GetAudioConfig(
            ref NativeEngineAudioConfiguration configuration);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_video_encoder_count")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial uint GetVideoEncoderCount();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_video_encoder_at")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte GetVideoEncoderAt(
            uint index,
            ref NativeEngineVideoEncoderInfo info);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_video_encoder_config")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeEngineResult ValidateVideoEncoderConfig(
            in NativeEngineVideoEncoderConfiguration configuration);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_configure_video_encoder")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeEngineResult ConfigureVideoEncoder(
            in NativeEngineVideoEncoderConfiguration configuration);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_is_video_encoder_configured")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte IsVideoEncoderConfigured();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_video_encoder_config")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte GetVideoEncoderConfig(
            ref NativeEngineVideoEncoderConfiguration configuration);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_selected_video_encoder")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte GetSelectedVideoEncoder(
            ref NativeEngineVideoEncoderInfo info);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_video_encoder_fallback_notice")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial nint GetVideoEncoderFallbackNotice();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_validate_audio_encoder_config")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeEngineResult ValidateAudioEncoderConfig(
            in NativeEngineVideoEncoderConfiguration configuration);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_configure_audio_encoder")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeEngineResult ConfigureAudioEncoder(
            in NativeEngineVideoEncoderConfiguration configuration);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_is_audio_encoder_configured")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte IsAudioEncoderConfigured();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_selected_audio_encoder")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte GetSelectedAudioEncoder(
            ref NativeEngineVideoEncoderInfo info);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_video_encoder_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial nint GetVideoEncoderHandle();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_audio_encoder_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial nint GetAudioEncoderHandle();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_start_recording")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeEngineResult StartRecording(
            in NativeEngineRecordingConfiguration configuration);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_stop_recording")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeEngineResult StopRecording();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_is_recording_active")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte IsRecordingActive();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_create_main_scene")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeEngineResult CreateMainScene();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_has_active_scene")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte HasActiveScene();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_shutdown")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial void Shutdown();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_is_initialized")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial byte IsInitialized();
    }
}
