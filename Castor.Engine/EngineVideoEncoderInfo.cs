namespace Castor.Engine
{
    /// <summary>
    /// Engine-owned metadata describing an available video encoder. Never
    /// carries a native or platform handle.
    /// </summary>
    public sealed class EngineVideoEncoderInfo
    {
        /// <summary>
        /// Initializes new video encoder metadata.
        /// </summary>
        public EngineVideoEncoderInfo(string id, string name, bool isHardware, bool isAvailable)
        {
            Id = id;
            Name = name;
            IsHardware = isHardware;
            IsAvailable = isAvailable;
        }

        /// <summary>
        /// Gets the engine-owned video encoder identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the human-readable encoder name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets whether this is a hardware encoder.
        /// </summary>
        public bool IsHardware { get; }

        /// <summary>
        /// Gets whether this encoder is currently available.
        /// </summary>
        public bool IsAvailable { get; }
    }
}
