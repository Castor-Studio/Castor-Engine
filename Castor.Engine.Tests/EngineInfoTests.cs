namespace Castor.Engine.Tests
{
    public sealed class EngineInfoTests
    {
        [Fact]
        public void AbiVersion_ShouldBeSupported()
        {
            Assert.Equal(
                EngineInfo.SupportedAbiVersion,
                EngineInfo.AbiVersion);
        }

        [Fact]
        public void Version_ShouldMatchInitialVersion()
        {
            Assert.Equal(
                "0.1.0-alpha.1",
                EngineInfo.Version);
        }

        [Fact]
        public void ValidateCompatibility_ShouldNotThrow()
        {
            var exception = Record.Exception(
                EngineInfo.ValidateCompatibility);

            Assert.Null(exception);
        }
    }
}
