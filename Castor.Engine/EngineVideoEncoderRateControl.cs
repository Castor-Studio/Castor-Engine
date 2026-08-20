namespace Castor.Engine
{
    /// <summary>
    /// Describes the rate control mode applied to the video encoder.
    /// </summary>
    public enum EngineVideoEncoderRateControl
    {
        /// <summary>Constant bitrate.</summary>
        ConstantBitrate = 0,

        /// <summary>Variable bitrate.</summary>
        VariableBitrate = 1,

        /// <summary>Constant quantization parameter.</summary>
        ConstantQp = 2,

        /// <summary>Constant rate factor.</summary>
        ConstantRateFactor = 3,
    }
}
