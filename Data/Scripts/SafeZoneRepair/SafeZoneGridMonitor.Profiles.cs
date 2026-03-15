using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ObjectBuilders.Components;
using VRage.Game.ModAPI;

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
                bool saveNormalizedConfig = false;
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(ZoneGenerationConfigFileName, typeof(SafeZoneGridMonitor)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(ZoneGenerationConfigFileName, typeof(SafeZoneGridMonitor)))
                    {
                        string xml = reader.ReadToEnd();
                        var loaded = MyAPIGateway.Utilities.SerializeFromXML<ZoneGenerationConfig>(xml);
                        _zoneGenerationConfig = loaded ?? ZoneGenerationConfig.CreateDefault();
                    }

                    saveNormalizedConfig = NormalizeZoneGenerationConfig(_zoneGenerationConfig);
                }
                else
                {
                    _zoneGenerationConfig = ZoneGenerationConfig.CreateDefault();
                    saveNormalizedConfig = true;
                }

                if (saveNormalizedConfig)
                    SaveZoneGenerationConfig(_zoneGenerationConfig);
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

        private static bool NormalizeZoneGenerationConfig(ZoneGenerationConfig cfg)
        {
            if (cfg == null)
                return false;

            bool changed = false;
            var defaults = ZoneGenerationConfig.CreateDefault();

            if (string.IsNullOrWhiteSpace(cfg.FallbackProfileId))
            {
                cfg.FallbackProfileId = ZoneProfileIds.Neutral;
                changed = true;
            }

            if (cfg.TagAliases == null)
            {
                cfg.TagAliases = defaults.TagAliases;
                changed = true;
            }

            if (cfg.CustomProfiles == null || cfg.CustomProfiles.Count == 0)
            {
                cfg.CustomProfiles = defaults.CustomProfiles;
                changed = true;
            }

            if (cfg.ProtectedCoreComponents == null)
            {
                cfg.ProtectedCoreComponents = defaults.ProtectedCoreComponents;
                changed = true;
            }

            if (cfg.GlobalGenerationBlacklist == null)
            {
                cfg.GlobalGenerationBlacklist = defaults.GlobalGenerationBlacklist;
                changed = true;
            }

            if (cfg.GlobalGenerationLimitedList == null)
            {
                cfg.GlobalGenerationLimitedList = new List<string>();
                changed = true;
            }

            if (cfg.VariantJitterPercent < 0f)
            {
                cfg.VariantJitterPercent = 0f;
                changed = true;
            }
            else if (cfg.VariantJitterPercent > 0.5f)
            {
                cfg.VariantJitterPercent = 0.5f;
                changed = true;
            }

            if (cfg.CustomProfiles != null)
            {
                for (int i = 0; i < cfg.CustomProfiles.Count; i++)
                {
                    var profile = cfg.CustomProfiles[i];
                    if (profile == null)
                        continue;

                    if (profile.Variants == null)
                    {
                        profile.Variants = new List<ZoneProfileVariantDefinition>();
                        changed = true;
                    }

                    for (int j = 0; j < profile.Variants.Count; j++)
                    {
                        var variant = profile.Variants[j];
                        if (variant == null)
                            continue;

                        if (variant.ForbiddenComponents == null)
                        {
                            variant.ForbiddenComponents = new List<string>();
                            changed = true;
                        }

                        if (variant.ComponentPriceModifiers == null)
                        {
                            variant.ComponentPriceModifiers = new List<ComponentPriceModifierEntry>();
                            changed = true;
                        }

                        if (string.IsNullOrWhiteSpace(variant.PlayerServiceName))
                            variant.PlayerServiceName = null;

                        if (string.IsNullOrWhiteSpace(variant.PlayerRestrictionsSummary))
                            variant.PlayerRestrictionsSummary = null;

                        if (string.IsNullOrWhiteSpace(variant.PlayerDetailsDescription))
                            variant.PlayerDetailsDescription = null;
                    }
                }
            }

            return changed;
        }

        private string GetZoneProfileSourceName(MySafeZone zone)
        {
            string sourceName;
            if (TryGetSafeZoneOwnerGridName(zone, out sourceName))
                return sourceName;

            if (TryGetSafeZoneOwnerBlockName(zone, out sourceName))
                return sourceName;

            return GetSafeZoneDefaultName(zone);
        }

        private bool TryGetSafeZoneOwnerGridName(MySafeZone zone, out string gridName)
        {
            gridName = null;

            IMyCubeBlock block;
            if (!TryGetSafeZoneOwnerBlock(zone, out block) || block == null)
                return false;

            var grid = block.CubeGrid;
            if (grid == null || string.IsNullOrWhiteSpace(grid.DisplayName))
                return false;

            gridName = grid.DisplayName.Trim();
            return !string.IsNullOrWhiteSpace(gridName);
        }

        private bool TryGetSafeZoneOwnerBlockName(MySafeZone zone, out string blockName)
        {
            blockName = null;

            IMyCubeBlock block;
            if (!TryGetSafeZoneOwnerBlock(zone, out block) || block == null)
                return false;

            var terminalBlock = block as Sandbox.ModAPI.IMyTerminalBlock;
            if (terminalBlock != null && !string.IsNullOrWhiteSpace(terminalBlock.CustomName))
            {
                blockName = terminalBlock.CustomName.Trim();
                return true;
            }

            if (!string.IsNullOrWhiteSpace(block.DisplayNameText))
            {
                blockName = block.DisplayNameText.Trim();
                return true;
            }

            return false;
        }

        private bool TryGetSafeZoneOwnerBlock(MySafeZone zone, out IMyCubeBlock block)
        {
            block = null;

            long safeZoneBlockId;
            if (!TryGetSafeZoneBlockEntityId(zone, out safeZoneBlockId))
                return false;

            IMyEntity entity;
            if (MyAPIGateway.Entities == null || !MyAPIGateway.Entities.TryGetEntityById(safeZoneBlockId, out entity) || entity == null)
                return false;

            block = entity as IMyCubeBlock;
            return block != null;
        }

        private bool TryGetSafeZoneBlockEntityId(MySafeZone zone, out long safeZoneBlockId)
        {
            safeZoneBlockId = 0L;
            if (zone == null)
                return false;

            try
            {
                var objectBuilder = zone.GetObjectBuilder() as MyObjectBuilder_SafeZone;
                if (objectBuilder == null || objectBuilder.SafeZoneBlockId == 0L)
                    return false;

                safeZoneBlockId = objectBuilder.SafeZoneBlockId;
                return true;
            }
            catch (Exception ex)
            {
                LogError($"TryGetSafeZoneBlockEntityId error for zone {zone.EntityId}: {ex}");
                return false;
            }
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
                cfg.ProfileSourceName = sourceName;
                cfg.PlayerServiceName = null;
                cfg.PlayerRestrictionsSummary = null;
                cfg.PlayerDetailsDescription = null;
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
            cfg.ProfileSourceName = sourceName;
            cfg.PlayerServiceName = generated.PlayerServiceName;
            cfg.PlayerRestrictionsSummary = generated.PlayerRestrictionsSummary;
            cfg.PlayerDetailsDescription = generated.PlayerDetailsDescription;
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
            else if (string.IsNullOrWhiteSpace(cfg.ProfileSourceName))
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
            string sourceName = cfg.ProfileSourceName;

            if (string.IsNullOrWhiteSpace(sourceName) && cfg.ZoneEntityId != 0L)
            {
                MySafeZone zone;
                if (TryGetSafeZoneByEntityId(cfg.ZoneEntityId, out zone) && zone != null)
                    sourceName = GetZoneProfileSourceName(zone);
            }

            if (string.IsNullOrWhiteSpace(sourceName))
                sourceName = !string.IsNullOrWhiteSpace(cfg.DisplayName) ? cfg.DisplayName : cfg.ZoneName;

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

            if (string.IsNullOrWhiteSpace(cfg.ProfileSourceName))
            {
                cfg.ProfileSourceName = sourceName;
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
