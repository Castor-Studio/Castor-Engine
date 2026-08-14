namespace Castor.Engine
{
    /// <summary>
    /// Describes the video resolution and frame rate used by Castor Engine.
    /// </summary>
    public sealed class EngineVideoConfiguration
    {
        /// <summary>
        /// Initializes a new video configuration.
        /// </summary>
        public EngineVideoConfiguration(
            uint baseWidth,
            uint baseHeight,
            uint outputWidth,
            uint outputHeight,
            uint framesPerSecondNumerator,
            uint framesPerSecondDenominator)
        {
            BaseWidth = baseWidth;
            BaseHeight = baseHeight;
            OutputWidth = outputWidth;
            OutputHeight = outputHeight;
            FramesPerSecondNumerator = framesPerSecondNumerator;
            FramesPerSecondDenominator = framesPerSecondDenominator;
        }

        /// <summary>
        /// Gets the base compositing width in pixels.
        /// </summary>
        public uint BaseWidth { get; }

        /// <summary>
        /// Gets the base compositing height in pixels.
        /// </summary>
        public uint BaseHeight { get; }

        /// <summary>
        /// Gets the scaled output width in pixels.
        /// </summary>
        public uint OutputWidth { get; }

        /// <summary>
        /// Gets the scaled output height in pixels.
        /// </summary>
        public uint OutputHeight { get; }

        /// <summary>
        /// Gets the output frame-rate numerator.
        /// </summary>
        public uint FramesPerSecondNumerator { get; }

        /// <summary>
        /// Gets the output frame-rate denominator.
        /// </summary>
        public uint FramesPerSecondDenominator { get; }
    }
}
