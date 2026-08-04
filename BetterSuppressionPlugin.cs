using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;

namespace BetterSuppression
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
    public class BetterSuppressionPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.lethalcompany.bettersuppression";
        public const string PluginName = "BetterSuppression";
        public const string PluginVersion = "1.0.0";

        public static ManualLogSource Log { get; private set; }

        public static ConfigEntry<bool> EnableNoiseSuppression { get; private set; }
        public static ConfigEntry<float> VADThreshold { get; private set; }
        public static ConfigEntry<bool> EnableNoiseGate { get; private set; }
        public static ConfigEntry<float> GateCloseThresholdDb { get; private set; }
        public static ConfigEntry<float> GateOpenThresholdDb { get; private set; }
        public static ConfigEntry<float> GateAttackTimeMs { get; private set; }
        public static ConfigEntry<float> GateHoldTimeMs { get; private set; }
        public static ConfigEntry<float> GateReleaseTimeMs { get; private set; }

        private Harmony _harmony;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private void Awake()
        {
            Log = Logger;

            // 1. Preload native library (rnnoise.dll) from plugin directory
            PreloadNativeLibrary();

            // 2. Define BepInEx Configuration Options with English & Korean descriptions
            EnableNoiseSuppression = Config.Bind(
                "General",
                "Enable Noise Suppression",
                true,
                "Enable RNNoise AI Noise Suppression for microphone input.\nRNNoise AI 기반 소음 억제 기능을 활성화합니다."
            );

            VADThreshold = Config.Bind(
                "General",
                "VAD Threshold",
                0.0f,
                new ConfigDescription(
                    "Voice Activity Detection threshold. 0.0 = disabled, higher values require stronger speech signal.\n음성 감지 임계값입니다. 0.0은 비활성화이며, 수치가 높을수록 마이크 입력에 큰 소리가 필요합니다.",
                    new AcceptableValueRange<float>(0.0f, 1.0f))
            );

            EnableNoiseGate = Config.Bind(
                "Noise Gate",
                "Enable Noise Gate",
                true,
                "Enable volume-based Noise Gate. Mutes input when audio level is below threshold.\n노이즈 게이트를 활성화합니다. 소리가 설정한 수치 이하라면, 마이크 입력을 차단합니다."
            );

            GateCloseThresholdDb = Config.Bind(
                "Noise Gate",
                "Gate Close Threshold (dB)",
                -32.0f,
                new ConfigDescription(
                    "Signal level in dBFS below which the noise gate closes and begins fading out.\n말이 끝났을 때, 소리를 자르는 기준을 정합니다.",
                    new AcceptableValueRange<float>(-80.0f, 0.0f))
            );

            GateOpenThresholdDb = Config.Bind(
                "Noise Gate",
                "Gate Open Threshold (dB)",
                -26.0f,
                new ConfigDescription(
                    "Signal level in dBFS required to trigger the noise gate open.\n말을 할 때, 마이크 입력에 필요한 최소 크기를 정합니다.",
                    new AcceptableValueRange<float>(-80.0f, 0.0f))
            );

            GateAttackTimeMs = Config.Bind(
                "Noise Gate",
                "Gate Attack Time (ms)",
                25.0f,
                new ConfigDescription(
                    "Time in milliseconds to fade in from muted to full audio level when gate opens.\n마이크 입력에 필요한 소리가 충족됐을 때, 음성이 페이드 인 되는 시간입니다.",
                    new AcceptableValueRange<float>(0.0f, 200.0f))
            );

            GateHoldTimeMs = Config.Bind(
                "Noise Gate",
                "Gate Hold Time (ms)",
                200.0f,
                new ConfigDescription(
                    "Time in milliseconds to hold the gate open after signal falls below close threshold.\n마이크에 입력되는 소리가 임계값 이하로 떨어졌을 때, 페이드 아웃이 되기까지의 시간을 정합니다.",
                    new AcceptableValueRange<float>(0.0f, 1000.0f))
            );

            GateReleaseTimeMs = Config.Bind(
                "Noise Gate",
                "Gate Release Time (ms)",
                150.0f,
                new ConfigDescription(
                    "Time in milliseconds to fade out from full audio level to muted when gate closes.\n마이크에 입력되는 소리가 임계값 이하로 떨어졌을 때, 페이드 아웃 되는 시간입니다.",
                    new AcceptableValueRange<float>(0.0f, 1000.0f))
            );

            // 3. Register LethalConfig Explicitly if present
            if (Chainloader.PluginInfos.ContainsKey("ainavt.lc.lethalconfig"))
            {
                try
                {
                    RegisterLethalConfig();
                    Log.LogInfo("Successfully registered configs with LethalConfig.");
                }
                catch (Exception ex)
                {
                    Log.LogWarning(string.Format("Failed to register with LethalConfig API: {0}", ex.Message));
                }
            }

            // 4. Initialize Audio Processor & Harmony Patches safely
            try
            {
                DissonancePatch.Initialize();

                if (DissonancePatch.Processor != null)
                {
                    DissonancePatch.Processor.IsEnabled = EnableNoiseSuppression.Value;
                    DissonancePatch.Processor.VADThreshold = VADThreshold.Value;
                    DissonancePatch.Processor.EnableNoiseGate = EnableNoiseGate.Value;
                    DissonancePatch.Processor.GateCloseThresholdDb = GateCloseThresholdDb.Value;
                    DissonancePatch.Processor.GateOpenThresholdDb = GateOpenThresholdDb.Value;
                    DissonancePatch.Processor.GateAttackTimeMs = GateAttackTimeMs.Value;
                    DissonancePatch.Processor.GateHoldTimeMs = GateHoldTimeMs.Value;
                    DissonancePatch.Processor.GateReleaseTimeMs = GateReleaseTimeMs.Value;
                }

                EnableNoiseSuppression.SettingChanged += (sender, args) =>
                {
                    if (DissonancePatch.Processor != null)
                        DissonancePatch.Processor.IsEnabled = EnableNoiseSuppression.Value;
                };

                VADThreshold.SettingChanged += (sender, args) =>
                {
                    if (DissonancePatch.Processor != null)
                        DissonancePatch.Processor.VADThreshold = VADThreshold.Value;
                };

                EnableNoiseGate.SettingChanged += (sender, args) =>
                {
                    if (DissonancePatch.Processor != null)
                        DissonancePatch.Processor.EnableNoiseGate = EnableNoiseGate.Value;
                };

                GateCloseThresholdDb.SettingChanged += (sender, args) =>
                {
                    if (DissonancePatch.Processor != null)
                        DissonancePatch.Processor.GateCloseThresholdDb = GateCloseThresholdDb.Value;
                };

                GateOpenThresholdDb.SettingChanged += (sender, args) =>
                {
                    if (DissonancePatch.Processor != null)
                        DissonancePatch.Processor.GateOpenThresholdDb = GateOpenThresholdDb.Value;
                };

                GateAttackTimeMs.SettingChanged += (sender, args) =>
                {
                    if (DissonancePatch.Processor != null)
                        DissonancePatch.Processor.GateAttackTimeMs = GateAttackTimeMs.Value;
                };

                GateHoldTimeMs.SettingChanged += (sender, args) =>
                {
                    if (DissonancePatch.Processor != null)
                        DissonancePatch.Processor.GateHoldTimeMs = GateHoldTimeMs.Value;
                };

                GateReleaseTimeMs.SettingChanged += (sender, args) =>
                {
                    if (DissonancePatch.Processor != null)
                        DissonancePatch.Processor.GateReleaseTimeMs = GateReleaseTimeMs.Value;
                };

                _harmony = new Harmony(PluginGUID);
                _harmony.PatchAll(typeof(DissonancePatch));

                Log.LogInfo(string.Format("{0} v{1} loaded successfully.", PluginName, PluginVersion));
            }
            catch (Exception ex)
            {
                Log.LogError(string.Format("Error during Dissonance patch initialization: {0}", ex.Message));
            }
        }

        private void PreloadNativeLibrary()
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Info.Location);
                if (string.IsNullOrEmpty(pluginDir)) return;

                SetDllDirectory(pluginDir);

                string[] nativeDlls = new string[] { "rnnoise.dll", "librnnoise.dll" };
                foreach (var dll in nativeDlls)
                {
                    string fullPath = Path.Combine(pluginDir, dll);
                    if (File.Exists(fullPath))
                    {
                        IntPtr handle = LoadLibrary(fullPath);
                        if (handle != IntPtr.Zero && Log != null)
                        {
                            Log.LogInfo(string.Format("Preloaded native DLL: {0}", fullPath));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Log != null)
                {
                    Log.LogWarning(string.Format("Native library preload warning: {0}", ex.Message));
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterLethalConfig()
        {
            LethalConfigManager.SetModDescription("RNNoise AI Deep Learning Microphone Noise Suppression and Noise Gate Mod for Lethal Company.");

            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(EnableNoiseSuppression, new BoolCheckBoxOptions
            {
                RequiresRestart = false
            }));

            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(VADThreshold, new FloatSliderOptions
            {
                Min = 0.0f,
                Max = 1.0f,
                RequiresRestart = false
            }));

            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(EnableNoiseGate, new BoolCheckBoxOptions
            {
                RequiresRestart = false
            }));

            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(GateCloseThresholdDb, new FloatSliderOptions
            {
                Min = -80.0f,
                Max = 0.0f,
                RequiresRestart = false
            }));

            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(GateOpenThresholdDb, new FloatSliderOptions
            {
                Min = -80.0f,
                Max = 0.0f,
                RequiresRestart = false
            }));

            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(GateAttackTimeMs, new FloatSliderOptions
            {
                Min = 0.0f,
                Max = 200.0f,
                RequiresRestart = false
            }));

            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(GateHoldTimeMs, new FloatSliderOptions
            {
                Min = 0.0f,
                Max = 1000.0f,
                RequiresRestart = false
            }));

            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(GateReleaseTimeMs, new FloatSliderOptions
            {
                Min = 0.0f,
                Max = 1000.0f,
                RequiresRestart = false
            }));
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
            DissonancePatch.Cleanup();
        }
    }
}
