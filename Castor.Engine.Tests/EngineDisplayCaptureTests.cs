using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class EngineDisplayCaptureTests : IDisposable
    {
        public EngineDisplayCaptureTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void EnumerateDisplaysShouldRequireInitialization()
        {
            var exception = Assert.Throws<InvalidOperationException>(EngineRuntime.EnumerateDisplays);

            Assert.Contains("must be initialized", exception.Message);
        }

        [Fact]
        public void ConfigureDisplayCaptureShouldRejectNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(
                () => EngineRuntime.ConfigureDisplayCapture(null!));
        }

        [Fact]
        public void ConfigureDisplayCaptureShouldRequireInitialization()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureDisplayCapture(
                    new EngineDisplayCaptureConfiguration("display-1")));

            Assert.Contains("NotInitialized", exception.Message);
        }

        [StaFact]
        public void ConfigureDisplayCaptureShouldRequireVideoConfiguration()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureDisplayCapture(
                    new EngineDisplayCaptureConfiguration("display-1")));

            Assert.Contains("VideoNotConfigured", exception.Message);
        }

        [StaFact]
        public void ConfigureDisplayCaptureShouldRequireActiveScene()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureDisplayCapture(
                    new EngineDisplayCaptureConfiguration("display-1")));

            Assert.Contains("DisplayNoActiveScene", exception.Message);
        }

        [StaFact]
        public void ConfigureDisplayCaptureShouldRejectUnavailableIdentifier()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateMainScene();

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureDisplayCapture(
                    new EngineDisplayCaptureConfiguration("castor-missing-display")));

            Assert.Contains("DisplayNotFound", exception.Message);
            Assert.False(EngineRuntime.IsDisplayCaptureActive);
        }

        [StaFact]
        public void ManagedAndNativeDisplayStateShouldStayConsistent()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var displays = EngineRuntime.EnumerateDisplays();

            Assert.Equal(displays.Count, checked((int)NativeDisplayMethods.GetDisplayCount()));
            Assert.Equal(displays.Count, displays.Select(display => display.Id).Distinct().Count());

            if (displays.Count > 0)
            {
                Assert.Single(displays.Where(display => display.IsPrimary));
            }

            for (uint index = 0; index < displays.Count; index++)
            {
                var nativeInfo = NativeDisplayMethods.CreateInfo();
                Assert.NotEqual(0, NativeDisplayMethods.GetDisplayAt(index, ref nativeInfo));
                Assert.Equal(displays[(int)index].Id, NativeDisplayMethods.Decode(nativeInfo.Id));
                Assert.Equal(displays[(int)index].Name, NativeDisplayMethods.Decode(nativeInfo.Name));
                Assert.Equal(displays[(int)index].IsPrimary, nativeInfo.IsPrimary != 0);
            }
        }

        [StaFact]
        public void NativeDisplayRetrievalShouldValidateItsOutputStructure()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            Assert.Equal(0, NativeDisplayMethods.GetDisplayAtRaw(0, nint.Zero));
            Assert.Contains("must not be null", NativeDisplayMethods.GetLastErrorMessage());

            var info = NativeDisplayMethods.CreateInfo();
            info.StructSize = 1;

            Assert.Equal(0, NativeDisplayMethods.GetDisplayAt(0, ref info));
            Assert.Contains("too small", NativeDisplayMethods.GetLastErrorMessage());
        }

        [StaFact]
        public void ActiveDisplayCaptureShouldBeReleasedByShutdown()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateMainScene();
            var displays = EngineRuntime.EnumerateDisplays();

            if (displays.Count == 0)
            {
                return;
            }

            var configuration = new EngineDisplayCaptureConfiguration(displays[0].Id, captureCursor: false);
            EngineRuntime.ConfigureDisplayCapture(configuration);
            EngineRuntime.ConfigureDisplayCapture(configuration);
            EngineRuntime.ConfigureDisplayCapture(
                new EngineDisplayCaptureConfiguration(displays[0].Id, captureCursor: true));

            if (displays.Count > 1)
            {
                EngineRuntime.ConfigureDisplayCapture(
                    new EngineDisplayCaptureConfiguration(displays[1].Id, captureCursor: false));
            }

            Assert.True(EngineRuntime.IsDisplayCaptureActive);
            Assert.NotEqual(0, NativeDisplayMethods.IsDisplayCaptureActive());

            EngineRuntime.Shutdown();

            Assert.False(EngineRuntime.IsDisplayCaptureActive);
            Assert.Equal(0, NativeDisplayMethods.IsDisplayCaptureActive());
        }

        [StaFact]
        public void DisplayCaptureLifecycleShouldRepeat()
        {
            RunLifecycle();
            RunLifecycle();

            static void RunLifecycle()
            {
                EngineRuntime.Initialize(CreateRuntimeConfiguration());
                var displays = EngineRuntime.EnumerateDisplays();

                if (displays.Count > 0)
                {
                    EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
                    EngineRuntime.CreateMainScene();
                    EngineRuntime.ConfigureDisplayCapture(
                        new EngineDisplayCaptureConfiguration(displays[0].Id));
                    Assert.True(EngineRuntime.IsDisplayCaptureActive);
                }

                EngineRuntime.Shutdown();
                Assert.False(EngineRuntime.IsDisplayCaptureActive);
            }
        }

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration()
        {
            return new EngineRuntimeConfiguration(AppContext.BaseDirectory);
        }

        private static EngineVideoConfiguration CreateVideoConfiguration()
        {
            return new EngineVideoConfiguration(1280, 720, 1280, 720, 30, 1);
        }
    }
}
