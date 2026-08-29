using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class SceneItemTransformTests : IDisposable
    {
        public SceneItemTransformTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void ManagedTransformShouldUseObsDefaults()
        {
            var transform = new EngineSceneItemTransform();

            Assert.Equal(0.0F, transform.PositionX);
            Assert.Equal(0.0F, transform.PositionY);
            Assert.Equal(1.0F, transform.ScaleX);
            Assert.Equal(1.0F, transform.ScaleY);
            Assert.Equal(0.0F, transform.RotationDegrees);
            Assert.Equal(EngineSceneItemBoundsMode.None, transform.BoundsMode);
            Assert.Equal(0.0F, transform.BoundsWidth);
            Assert.Equal(0.0F, transform.BoundsHeight);
            Assert.Equal(0U, transform.CropLeft);
            Assert.Equal(0U, transform.CropTop);
            Assert.Equal(0U, transform.CropRight);
            Assert.Equal(0U, transform.CropBottom);
        }

        [Fact]
        public void ManagedMethodsShouldValidateArguments()
        {
            Assert.Throws<ArgumentException>(() => EngineRuntime.GetSceneItemTransform(" "));
            Assert.Throws<ArgumentException>(
                () => EngineRuntime.SetSceneItemTransform(" ", new EngineSceneItemTransform()));
            Assert.Throws<ArgumentNullException>(() => EngineRuntime.SetSceneItemTransform("wide", null!));
        }

        [Fact]
        public void TransformAccessShouldRequireInitialization()
        {
            var getException = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.GetSceneItemTransform("wide"));
            var setException = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.SetSceneItemTransform("wide", new EngineSceneItemTransform()));

            Assert.Contains("NotInitialized", getException.Message);
            Assert.Contains("NotInitialized", setException.Message);
        }

        [StaFact]
        public void TransformAccessShouldDistinguishUnknownAndEmptyScenes()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());

            var unknown = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.GetSceneItemTransform("ghost"));
            Assert.Contains("SceneNotFound", unknown.Message);

            EngineRuntime.CreateScene("wide");
            var empty = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.GetSceneItemTransform("wide"));
            Assert.Contains("SceneItemNotFound", empty.Message);
        }

        [StaFact]
        public void NativeAbiShouldValidatePointersAndStructureSizes()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");

            Assert.Equal(
                NativeSceneItemResult.SceneItemInvalidTransform,
                NativeSceneItemMethods.GetSceneItemTransformRaw("wide", nint.Zero));
            Assert.Contains("must not be null", NativeSceneItemMethods.GetLastErrorMessage());

            Assert.Equal(
                NativeSceneItemResult.SceneItemInvalidTransform,
                NativeSceneItemMethods.SetSceneItemTransformRaw("wide", nint.Zero));
            Assert.Contains("must not be null", NativeSceneItemMethods.GetLastErrorMessage());

            var undersized = NativeSceneItemMethods.CreateTransform();
            undersized.StructSize = 1;
            Assert.Equal(
                NativeSceneItemResult.SceneItemInvalidTransform,
                NativeSceneItemMethods.GetSceneItemTransform("wide", ref undersized));
            Assert.Contains("too small", NativeSceneItemMethods.GetLastErrorMessage());

            Assert.Equal(
                NativeSceneItemResult.SceneItemInvalidTransform,
                NativeSceneItemMethods.SetSceneItemTransform("wide", in undersized));
            Assert.Contains("too small", NativeSceneItemMethods.GetLastErrorMessage());
        }

        [StaFact]
        public void NativeAbiShouldRejectInvalidTransformValues()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");

            var invalid = NativeSceneItemMethods.CreateTransform();
            invalid.PositionX = float.NaN;
            AssertInvalid(invalid, "finite");

            invalid = NativeSceneItemMethods.CreateTransform();
            invalid.ScaleY = float.PositiveInfinity;
            AssertInvalid(invalid, "finite");

            invalid = NativeSceneItemMethods.CreateTransform();
            invalid.BoundsMode = 7;
            AssertInvalid(invalid, "not recognized");

            invalid = NativeSceneItemMethods.CreateTransform();
            invalid.BoundsWidth = 0.0F;
            AssertInvalid(invalid, "must be positive");

            invalid = NativeSceneItemMethods.CreateTransform(0);
            invalid.BoundsHeight = -1.0F;
            AssertInvalid(invalid, "must not be negative");

            invalid = NativeSceneItemMethods.CreateTransform();
            invalid.CropBottom = uint.MaxValue;
            AssertInvalid(invalid, "INT32_MAX");
        }

        [StaFact]
        public void RealObsTransformsShouldRoundTripIndependentlyForActiveAndBackgroundScenes()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var displays = EngineRuntime.EnumerateDisplays();

            if (displays.Count == 0)
            {
                return;
            }

            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");
            EngineRuntime.CreateScene("closeup");
            EngineRuntime.ConfigureDisplayCapture(
                new EngineDisplayCaptureConfiguration("wide", displays[0].Id, captureCursor: true));
            EngineRuntime.ConfigureDisplayCapture(
                new EngineDisplayCaptureConfiguration("closeup", displays[0].Id, captureCursor: false));

            var wide = CreateManagedTransform(EngineSceneItemBoundsMode.ScaleInner);
            var closeup = CreateManagedTransform(EngineSceneItemBoundsMode.Stretch);
            closeup.PositionX = 800.0F;
            closeup.ScaleY = -2.0F;

            EngineRuntime.SetSceneItemTransform("wide", wide);
            EngineRuntime.SetSceneItemTransform("closeup", closeup);
            AssertTransformEqual(wide, EngineRuntime.GetSceneItemTransform("wide"));
            AssertTransformEqual(closeup, EngineRuntime.GetSceneItemTransform("closeup"));

            EngineRuntime.SwitchScene(
                "wide",
                new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
            wide.PositionX = 240.5F;
            wide.RotationDegrees = -90.0F;
            EngineRuntime.SetSceneItemTransform("wide", wide);

            AssertTransformEqual(wide, EngineRuntime.GetSceneItemTransform("wide"));
            AssertTransformEqual(closeup, EngineRuntime.GetSceneItemTransform("closeup"));
        }

        [Theory]
        [InlineData(EngineSceneItemBoundsMode.None, 0)]
        [InlineData(EngineSceneItemBoundsMode.Stretch, 1)]
        [InlineData(EngineSceneItemBoundsMode.ScaleInner, 2)]
        [InlineData(EngineSceneItemBoundsMode.ScaleOuter, 3)]
        [InlineData(EngineSceneItemBoundsMode.ScaleToWidth, 4)]
        [InlineData(EngineSceneItemBoundsMode.ScaleToHeight, 5)]
        [InlineData(EngineSceneItemBoundsMode.MaxOnly, 6)]
        public void BoundsModeValuesShouldMatchTheNativeContract(EngineSceneItemBoundsMode mode, int expected)
        {
            Assert.Equal(expected, (int)mode);
        }

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration()
        {
            return new EngineRuntimeConfiguration(AppContext.BaseDirectory);
        }

        private static EngineVideoConfiguration CreateVideoConfiguration()
        {
            return new EngineVideoConfiguration(1280, 720, 1280, 720, 30, 1);
        }

        private static EngineSceneItemTransform CreateManagedTransform(EngineSceneItemBoundsMode boundsMode)
        {
            return new EngineSceneItemTransform
            {
                PositionX = 120.25F,
                PositionY = 64.5F,
                ScaleX = -0.75F,
                ScaleY = 1.25F,
                RotationDegrees = 450.0F,
                BoundsMode = boundsMode,
                BoundsWidth = boundsMode == EngineSceneItemBoundsMode.None ? 0.0F : 640.0F,
                BoundsHeight = boundsMode == EngineSceneItemBoundsMode.None ? 0.0F : 360.0F,
                CropLeft = 11,
                CropTop = 12,
                CropRight = 13,
                CropBottom = 14,
            };
        }

        private static void AssertInvalid(NativeSceneItemTransform transform, string messageFragment)
        {
            Assert.Equal(
                NativeSceneItemResult.SceneItemInvalidTransform,
                NativeSceneItemMethods.SetSceneItemTransform("wide", in transform));
            Assert.Contains(messageFragment, NativeSceneItemMethods.GetLastErrorMessage());
        }

        private static void AssertTransformEqual(
            EngineSceneItemTransform expected,
            EngineSceneItemTransform actual)
        {
            Assert.Equal(expected.PositionX, actual.PositionX);
            Assert.Equal(expected.PositionY, actual.PositionY);
            Assert.Equal(expected.ScaleX, actual.ScaleX);
            Assert.Equal(expected.ScaleY, actual.ScaleY);
            Assert.Equal(expected.RotationDegrees, actual.RotationDegrees);
            Assert.Equal(expected.BoundsMode, actual.BoundsMode);
            Assert.Equal(expected.BoundsWidth, actual.BoundsWidth);
            Assert.Equal(expected.BoundsHeight, actual.BoundsHeight);
            Assert.Equal(expected.CropLeft, actual.CropLeft);
            Assert.Equal(expected.CropTop, actual.CropTop);
            Assert.Equal(expected.CropRight, actual.CropRight);
            Assert.Equal(expected.CropBottom, actual.CropBottom);
        }
    }
}
