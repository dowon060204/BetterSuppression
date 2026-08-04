using System;
using System.Collections.Generic;

namespace BetterSuppression
{
    public class VoiceAudioProcessor : IDisposable
    {
        private enum GateState
        {
            Closed,
            Attack,
            Open,
            Hold,
            Release
        }

        private IntPtr _rnnoiseState;
        private readonly Queue<float> _inputFifo = new Queue<float>();
        private readonly Queue<float> _outputFifo = new Queue<float>();
        private readonly float[] _frameBufferIn = new float[RNNoiseNative.FrameSize];
        private readonly float[] _frameBufferOut = new float[RNNoiseNative.FrameSize];

        // Noise Gate State
        private GateState _gateState = GateState.Closed;
        private float _currentGain = 0.0f;
        private float _holdTimerMs = 0.0f;

        public bool IsEnabled { get; set; }
        public float VADThreshold { get; set; }
        public bool EnableNoiseGate { get; set; }
        public float GateCloseThresholdDb { get; set; }
        public float GateOpenThresholdDb { get; set; }
        public float GateAttackTimeMs { get; set; }
        public float GateHoldTimeMs { get; set; }
        public float GateReleaseTimeMs { get; set; }

        public VoiceAudioProcessor()
        {
            IsEnabled = true;
            VADThreshold = 0.0f;
            EnableNoiseGate = true;

            // Default Noise Gate values requested
            GateCloseThresholdDb = -32.0f;
            GateOpenThresholdDb = -26.0f;
            GateAttackTimeMs = 25.0f;
            GateHoldTimeMs = 200.0f;
            GateReleaseTimeMs = 150.0f;

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
            _gateState = GateState.Closed;
            _currentGain = 0.0f;
            _holdTimerMs = 0.0f;
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

            // 1. Process Noise Gate with Attack, Hold, Release & Hysteresis
            if (EnableNoiseGate)
            {
                float currentDb = CalculateRmsDb(samples, offset, count);
                bool isAboveOpen = currentDb >= GateOpenThresholdDb;
                bool isAboveClose = currentDb >= GateCloseThresholdDb;

                // Duration of one audio sample in milliseconds (at 48kHz)
                float msPerSample = 1.0f / 48.0f;

                for (int i = 0; i < count; i++)
                {
                    int sampleIdx = offset + i;

                    switch (_gateState)
                    {
                        case GateState.Closed:
                            if (isAboveOpen)
                            {
                                _gateState = (GateAttackTimeMs <= 0.0f) ? GateState.Open : GateState.Attack;
                            }
                            else
                            {
                                _currentGain = 0.0f;
                            }
                            break;

                        case GateState.Attack:
                            if (isAboveClose)
                            {
                                float attackStep = (GateAttackTimeMs > 0.0f) ? (msPerSample / GateAttackTimeMs) : 1.0f;
                                _currentGain += attackStep;
                                if (_currentGain >= 1.0f)
                                {
                                    _currentGain = 1.0f;
                                    _gateState = GateState.Open;
                                }
                            }
                            else
                            {
                                _gateState = GateState.Release;
                            }
                            break;

                        case GateState.Open:
                            if (isAboveClose)
                            {
                                _currentGain = 1.0f;
                            }
                            else
                            {
                                _gateState = GateState.Hold;
                                _holdTimerMs = GateHoldTimeMs;
                            }
                            break;

                        case GateState.Hold:
                            if (isAboveClose)
                            {
                                _gateState = GateState.Open;
                                _currentGain = 1.0f;
                            }
                            else
                            {
                                _holdTimerMs -= msPerSample;
                                _currentGain = 1.0f;
                                if (_holdTimerMs <= 0.0f)
                                {
                                    _gateState = GateState.Release;
                                }
                            }
                            break;

                        case GateState.Release:
                            if (isAboveOpen)
                            {
                                _gateState = (GateAttackTimeMs <= 0.0f) ? GateState.Open : GateState.Attack;
                            }
                            else
                            {
                                float releaseStep = (GateReleaseTimeMs > 0.0f) ? (msPerSample / GateReleaseTimeMs) : 1.0f;
                                _currentGain -= releaseStep;
                                if (_currentGain <= 0.0f)
                                {
                                    _currentGain = 0.0f;
                                    _gateState = GateState.Closed;
                                }
                            }
                            break;
                    }

                    samples[sampleIdx] *= _currentGain;
                }

                // If gain is completely zero, reset FIFO and return early
                if (_gateState == GateState.Closed && _currentGain <= 0.0f)
                {
                    Array.Clear(samples, offset, count);
                    InitFifo();
                    return;
                }
            }

            // 2. Process RNNoise AI Noise Suppression
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
