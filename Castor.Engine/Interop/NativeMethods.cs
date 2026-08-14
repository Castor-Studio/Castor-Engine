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
