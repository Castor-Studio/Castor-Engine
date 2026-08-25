namespace Castor.Engine
{
    /// <summary>
    /// The transition applied when switching the active scene.
    /// </summary>
    public enum EngineSceneTransitionType
    {
        /// <summary>Switches instantly. Ignores the requested duration.</summary>
        Cut = 0,

        /// <summary>Crossfades from the current scene to the new one.</summary>
        Fade = 1,

        /// <summary>Slides the new scene in over the current one.</summary>
        Slide = 2,

        /// <summary>Swipes the new scene in over the current one.</summary>
        Swipe = 3,
    }

    /// <summary>
    /// Describes the transition applied by
    /// <see cref="EngineRuntime.SwitchScene"/>.
    /// </summary>
    public sealed class EngineSceneTransitionConfiguration
    {
        /// <summary>Initializes a scene transition request.</summary>
        /// <param name="transitionType">The transition type.</param>
        /// <param name="durationMilliseconds">
        /// The transition duration, in milliseconds. Ignored for
        /// <see cref="EngineSceneTransitionType.Cut"/>.
        /// </param>
        public EngineSceneTransitionConfiguration(
            EngineSceneTransitionType transitionType,
            uint durationMilliseconds = 300)
        {
            TransitionType = transitionType;
            DurationMilliseconds = durationMilliseconds;
        }

        /// <summary>Gets the transition type.</summary>
        public EngineSceneTransitionType TransitionType { get; }

        /// <summary>Gets the transition duration, in milliseconds.</summary>
        public uint DurationMilliseconds { get; }
    }
}
