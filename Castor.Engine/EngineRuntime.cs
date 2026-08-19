using System.Runtime.InteropServices;
using Castor.Engine.Interop;

namespace Castor.Engine
{
    /// <summary>
    /// Manages the lifecycle of the OBS runtime.
    /// </summary>
    public static class EngineRuntime
    {
        /// <summary>
        /// Gets whether the OBS runtime is initialized.
        /// </summary>
        public static bool IsInitialized =>
            NativeMethods.IsInitialized() != 0;

        /// <summary>
        /// Gets the number of OBS modules loaded by Castor Engine.
        /// </summary>
        public static uint LoadedModuleCount =>
            NativeMethods.GetLoadedModuleCount();

        /// <summary>
        /// Gets whether the OBS video subsystem is configured.
        /// </summary>
        public static bool IsVideoConfigured =>
            NativeMethods.IsVideoConfigured() != 0;

        /// <summary>
        /// Gets whether the OBS audio subsystem is configured.
        /// </summary>
        public static bool IsAudioConfigured =>
            NativeMethods.IsAudioConfigured() != 0;

        /// <summary>
        /// Gets whether the engine-owned main scene exists and is connected
        /// to the primary OBS video output.
        /// </summary>
        public static bool HasActiveScene =>
            NativeMethods.HasActiveScene() != 0;

        /// <summary>
        /// Initializes the OBS runtime and loads the packaged OBS modules.
        /// </summary>
        /// <param name="configuration">
        /// The packaged runtime location and OBS locale.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configuration"/> is null.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the OBS runtime or its modules cannot be initialized.
        /// </exception>
        public static void Initialize(EngineRuntimeConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            EngineInfo.ValidateCompatibility();

            var runtimeRoot = Path.GetFullPath(configuration.RuntimeRoot);
            var locale = string.IsNullOrWhiteSpace(configuration.Locale)
                ? "en-US"
                : configuration.Locale;

            nint runtimeRootPointer = nint.Zero;
            nint localePointer = nint.Zero;

            try
            {
                runtimeRootPointer = Marshal.StringToCoTaskMemUTF8(runtimeRoot);
                localePointer = Marshal.StringToCoTaskMemUTF8(locale);

                var nativeConfiguration = new NativeEngineConfiguration
                {
                    StructSize = checked(
                        (uint)Marshal.SizeOf<NativeEngineConfiguration>()),
                    RuntimeRoot = runtimeRootPointer,
                    Locale = localePointer,
                };

                var result = NativeMethods.Initialize(in nativeConfiguration);

                if (result != NativeEngineResult.Ok)
                {
                    throw CreateNativeOperationException(
                        "initialize the OBS runtime",
                        result);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(localePointer);
                Marshal.FreeCoTaskMem(runtimeRootPointer);
            }
        }

        /// <summary>
        /// Gets whether an OBS module with the specified name is loaded.
        /// </summary>
        /// <param name="moduleName">The OBS module name.</param>
        /// <returns>
        /// <see langword="true"/> when the module is loaded; otherwise,
        /// <see langword="false"/>.
        /// </returns>
        public static bool IsModuleLoaded(string moduleName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
            return NativeMethods.IsModuleLoaded(moduleName) != 0;
        }

        /// <summary>
        /// Configures the OBS video subsystem for rendering and encoding.
        /// </summary>
        /// <param name="configuration">
        /// The base resolution, output resolution, and frame rate.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configuration"/> is null.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine is not initialized or OBS cannot configure
        /// video with the requested settings.
        /// </exception>
        public static void ConfigureVideo(
            EngineVideoConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            EngineInfo.ValidateCompatibility();

            var nativeConfiguration = new NativeEngineVideoConfiguration
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineVideoConfiguration>()),
                BaseWidth = configuration.BaseWidth,
                BaseHeight = configuration.BaseHeight,
                OutputWidth = configuration.OutputWidth,
                OutputHeight = configuration.OutputHeight,
                FramesPerSecondNumerator =
                    configuration.FramesPerSecondNumerator,
                FramesPerSecondDenominator =
                    configuration.FramesPerSecondDenominator,
            };

