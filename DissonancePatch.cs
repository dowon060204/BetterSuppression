using System;
using HarmonyLib;
using Dissonance.Audio.Capture;

namespace BetterSuppression
{
    [HarmonyPatch]
    public static class DissonancePatch
    {
        private static VoiceAudioProcessor _processor;

        public static void Initialize()
        {
            _processor = new VoiceAudioProcessor();
        }

        public static VoiceAudioProcessor Processor
        {
            get { return _processor; }
        }

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

        public static void Cleanup()
        {
            if (_processor != null)
            {
                _processor.Dispose();
            }
            _processor = null;
        }
    }
}
