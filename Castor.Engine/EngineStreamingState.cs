namespace Castor.Engine
{
    /// <summary>Describes the current RTMP output lifecycle state.</summary>
    public enum EngineStreamingState
    {
        /// <summary>No streaming session is active.</summary>
        Idle = 0,
        /// <summary>The output is connecting to the ingest endpoint.</summary>
        Connecting = 1,
        /// <summary>The output is actively delivering media.</summary>
        Live = 2,
        /// <summary>The output is attempting to recover a lost connection.</summary>
        Reconnecting = 3,
        /// <summary>The output is terminating.</summary>
        Stopping = 4,
        /// <summary>The session ended with an observable failure.</summary>
        Failed = 5,
    }
}
