namespace Castor.Engine.Tests
{
    public sealed class EngineInfoTests
    {
        [Fact]
        public void AbiVersionShouldBeSupported()
        {
            Assert.Equal(
                EngineInfo.SupportedAbiVersion,
                EngineInfo.AbiVersion);
        }

        [Fact]
        public void VersionShouldMatchInitialVersion()
        {
            Assert.Equal(
                "0.1.0-alpha.1",
                EngineInfo.Version);
        }

        [Fact]
        public void ValidateCompatibilityShouldNotThrow()
        {
            var exception = Record.Exception(
                EngineInfo.ValidateCompatibility);

            Assert.Null(exception);
        }

        [Fact]
        public void ObsVersionShouldBeAvailable()
        {
            Assert.False(
                string.IsNullOrWhiteSpace(EngineInfo.ObsVersion));
        }
    }
}
