using System.Net.Sockets;

namespace Castor.Engine.Tests
{
    public sealed class RtmpIntegrationStaFactAttribute : StaFactAttribute
    {
        public RtmpIntegrationStaFactAttribute()
        {
            var server = Environment.GetEnvironmentVariable("CASTOR_RTMP_TEST_SERVER");
            var key = Environment.GetEnvironmentVariable("CASTOR_RTMP_TEST_KEY");
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(key))
            {
                Skip = "CASTOR_RTMP_TEST_SERVER and CASTOR_RTMP_TEST_KEY are not configured.";
                return;
            }
            if (!Uri.TryCreate(server, UriKind.Absolute, out var endpoint))
            {
                Skip = "CASTOR_RTMP_TEST_SERVER is not a valid absolute URL.";
                return;
            }
            try
            {
                using var client = new TcpClient();
                client.ConnectAsync(endpoint.Host, endpoint.IsDefaultPort ? 1935 : endpoint.Port)
                    .Wait(TimeSpan.FromSeconds(2));
                if (!client.Connected)
                {
                    Skip = $"No local RTMP ingest endpoint is reachable at {server}.";
                }
            }
            catch (Exception exception) when (exception is SocketException or AggregateException)
            {
                Skip = $"No local RTMP ingest endpoint is reachable at {server}.";
            }
        }
    }

    public sealed class StreamingIntegrationTests : IDisposable
    {
        public StreamingIntegrationTests() => EngineRuntime.Shutdown();
        public void Dispose() => EngineRuntime.Shutdown();

        [RtmpIntegrationStaFact]
        public async Task StreamingShouldDeliverFramesToLocalRtmpEndpoint()
        {
            var server = Environment.GetEnvironmentVariable("CASTOR_RTMP_TEST_SERVER");
            var key = Environment.GetEnvironmentVariable("CASTOR_RTMP_TEST_KEY");
            Assert.False(string.IsNullOrWhiteSpace(server));
            Assert.False(string.IsNullOrWhiteSpace(key));

            EngineRuntime.Initialize(new EngineRuntimeConfiguration(AppContext.BaseDirectory));
            EngineRuntime.ConfigureVideo(new EngineVideoConfiguration(1280, 720, 1280, 720, 30, 1));
            EngineRuntime.ConfigureAudio(new EngineAudioConfiguration());
            EngineRuntime.CreateMainScene();
            EngineRuntime.ConfigureVideoEncoder(new EngineVideoEncoderConfiguration(
                selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced,
                bitrate: 2500,
                rateControl: EngineVideoEncoderRateControl.ConstantBitrate,
                keyframeIntervalSeconds: 2));
            EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);
            EngineRuntime.ConfigureStreaming(new EngineStreamingConfiguration(server!, key!));

            EngineRuntime.StartStreaming();
            await WaitForStateAsync(EngineStreamingState.Live, TimeSpan.FromSeconds(15));

            Assert.Contains("StreamingAlreadyActive", Assert.Throws<InvalidOperationException>(
                EngineRuntime.StartStreaming).Message);
            Assert.Contains("StreamingReconfigurationWhileActive", Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureStreaming(
                    new EngineStreamingConfiguration(server!, key + "-replacement"))).Message);
            Assert.Contains("StreamingConflictingOutputActive", Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.StartRecording(
                    new EngineRecordingConfiguration(Path.Combine(Path.GetTempPath(), "stream-conflict.mkv")))).Message);

            await Task.Delay(TimeSpan.FromSeconds(3));

            var health = EngineRuntime.GetStreamingHealth();
            Assert.True(health.TotalFrames > 0);
            Assert.InRange(health.DroppedFrameRatio, 0, 0.05);

            EngineRuntime.StopStreaming();
            Assert.Equal(EngineStreamingState.Idle, EngineRuntime.GetStreamingStatus().State);
        }

        private static async Task WaitForStateAsync(EngineStreamingState expected, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var status = EngineRuntime.GetStreamingStatus();
                if (status.State == expected)
                {
                    return;
                }
                if (status.State == EngineStreamingState.Failed)
                {
                    Assert.Fail($"Streaming failed ({status.LastFailure}): {status.LastFailureMessage}");
                }
                await Task.Delay(100);
            }
            Assert.Fail($"Streaming did not reach {expected} before the timeout.");
        }

    }
}
