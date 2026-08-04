using System;
using System.Collections.Generic;

namespace BetterSuppression
{
    public class VoiceAudioProcessor : IDisposable
    {
        private IntPtr _rnnoiseState;
        private readonly Queue<float> _inputFifo = new Queue<float>();
        private readonly Queue<float> _outputFifo = new Queue<float>();
        private readonly float[] _frameBufferIn = new float[RNNoiseNative.FrameSize];
        private readonly float[] _frameBufferOut = new float[RNNoiseNative.FrameSize];

        public bool IsEnabled { get; set; }
        public float VADThreshold { get; set; }
        public bool EnableNoiseGate { get; set; }
        public float NoiseGateThresholdDb { get; set; }

        public VoiceAudioProcessor()
        {
            IsEnabled = true;
            VADThreshold = 0.0f;
            EnableNoiseGate = true;
            NoiseGateThresholdDb = -45.0f;

            try
            {
                _rnnoiseState = RNNoiseNative.rnnoise_create(IntPtr.Zero);
                InitFifo();
            }
            catch (Exception ex)
            {
                if (BetterSuppressionPlugin.Log != null)
                {
                    BetterSuppressionPlugin.Log.LogError(string.Format("Failed to initialize RNNoise native library: {0}", ex.Message));
                }
                _rnnoiseState = IntPtr.Zero;
            }
        }

        private void InitFifo()
        {
            _inputFifo.Clear();
            _outputFifo.Clear();
            for (int i = 0; i < RNNoiseNative.FrameSize; i++)
            {
                _inputFifo.Enqueue(0.0f);
            }
        }

        private float CalculateRmsDb(float[] samples, int offset, int count)
        {
            double sumSquare = 0;
            for (int i = 0; i < count; i++)
            {
                float sample = samples[offset + i];
                sumSquare += sample * sample;
            }
            double rms = Math.Sqrt(sumSquare / count);
            if (rms <= 1e-7) return -100.0f;
            return (float)(20.0 * Math.Log10(rms));
        }

        public void ProcessAudio(float[] samples, int offset, int count)
        {
            if (!IsEnabled || samples == null || count <= 0 || offset < 0 || offset + count > samples.Length)
                return;

            if (EnableNoiseGate)
            {
                float currentDb = CalculateRmsDb(samples, offset, count);
                if (currentDb < NoiseGateThresholdDb)
                {
                    Array.Clear(samples, offset, count);
                    InitFifo();
                    return;
                }
            }

            if (_rnnoiseState == IntPtr.Zero)
                return;

            for (int i = 0; i < count; i++)
            {
                _inputFifo.Enqueue(samples[offset + i]);
            }

            while (_inputFifo.Count >= RNNoiseNative.FrameSize)
            {
                for (int i = 0; i < RNNoiseNative.FrameSize; i++)
                {
                    _frameBufferIn[i] = _inputFifo.Dequeue() * 32768.0f;
                }

                float vadProbability = RNNoiseNative.rnnoise_process_frame(_rnnoiseState, _frameBufferOut, _frameBufferIn);

                for (int i = 0; i < RNNoiseNative.FrameSize; i++)
                {
                    float cleanedSample = _frameBufferOut[i] / 32768.0f;

                    if (cleanedSample > 1.0f) cleanedSample = 1.0f;
                    else if (cleanedSample < -1.0f) cleanedSample = -1.0f;

                    if (VADThreshold > 0.0f && vadProbability < VADThreshold)
                    {
                        cleanedSample = 0.0f;
                    }

                    _outputFifo.Enqueue(cleanedSample);
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (_outputFifo.Count > 0)
                {
                    samples[offset + i] = _outputFifo.Dequeue();
                }
                else
                {
                    samples[offset + i] = 0.0f;
                }
            }
        }

        public void Dispose()
        {
            if (_rnnoiseState != IntPtr.Zero)
            {
                RNNoiseNative.rnnoise_destroy(_rnnoiseState);
                _rnnoiseState = IntPtr.Zero;
            }
        }
    }
}
