using System;
using UnityEngine;

namespace BetterSuppression
{
    /// <summary>
    /// In-game microphone test overlay.
    /// Press the configured test key (default: F7) to toggle.
    /// Shows real-time FFT spectrum, dB meters, noise gate state, and RNNoise status.
    /// </summary>
    public class MicTestOverlay : MonoBehaviour
    {
        private bool _visible = false;
        private float[] _fftBuffer = new float[2048];
        private float[] _fftMagnitudes = new float[64]; // 64 frequency bands
        private float[] _smoothedMagnitudes = new float[64];

        // Smoothed dB values for display
        private float _smoothedInputDb = -100f;
        private float _smoothedPreGateDb = -100f;
        private float _smoothedOutputDb = -100f;
        private long _lastFrameCount = 0;

        // UI layout constants
        private const float PanelWidth = 420f;
        private const float PanelHeight = 440f;
        private const float Padding = 12f;
        private const float BarHeight = 16f;
        private const float SectionGap = 10f;

        // Colors
        private static readonly Color PanelBg = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        private static readonly Color HeaderColor = new Color(0.55f, 0.82f, 1.0f);
        private static readonly Color LabelColor = new Color(0.85f, 0.85f, 0.9f);
        private static readonly Color DimColor = new Color(0.5f, 0.5f, 0.55f);
        private static readonly Color GateClosedColor = new Color(0.9f, 0.25f, 0.25f);
        private static readonly Color GateAttackColor = new Color(1.0f, 0.8f, 0.2f);
        private static readonly Color GateOpenColor = new Color(0.2f, 0.9f, 0.35f);
        private static readonly Color GateHoldColor = new Color(0.4f, 0.85f, 0.9f);
        private static readonly Color GateReleaseColor = new Color(1.0f, 0.6f, 0.2f);

        // Cached styles
        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _dimStyle;
        private GUIStyle _statusStyle;
        private bool _stylesInitialized = false;

        private Texture2D _whiteTex;
        private Texture2D _panelBgTex;

        private void Awake()
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            _panelBgTex = new Texture2D(1, 1);
            _panelBgTex.SetPixel(0, 0, PanelBg);
            _panelBgTex.Apply();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = _panelBgTex;

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontSize = 16;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.normal.textColor = HeaderColor;
            _headerStyle.alignment = TextAnchor.MiddleLeft;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 12;
            _labelStyle.normal.textColor = LabelColor;

            _valueStyle = new GUIStyle(GUI.skin.label);
            _valueStyle.fontSize = 12;
            _valueStyle.fontStyle = FontStyle.Bold;
            _valueStyle.normal.textColor = Color.white;
            _valueStyle.alignment = TextAnchor.MiddleRight;

            _dimStyle = new GUIStyle(GUI.skin.label);
            _dimStyle.fontSize = 10;
            _dimStyle.normal.textColor = DimColor;

            _statusStyle = new GUIStyle(GUI.skin.label);
            _statusStyle.fontSize = 13;
            _statusStyle.fontStyle = FontStyle.Bold;
            _statusStyle.alignment = TextAnchor.MiddleCenter;

            _stylesInitialized = true;
        }

        private void Update()
        {
            // Overlay visibility is controlled by LethalConfig toggle only
            try
            {
                _visible = BetterSuppressionPlugin.EnableTestOverlay.Value;
            }
            catch { _visible = false; }

            if (!_visible) return;

            // Update diagnostic data from processor
            var proc = DissonancePatch.Processor;
            if (proc == null) return;

            // Smooth dB values
            float smoothing = 1.0f - Mathf.Exp(-Time.deltaTime * 15.0f);
            _smoothedInputDb = Mathf.Lerp(_smoothedInputDb, proc.CurrentInputDbFS, smoothing);
            _smoothedPreGateDb = Mathf.Lerp(_smoothedPreGateDb, proc.PreGateDbFS, smoothing);
            _smoothedOutputDb = Mathf.Lerp(_smoothedOutputDb, proc.CurrentOutputDbFS, smoothing);

            // Get FFT buffer and compute magnitudes
            int copied = proc.CopyFftBuffer(_fftBuffer);
            if (copied > 0)
            {
                ComputeFFTMagnitudes();
            }
        }

