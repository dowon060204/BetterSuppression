using System;
using System.Collections.Generic;

namespace BetterSuppression
{
    public class VoiceAudioProcessor : IDisposable
    {
        public enum GateState
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

        // Analysis window size for noise gate (10ms at 48kHz = 480 samples)
        private const int AnalysisWindowSize = 480;

        // ── Configuration Properties ──
        public bool IsEnabled { get; set; } // RNNoise AI Noise Suppression toggle
        public bool EnableNoiseGate { get; set; } // Noise Gate toggle
        public bool IsActive { get { return IsEnabled || EnableNoiseGate; } }
        public float GateCloseThresholdDb { get; set; }
        public float GateOpenThresholdDb { get; set; }
        public float GateAttackTimeMs { get; set; }
        public float GateHoldTimeMs { get; set; }
        public float GateReleaseTimeMs { get; set; }

        // ── Diagnostic Properties (read-only, for MicTestOverlay) ──
        public float CurrentInputDbFS { get; private set; }
        public float CurrentOutputDbFS { get; private set; }
        public float PreGateDbFS { get; private set; }
        public GateState CurrentGateState { get { return _gateState; } }
        public float CurrentGateGain { get { return _currentGain; } }
        public long AudioFrameCount { get; private set; }
        public bool HasRNNoiseState { get { return _rnnoiseState != IntPtr.Zero; } }

        // Circular buffer for FFT visualization (latest 2048 output samples)
        private const int FftBufferSize = 2048;
        private readonly float[] _fftCircularBuffer = new float[FftBufferSize];
        private int _fftWriteIndex = 0;
        private readonly object _fftLock = new object();

        public VoiceAudioProcessor()
        {
            IsEnabled = true;
            EnableNoiseGate = true;

            // Default Noise Gate values
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

        /// <summary>
        /// Copies the latest FFT buffer for external visualization.
        /// Returns the number of valid samples copied.
        /// </summary>
        public int CopyFftBuffer(float[] destination)
        {
            if (destination == null || destination.Length < FftBufferSize) return 0;
            lock (_fftLock)
            {
                // Copy in correct order: oldest to newest
                int readIdx = _fftWriteIndex;
                for (int i = 0; i < FftBufferSize; i++)
                {
                    destination[i] = _fftCircularBuffer[readIdx];
                    readIdx = (readIdx + 1) % FftBufferSize;
                }
            }
            return FftBufferSize;
        }

        private static float CalculateRmsDb(float[] samples, int offset, int count)
        {
            if (count <= 0) return -100.0f;
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

        private static float CalculatePeakDb(float[] samples, int offset, int count)
        {
            if (count <= 0) return -100.0f;
            float maxAbs = 0.0f;
            for (int i = 0; i < count; i++)
            {
                float abs = Math.Abs(samples[offset + i]);
                if (abs > maxAbs) maxAbs = abs;
            }
            if (maxAbs <= 1e-7f) return -100.0f;
            return (float)(20.0 * Math.Log10(maxAbs));
        }

        public void ProcessAudio(float[] samples, int offset, int count)
        {
            if (!IsActive || samples == null || count <= 0 || offset < 0 || offset + count > samples.Length)
                return;

            AudioFrameCount++;

            // Record input dB before any processing
            CurrentInputDbFS = CalculatePeakDb(samples, offset, count);

            // ──────────────────────────────────────────────
            // STEP 1: RNNoise AI Noise Suppression (FIRST)
            // ──────────────────────────────────────────────
            if (IsEnabled && _rnnoiseState != IntPtr.Zero)
            {
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

                    RNNoiseNative.rnnoise_process_frame(_rnnoiseState, _frameBufferOut, _frameBufferIn);

                    for (int i = 0; i < RNNoiseNative.FrameSize; i++)
                    {
                        float cleanedSample = _frameBufferOut[i] / 32768.0f;
                        if (cleanedSample > 1.0f) cleanedSample = 1.0f;
                        else if (cleanedSample < -1.0f) cleanedSample = -1.0f;
                        _outputFifo.Enqueue(cleanedSample);
                    }
                }

                // Write RNNoise output back to samples buffer
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

            // Record post-RNNoise / pre-gate dB
            PreGateDbFS = CalculatePeakDb(samples, offset, count);

            // ──────────────────────────────────────────────
            // STEP 2: Noise Gate (SECOND, after RNNoise)
            //         Uses windowed RMS for accurate gating
            // ──────────────────────────────────────────────
            if (EnableNoiseGate)
            {
                // Duration of one audio sample in milliseconds (at 48kHz)
                float msPerSample = 1000.0f / 48000.0f;

                int processed = 0;
                while (processed < count)
                {
                    // Determine analysis window: up to AnalysisWindowSize samples
                    int remaining = count - processed;
                    int windowSize = remaining < AnalysisWindowSize ? remaining : AnalysisWindowSize;

                    float windowDb = CalculatePeakDb(samples, offset + processed, windowSize);
                    bool isAboveOpen = windowDb >= GateOpenThresholdDb;
                    bool isAboveClose = windowDb >= GateCloseThresholdDb;

                    for (int i = 0; i < windowSize; i++)
                    {
                        int sampleIdx = offset + processed + i;

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

                    processed += windowSize;
                }

                // If gate fully closed, clear remaining and reset FIFO
                if (_gateState == GateState.Closed && _currentGain <= 0.0f)
                {
                    Array.Clear(samples, offset, count);
                    InitFifo();
                }
            }
            else
            {
                _currentGain = 1.0f;
                _gateState = GateState.Open;
            }

            // Record final output dB
            CurrentOutputDbFS = CalculatePeakDb(samples, offset, count);

            // Write to FFT circular buffer for visualization
            lock (_fftLock)
            {
                for (int i = 0; i < count; i++)
                {
                    _fftCircularBuffer[_fftWriteIndex] = samples[offset + i];
                    _fftWriteIndex = (_fftWriteIndex + 1) % FftBufferSize;
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
