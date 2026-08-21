namespace Castor.Engine
{
    /// <summary>
    /// Engine-owned metadata describing a display exposed by OBS. It never
    /// contains an OBS or platform-native handle.
    /// </summary>
    public sealed class EngineDisplayInfo
    {
        /// <summary>Initializes display metadata.</summary>
        public EngineDisplayInfo(string id, string name, bool isPrimary)
        {
            Id = id;
            Name = name;
            IsPrimary = isPrimary;
        }

        /// <summary>Gets the opaque identifier accepted by display capture.</summary>
        public string Id { get; }

        /// <summary>Gets the human-readable name supplied by OBS.</summary>
        public string Name { get; }

        /// <summary>Gets whether Windows identifies this as the primary display.</summary>
        public bool IsPrimary { get; }
    }
}