        private void ComputeFFTMagnitudes()
        {
            // Simple DFT for 64 frequency bands (optimized for display, not precision)
            int N = _fftBuffer.Length;
            int numBands = _fftMagnitudes.Length;
            int samplesPerBand = N / (numBands * 2); // Nyquist: only use first half

            for (int band = 0; band < numBands; band++)
            {
                double realSum = 0;
                double imagSum = 0;

                // Use Goertzel-like approach for each band's center frequency
                int k = band + 1; // skip DC
                double angle = 2.0 * Math.PI * k / N;

                for (int n = 0; n < N; n++)
                {
                    // Apply Hanning window
                    float window = 0.5f * (1.0f - Mathf.Cos(2.0f * Mathf.PI * n / (N - 1)));
                    float sample = _fftBuffer[n] * window;
                    realSum += sample * Math.Cos(angle * n);
                    imagSum -= sample * Math.Sin(angle * n);
                }

                double magnitude = Math.Sqrt(realSum * realSum + imagSum * imagSum) / N;
                // Convert to dB, clamp
                float db = (magnitude > 1e-10) ? (float)(20.0 * Math.Log10(magnitude)) : -100f;
                float normalized = Mathf.InverseLerp(-80f, 0f, db);
                _fftMagnitudes[band] = normalized;

                // Smooth for display
                float target = _fftMagnitudes[band];
                _smoothedMagnitudes[band] = Mathf.Lerp(_smoothedMagnitudes[band], target,
                    1.0f - Mathf.Exp(-Time.deltaTime * 12.0f));
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            InitStyles();

            var proc = DissonancePatch.Processor;

            // Panel position: top-left with margin
            Rect panelRect = new Rect(20, 20, PanelWidth, PanelHeight);
            GUI.Box(panelRect, GUIContent.none, _panelStyle);

            float x = panelRect.x + Padding;
            float y = panelRect.y + Padding;
            float contentWidth = PanelWidth - Padding * 2;

            // ── Header ──
            GUI.Label(new Rect(x, y, contentWidth, 24), "BetterSuppression Mic Test", _headerStyle);
            y += 28;

            // Hint
            GUI.Label(new Rect(x, y, contentWidth, 16), "LethalConfig > Test Mode > Disable to close", _dimStyle);
            y += 20 + SectionGap;

            // ── Read config values directly ──
            bool cfgNoiseSuppression = false;
            bool cfgNoiseGate = false;
            try { cfgNoiseSuppression = BetterSuppressionPlugin.EnableNoiseSuppression.Value; } catch { }
            try { cfgNoiseGate = BetterSuppressionPlugin.EnableNoiseGate.Value; } catch { }

            // ── RNNoise Status (read from config directly) ──
            bool rnnoiseNativeOk = proc != null && proc.HasRNNoiseState;
            string rnnoiseStatus = "OFF";
            Color rnnoiseColor = GateClosedColor;
            if (cfgNoiseSuppression && rnnoiseNativeOk)
            {
                rnnoiseStatus = "ACTIVE";
                rnnoiseColor = GateOpenColor;
            }
            else if (cfgNoiseSuppression && !rnnoiseNativeOk)
            {
                rnnoiseStatus = "NO DLL";
                rnnoiseColor = GateAttackColor;
            }
            DrawStatusRow(x, y, contentWidth, "RNNoise AI", rnnoiseStatus, rnnoiseColor);
            y += 22;

            // ── Noise Gate Status (read from config directly) ──
            string gateStateStr = "OFF";
            Color gateColor = DimColor;
            if (cfgNoiseGate)
            {
                if (proc != null)
                {
                    gateStateStr = GetGateStateString(proc.CurrentGateState);
                    gateColor = GetGateStateColor(proc.CurrentGateState);
                }
                else
                {
                    gateStateStr = "ON (no proc)";
                    gateColor = GateAttackColor;
                }
            }
            DrawStatusRow(x, y, contentWidth, "Noise Gate", gateStateStr, gateColor);
            y += 22;

            // ── Audio Flow Status ──
            long frames = (proc != null) ? proc.AudioFrameCount : 0;
            bool audioFlowing = frames > _lastFrameCount;
            _lastFrameCount = frames;
            DrawStatusRow(x, y, contentWidth, "Audio Flow",
                audioFlowing ? "RECEIVING" : (frames > 0 ? "IDLE" : "NO DATA"),
                audioFlowing ? GateOpenColor : (frames > 0 ? GateHoldColor : GateClosedColor));
            y += 22;

            if (proc != null && cfgNoiseGate)
            {
                // Gate gain bar
                float gainPct = proc.CurrentGateGain;
                DrawLabeledBar(x, y, contentWidth, "Gate Gain", gainPct,
                    Color.Lerp(GateClosedColor, GateOpenColor, gainPct));
                y += BarHeight + 6;
            }
            y += SectionGap;

            // ── dB Meters ──
            GUI.Label(new Rect(x, y, contentWidth, 18), "Level Meters", _labelStyle);
            y += 20;

            DrawDbMeter(x, y, contentWidth, "Input (Raw)", _smoothedInputDb);
            y += BarHeight + 6;
            DrawDbMeter(x, y, contentWidth, "After RNNoise", _smoothedPreGateDb);
            y += BarHeight + 6;
            DrawDbMeter(x, y, contentWidth, "Output (Final)", _smoothedOutputDb);
            y += BarHeight + 6 + SectionGap;

            // ── FFT Spectrum ──
            GUI.Label(new Rect(x, y, contentWidth, 18), "Frequency Spectrum (Output)", _labelStyle);
            y += 20;

            float spectrumHeight = 80f;
            DrawSpectrum(x, y, contentWidth, spectrumHeight);
            y += spectrumHeight + 4;

            // Frequency labels
            GUI.Label(new Rect(x, y, 40, 14), "Low", _dimStyle);
            var rightAlign = new GUIStyle(_dimStyle);
            rightAlign.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(x + contentWidth - 40, y, 40, 14), "High", rightAlign);
            y += 18;

            // ── Debug info ──
            string debugInfo = string.Format("Proc: {0} | Frames: {1}", proc != null ? "OK" : "NULL", frames);
            GUI.Label(new Rect(x, y, contentWidth, 14), debugInfo, _dimStyle);
        }

