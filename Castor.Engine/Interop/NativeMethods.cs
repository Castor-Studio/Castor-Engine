using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Castor.Engine.Interop
{
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
        internal static partial byte Initialize();

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
