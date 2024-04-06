#define BURST

using Game;
using Game.Modding;
using Colossal.Logging;
using Game.SceneFlow;
using HarmonyLib;

namespace TransportPolicyAdjuster
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(TransportPolicyAdjuster)}.{nameof(Mod)}").SetShowsErrorsInUI(true);

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            var harmony = new Harmony("com.github.sonnyx.transportpolicyadjuster");
            harmony.PatchAll(typeof(Mod).Assembly);

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            updateSystem.UpdateAt<ModifiedSystem>(SystemUpdatePhase.Modification4);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
        }
    }
}