        private void DrawStatusRow(float x, float y, float width, string label, string value, Color valueColor)
        {
            GUI.Label(new Rect(x, y, width * 0.6f, 20), label, _labelStyle);
            var style = new GUIStyle(_statusStyle);
            style.normal.textColor = valueColor;
            GUI.Label(new Rect(x + width * 0.6f, y, width * 0.4f, 20), value, style);
        }

        private string GetGateStateString(VoiceAudioProcessor.GateState state)
        {
            switch (state)
            {
                case VoiceAudioProcessor.GateState.Closed: return "CLOSED";
                case VoiceAudioProcessor.GateState.Attack: return "OPENING";
                case VoiceAudioProcessor.GateState.Open: return "OPEN";
                case VoiceAudioProcessor.GateState.Hold: return "HOLDING";
                case VoiceAudioProcessor.GateState.Release: return "CLOSING";
                default: return "UNKNOWN";
            }
        }

        private Color GetGateStateColor(VoiceAudioProcessor.GateState state)
        {
            switch (state)
            {
                case VoiceAudioProcessor.GateState.Closed: return GateClosedColor;
                case VoiceAudioProcessor.GateState.Attack: return GateAttackColor;
                case VoiceAudioProcessor.GateState.Open: return GateOpenColor;
                case VoiceAudioProcessor.GateState.Hold: return GateHoldColor;
                case VoiceAudioProcessor.GateState.Release: return GateReleaseColor;
                default: return DimColor;
            }
        }

