using System;
using System.Collections.Generic;

namespace SafeZoneRepair
{
    public class ZoneGenerationTagAliasEntry
    {
        public string Tag { get; set; }
        public string ProfileId { get; set; }
    }

    public class ZoneGenerationProfileDefinition
    {
        public string ProfileId { get; set; }
        public List<ZoneProfileVariantDefinition> Variants { get; set; } = new List<ZoneProfileVariantDefinition>();
    }

    public class ZoneGenerationConfig
    {
        public bool Enabled { get; set; } = true;
        public string FallbackProfileId { get; set; } = ZoneProfileIds.Neutral;
        public bool ApplyLegacyMetadataFallback { get; set; } = true;
        public bool AllowVariantJitter { get; set; } = true;
        public float VariantJitterPercent { get; set; } = 0.08f;
        public bool UseBuiltInProfilesAsFallback { get; set; } = true;

        public List<ZoneGenerationTagAliasEntry> TagAliases { get; set; } = new List<ZoneGenerationTagAliasEntry>();
        public List<ZoneGenerationProfileDefinition> CustomProfiles { get; set; } = new List<ZoneGenerationProfileDefinition>();
        public List<string> ProtectedCoreComponents { get; set; } = new List<string>();
        public List<string> GlobalGenerationBlacklist { get; set; } = new List<string>();
        public List<string> GlobalGenerationLimitedList { get; set; } = new List<string>();

        public static ZoneGenerationConfig CreateDefault()
        {
            return new ZoneGenerationConfig
            {
                Enabled = true,
                FallbackProfileId = ZoneProfileIds.Neutral,
                ApplyLegacyMetadataFallback = true,
                AllowVariantJitter = true,
                VariantJitterPercent = 0.08f,
                UseBuiltInProfilesAsFallback = true,
                TagAliases = new List<ZoneGenerationTagAliasEntry>
                {
                    new ZoneGenerationTagAliasEntry { Tag = "[NEUT]", ProfileId = ZoneProfileIds.Neutral },
                    new ZoneGenerationTagAliasEntry { Tag = "[NEUTRAL]", ProfileId = ZoneProfileIds.Neutral },
                    new ZoneGenerationTagAliasEntry { Tag = "[IND]", ProfileId = ZoneProfileIds.Industrial },
                    new ZoneGenerationTagAliasEntry { Tag = "[INDUSTRIAL]", ProfileId = ZoneProfileIds.Industrial },
                    new ZoneGenerationTagAliasEntry { Tag = "[CIV]", ProfileId = ZoneProfileIds.Civilian },
                    new ZoneGenerationTagAliasEntry { Tag = "[CIVIL]", ProfileId = ZoneProfileIds.Civilian },
                    new ZoneGenerationTagAliasEntry { Tag = "[TRADE]", ProfileId = ZoneProfileIds.Civilian },
                    new ZoneGenerationTagAliasEntry { Tag = "[MIL]", ProfileId = ZoneProfileIds.Military },
                    new ZoneGenerationTagAliasEntry { Tag = "[MILITARY]", ProfileId = ZoneProfileIds.Military },
                    new ZoneGenerationTagAliasEntry { Tag = "[PREM]", ProfileId = ZoneProfileIds.Premium },
                    new ZoneGenerationTagAliasEntry { Tag = "[PREMIUM]", ProfileId = ZoneProfileIds.Premium }
                },
                CustomProfiles = ZoneProfileResolver.CreateBuiltInProfileDefinitions(),
                ProtectedCoreComponents = new List<string>
                {
                    "SteelPlate",
                    "InteriorPlate",
                    "Construction",
                    "MetalGrid",
                    "Motor",
                    "Computer",
                    "Display",
                    "Girder",
                    "SmallTube",
                    "LargeTube"
                },
                GlobalGenerationBlacklist = new List<string>
                {
                    "PrototechCapacitor",
                    "PrototechCircuitry",
                    "PrototechCoolingUnit",
                    "PrototechFrame",
                    "PrototechMachinery",
                    "PrototechPanel",
                    "PrototechPropulsionUnit"
                },
                GlobalGenerationLimitedList = new List<string>()
            };
        }
    }
}
