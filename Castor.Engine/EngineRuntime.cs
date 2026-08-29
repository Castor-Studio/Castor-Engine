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
        /// Gets whether a scene is currently active and connected to the
        /// primary OBS video output.
        /// </summary>
        public static bool HasActiveScene =>
            NativeMethods.HasActiveScene() != 0;

        /// <summary>
        /// Gets the name of the currently active scene, or
        /// <see langword="null"/> when no scene is active.
        /// </summary>
        public static string? ActiveSceneName
        {
            get
            {
                var buffer = new byte[SceneNameBufferSize];
                return NativeMethods.GetActiveSceneName(buffer, (uint)buffer.Length) == 0
                    ? null
                    : DecodeSceneName(buffer);
            }
        }

        /// <summary>
        /// Gets whether the named scene currently uses an OBS display
        /// capture source as its visual source.
        /// </summary>
        public static bool IsDisplayCaptureActive(string sceneName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
            return NativeMethods.IsDisplayCaptureActive(sceneName) != 0;
        }

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
        /// Enumerates displays exposed by the loaded OBS monitor-capture
        /// source. An empty result represents a valid headless environment.
        /// </summary>
        public static IReadOnlyList<EngineDisplayInfo> EnumerateDisplays()
        {
            EngineInfo.ValidateCompatibility();
            var count = NativeMethods.GetDisplayCount();

            if (count == 0)
            {
                var error = GetLastErrorDetailOrNull();

                if (error is not null)
                {
                    throw new InvalidOperationException(
                        $"Failed to enumerate displays: {error}");
                }
            }

            var displays = new List<EngineDisplayInfo>((int)count);

            for (uint index = 0; index < count; index++)
            {
                var nativeInfo = new NativeEngineDisplayInfo
                {
                    StructSize = checked((uint)Marshal.SizeOf<NativeEngineDisplayInfo>()),
                };

                if (NativeMethods.GetDisplayAt(index, ref nativeInfo) == 0)
                {
                    throw CreateNativeOperationException("enumerate displays");
                }

                displays.Add(new EngineDisplayInfo(
                    FixedBufferInterop.Decode(nativeInfo.Id),
                    FixedBufferInterop.Decode(nativeInfo.Name),
                    nativeInfo.IsPrimary != 0));
            }

            return displays;
        }

        /// <summary>
        /// Replaces the named scene's current visual source with capture of
        /// the selected display. Repeating the same configuration is a
        /// no-op.
        /// </summary>
        public static void ConfigureDisplayCapture(
            EngineDisplayCaptureConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            EngineInfo.ValidateCompatibility();

            var nativeConfiguration = new NativeEngineDisplayCaptureConfiguration
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineDisplayCaptureConfiguration>()),
                CaptureCursor = configuration.CaptureCursor ? (byte)1 : (byte)0,
            };
            FixedBufferInterop.Encode(
                configuration.SceneName,
                nativeConfiguration.SceneName);
            FixedBufferInterop.Encode(
                configuration.DisplayId,
                nativeConfiguration.DisplayId);

            var result = NativeMethods.ConfigureDisplayCapture(in nativeConfiguration);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    "configure display capture",
                    result);
            }
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
        /// Gets whether a recording is currently active.
        /// </summary>
        public static bool IsRecordingActive =>
            NativeMethods.IsRecordingActive() != 0;

        /// <summary>Configures the single custom RTMP destination.</summary>
        public static void ConfigureStreaming(EngineStreamingConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            EngineInfo.ValidateCompatibility();

            nint serverUrl = nint.Zero;
            nint streamKey = nint.Zero;
            nint username = nint.Zero;
            nint password = nint.Zero;
            try
            {
                serverUrl = Marshal.StringToCoTaskMemUTF8(configuration.ServerUrl);
                streamKey = Marshal.StringToCoTaskMemUTF8(configuration.StreamKey);
                username = Marshal.StringToCoTaskMemUTF8(configuration.Username);
                password = Marshal.StringToCoTaskMemUTF8(configuration.Password);
                var nativeConfiguration = new NativeEngineStreamingConfiguration
                {
                    StructSize = checked((uint)Marshal.SizeOf<NativeEngineStreamingConfiguration>()),
                    ServerUrl = serverUrl,
                    StreamKey = streamKey,
                    UseAuthentication = configuration.UseAuthentication ? (byte)1 : (byte)0,
                    Username = username,
                    Password = password,
                    ReconnectRetryCount = configuration.ReconnectRetryCount,
                    ReconnectDelaySeconds = configuration.ReconnectDelaySeconds,
                };
                var result = NativeMethods.ConfigureStreaming(in nativeConfiguration);
                if (result != NativeEngineResult.Ok)
                {
                    throw CreateNativeOperationException("configure streaming", result);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(serverUrl);
                Marshal.FreeCoTaskMem(streamKey);
                Marshal.FreeCoTaskMem(username);
                Marshal.FreeCoTaskMem(password);
            }
        }

        /// <summary>Starts streaming with the configured destination and shared encoders.</summary>
        public static void StartStreaming()
        {
            EngineInfo.ValidateCompatibility();
            var result = NativeMethods.StartStreaming();
            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException("start streaming", result);
            }
        }

        /// <summary>Stops the active stream and waits for the RTMP output to terminate.</summary>
        public static void StopStreaming()
        {
            var result = NativeMethods.StopStreaming();
            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException("stop streaming", result);
            }
        }

        /// <summary>Gets the current asynchronous streaming status.</summary>
        public static EngineStreamingStatus GetStreamingStatus()
        {
            var nativeStatus = new NativeEngineStreamingStatus
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeEngineStreamingStatus>()),
            };
            var result = NativeMethods.GetStreamingStatus(ref nativeStatus);
            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException("retrieve streaming status", result);
            }
            return new EngineStreamingStatus(
                (EngineStreamingState)nativeStatus.State,
                (EngineStreamingFailure)nativeStatus.LastFailureCode,
                FixedBufferInterop.Decode(nativeStatus.LastFailureMessage));
        }

        /// <summary>Gets the current network delivery counters.</summary>
        public static EngineStreamingHealth GetStreamingHealth()
        {
            var nativeHealth = new NativeEngineStreamingHealth
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeEngineStreamingHealth>()),
            };
            var result = NativeMethods.GetStreamingHealth(ref nativeHealth);
            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException("retrieve streaming health", result);
            }
            return new EngineStreamingHealth(nativeHealth.TotalFrames, nativeHealth.DroppedFrames);
        }

        /// <summary>
        /// Gets engine-wide render/encode pipeline counters. Available
        /// whenever the engine is initialized, independent of whether
        /// recording or streaming is active.
        /// </summary>
        public static EngineRenderStats GetRenderStats()
        {
            var nativeStats = new NativeEngineRenderStats
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeEngineRenderStats>()),
            };
            var result = NativeMethods.GetRenderStats(ref nativeStats);
            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException("retrieve render stats", result);
            }
            return new EngineRenderStats(nativeStats.TotalFrames, nativeStats.LaggedFrames);
        }

        /// <summary>
        /// Starts recording the active main scene to an MKV file. If no
        /// video encoder is configured yet, one is created automatically in
        /// forced-software mode. The OBS ffmpeg_muxer output this uses
        /// requires an audio track to start, so the OBS audio subsystem and
        /// the AAC audio encoder are auto-configured the same way when
        /// neither is configured yet, and the resulting MKV file carries
        /// both a video and an audio track.
        /// </summary>
        /// <param name="configuration">The recording destination.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configuration"/> is null.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine, video subsystem, or main scene is not
        /// ready, the destination path is invalid, a recording is already
        /// active, the configured video encoder is a hardware encoder, or
        /// OBS cannot create, connect, or start the recording output.
        /// </exception>
        public static void StartRecording(EngineRecordingConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            EngineInfo.ValidateCompatibility();

            nint destinationPathPointer = nint.Zero;

            try
            {
                destinationPathPointer = Marshal.StringToCoTaskMemUTF8(configuration.DestinationPath);

                var nativeConfiguration = new NativeEngineRecordingConfiguration
                {
                    StructSize = checked(
                        (uint)Marshal.SizeOf<NativeEngineRecordingConfiguration>()),
                    DestinationPath = destinationPathPointer,
                };

                var result = NativeMethods.StartRecording(in nativeConfiguration);

                if (result != NativeEngineResult.Ok)
                {
                    throw CreateNativeOperationException(
                        "start the recording",
                        result);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(destinationPathPointer);
            }
        }

        /// <summary>
        /// Stops the active recording and blocks until OBS has finalized
        /// the MKV container before returning.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no recording is active.
        /// </exception>
        public static void StopRecording()
        {
            var result = NativeMethods.StopRecording();

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    "stop the recording",
                    result);
            }
        }

        /// <summary>
        /// Creates an empty named scene. Scene creation has no fixed count
        /// limit. A visual source can be added to it through
        /// <see cref="ConfigureDisplayCapture"/>.
        /// </summary>
        /// <param name="sceneName">The unique name of the new scene.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="sceneName"/> is null, empty, or
        /// whitespace.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine is not initialized, a scene with the same
        /// name already exists, or OBS cannot create the scene.
        /// </exception>
        public static void CreateScene(string sceneName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
            EngineInfo.ValidateCompatibility();

            var result = NativeMethods.CreateScene(sceneName);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    $"create scene '{sceneName}'",
                    result);
            }
        }

        /// <summary>
        /// Deletes a named scene and its owned resources.
        /// </summary>
        /// <param name="sceneName">The name of the scene to delete.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="sceneName"/> is null, empty, or
        /// whitespace.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no scene with that name exists, or when it is
        /// currently the active scene.
        /// </exception>
        public static void DeleteScene(string sceneName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);

            var result = NativeMethods.DeleteScene(sceneName);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    $"delete scene '{sceneName}'",
                    result);
            }
        }

        /// <summary>
        /// Renames a scene in place. If it is the active scene,
        /// <see cref="ActiveSceneName"/> reflects the new name afterward.
        /// </summary>
        /// <param name="oldName">The scene's current name.</param>
        /// <param name="newName">The scene's new, unique name.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="oldName"/> or <paramref name="newName"/>
        /// is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no scene named <paramref name="oldName"/> exists, or
        /// a different scene already uses <paramref name="newName"/>.
        /// </exception>
        public static void RenameScene(string oldName, string newName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
            ArgumentException.ThrowIfNullOrWhiteSpace(newName);

            var result = NativeMethods.RenameScene(oldName, newName);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    $"rename scene '{oldName}' to '{newName}'",
                    result);
            }
        }

        /// <summary>
        /// Enumerates the names of every created scene, in creation order.
        /// </summary>
        public static IReadOnlyList<string> GetSceneNames()
        {
            var count = NativeMethods.GetSceneCount();
            var names = new List<string>((int)count);
            var buffer = new byte[SceneNameBufferSize];

            for (uint index = 0; index < count; index++)
            {
                if (NativeMethods.GetSceneNameAt(index, buffer, (uint)buffer.Length) == 0)
                {
                    throw CreateNativeOperationException("enumerate scenes");
                }

                names.Add(DecodeSceneName(buffer));
            }

            return names;
        }

        /// <summary>
        /// Switches the active scene to <paramref name="sceneName"/>,
        /// applying the requested transition. The first switch after
        /// startup, and every switch using
        /// <see cref="EngineSceneTransitionType.Cut"/>, applies instantly.
        /// Switching to the already-active scene is a no-op.
        /// </summary>
        /// <param name="sceneName">The name of the scene to switch to.</param>
        /// <param name="transition">The transition to apply.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="sceneName"/> is null, empty, or
        /// whitespace.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="transition"/> is null.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the native and managed ABI versions are incompatible.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine is not initialized, no scene named
        /// <paramref name="sceneName"/> exists, the requested transition
        /// type is unavailable, or OBS fails to create or run it.
        /// </exception>
        public static void SwitchScene(string sceneName, EngineSceneTransitionConfiguration transition)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
            ArgumentNullException.ThrowIfNull(transition);
            EngineInfo.ValidateCompatibility();

            var nativeTransition = new NativeEngineSceneTransitionConfiguration
            {
                StructSize = checked(
                    (uint)Marshal.SizeOf<NativeEngineSceneTransitionConfiguration>()),
                Type = (uint)transition.TransitionType,
                DurationMs = transition.DurationMilliseconds,
            };

            var result = NativeMethods.SwitchScene(sceneName, in nativeTransition);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    $"switch to scene '{sceneName}'",
                    result);
            }
        }

        /// <summary>
        /// Reads the stored transform of a named scene's single visual item.
        /// The scene does not need to be active.
        /// </summary>
        /// <param name="sceneName">The name of the scene to inspect.</param>
        /// <returns>A mutable snapshot of the visual item's transform.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="sceneName"/> is null, empty, or
        /// whitespace.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine is not initialized, the scene does not
        /// exist, or the scene has no visual item.
        /// </exception>
        public static EngineSceneItemTransform GetSceneItemTransform(string sceneName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
            EngineInfo.ValidateCompatibility();

            var nativeTransform = new NativeEngineSceneItemTransform
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeEngineSceneItemTransform>()),
            };

            var result = NativeMethods.GetSceneItemTransform(sceneName, ref nativeTransform);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    $"read the visual item transform for scene '{sceneName}'",
                    result);
            }

            return new EngineSceneItemTransform
            {
                PositionX = nativeTransform.PositionX,
                PositionY = nativeTransform.PositionY,
                ScaleX = nativeTransform.ScaleX,
                ScaleY = nativeTransform.ScaleY,
                RotationDegrees = nativeTransform.RotationDegrees,
                BoundsMode = (EngineSceneItemBoundsMode)nativeTransform.BoundsMode,
                BoundsWidth = nativeTransform.BoundsWidth,
                BoundsHeight = nativeTransform.BoundsHeight,
                CropLeft = nativeTransform.CropLeft,
                CropTop = nativeTransform.CropTop,
                CropRight = nativeTransform.CropRight,
                CropBottom = nativeTransform.CropBottom,
            };
        }

        /// <summary>
        /// Atomically applies a complete transform snapshot to a named
        /// scene's visual item without recreating the item or its source.
        /// The scene does not need to be active.
        /// </summary>
        /// <param name="sceneName">The name of the scene to update.</param>
        /// <param name="transform">The complete transform snapshot.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="sceneName"/> is null, empty, or
        /// whitespace.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="transform"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the engine is not initialized, the scene or visual
        /// item does not exist, or the transform is invalid.
        /// </exception>
        public static void SetSceneItemTransform(string sceneName, EngineSceneItemTransform transform)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
            ArgumentNullException.ThrowIfNull(transform);
            EngineInfo.ValidateCompatibility();

            var nativeTransform = new NativeEngineSceneItemTransform
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeEngineSceneItemTransform>()),
                PositionX = transform.PositionX,
                PositionY = transform.PositionY,
                ScaleX = transform.ScaleX,
                ScaleY = transform.ScaleY,
                RotationDegrees = transform.RotationDegrees,
                BoundsMode = (NativeEngineSceneItemBoundsMode)transform.BoundsMode,
                BoundsWidth = transform.BoundsWidth,
                BoundsHeight = transform.BoundsHeight,
                CropLeft = transform.CropLeft,
                CropTop = transform.CropTop,
                CropRight = transform.CropRight,
                CropBottom = transform.CropBottom,
            };

            var result = NativeMethods.SetSceneItemTransform(sceneName, in nativeTransform);

            if (result != NativeEngineResult.Ok)
            {
                throw CreateNativeOperationException(
                    $"apply the visual item transform for scene '{sceneName}'",
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
            var nativeMessage = GetLastErrorDetailOrNull();

            return string.IsNullOrWhiteSpace(nativeMessage)
                ? "The native engine did not provide additional diagnostics."
                : nativeMessage;
        }

        private static string? GetLastErrorDetailOrNull()
        {
            var errorPointer = NativeMethods.GetLastError();
            var nativeMessage = errorPointer == nint.Zero
                ? null
                : Marshal.PtrToStringUTF8(errorPointer);

            return string.IsNullOrWhiteSpace(nativeMessage) ? null : nativeMessage;
        }

        private const int SceneNameBufferSize = 256;

        private static string DecodeSceneName(byte[] buffer)
        {
            var terminatorIndex = Array.IndexOf(buffer, (byte)0);
            var length = terminatorIndex < 0 ? buffer.Length : terminatorIndex;
            return System.Text.Encoding.UTF8.GetString(buffer, 0, length);
        }
    }
}