        private void DrawDbMeter(float x, float y, float width, string label, float db)
        {
            float labelWidth = 100f;
            float valueWidth = 55f;
            float barWidth = width - labelWidth - valueWidth - 8f;

            GUI.Label(new Rect(x, y, labelWidth, BarHeight), label, _labelStyle);

            // dB value text
            string dbText = (db <= -99f) ? "-∞ dB" : string.Format("{0:F1} dB", db);
            GUI.Label(new Rect(x + labelWidth, y, valueWidth, BarHeight), dbText, _valueStyle);

            // Bar background
            float barX = x + labelWidth + valueWidth + 4f;
            DrawRect(barX, y + 2, barWidth, BarHeight - 4, new Color(0.15f, 0.15f, 0.2f));

            // Bar fill
            float normalized = Mathf.InverseLerp(-60f, 0f, db);
            if (normalized > 0)
            {
                Color barColor;
                if (normalized < 0.5f)
                    barColor = Color.Lerp(new Color(0.2f, 0.7f, 0.3f), new Color(0.9f, 0.9f, 0.2f), normalized * 2f);
                else
                    barColor = Color.Lerp(new Color(0.9f, 0.9f, 0.2f), new Color(0.95f, 0.25f, 0.2f), (normalized - 0.5f) * 2f);

                DrawRect(barX + 1, y + 3, (barWidth - 2) * normalized, BarHeight - 6, barColor);
            }
        }

        private void DrawLabeledBar(float x, float y, float width, string label, float value01, Color color)
        {
            float labelWidth = 100f;
            float valueWidth = 45f;
            float barWidth = width - labelWidth - valueWidth - 8f;

            GUI.Label(new Rect(x, y, labelWidth, BarHeight), label, _labelStyle);

            string pctText = string.Format("{0:F0}%", value01 * 100f);
            GUI.Label(new Rect(x + labelWidth, y, valueWidth, BarHeight), pctText, _valueStyle);

            float barX = x + labelWidth + valueWidth + 4f;
            DrawRect(barX, y + 2, barWidth, BarHeight - 4, new Color(0.15f, 0.15f, 0.2f));
            if (value01 > 0)
            {
                DrawRect(barX + 1, y + 3, (barWidth - 2) * value01, BarHeight - 6, color);
            }
        }

        private void DrawSpectrum(float x, float y, float width, float height)
        {
            // Background
            DrawRect(x, y, width, height, new Color(0.1f, 0.1f, 0.15f));

            int numBands = _smoothedMagnitudes.Length;
            float barWidth = (width - 2f) / numBands;
            float gap = 1f;

            for (int i = 0; i < numBands; i++)
            {
                float mag = _smoothedMagnitudes[i];
                if (mag < 0.005f) continue;

                float barH = mag * (height - 4f);
                float bx = x + 1f + i * barWidth;
                float by = y + height - 2f - barH;

                // Color gradient: blue → cyan → green → yellow
                Color c;
                float t = (float)i / numBands;
                if (t < 0.33f)
                    c = Color.Lerp(new Color(0.3f, 0.5f, 1.0f), new Color(0.2f, 0.9f, 0.9f), t * 3f);
                else if (t < 0.66f)
                    c = Color.Lerp(new Color(0.2f, 0.9f, 0.9f), new Color(0.3f, 0.95f, 0.4f), (t - 0.33f) * 3f);
                else
                    c = Color.Lerp(new Color(0.3f, 0.95f, 0.4f), new Color(1.0f, 0.9f, 0.3f), (t - 0.66f) * 3f);

                // Brightness based on magnitude
                c = Color.Lerp(c * 0.4f, c, mag);

                DrawRect(bx, by, barWidth - gap, barH, c);
            }

            // Threshold lines (if noise gate enabled)
            var proc = DissonancePatch.Processor;
            if (proc != null && proc.EnableNoiseGate)
            {
                // Open threshold line
                float openNorm = Mathf.InverseLerp(-80f, 0f, proc.GateOpenThresholdDb);
                float openY = y + height - 2f - openNorm * (height - 4f);
                DrawRect(x, openY, width, 1f, new Color(GateOpenColor.r, GateOpenColor.g, GateOpenColor.b, 0.6f));

                // Close threshold line
                float closeNorm = Mathf.InverseLerp(-80f, 0f, proc.GateCloseThresholdDb);
                float closeY = y + height - 2f - closeNorm * (height - 4f);
                DrawRect(x, closeY, width, 1f, new Color(GateClosedColor.r, GateClosedColor.g, GateClosedColor.b, 0.4f));
            }
        }

        private void DrawRect(float x, float y, float width, float height, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, width, height), _whiteTex);
            GUI.color = prev;
        }

        private void OnDestroy()
        {
            if (_whiteTex != null) Destroy(_whiteTex);
            if (_panelBgTex != null) Destroy(_panelBgTex);
        }
    }
}
