using BepInEx;
using Game.Common;
using Game;
using HarmonyLib;

namespace TransportPolicyAdjuster
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [HarmonyPatch]
    public class PublicTransportSettingsPlugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            Harmony.CreateAndPatchAll(typeof(PublicTransportSettingsPlugin).Assembly, MyPluginInfo.PLUGIN_GUID);
        }

        [HarmonyPatch(typeof(SystemOrder), nameof(SystemOrder.Initialize), new[] { typeof(UpdateSystem) })]
        [HarmonyPostfix]
        private static void InjectSystems(UpdateSystem updateSystem)
        {
            updateSystem.UpdateAt<ModifiedSystem>(SystemUpdatePhase.Modification4);
        }

        [HarmonyPatch(typeof(Game.Policies.ModifiedSystem), nameof(OnUpdate))]
        [HarmonyPrefix]
        private static bool OnUpdate(ref Game.Policies.ModifiedSystem __instance)
        {
            __instance.Enabled = false;
            return false;
        }
    }
}
