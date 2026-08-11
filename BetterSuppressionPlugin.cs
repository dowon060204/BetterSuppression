using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
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
        public const string PluginVersion = "1.1.0";

        public static ManualLogSource Log { get; private set; }

        // Local Player Settings
        public static ConfigEntry<bool> EnableNoiseSuppression { get; private set; }
        public static ConfigEntry<bool> EnableNoiseGate { get; private set; }

        // Remote Players Settings (다른 플레이어 음성 노이즈 제거)
        public static ConfigEntry<bool> EnableRemoteNoiseSuppression { get; private set; }
        public static ConfigEntry<bool> EnableRemoteNoiseGate { get; private set; }

        // Shared Noise Gate DSP Settings
        public static ConfigEntry<float> GateCloseThresholdDb { get; private set; }
        public static ConfigEntry<float> GateOpenThresholdDb { get; private set; }
        public static ConfigEntry<float> GateAttackTimeMs { get; private set; }
        public static ConfigEntry<float> GateHoldTimeMs { get; private set; }
        public static ConfigEntry<float> GateReleaseTimeMs { get; private set; }

        // Test Mode
        public static ConfigEntry<bool> EnableTestOverlay { get; private set; }

        private Harmony _harmony;
        private MicTestOverlay _micTestOverlay;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Log = Logger;

            // 1. Preload native library (rnnoise.dll) from plugin directory
            PreloadNativeLibrary();

            // 2. Define BepInEx Configuration Options with English & Korean descriptions
            EnableNoiseSuppression = Config.Bind(
                "Local Player",
                "Enable Noise Suppression",
                true,
                "Enable/Disable RNNoise AI Noise Suppression for your microphone input.\nRNNoise AI 기반 소음 억제 기능을 내 마이크에 활성화/비활성화합니다."
            );

            EnableNoiseGate = Config.Bind(
                "Local Player",
                "Enable Noise Gate",
                true,
                "Enable volume-based Noise Gate for your microphone input. Mutes input when audio level is below threshold.\n내 마이크에 노이즈 게이트를 활성화합니다. 소리가 설정한 수치 이하라면, 마이크 입력을 차단합니다."
            );

            // Remote Players Configs
            EnableRemoteNoiseSuppression = Config.Bind(
                "Remote Players",
                "Enable Remote Noise Suppression",
                true,
                "Enable/Disable RNNoise AI Noise Suppression for incoming voice audio from other players.\n다른 플레이어의 마이크 음성에도 RNNoise AI 노이즈 제거를 적용합니다."
            );

            EnableRemoteNoiseGate = Config.Bind(
                "Remote Players",
                "Enable Remote Noise Gate",
                true,
                "Enable volume-based Noise Gate for incoming voice audio from other players.\n다른 플레이어의 마이크 음성에도 노이즈 게이트를 적용합니다."
            );

            // Noise Gate DSP Configs
            GateCloseThresholdDb = Config.Bind(
                "Noise Gate DSP",
                "Gate Close Threshold (dB)",
                -32.0f,
                new ConfigDescription(
                    "Signal level in dBFS below which the noise gate closes and begins fading out.\n말이 끝났을 때, 소리를 자르는 기준을 정합니다.",
                    new AcceptableValueRange<float>(-80.0f, 0.0f))
            );

            GateOpenThresholdDb = Config.Bind(
                "Noise Gate DSP",
                "Gate Open Threshold (dB)",
                -26.0f,
                new ConfigDescription(
                    "Signal level in dBFS required to trigger the noise gate open.\n말을 할 때, 마이크 입력에 필요한 최소 크기를 정합니다.",
                    new AcceptableValueRange<float>(-80.0f, 0.0f))
            );

            GateAttackTimeMs = Config.Bind(
                "Noise Gate DSP",
                "Gate Attack Time (ms)",
                25.0f,
                new ConfigDescription(
                    "Time in milliseconds to fade in from muted to full audio level when gate opens.\n마이크 입력에 필요한 소리가 충족됐을 때, 음성이 페이드 인 되는 시간입니다.",
                    new AcceptableValueRange<float>(0.0f, 200.0f))
            );

            GateHoldTimeMs = Config.Bind(
                "Noise Gate DSP",
                "Gate Hold Time (ms)",
                200.0f,
                new ConfigDescription(
                    "Time in milliseconds to hold the gate open after signal falls below close threshold.\n마이크에 입력되는 소리가 임계값 이하로 떨어졌을 때, 페이드 아웃이 되기까지의 시간을 정합니다.",
                    new AcceptableValueRange<float>(0.0f, 1000.0f))
            );

            GateReleaseTimeMs = Config.Bind(
                "Noise Gate DSP",
                "Gate Release Time (ms)",
                150.0f,
                new ConfigDescription(
                    "Time in milliseconds to fade out from full audio level to muted when gate closes.\n마이크에 입력되는 소리가 임계값 이하로 떨어졌을 때, 페이드 아웃 되는 시간입니다.",
                    new AcceptableValueRange<float>(0.0f, 1000.0f))
            );

            // Test Mode
            EnableTestOverlay = Config.Bind(
                "Test Mode",
                "Enable Test Overlay",
                false,
                "Enable the in-game microphone test overlay. Shows real-time dB meters, noise gate state, and frequency spectrum.\n게임 내 마이크 테스트 오버레이를 활성화합니다. 실시간 dB 미터, 노이즈 게이트 상태, 주파수 스펙트럼을 표시합니다."
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

            // 4. Create MicTestOverlay component on this GameObject
            try
            {
                _micTestOverlay = gameObject.AddComponent<MicTestOverlay>();
            }
            catch (Exception ex)
            {
                Log.LogError(string.Format("Error adding MicTestOverlay: {0}", ex.Message));
            }

            // 5. Initialize Audio Processors & Harmony Patches safely
            try
            {
                DissonancePatch.Initialize();

                ApplyProcessorSettings();

                // Local player setting change handlers
                EnableNoiseSuppression.SettingChanged += (sender, args) => ApplyProcessorSettings();
                EnableNoiseGate.SettingChanged += (sender, args) => ApplyProcessorSettings();

                // Remote players setting change handlers
                EnableRemoteNoiseSuppression.SettingChanged += (sender, args) => ApplyProcessorSettings();
                EnableRemoteNoiseGate.SettingChanged += (sender, args) => ApplyProcessorSettings();

                // DSP Noise Gate setting change handlers
                GateCloseThresholdDb.SettingChanged += (sender, args) => ApplyProcessorSettings();
                GateOpenThresholdDb.SettingChanged += (sender, args) => ApplyProcessorSettings();
                GateAttackTimeMs.SettingChanged += (sender, args) => ApplyProcessorSettings();
                GateHoldTimeMs.SettingChanged += (sender, args) => ApplyProcessorSettings();
                GateReleaseTimeMs.SettingChanged += (sender, args) => ApplyProcessorSettings();

                _harmony = new Harmony(PluginGUID);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());

                Log.LogInfo(string.Format("{0} v{1} loaded successfully.", PluginName, PluginVersion));
            }
            catch (Exception ex)
            {
                Log.LogError(string.Format("Error during Dissonance patch initialization: {0}", ex.Message));
            }
        }

        public static void ApplyProcessorSettings()
        {
            var proc = DissonancePatch.Processor;
            if (proc != null)
            {
                proc.IsEnabled = EnableNoiseSuppression != null ? EnableNoiseSuppression.Value : true;
                proc.EnableNoiseGate = EnableNoiseGate != null ? EnableNoiseGate.Value : true;
                proc.GateCloseThresholdDb = GateCloseThresholdDb != null ? GateCloseThresholdDb.Value : -32.0f;
                proc.GateOpenThresholdDb = GateOpenThresholdDb != null ? GateOpenThresholdDb.Value : -26.0f;
                proc.GateAttackTimeMs = GateAttackTimeMs != null ? GateAttackTimeMs.Value : 25.0f;
                proc.GateHoldTimeMs = GateHoldTimeMs != null ? GateHoldTimeMs.Value : 200.0f;
                proc.GateReleaseTimeMs = GateReleaseTimeMs != null ? GateReleaseTimeMs.Value : 150.0f;
            }

            var remoteProc = DissonancePatch.RemoteProcessor;
            if (remoteProc != null)
            {
                remoteProc.IsEnabled = EnableRemoteNoiseSuppression != null ? EnableRemoteNoiseSuppression.Value : true;
                remoteProc.EnableNoiseGate = EnableRemoteNoiseGate != null ? EnableRemoteNoiseGate.Value : true;
                remoteProc.GateCloseThresholdDb = GateCloseThresholdDb != null ? GateCloseThresholdDb.Value : -32.0f;
                remoteProc.GateOpenThresholdDb = GateOpenThresholdDb != null ? GateOpenThresholdDb.Value : -26.0f;
                remoteProc.GateAttackTimeMs = GateAttackTimeMs != null ? GateAttackTimeMs.Value : 25.0f;
                remoteProc.GateHoldTimeMs = GateHoldTimeMs != null ? GateHoldTimeMs.Value : 200.0f;
                remoteProc.GateReleaseTimeMs = GateReleaseTimeMs != null ? GateReleaseTimeMs.Value : 150.0f;
            }
        }

        private void PreloadNativeLibrary()
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Info.Location);
                Log.LogInfo(string.Format("Plugin location: {0}", Info.Location));
                Log.LogInfo(string.Format("Native DLL search directory: {0}", pluginDir));

                if (string.IsNullOrEmpty(pluginDir))
                {
                    Log.LogError("Plugin directory is null or empty, cannot load rnnoise.dll!");
                    return;
                }

                SetDllDirectory(pluginDir);

                bool anyLoaded = false;
                string[] nativeDlls = new string[] { "rnnoise.dll", "librnnoise.dll" };
                foreach (var dll in nativeDlls)
                {
                    string fullPath = Path.Combine(pluginDir, dll);
                    bool exists = File.Exists(fullPath);
                    Log.LogInfo(string.Format("Checking for native DLL: {0} -> {1}", fullPath, exists ? "FOUND" : "NOT FOUND"));

                    if (exists)
                    {
                        IntPtr handle = LoadLibrary(fullPath);
                        if (handle != IntPtr.Zero)
                        {
                            Log.LogInfo(string.Format("Successfully preloaded native DLL: {0}", fullPath));
                            anyLoaded = true;
                        }
                        else
                        {
                            int errorCode = Marshal.GetLastWin32Error();
                            Log.LogError(string.Format("LoadLibrary FAILED for {0}, Win32 error code: {1}", fullPath, errorCode));
                        }
                    }
                }

                if (!anyLoaded)
                {
                    Log.LogWarning("=== rnnoise.dll was NOT found! ===");
                    Log.LogWarning(string.Format("Please place rnnoise.dll in: {0}", pluginDir));
                    Log.LogWarning("RNNoise noise suppression will be DISABLED. Noise Gate will still work.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError(string.Format("Native library preload error: {0}", ex.Message));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterLethalConfig()
        {
            LethalConfigManager.SetModDescription("RNNoise AI Deep Learning Microphone Noise Suppression and Noise Gate Mod for Lethal Company.");

            // Local Player Controls
            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(EnableNoiseSuppression, new BoolCheckBoxOptions { RequiresRestart = false }));
            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(EnableNoiseGate, new BoolCheckBoxOptions { RequiresRestart = false }));

            // Remote Players Controls
            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(EnableRemoteNoiseSuppression, new BoolCheckBoxOptions { RequiresRestart = false }));
            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(EnableRemoteNoiseGate, new BoolCheckBoxOptions { RequiresRestart = false }));

            // Shared DSP Controls
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(GateCloseThresholdDb, new FloatSliderOptions { Min = -80.0f, Max = 0.0f, RequiresRestart = false }));
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(GateOpenThresholdDb, new FloatSliderOptions { Min = -80.0f, Max = 0.0f, RequiresRestart = false }));
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(GateAttackTimeMs, new FloatSliderOptions { Min = 0.0f, Max = 200.0f, RequiresRestart = false }));
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(GateHoldTimeMs, new FloatSliderOptions { Min = 0.0f, Max = 1000.0f, RequiresRestart = false }));
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(GateReleaseTimeMs, new FloatSliderOptions { Min = 0.0f, Max = 1000.0f, RequiresRestart = false }));

            // Test Mode
            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(EnableTestOverlay, new BoolCheckBoxOptions { RequiresRestart = false }));
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
