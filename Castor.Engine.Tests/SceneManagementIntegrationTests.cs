namespace Castor.Engine.Tests
{
    /// <summary>
    /// End-to-end scene management flow against the real packaged OBS
    /// runtime: creating several scenes, switching between them with each
    /// transition type, and querying the active scene, exactly as a live
    /// operator would drive match coverage.
    /// </summary>
    public sealed class SceneManagementIntegrationTests : IDisposable
    {
        private static readonly string[] AllThreeSceneNames = { "wide", "closeup", "halftime" };
        private static readonly string[] RemainingSceneNamesAfterDelete = { "wide", "halftime" };

        public SceneManagementIntegrationTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [StaFact]
        public void SceneManagementShouldSupportCreatingListingAndSwitchingBetweenMultipleScenes()
        {
            EngineRuntime.Initialize(new EngineRuntimeConfiguration(AppContext.BaseDirectory));
            EngineRuntime.ConfigureVideo(
                new EngineVideoConfiguration(1280, 720, 1280, 720, 30, 1));

            // 1. Create at least three independently switchable scenes.
            EngineRuntime.CreateScene("wide");
            EngineRuntime.CreateScene("closeup");
            EngineRuntime.CreateScene("halftime");

            Assert.Equal(AllThreeSceneNames, EngineRuntime.GetSceneNames());
            Assert.Null(EngineRuntime.ActiveSceneName);
            Assert.False(EngineRuntime.HasActiveScene);

            // 2. The first switch after startup activates instantly.
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
            Assert.True(EngineRuntime.HasActiveScene);
            Assert.Equal("wide", EngineRuntime.ActiveSceneName);

            // 3. Switching with each transition type in turn updates the
            // active scene.
            EngineRuntime.SwitchScene(
                "closeup", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Fade, 200));
            Assert.Equal("closeup", EngineRuntime.ActiveSceneName);

            EngineRuntime.SwitchScene(
                "halftime", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Slide, 200));
            Assert.Equal("halftime", EngineRuntime.ActiveSceneName);

            EngineRuntime.SwitchScene(
                "wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Swipe, 200));
            Assert.Equal("wide", EngineRuntime.ActiveSceneName);

            // 4. Switching to an unknown scene fails cleanly and leaves the
            // active scene untouched.
            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.SwitchScene(
                    "ghost", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut)));
            Assert.Contains("SceneNotFound", exception.Message);
            Assert.Equal("wide", EngineRuntime.ActiveSceneName);

            // 5. The active scene cannot be deleted, but a background scene
            // can be renamed and deleted freely.
            Assert.Throws<InvalidOperationException>(() => EngineRuntime.DeleteScene("wide"));
            EngineRuntime.RenameScene("closeup", "close-up-camera");
            EngineRuntime.DeleteScene("close-up-camera");

            Assert.Equal(RemainingSceneNamesAfterDelete, EngineRuntime.GetSceneNames());
        }
    }
}
