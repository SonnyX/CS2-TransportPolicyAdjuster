/*
#define BURST

using Game;
using Game.Modding;
using Colossal.Logging;

namespace TransportPolicyAdjuster
{
    public class TestMod : IMod
    {
        public static ILog log = LogManager.GetLogger(nameof(TestMod), false);

        public void OnCreateWorld(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnCreateWorld));
            updateSystem.UpdateAt<PrintPopulationSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<DeltaTimePrintSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<TestModSystem>(SystemUpdatePhase.GameSimulation);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
        }

        public void OnLoad()
        {
            log.Info(nameof(OnLoad));
        }
    }
}
*/