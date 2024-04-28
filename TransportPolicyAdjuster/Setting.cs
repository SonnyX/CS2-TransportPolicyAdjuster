using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Prefabs;
using Game.Settings;
using System.Collections.Generic;

namespace TransportPolicyAdjuster
{
    [FileLocation(nameof(TransportPolicyAdjuster))]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public Setting(IMod mod) : base(mod)
        {
        }

        [SettingsUISlider(min = 10, max = 250, step = 10, scalarMultiplier = 1, unit = "vehicles")]
        [SettingsUISection(kSection)]
        public int Bus { get; set; } = 50;

        [SettingsUISlider(min = 10, max = 100, step = 10, scalarMultiplier = 1, unit = "vehicles")]
        [SettingsUISection(kSection)]
        public int Train { get; set; } = 20;

        [SettingsUISlider(min = 5, max = 200, step = 5, scalarMultiplier = 1, unit = "vehicles")]
        [SettingsUISection(kSection)]
        public int Tram { get; set; } = 25;

        [SettingsUISlider(min = 5, max = 50, step = 5, scalarMultiplier = 1, unit = "vehicles")]
        [SettingsUISection(kSection)]
        public int Ship { get; set; } = 5;

        [SettingsUISlider(min = 5, max = 50, step = 5, scalarMultiplier = 1, unit = "vehicles")]
        [SettingsUISection(kSection)]
        public int Airplane { get; set; } = 5;

        [SettingsUISlider(min = 5, max = 100, step = 5, scalarMultiplier = 1, unit = "vehicles")]
        [SettingsUISection(kSection)]
        public int Subway { get; set; } = 10;

        public int GetMaximumCount(TransportType transportType) => transportType switch
        {
            TransportType.Bus => Bus,
            TransportType.Train => Train,
            TransportType.Tram => Tram,
            TransportType.Ship => Ship,
            TransportType.Airplane => Airplane,
            TransportType.Subway => Subway,
            _ => 50
        };

        public override void SetDefaults()
        {
            Bus = 50;
            Train = 10;
            Tram = 25;
            Ship = 5;
            Airplane = 5;
            Subway = 5;
        }
    }

    public class LocaleEn : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEn(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Transport Policy Adjuster" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Bus)), "Maximum Vehicle Count (Bus)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Bus)), $"Adjust the maximum number of vehicles selectable per line" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Train)), "Maximum Vehicle Count (Train)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Train)), $"Adjust the maximum number of vehicles selectable per line" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Tram)), "Maximum Vehicle Count (Tram)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Tram)), $"Adjust the maximum number of vehicles selectable per line" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Ship)), "Maximum Vehicle Count (Ship)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Ship)), $"Adjust the maximum number of vehicles selectable per line" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Airplane)), "Maximum Vehicle Count (Airplane)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Airplane)), $"Adjust the maximum number of vehicles selectable per line" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Subway)), "Maximum Vehicle Count (Subway)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Subway)), $"Adjust the maximum number of vehicles selectable per line" },
            };
        }

        public void Unload()
        {
        }
    }
}