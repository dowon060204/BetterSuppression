using System;
using HarmonyLib;
using Dissonance.Audio.Capture;
using Dissonance.Audio.Playback;

namespace BetterSuppression
{
    [HarmonyPatch]
    public static class DissonancePatch
    {
        private static VoiceAudioProcessor _processor;
        private static VoiceAudioProcessor _remoteProcessor;

        public static void Initialize()
        {
            _processor = new VoiceAudioProcessor();
            _remoteProcessor = new VoiceAudioProcessor();
        }

        public static VoiceAudioProcessor Processor
        {
            get { return _processor; }
        }

        public static VoiceAudioProcessor RemoteProcessor
        {
            get { return _remoteProcessor; }
        }

        /// <summary>
        /// Local Player Microphone capture patch.
        /// Filters outgoing microphone PCM audio through RNNoise & Noise Gate.
        /// </summary>
        [HarmonyPatch(typeof(BasicMicrophoneCapture), "ConsumeSamples")]
        [HarmonyPostfix]
        public static void ConsumeSamplesPostfix(ArraySegment<float> samples)
        {
            if (_processor == null || !_processor.IsEnabled)
                return;

            if (samples.Array != null)
            {
                _processor.ProcessAudio(samples.Array, samples.Offset, samples.Count);
            }
        }

        /// <summary>
        /// Remote Players Voice playback patch.
        /// Filters incoming voice audio from other players before outputting to local speaker/headset.
        /// </summary>
        [HarmonyPatch(typeof(SamplePlaybackComponent), "OnAudioFilterRead")]
        [HarmonyPostfix]
        public static void OnAudioFilterReadPostfix(float[] data, int channels)
        {
            if (_remoteProcessor == null || !_remoteProcessor.IsEnabled || data == null || data.Length == 0)
                return;

            _remoteProcessor.ProcessAudio(data, 0, data.Length);
        }

        public static void Cleanup()
        {
            if (_processor != null)
            {
                _processor.Dispose();
                _processor = null;
            }
            if (_remoteProcessor != null)
            {
                _remoteProcessor.Dispose();
                _remoteProcessor = null;
            }
        }
    }
}
