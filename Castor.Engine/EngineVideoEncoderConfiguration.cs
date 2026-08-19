namespace Castor.Engine
{
    /// <summary>
    /// Describes how Castor Engine should select, create, and configure the
    /// video encoder.
    /// </summary>
    public sealed class EngineVideoEncoderConfiguration
    {
        /// <summary>
        /// Initializes a new video encoder configuration.
        /// </summary>
        /// <param name="selectionMode">
        /// How to select an encoder when <paramref name="encoderId"/> is
        /// empty.
        /// </param>
        /// <param name="encoderId">
        /// An engine-owned video encoder identifier, as returned by
        /// <see cref="EngineRuntime.EnumerateVideoEncoders"/>. When empty,
        /// <paramref name="selectionMode"/> decides the encoder; when set,
        /// this identifier is used directly with no fallback.
        /// </param>
        /// <param name="bitrate">The video bitrate, in kbps.</param>
        /// <param name="rateControl">The rate control mode.</param>
        /// <param name="keyframeIntervalSeconds">
        /// The keyframe interval, in seconds. Zero lets the encoder apply
        /// its own default.
        /// </param>
        /// <param name="preset">
        /// An encoder-specific preset name. Empty leaves the encoder's own
        /// default in place.
        /// </param>
        /// <param name="profile">
        /// An encoder-specific profile name. Empty leaves the encoder's own
        /// default in place.
        /// </param>
        /// <param name="audioBitrate">
        /// The audio bitrate, in kbps. Reserved: has no effect until the
        /// audio encoder is introduced in a follow-up feature.
        /// </param>
        /// <param name="audioTrackIndex">
        /// The audio track index the audio encoder will be bound to.
        /// Reserved: has no effect until the audio encoder is introduced in
        /// a follow-up feature.
        /// </param>
        public EngineVideoEncoderConfiguration(
            EngineVideoEncoderSelectionMode selectionMode = EngineVideoEncoderSelectionMode.Automatic,
            string encoderId = "",
            uint bitrate = 2500,
            EngineVideoEncoderRateControl rateControl = EngineVideoEncoderRateControl.ConstantBitrate,
            uint keyframeIntervalSeconds = 0,
            string preset = "",
            string profile = "",
            uint audioBitrate = 0,
            uint audioTrackIndex = 0)
        {
            SelectionMode = selectionMode;
            EncoderId = encoderId;
            Bitrate = bitrate;
            RateControl = rateControl;
            KeyframeIntervalSeconds = keyframeIntervalSeconds;
            Preset = preset;
            Profile = profile;
            AudioBitrate = audioBitrate;
            AudioTrackIndex = audioTrackIndex;
        }

        /// <summary>
        /// Gets how to select an encoder when <see cref="EncoderId"/> is
        /// empty.
        /// </summary>
        public EngineVideoEncoderSelectionMode SelectionMode { get; }

        /// <summary>
        /// Gets the explicit engine-owned video encoder identifier, or an
        /// empty string when <see cref="SelectionMode"/> should decide.
        /// </summary>
        public string EncoderId { get; }

        /// <summary>
        /// Gets the video bitrate, in kbps.
        /// </summary>
        public uint Bitrate { get; }

        /// <summary>
        /// Gets the rate control mode.
        /// </summary>
        public EngineVideoEncoderRateControl RateControl { get; }

        /// <summary>
        /// Gets the keyframe interval, in seconds.
        /// </summary>
        public uint KeyframeIntervalSeconds { get; }

        /// <summary>
        /// Gets the encoder-specific preset name.
        /// </summary>
        public string Preset { get; }

        /// <summary>
        /// Gets the encoder-specific profile name.
        /// </summary>
        public string Profile { get; }

        /// <summary>
        /// Gets the audio bitrate, in kbps. Reserved for a follow-up
        /// feature; has no effect on the video encoder.
        /// </summary>
        public uint AudioBitrate { get; }

        /// <summary>
        /// Gets the audio track index. Reserved for a follow-up feature;
        /// has no effect on the video encoder.
        /// </summary>
        public uint AudioTrackIndex { get; }
    }
}
