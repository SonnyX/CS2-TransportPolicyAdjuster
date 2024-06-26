#define BURST

using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Colossal.Logging;
using Game.SceneFlow;
using HarmonyLib;
using Unity.Burst;
using UnityEngine;

namespace TransportPolicyAdjuster
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger(nameof(TransportPolicyAdjuster)).SetShowsErrorsInUI(true);
        public static Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("Loading TransportPolicyAdjuster");

            var loadDll = SystemInfo.operatingSystemFamily switch
            {
                OperatingSystemFamily.MacOSX => "TransportPolicyAdjuster_mac_x86_64.bundle",
                OperatingSystemFamily.Windows => "TransportPolicyAdjuster_win_x86_64.dll",
                OperatingSystemFamily.Linux => "TransportPolicyAdjuster_linux_x86_64.so",
                _ => throw new System.NotImplementedException(),
            };
            var loadedBurstLibraries = BurstRuntime.LoadAdditionalLibrary(loadDll);

            log.Info($"Did we manage to load the burst libraries? {loadedBurstLibraries}");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEn(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(TransportPolicyAdjuster), m_Setting, new Setting(this));


            var harmony = new Harmony("com.github.sonnyx.transportpolicyadjuster");
            harmony.PatchAll(typeof(Mod).Assembly);

            log.Info($"Harmony patches loaded");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            updateSystem.UpdateAt<ModifiedSystem>(SystemUpdatePhase.Modification4);

        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            m_Setting.UnregisterInOptionsUI();
            m_Setting = null;
        }
    }
}