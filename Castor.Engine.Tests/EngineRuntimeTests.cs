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
            EngineRuntime.Initialize();

            Assert.True(EngineRuntime.IsInitialized);
        }

        [StaFact]
        public void InitializeShouldBeIdempotent()
        {
            EngineRuntime.Initialize();
            EngineRuntime.Initialize();

            Assert.True(EngineRuntime.IsInitialized);
        }

        [StaFact]
        public void ShutdownShouldBeIdempotent()
        {
            EngineRuntime.Initialize();

            EngineRuntime.Shutdown();
            EngineRuntime.Shutdown();

            Assert.False(EngineRuntime.IsInitialized);
        }
    }
}
