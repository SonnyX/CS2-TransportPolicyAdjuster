using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using System.Collections.Generic;

namespace TransportPolicyAdjuster
{
    [FileLocation(nameof(TransportPolicyAdjuster))]
    [SettingsUIGroupOrder(kSliderGroup)]
    [SettingsUIShowGroupName(kSliderGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public const string kSliderGroup = "Slider";

        public Setting(IMod mod) : base(mod)
        {
        }

        [SettingsUISlider(min = 10, max = 1000, step = 10, scalarMultiplier = 1, unit = "vehicles")]
        [SettingsUISection(kSection, kSliderGroup)]
        public int MaxVehicleCount { get; set; } = 50;

        public override void SetDefaults()
        {
            MaxVehicleCount = 50;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Transport Policy Adjuster" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kSliderGroup), "Sliders" },


                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MaxVehicleCount)), "Maximum Vehicle Count" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.MaxVehicleCount)),
                    $"Adjust the maximum number of vehicles selectable per line"
                },
            };
        }

        public void Unload()
        {
        }
    }
}