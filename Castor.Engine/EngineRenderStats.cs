namespace Castor.Engine
{
    /// <summary>
    /// Represents engine-wide render/encode pipeline counters, independent
    /// of any single output.
    /// </summary>
    public sealed class EngineRenderStats
    {
        internal EngineRenderStats(ulong totalFrames, ulong laggedFrames)
        {
            TotalFrames = totalFrames;
            LaggedFrames = laggedFrames;
        }

        /// <summary>Gets the total number of frames the render loop has presented.</summary>
        public ulong TotalFrames { get; }
        /// <summary>Gets the frames the render loop could not produce in time.</summary>
        public ulong LaggedFrames { get; }
        /// <summary>Gets lagged frames divided by total frames.</summary>
        public double LaggedFrameRatio => TotalFrames == 0 ? 0 : (double)LaggedFrames / TotalFrames;
    }
}
