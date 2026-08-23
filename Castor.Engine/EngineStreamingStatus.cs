namespace Castor.Engine
{
    /// <summary>Represents a snapshot of streaming state and its last failure.</summary>
    public sealed class EngineStreamingStatus
    {
        internal EngineStreamingStatus(
            EngineStreamingState state,
            EngineStreamingFailure lastFailure,
            string lastFailureMessage)
        {
            State = state;
            LastFailure = lastFailure;
            LastFailureMessage = lastFailureMessage;
        }

        /// <summary>Gets the current lifecycle state.</summary>
        public EngineStreamingState State { get; }
        /// <summary>Gets the last asynchronous failure classification.</summary>
        public EngineStreamingFailure LastFailure { get; }
        /// <summary>Gets the sanitized native diagnostic.</summary>
        public string LastFailureMessage { get; }
        /// <summary>Gets whether this snapshot contains a failure.</summary>
        public bool HasFailure => LastFailure != EngineStreamingFailure.None;
    }
}
