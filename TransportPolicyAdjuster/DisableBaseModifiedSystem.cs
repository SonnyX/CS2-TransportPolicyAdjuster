using HarmonyLib;

namespace TransportPolicyAdjuster
{
    [HarmonyPatch]
    public class DisableBaseModifiedSystem
    {
        [HarmonyPatch(typeof(Game.Policies.ModifiedSystem), nameof(OnUpdate))]
        [HarmonyPrefix]
        private static bool OnUpdate(ref Game.Policies.ModifiedSystem __instance)
        {
            __instance.Enabled = false;
            return false;
        }
    }
}
