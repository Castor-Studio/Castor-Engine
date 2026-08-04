namespace Castor.Engine.Tests
{
    public sealed class EngineRuntimeTests : IDisposable
    {
        public EngineRuntimeTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [StaFact]
        public void InitializeShouldInitializeRuntime()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            Assert.True(EngineRuntime.IsInitialized);
        }

        [StaFact]
        public void InitializeShouldBeIdempotent()
        {
            var configuration = CreateRuntimeConfiguration();

            EngineRuntime.Initialize(configuration);
            var firstModuleCount = EngineRuntime.LoadedModuleCount;

            EngineRuntime.Initialize(configuration);
            var secondModuleCount = EngineRuntime.LoadedModuleCount;

            Assert.True(EngineRuntime.IsInitialized);
            Assert.NotEqual(0U, firstModuleCount);
            Assert.Equal(firstModuleCount, secondModuleCount);
        }

        [StaFact]
        public void InitializeShouldLoadPackagedObsModule()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            Assert.NotEqual(0U, EngineRuntime.LoadedModuleCount);
            Assert.True(EngineRuntime.IsModuleLoaded("image-source"));
        }

        [StaFact]
        public void InitializeShouldRejectInvalidRuntimeRoot()
        {
            var invalidRuntimeRoot = Path.Combine(
                AppContext.BaseDirectory,
                "missing-runtime");
            var configuration = new EngineRuntimeConfiguration(
                invalidRuntimeRoot);

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.Initialize(configuration));

            Assert.Contains("InvalidRuntime", exception.Message);
            Assert.Contains("missing-runtime", exception.Message);
            Assert.False(EngineRuntime.IsInitialized);
        }

        [StaFact]
        public void ShutdownShouldBeIdempotent()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            EngineRuntime.Shutdown();
            EngineRuntime.Shutdown();

            Assert.False(EngineRuntime.IsInitialized);
            Assert.Equal(0U, EngineRuntime.LoadedModuleCount);
        }

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration()
        {
            return new EngineRuntimeConfiguration(AppContext.BaseDirectory);
        }
    }
}
