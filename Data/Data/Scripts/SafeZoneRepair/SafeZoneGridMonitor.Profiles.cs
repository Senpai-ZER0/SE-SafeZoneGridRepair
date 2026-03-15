using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;

namespace SafeZoneRepair
{
    public partial class SafeZoneGridMonitor
    {
        private ZoneGenerationConfig _zoneGenerationConfig = ZoneGenerationConfig.CreateDefault();
        private const string ZoneGenerationConfigFileName = "ZoneGenerationConfig.xml";

        private void LoadZoneGenerationConfig()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(ZoneGenerationConfigFileName, typeof(SafeZoneGridMonitor)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(ZoneGenerationConfigFileName, typeof(SafeZoneGridMonitor)))
                    {
                        string xml = reader.ReadToEnd();
                        var loaded = MyAPIGateway.Utilities.SerializeFromXML<ZoneGenerationConfig>(xml);
                        _zoneGenerationConfig = loaded ?? ZoneGenerationConfig.CreateDefault();
                    }
                }
                else
                {
                    _zoneGenerationConfig = ZoneGenerationConfig.CreateDefault();
                    SaveZoneGenerationConfig(_zoneGenerationConfig);
                }

                NormalizeZoneGenerationConfig(_zoneGenerationConfig);
            }
            catch (Exception ex)
            {
                _zoneGenerationConfig = ZoneGenerationConfig.CreateDefault();
                LogError($"LoadZoneGenerationConfig error: {ex}");
            }
        }

        private void SaveZoneGenerationConfig(ZoneGenerationConfig cfg)
        {
            try
            {
                NormalizeZoneGenerationConfig(cfg);
                string xml = MyAPIGateway.Utilities.SerializeToXML(cfg);
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(ZoneGenerationConfigFileName, typeof(SafeZoneGridMonitor)))
                {
                    writer.Write(xml);
                }
            }
            catch (Exception ex)
            {
                LogError($"SaveZoneGenerationConfig error: {ex}");
            }
        }

        private static void NormalizeZoneGenerationConfig(ZoneGenerationConfig cfg)
        {
            if (cfg == null)
                return;

            if (string.IsNullOrWhiteSpace(cfg.FallbackProfileId))
                cfg.FallbackProfileId = ZoneProfileIds.Neutral;

            if (cfg.TagAliases == null)
                cfg.TagAliases = ZoneGenerationConfig.CreateDefault().TagAliases;

            if (cfg.ProtectedCoreComponents == null)
                cfg.ProtectedCoreComponents = ZoneGenerationConfig.CreateDefault().ProtectedCoreComponents;

            if (cfg.GlobalGenerationBlacklist == null)
                cfg.GlobalGenerationBlacklist = ZoneGenerationConfig.CreateDefault().GlobalGenerationBlacklist;

            if (cfg.GlobalGenerationLimitedList == null)
                cfg.GlobalGenerationLimitedList = new List<string>();

            if (cfg.VariantJitterPercent < 0f)
                cfg.VariantJitterPercent = 0f;
            else if (cfg.VariantJitterPercent > 0.5f)
                cfg.VariantJitterPercent = 0.5f;
        }

        private string GetZoneProfileSourceName(MySafeZone zone)
        {
            return GetSafeZoneDefaultName(zone);
        }

        private SafeZoneConfig CreateExampleZoneConfig()
        {
            var cfg = new SafeZoneConfig
            {
                ZoneName = "Example Zone [NEUT]",
                DisplayName = "Example Zone [NEUT]",
                ZoneEntityId = 1234567890,
                Enabled = true,
                DebugMode = false,
                ZoneCreationType = ZoneCreationTypeSafeZoneBlock,
                ComponentPriceModifiers = new List<ComponentPriceModifierEntry>()
            };

            ApplyGeneratedProfileToZoneConfig(cfg, cfg.ZoneName, cfg.ZoneCreationType);
            NormalizeZoneConfig(cfg);
            return cfg;
        }

        private SafeZoneConfig CreateDefaultZoneConfig(MySafeZone zone)
        {
            string zoneName = GetSafeZoneDefaultName(zone);
            string creationType = DetectZoneCreationType(zone);
            var cfg = new SafeZoneConfig
            {
                ZoneEntityId = zone != null ? zone.EntityId : 0L,
                ZoneName = zoneName,
                DisplayName = zoneName,
                Enabled = true,
                DebugMode = false,
                ZoneCreationType = creationType,
                ComponentPriceModifiers = new List<ComponentPriceModifierEntry>()
            };

            ApplyGeneratedProfileToZoneConfig(cfg, zone);
            NormalizeZoneConfig(cfg);
            return cfg;
        }

        private void ApplyGeneratedProfileToZoneConfig(SafeZoneConfig cfg, MySafeZone zone)
        {
            ApplyGeneratedProfileToZoneConfig(cfg, GetZoneProfileSourceName(zone), zone != null ? DetectZoneCreationType(zone) : ZoneCreationTypeSafeZoneBlock);
        }

        private void ApplyGeneratedProfileToZoneConfig(SafeZoneConfig cfg, string sourceName, string creationType)
        {
            if (cfg == null)
                return;

            NormalizeZoneGenerationConfig(_zoneGenerationConfig);
            if (_zoneGenerationConfig == null || !_zoneGenerationConfig.Enabled)
            {
                cfg.ZoneCreationType = NormalizeZoneCreationTypeValue(creationType);
                cfg.ZoneName = string.IsNullOrWhiteSpace(cfg.ZoneName) ? sourceName : cfg.ZoneName;
                cfg.DisplayName = string.IsNullOrWhiteSpace(cfg.DisplayName) ? cfg.ZoneName : cfg.DisplayName;
                cfg.WeldingSpeed = weldingSpeed;
                cfg.CostModifier = costModifier;
                cfg.AllowProjections = true;
                cfg.ProjectionBuildDelay = 1f;
                cfg.ProjectionWeldingSpeed = 1f;
                cfg.ForbiddenComponents = NormalizeForbiddenComponentList(null, true);
                cfg.ComponentPriceModifiers = new List<ComponentPriceModifierEntry>();
                cfg.AssignedProfileId = ZoneProfileIds.Neutral;
                cfg.AssignedVariantId = "GeneratorDisabled";
                cfg.AssignmentSource = ZoneAssignmentSources.GeneratorDisabled;
                cfg.VariantSeed = 0;
                cfg.WasManuallyEdited = false;
                return;
            }

            var generated = ZoneProfileResolver.Resolve(_zoneGenerationConfig, sourceName, cfg.ZoneEntityId);
            cfg.ZoneCreationType = NormalizeZoneCreationTypeValue(creationType);
            cfg.ZoneName = string.IsNullOrWhiteSpace(cfg.ZoneName) ? sourceName : cfg.ZoneName;
            cfg.DisplayName = string.IsNullOrWhiteSpace(cfg.DisplayName) ? cfg.ZoneName : cfg.DisplayName;
            cfg.WeldingSpeed = generated.WeldingSpeed;
            cfg.CostModifier = generated.CostModifier;
            cfg.Enabled = true;
            cfg.AllowProjections = generated.AllowProjections;
            cfg.ProjectionBuildDelay = generated.ProjectionBuildDelay;
            cfg.ProjectionWeldingSpeed = generated.ProjectionWeldingSpeed;
            cfg.ForbiddenComponents = NormalizeForbiddenComponentList(generated.ForbiddenComponents, false);
            cfg.ComponentPriceModifiers = CloneComponentPriceModifiers(generated.ComponentPriceModifiers);
            cfg.AssignedProfileId = generated.ProfileId;
            cfg.AssignedVariantId = generated.VariantId;
            cfg.AssignmentSource = generated.AssignmentSource;
            cfg.VariantSeed = generated.VariantSeed;
            cfg.WasManuallyEdited = false;
        }

        private bool EnsureZoneProfileMetadata(SafeZoneConfig cfg)
        {
            if (cfg == null)
                return false;

            bool changed = false;
            if (string.IsNullOrWhiteSpace(cfg.AssignedProfileId) || string.IsNullOrWhiteSpace(cfg.AssignedVariantId) || string.IsNullOrWhiteSpace(cfg.AssignmentSource))
            {
                changed |= TryAssignProfileToLegacyConfig(cfg);
            }

            return changed;
        }

        private bool TryAssignProfileToLegacyConfig(SafeZoneConfig cfg)
        {
            if (cfg == null)
                return false;

            NormalizeZoneGenerationConfig(_zoneGenerationConfig);
            string sourceName = !string.IsNullOrWhiteSpace(cfg.DisplayName) ? cfg.DisplayName : cfg.ZoneName;
            var resolved = ZoneProfileResolver.Resolve(_zoneGenerationConfig, sourceName, cfg.ZoneEntityId);

            bool changed = false;
            if (string.IsNullOrWhiteSpace(cfg.AssignedProfileId))
            {
                cfg.AssignedProfileId = string.IsNullOrWhiteSpace(resolved.ProfileId) ? ZoneProfileIds.Neutral : resolved.ProfileId;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(cfg.AssignedVariantId))
            {
                cfg.AssignedVariantId = "Legacy";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(cfg.AssignmentSource))
            {
                cfg.AssignmentSource = ZoneAssignmentSources.LegacyConfig;
                changed = true;
            }

            if (cfg.VariantSeed == 0)
            {
                cfg.VariantSeed = resolved.VariantSeed;
                changed = true;
            }

            if (!cfg.WasManuallyEdited)
            {
                cfg.WasManuallyEdited = true;
                changed = true;
            }

            return changed;
        }
    }
}
