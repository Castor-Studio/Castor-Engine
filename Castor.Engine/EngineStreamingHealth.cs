namespace Castor.Engine
{
    /// <summary>Represents the current RTMP frame delivery counters.</summary>
    public sealed class EngineStreamingHealth
    {
        internal EngineStreamingHealth(ulong totalFrames, ulong droppedFrames)
        {
            TotalFrames = totalFrames;
            DroppedFrames = droppedFrames;
        }

        /// <summary>Gets the total number of processed frames.</summary>
        public ulong TotalFrames { get; }
        /// <summary>Gets the frames dropped due to network congestion.</summary>
        public ulong DroppedFrames { get; }
        /// <summary>Gets dropped frames divided by total frames.</summary>
        public double DroppedFrameRatio => TotalFrames == 0 ? 0 : (double)DroppedFrames / TotalFrames;
    }
}
