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
        public static ConfigEntry<float> NoiseGateThresholdDb { get; private set; }

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

            // 2. Define BepInEx Configuration Options
            EnableNoiseSuppression = Config.Bind(
                "General",
                "Enable Noise Suppression",
                true,
                "Enable RNNoise AI Noise Suppression for microphone input."
            );

            VADThreshold = Config.Bind(
                "General",
                "VAD Threshold",
                0.0f,
                new ConfigDescription(
                    "Voice Activity Detection threshold. 0.0 = disabled, higher values require stronger speech signal.",
                    new AcceptableValueRange<float>(0.0f, 1.0f))
            );

            EnableNoiseGate = Config.Bind(
                "Noise Gate",
                "Enable Noise Gate",
                true,
                "Enable volume-based Noise Gate. Mutes input when audio level is below threshold."
            );

            NoiseGateThresholdDb = Config.Bind(
                "Noise Gate",
                "Gate Threshold (dB)",
                -45.0f,
                new ConfigDescription(
                    "Noise Gate volume threshold in dBFS. Sounds quieter than this will be muted.",
                    new AcceptableValueRange<float>(-80.0f, 0.0f))
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
                    DissonancePatch.Processor.NoiseGateThresholdDb = NoiseGateThresholdDb.Value;
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

                NoiseGateThresholdDb.SettingChanged += (sender, args) =>
                {
                    if (DissonancePatch.Processor != null)
                        DissonancePatch.Processor.NoiseGateThresholdDb = NoiseGateThresholdDb.Value;
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

            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(NoiseGateThresholdDb, new FloatSliderOptions
            {
                Min = -80.0f,
                Max = 0.0f,
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
