namespace Castor.Engine
{
    /// <summary>Describes a single custom RTMP destination.</summary>
    public sealed class EngineStreamingConfiguration
    {
        /// <summary>Initializes a custom RTMP destination.</summary>
        public EngineStreamingConfiguration(
            string serverUrl,
            string streamKey,
            bool useAuthentication = false,
            string username = "",
            string password = "",
            uint reconnectRetryCount = 20,
            uint reconnectDelaySeconds = 2)
        {
            ServerUrl = serverUrl;
            StreamKey = streamKey;
            UseAuthentication = useAuthentication;
            Username = username;
            Password = password;
            ReconnectRetryCount = reconnectRetryCount;
            ReconnectDelaySeconds = reconnectDelaySeconds;
        }

        /// <summary>Gets the absolute RTMP or RTMPS server URL.</summary>
        public string ServerUrl { get; }
        /// <summary>Gets the secret stream key.</summary>
        public string StreamKey { get; }
        /// <summary>Gets whether separate username/password authentication is enabled.</summary>
        public bool UseAuthentication { get; }
        /// <summary>Gets the optional authentication username.</summary>
        public string Username { get; }
        /// <summary>Gets the optional authentication password.</summary>
        public string Password { get; }
        /// <summary>Gets the maximum automatic reconnect attempts.</summary>
        public uint ReconnectRetryCount { get; }
        /// <summary>Gets the initial reconnect delay in seconds.</summary>
        public uint ReconnectDelaySeconds { get; }
    }
}
