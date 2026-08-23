namespace Castor.Engine
{
    /// <summary>Describes the last asynchronous RTMP output failure.</summary>
    public enum EngineStreamingFailure
    {
        /// <summary>No failure has been reported.</summary>
        None = 0,
        /// <summary>The destination configuration is invalid.</summary>
        InvalidConfiguration = 44,
        /// <summary>No destination has been configured.</summary>
        NotConfigured = 45,
        /// <summary>The OBS custom RTMP service is unavailable.</summary>
        ServiceUnavailable = 46,
        /// <summary>The OBS RTMP output is unavailable.</summary>
        OutputUnavailable = 47,
        /// <summary>The OBS service could not be created.</summary>
        ServiceCreationFailed = 48,
        /// <summary>The OBS output could not be created.</summary>
        OutputCreationFailed = 49,
        /// <summary>The shared encoders are not configured.</summary>
        EncodersNotConfigured = 50,
        /// <summary>The main scene is not active.</summary>
        NoActiveScene = 51,
        /// <summary>A streaming session is already active.</summary>
        AlreadyActive = 52,
        /// <summary>No streaming session is active.</summary>
        NotActive = 53,
        /// <summary>The destination cannot change during a session.</summary>
        ReconfigurationWhileActive = 54,
        /// <summary>Recording and streaming cannot run simultaneously.</summary>
        ConflictingOutputActive = 55,
        /// <summary>OBS rejected the start request.</summary>
        StartFailed = 56,
        /// <summary>The ingest endpoint could not be reached.</summary>
        ConnectionFailed = 57,
        /// <summary>The server rejected the stream path or key.</summary>
        StreamRejected = 58,
        /// <summary>An established connection was lost.</summary>
        Disconnected = 59,
        /// <summary>Automatic reconnection attempts were exhausted.</summary>
        ReconnectExhausted = 60,
        /// <summary>The output does not support the requested media format.</summary>
        Unsupported = 61,
        /// <summary>A shared encoder failed.</summary>
        EncoderError = 62,
        /// <summary>The output did not terminate within the stop timeout.</summary>
        StopTimeout = 63,
        /// <summary>An unclassified output failure occurred.</summary>
        OutputError = 64,
    }
}