            var result = NativeMethods.ConfigureVideo(in nativeConfiguration);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    "configure OBS video",
                    result);
            }
        }

        /// <summary>
        /// Configures the OBS audio subsystem. Requires no physical playback
        /// or capture device.
        /// </summary>
        /// <param name="configuration">
        /// The audio sample rate and speaker layout.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configuration"/> is null.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine is not initialized, the requested
        /// configuration is invalid or unsupported, OBS cannot configure
        /// audio with the requested settings, or the audio subsystem is
        /// already configured with different settings. OBS does not support
        /// runtime audio reconfiguration, so shut down the engine first to
        /// apply different audio settings.
        /// </exception>
        public static void ConfigureAudio(EngineAudioConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            EngineInfo.ValidateCompatibility();

            var nativeConfiguration = new NativeEngineAudioConfiguration
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineAudioConfiguration>()),
                SampleRate = configuration.SampleRate,
                SpeakerLayout = (uint)configuration.SpeakerLayout,
            };

            var result = NativeMethods.ConfigureAudio(in nativeConfiguration);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    "configure OBS audio",
                    result);
            }
        }

        /// <summary>
        /// Gets the effective audio configuration.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the audio subsystem is not configured.
        /// </exception>
        public static EngineAudioConfiguration GetAudioConfiguration()
        {
            var nativeConfiguration = new NativeEngineAudioConfiguration
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineAudioConfiguration>()),
            };

            if (NativeMethods.GetAudioConfig(ref nativeConfiguration) == 0)
            {
                throw CreateNativeOperationException(
                    "retrieve the audio configuration");
            }

            return new EngineAudioConfiguration(
                nativeConfiguration.SampleRate,
                (EngineSpeakerLayout)nativeConfiguration.SpeakerLayout);
        }

        /// <summary>
        /// Gets whether the video encoder is created and bound to the video
        /// pipeline.
        /// </summary>
        public static bool IsVideoEncoderConfigured =>
            NativeMethods.IsVideoEncoderConfigured() != 0;

        /// <summary>
        /// Gets the diagnostic describing why the video encoder fell back
        /// from hardware to software on the current configuration, or an
        /// empty string when it did not fall back.
        /// </summary>
        public static string VideoEncoderFallbackNotice
        {
            get
            {
                var pointer = NativeMethods.GetVideoEncoderFallbackNotice();
                return pointer == nint.Zero
                    ? string.Empty
                    : Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
            }
        }

        /// <summary>
        /// Enumerates the video encoders available in the current OBS
        /// runtime.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine is not initialized or enumeration fails.
        /// </exception>
        public static IReadOnlyList<EngineVideoEncoderInfo> EnumerateVideoEncoders()
        {
            var count = NativeMethods.GetVideoEncoderCount();
            var encoders = new List<EngineVideoEncoderInfo>((int)count);

            for (uint index = 0; index < count; index++)
            {
                var nativeInfo = new NativeEngineVideoEncoderInfo
                {
                    StructSize = checked(
                        (uint)Marshal.SizeOf<NativeEngineVideoEncoderInfo>()),
                };

                if (NativeMethods.GetVideoEncoderAt(index, ref nativeInfo) == 0)
                {
                    throw CreateNativeOperationException("enumerate video encoders");
                }

                encoders.Add(ToVideoEncoderInfo(nativeInfo));
            }

            return encoders;
        }

        /// <summary>
        /// Selects, creates, and binds the video encoder to the OBS video
        /// pipeline. Requires the video subsystem to already be configured.
        /// </summary>
        /// <param name="configuration">
        /// The video encoder selection and settings.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configuration"/> is null.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine or video subsystem is not initialized,
        /// the requested configuration or encoder identifier is invalid,
        /// the video encoder is already configured with different settings,
        /// or OBS cannot create or bind the encoder. A hardware-preferred
        /// or automatic selection that falls back to software never fails
        /// silently: check <see cref="VideoEncoderFallbackNotice"/> after a
        /// successful call.
        /// </exception>
        public static void ConfigureVideoEncoder(
            EngineVideoEncoderConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            EngineInfo.ValidateCompatibility();

            var nativeConfiguration = new NativeEngineVideoEncoderConfiguration
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineVideoEncoderConfiguration>()),
                SelectionMode = (uint)configuration.SelectionMode,
                Bitrate = configuration.Bitrate,
                RateControl = (uint)configuration.RateControl,
                KeyframeIntervalSeconds = configuration.KeyframeIntervalSeconds,
                AudioBitrate = configuration.AudioBitrate,
                AudioTrackIndex = configuration.AudioTrackIndex,
            };
            FixedBufferInterop.Encode(configuration.EncoderId, nativeConfiguration.EncoderId);
            FixedBufferInterop.Encode(configuration.Preset, nativeConfiguration.Preset);
            FixedBufferInterop.Encode(configuration.Profile, nativeConfiguration.Profile);

            var result = NativeMethods.ConfigureVideoEncoder(in nativeConfiguration);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    "configure the video encoder",
                    result);
            }
        }

        /// <summary>
        /// Gets the effective video encoder configuration.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the video encoder is not configured.
        /// </exception>
        public static EngineVideoEncoderConfiguration GetVideoEncoderConfiguration()
        {
            var nativeConfiguration = new NativeEngineVideoEncoderConfiguration
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineVideoEncoderConfiguration>()),
            };

            if (NativeMethods.GetVideoEncoderConfig(ref nativeConfiguration) == 0)
            {
                throw CreateNativeOperationException(
                    "retrieve the video encoder configuration");
            }

            return new EngineVideoEncoderConfiguration(
                (EngineVideoEncoderSelectionMode)nativeConfiguration.SelectionMode,
                FixedBufferInterop.Decode(nativeConfiguration.EncoderId),
                nativeConfiguration.Bitrate,
                (EngineVideoEncoderRateControl)nativeConfiguration.RateControl,
                nativeConfiguration.KeyframeIntervalSeconds,
                FixedBufferInterop.Decode(nativeConfiguration.Preset),
                FixedBufferInterop.Decode(nativeConfiguration.Profile),
                nativeConfiguration.AudioBitrate,
                nativeConfiguration.AudioTrackIndex);
        }

        /// <summary>
        /// Gets the video encoder actually selected by the last successful
        /// <see cref="ConfigureVideoEncoder"/> call.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the video encoder is not configured.
        /// </exception>
        public static EngineVideoEncoderInfo GetSelectedVideoEncoder()
        {
            var nativeInfo = new NativeEngineVideoEncoderInfo
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineVideoEncoderInfo>()),
            };

            if (NativeMethods.GetSelectedVideoEncoder(ref nativeInfo) == 0)
            {
                throw CreateNativeOperationException(
                    "retrieve the selected video encoder");
            }

            return ToVideoEncoderInfo(nativeInfo);
        }

        private static EngineVideoEncoderInfo ToVideoEncoderInfo(NativeEngineVideoEncoderInfo nativeInfo)
        {
            return new EngineVideoEncoderInfo(
                FixedBufferInterop.Decode(nativeInfo.Id),
                FixedBufferInterop.Decode(nativeInfo.Name),
                nativeInfo.IsHardware != 0,
                nativeInfo.IsAvailable != 0);
        }

        /// <summary>
        /// Gets whether the audio encoder is created and bound to the
        /// audio pipeline. Independent of the video encoder: neither
        /// requires the other to be configured first.
        /// </summary>
        public static bool IsAudioEncoderConfigured =>
            NativeMethods.IsAudioEncoderConfigured() != 0;

        /// <summary>
        /// Creates the AAC audio encoder and binds it to the OBS audio
        /// pipeline. Requires the OBS audio subsystem
        /// (<see cref="ConfigureAudio"/>) to already be configured.
        /// </summary>
        /// <param name="audioBitrate">The audio bitrate, in kbps.</param>
        /// <param name="audioTrackIndex">
        /// The audio track (OBS mixer) index the encoder binds to.
        /// </param>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine or audio subsystem is not initialized,
        /// the requested bitrate or track index is invalid, the audio
        /// encoder is already configured with different settings, or OBS
        /// cannot create or bind the encoder.
        /// </exception>
        public static void ConfigureAudioEncoder(uint audioBitrate, uint audioTrackIndex)
        {
            EngineInfo.ValidateCompatibility();

            var nativeConfiguration = new NativeEngineVideoEncoderConfiguration
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineVideoEncoderConfiguration>()),
                AudioBitrate = audioBitrate,
                AudioTrackIndex = audioTrackIndex,
            };

            var result = NativeMethods.ConfigureAudioEncoder(in nativeConfiguration);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    "configure the audio encoder",
                    result);
            }
        }

        /// <summary>
        /// Gets the audio encoder actually selected by the last successful
        /// <see cref="ConfigureAudioEncoder"/> call.
        /// <see cref="EngineVideoEncoderInfo.IsHardware"/> is always
        /// <see langword="false"/> for it.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the audio encoder is not configured.
        /// </exception>
        public static EngineVideoEncoderInfo GetSelectedAudioEncoder()
        {
            var nativeInfo = new NativeEngineVideoEncoderInfo
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineVideoEncoderInfo>()),
            };

            if (NativeMethods.GetSelectedAudioEncoder(ref nativeInfo) == 0)
            {
                throw CreateNativeOperationException(
                    "retrieve the selected audio encoder");
            }

            return ToVideoEncoderInfo(nativeInfo);
        }

        /// <summary>
        /// Gets an opaque, engine-owned handle to the configured video
        /// encoder, for a future native output feature to bind. There is
        /// no managed API to do anything with this value directly; it is
        /// exposed now so that adding output support later does not
        /// require another ABI bump just to add retrieval.
        /// </summary>
        /// <returns>
        /// A non-zero, opaque handle, or <see cref="nint.Zero"/> when the
        /// video encoder is not configured. The handle becomes invalid
        /// when the engine shuts down or the video encoder is
        /// reconfigured.
        /// </returns>
        public static nint GetVideoEncoderHandle()
        {
            return NativeMethods.GetVideoEncoderHandle();
        }

        /// <summary>
        /// Gets an opaque, engine-owned handle to the configured audio
        /// encoder, for a future native output feature to bind. Same
        /// contract as <see cref="GetVideoEncoderHandle"/>.
        /// </summary>
        /// <returns>
        /// A non-zero, opaque handle, or <see cref="nint.Zero"/> when the
        /// audio encoder is not configured.
        /// </returns>
        public static nint GetAudioEncoderHandle()
        {
            return NativeMethods.GetAudioEncoderHandle();
        }

        /// <summary>
        /// Creates the engine-owned main scene, adds a solid-color source,
        /// and connects it to the primary OBS video output.
        /// </summary>
        /// <remarks>
        /// Repeated calls are idempotent while the main scene remains active.
        /// </remarks>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine or video subsystem is not initialized, or
        /// when OBS cannot create and activate the main scene.
        /// </exception>
        public static void CreateMainScene()
        {
            EngineInfo.ValidateCompatibility();

            var result = NativeMethods.CreateMainScene();

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    "create and activate the main OBS scene",
                    result);
            }
        }

        /// <summary>
        /// Shuts down the OBS runtime.
        /// </summary>
        public static void Shutdown()
        {
            NativeMethods.Shutdown();
        }

        private static InvalidOperationException CreateNativeOperationException(
            string operation,
            NativeEngineResult result)
        {
            return new InvalidOperationException(
                $"Failed to {operation} ({result}): {GetLastErrorDetail()}");
        }

        private static InvalidOperationException CreateNativeOperationException(
            string operation)
        {
            return new InvalidOperationException(
                $"Failed to {operation}: {GetLastErrorDetail()}");
        }

        private static string GetLastErrorDetail()
        {
            var errorPointer = NativeMethods.GetLastError();
            var nativeMessage = errorPointer == nint.Zero
                ? null
                : Marshal.PtrToStringUTF8(errorPointer);

            return string.IsNullOrWhiteSpace(nativeMessage)
                ? "The native engine did not provide additional diagnostics."
                : nativeMessage;
        }
    }
}
