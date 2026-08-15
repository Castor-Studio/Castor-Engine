namespace Castor.Engine
{
    /// <summary>
    /// Describes the audio sample rate and speaker layout used by Castor Engine.
    /// </summary>
    public sealed class EngineAudioConfiguration
    {
        /// <summary>
        /// Initializes a new audio configuration. A <paramref name="sampleRate"/>
        /// of <c>0</c> and a <paramref name="speakerLayout"/> of
        /// <see cref="EngineSpeakerLayout.Default"/> resolve to the documented
        /// defaults of 48 kHz and stereo.
        /// </summary>
        public EngineAudioConfiguration(
            uint sampleRate = 0,
            EngineSpeakerLayout speakerLayout = EngineSpeakerLayout.Default)
        {
            SampleRate = sampleRate;
            SpeakerLayout = speakerLayout;
        }

        /// <summary>
        /// Gets the audio sample rate, in Hz.
        /// </summary>
        public uint SampleRate { get; }

        /// <summary>
        /// Gets the speaker layout.
        /// </summary>
        public EngineSpeakerLayout SpeakerLayout { get; }
    }
}
