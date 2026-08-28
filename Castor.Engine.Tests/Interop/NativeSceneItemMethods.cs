using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Castor.Engine.Tests.Interop
{
    internal enum NativeSceneItemResult
    {
        Ok = 0,
        NotInitialized = 5,
        SceneInvalidName = 66,
        SceneNotFound = 68,
        SceneItemNotFound = 73,
        SceneItemInvalidTransform = 74,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeSceneItemTransform
    {
        internal uint StructSize;
        internal float PositionX;
        internal float PositionY;
        internal float ScaleX;
        internal float ScaleY;
        internal float RotationDegrees;
        internal uint BoundsMode;
        internal float BoundsWidth;
        internal float BoundsHeight;
        internal uint CropLeft;
        internal uint CropTop;
        internal uint CropRight;
        internal uint CropBottom;
    }

    internal static partial class NativeSceneItemMethods
    {
        private const string LibraryName = "Castor.Engine.Host";

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_last_error")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial nint GetLastError();

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_scene_item_transform",
            StringMarshalling = StringMarshalling.Utf8)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeSceneItemResult GetSceneItemTransform(
            string sceneName,
            ref NativeSceneItemTransform transform);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_get_scene_item_transform",
            StringMarshalling = StringMarshalling.Utf8)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeSceneItemResult GetSceneItemTransformRaw(
            string sceneName,
            nint transform);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_set_scene_item_transform",
            StringMarshalling = StringMarshalling.Utf8)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeSceneItemResult SetSceneItemTransform(
            string sceneName,
            in NativeSceneItemTransform transform);

        [LibraryImport(
            LibraryName,
            EntryPoint = "castor_engine_set_scene_item_transform",
            StringMarshalling = StringMarshalling.Utf8)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial NativeSceneItemResult SetSceneItemTransformRaw(
            string sceneName,
            nint transform);

        internal static NativeSceneItemTransform CreateTransform(uint boundsMode = 2)
        {
            return new NativeSceneItemTransform
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeSceneItemTransform>()),
                PositionX = 120.25F,
                PositionY = 64.5F,
                ScaleX = -0.75F,
                ScaleY = 1.25F,
                RotationDegrees = 450.0F,
                BoundsMode = boundsMode,
                BoundsWidth = boundsMode == 0 ? 0.0F : 640.0F,
                BoundsHeight = boundsMode == 0 ? 0.0F : 360.0F,
                CropLeft = 11,
                CropTop = 12,
                CropRight = 13,
                CropBottom = 14,
            };
        }

        internal static string? GetLastErrorMessage()
        {
            var pointer = GetLastError();
            return pointer == nint.Zero ? null : Marshal.PtrToStringUTF8(pointer);
        }
    }
}
