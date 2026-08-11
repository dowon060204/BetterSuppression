using System;
using HarmonyLib;
using Dissonance.Audio.Capture;
using Dissonance.Audio.Playback;

namespace BetterSuppression
{
    public static class DissonancePatch
    {
        private static VoiceAudioProcessor _processor;
        private static VoiceAudioProcessor _remoteProcessor;

        public static void Initialize()
        {
            if (_processor == null) _processor = new VoiceAudioProcessor();
            if (_remoteProcessor == null) _remoteProcessor = new VoiceAudioProcessor();
        }

        public static VoiceAudioProcessor Processor
        {
            get
            {
                if (_processor == null)
                {
                    _processor = new VoiceAudioProcessor();
                    BetterSuppressionPlugin.ApplyProcessorSettings();
                }
                return _processor;
            }
        }

        public static VoiceAudioProcessor RemoteProcessor
        {
            get
            {
                if (_remoteProcessor == null)
                {
                    _remoteProcessor = new VoiceAudioProcessor();
                    BetterSuppressionPlugin.ApplyProcessorSettings();
                }
                return _remoteProcessor;
            }
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

    /// <summary>
    /// Filters local microphone audio at the very source of Dissonance capture.
    /// Prefix on BasicMicrophoneCapture.ConsumeSamples ensures RNNoise AI & Noise Gate
    /// process and filter the audio BEFORE Voice Activity Detection (VAD), BEFORE LethalFixes UI,
    /// and BEFORE network encoding.
    /// </summary>
    [HarmonyPatch(typeof(BasicMicrophoneCapture), "ConsumeSamples")]
    public static class BasicMicConsumePatch
    {
        [HarmonyPrefix]
        public static void Prefix(ArraySegment<float> samples)
        {
            var proc = DissonancePatch.Processor;
            if (proc == null || !proc.IsActive || samples.Array == null || samples.Count == 0)
                return;

            proc.ProcessAudio(samples.Array, samples.Offset, samples.Count);
        }
    }

    /// <summary>
    /// Remote player voice playback audio filter patch.
    /// Filters incoming voice audio from other players before outputting to local speaker/headset.
    /// </summary>
    [HarmonyPatch(typeof(SamplePlaybackComponent), "OnAudioFilterRead")]
    public static class SamplePlaybackComponentPatch
    {
        [HarmonyPostfix]
        public static void Postfix(float[] data, int channels)
        {
            var remoteProc = DissonancePatch.RemoteProcessor;
            if (remoteProc == null || !remoteProc.IsActive || data == null || data.Length == 0)
                return;

            remoteProc.ProcessAudio(data, 0, data.Length);
        }
    }
}
