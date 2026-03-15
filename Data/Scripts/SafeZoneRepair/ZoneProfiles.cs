using System;
using System.Collections.Generic;

namespace SafeZoneRepair
{
    public static class ZoneProfileIds
    {
        public const string Neutral = "Neutral";
        public const string Industrial = "Industrial";
        public const string Civilian = "Civilian";
        public const string Military = "Military";
        public const string Premium = "Premium";
    }

    public static class ZoneAssignmentSources
    {
        public const string NameTag = "NameTag";
        public const string FallbackNeutral = "FallbackNeutral";
        public const string LegacyConfig = "LegacyConfig";
        public const string GeneratorDisabled = "GeneratorDisabled";
    }

    public class ZoneProfileVariantDefinition
    {
        public string VariantId { get; set; }
        public float WeldingSpeed { get; set; }
        public float ProjectionWeldingSpeed { get; set; }
        public float CostModifier { get; set; }
        public bool AllowProjections { get; set; }
        public float ProjectionBuildDelay { get; set; }
        public string PlayerServiceName { get; set; }
        public string PlayerRestrictionsSummary { get; set; }
        public string PlayerDetailsDescription { get; set; }
        public List<string> ForbiddenComponents { get; set; } = new List<string>();
        public List<ComponentPriceModifierEntry> ComponentPriceModifiers { get; set; } = new List<ComponentPriceModifierEntry>();
    }

    public class GeneratedZoneProfileResult
    {
        public string ProfileId { get; set; }
        public string VariantId { get; set; }
        public string AssignmentSource { get; set; }
        public int VariantSeed { get; set; }

        public float WeldingSpeed { get; set; }
        public float ProjectionWeldingSpeed { get; set; }
        public float CostModifier { get; set; }
        public bool AllowProjections { get; set; }
        public float ProjectionBuildDelay { get; set; }
        public string PlayerServiceName { get; set; }
        public string PlayerRestrictionsSummary { get; set; }
        public string PlayerDetailsDescription { get; set; }
        public List<string> ForbiddenComponents { get; set; } = new List<string>();
        public List<ComponentPriceModifierEntry> ComponentPriceModifiers { get; set; } = new List<ComponentPriceModifierEntry>();
    }

    public static class ZoneProfileResolver
    {
        public static GeneratedZoneProfileResult Resolve(ZoneGenerationConfig config, string sourceName, long zoneEntityId)
        {
            config = config ?? ZoneGenerationConfig.CreateDefault();

            string assignmentSource;
            string profileId = DetectProfileIdFromName(config, sourceName, out assignmentSource);
            if (string.IsNullOrWhiteSpace(profileId))
            {
                profileId = string.IsNullOrWhiteSpace(config.FallbackProfileId) ? ZoneProfileIds.Neutral : config.FallbackProfileId;
                assignmentSource = ZoneAssignmentSources.FallbackNeutral;
            }

            int seed = ComputeSeed(sourceName, zoneEntityId);
            var variants = GetVariants(config, profileId);
            if (variants.Count == 0 && !string.Equals(profileId, ZoneProfileIds.Neutral, StringComparison.OrdinalIgnoreCase))
                variants = GetVariants(config, ZoneProfileIds.Neutral);

            if (variants.Count == 0)
                variants = CloneProfileVariants(CreateBuiltInProfileDefinitions()[0].Variants);

            var variant = variants[Math.Abs(seed) % variants.Count];

            var result = new GeneratedZoneProfileResult
            {
                ProfileId = profileId,
                VariantId = variant.VariantId,
                AssignmentSource = assignmentSource,
                VariantSeed = seed,
                WeldingSpeed = variant.WeldingSpeed,
                ProjectionWeldingSpeed = variant.ProjectionWeldingSpeed,
                CostModifier = variant.CostModifier,
                AllowProjections = variant.AllowProjections,
                ProjectionBuildDelay = variant.ProjectionBuildDelay,
                PlayerServiceName = variant.PlayerServiceName,
                PlayerRestrictionsSummary = variant.PlayerRestrictionsSummary,
                PlayerDetailsDescription = variant.PlayerDetailsDescription,
                ForbiddenComponents = CloneComponents(variant.ForbiddenComponents),
                ComponentPriceModifiers = CloneModifiers(variant.ComponentPriceModifiers)
            };

            if (config.AllowVariantJitter && config.VariantJitterPercent > 0f)
                ApplyJitter(result, seed, config.VariantJitterPercent);

            ApplyGlobalGenerationRules(result, config);
            return result;
        }

        public static List<ZoneGenerationProfileDefinition> CreateBuiltInProfileDefinitions()
        {
            return new List<ZoneGenerationProfileDefinition>
            {
                new ZoneGenerationProfileDefinition
                {
                    ProfileId = ZoneProfileIds.Neutral,
                    Variants = new List<ZoneProfileVariantDefinition>
                    {
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "Default",
                            WeldingSpeed = 1.0f,
                            ProjectionWeldingSpeed = 1.0f,
                            CostModifier = 1.0f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 1.0f,
                            ForbiddenComponents = new List<string>()
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "Conservative",
                            WeldingSpeed = 0.9f,
                            ProjectionWeldingSpeed = 0.9f,
                            CostModifier = 0.95f,
                            AllowProjections = false,
                            ProjectionBuildDelay = 1.11f,
                            ForbiddenComponents = new List<string> { "Superconductor" }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "LightService",
                            WeldingSpeed = 1.05f,
                            ProjectionWeldingSpeed = 0.95f,
                            CostModifier = 1.05f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 1.05f,
                            ForbiddenComponents = new List<string> { "GravityGenerator" }
                        }
                    }
                },
                new ZoneGenerationProfileDefinition
                {
                    ProfileId = ZoneProfileIds.Industrial,
                    Variants = new List<ZoneProfileVariantDefinition>
                    {
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "Balanced",
                            WeldingSpeed = 1.15f,
                            ProjectionWeldingSpeed = 1.15f,
                            CostModifier = 1.15f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.87f,
                            ForbiddenComponents = new List<string> { "GravityGenerator" }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "FastExpensive",
                            WeldingSpeed = 1.5f,
                            ProjectionWeldingSpeed = 1.45f,
                            CostModifier = 1.6f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.7f,
                            ForbiddenComponents = new List<string>()
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "ProjectionFriendly",
                            WeldingSpeed = 1.05f,
                            ProjectionWeldingSpeed = 1.5f,
                            CostModifier = 1.35f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.65f,
                            ForbiddenComponents = new List<string> { "Medical" }
                        }
                    }
                },
                new ZoneGenerationProfileDefinition
                {
                    ProfileId = ZoneProfileIds.Civilian,
                    Variants = new List<ZoneProfileVariantDefinition>
                    {
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "Affordable",
                            WeldingSpeed = 0.9f,
                            ProjectionWeldingSpeed = 0.85f,
                            CostModifier = 0.95f,
                            AllowProjections = false,
                            ProjectionBuildDelay = 1.18f,
                            ForbiddenComponents = new List<string> { "Superconductor", "GravityGenerator" }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "Reliable",
                            WeldingSpeed = 1.0f,
                            ProjectionWeldingSpeed = 1.0f,
                            CostModifier = 1.05f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 1.0f,
                            ForbiddenComponents = new List<string> { "Superconductor" }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "BasicService",
                            WeldingSpeed = 0.85f,
                            ProjectionWeldingSpeed = 0.75f,
                            CostModifier = 0.9f,
                            AllowProjections = false,
                            ProjectionBuildDelay = 1.33f,
                            ForbiddenComponents = new List<string> { "GravityGenerator", "Reactor", "Superconductor" }
                        }
                    }
                },
                new ZoneGenerationProfileDefinition
                {
                    ProfileId = ZoneProfileIds.Military,
                    Variants = new List<ZoneProfileVariantDefinition>
                    {
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "RapidService",
                            WeldingSpeed = 1.4f,
                            ProjectionWeldingSpeed = 1.15f,
                            CostModifier = 1.55f,
                            AllowProjections = false,
                            ProjectionBuildDelay = 0.87f,
                            ForbiddenComponents = new List<string> { "Medical" }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "RestrictedTech",
                            WeldingSpeed = 1.2f,
                            ProjectionWeldingSpeed = 1.0f,
                            CostModifier = 1.3f,
                            AllowProjections = false,
                            ProjectionBuildDelay = 1.0f,
                            ForbiddenComponents = new List<string> { "Medical", "GravityGenerator" }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "SecureExpensive",
                            WeldingSpeed = 1.25f,
                            ProjectionWeldingSpeed = 1.1f,
                            CostModifier = 1.7f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.9f,
                            ForbiddenComponents = new List<string> { "Medical" }
                        }
                    }
                },
                new ZoneGenerationProfileDefinition
                {
                    ProfileId = ZoneProfileIds.Premium,
                    Variants = new List<ZoneProfileVariantDefinition>
                    {
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "HighEnd",
                            WeldingSpeed = 1.6f,
                            ProjectionWeldingSpeed = 1.6f,
                            CostModifier = 1.85f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.63f,
                            ForbiddenComponents = new List<string>()
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "FullService",
                            WeldingSpeed = 1.45f,
                            ProjectionWeldingSpeed = 1.5f,
                            CostModifier = 1.7f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.67f,
                            ForbiddenComponents = new List<string>()
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "FastLuxury",
                            WeldingSpeed = 1.75f,
                            ProjectionWeldingSpeed = 1.35f,
                            CostModifier = 2.0f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.74f,
                            ForbiddenComponents = new List<string>()
                        }
                    }
                }
            };
        }

        public static List<ZoneProfileVariantDefinition> CloneProfileVariants(List<ZoneProfileVariantDefinition> source)
        {
            var clone = new List<ZoneProfileVariantDefinition>();
            if (source == null)
                return clone;

            for (int i = 0; i < source.Count; i++)
            {
                var variant = source[i];
                if (variant == null || string.IsNullOrWhiteSpace(variant.VariantId))
                    continue;

                clone.Add(new ZoneProfileVariantDefinition
                {
                    VariantId = variant.VariantId.Trim(),
                    WeldingSpeed = variant.WeldingSpeed,
                    ProjectionWeldingSpeed = variant.ProjectionWeldingSpeed,
                    CostModifier = variant.CostModifier,
                    AllowProjections = variant.AllowProjections,
                    ProjectionBuildDelay = variant.ProjectionBuildDelay,
                    ForbiddenComponents = CloneComponents(variant.ForbiddenComponents),
                    ComponentPriceModifiers = CloneModifiers(variant.ComponentPriceModifiers)
                });
            }

            return clone;
        }

        private static string DetectProfileIdFromName(ZoneGenerationConfig config, string sourceName, out string assignmentSource)
        {
            assignmentSource = null;
            if (string.IsNullOrWhiteSpace(sourceName) || config == null || config.TagAliases == null)
                return null;

            string normalizedName = sourceName.ToUpperInvariant();
            for (int i = 0; i < config.TagAliases.Count; i++)
            {
                var alias = config.TagAliases[i];
                if (alias == null || string.IsNullOrWhiteSpace(alias.Tag) || string.IsNullOrWhiteSpace(alias.ProfileId))
                    continue;

                if (normalizedName.Contains(alias.Tag.Trim().ToUpperInvariant()))
                {
                    assignmentSource = ZoneAssignmentSources.NameTag;
                    return alias.ProfileId.Trim();
                }
            }

            return null;
        }

        private static int ComputeSeed(string sourceName, long zoneEntityId)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + zoneEntityId.GetHashCode();
                if (!string.IsNullOrWhiteSpace(sourceName))
                    hash = (hash * 31) + sourceName.ToLowerInvariant().GetHashCode();

                return hash == int.MinValue ? int.MaxValue : Math.Abs(hash);
            }
        }

        private static void ApplyJitter(GeneratedZoneProfileResult result, int seed, float percent)
        {
            result.WeldingSpeed = ClampJitterValue(result.WeldingSpeed, 0.1f, 100f, seed ^ 0x11A37, percent);
            result.ProjectionWeldingSpeed = ClampJitterValue(result.ProjectionWeldingSpeed, 0.1f, 100f, seed ^ 0x34BCD, percent);
            result.CostModifier = ClampJitterValue(result.CostModifier, 0f, 100f, seed ^ 0x52F11, percent);
            result.ProjectionBuildDelay = ClampJitterValue(result.ProjectionBuildDelay, 0.1f, 100f, seed ^ 0x77E29, percent);
        }

        private static float ClampJitterValue(float value, float min, float max, int localSeed, float percent)
        {
            var random = new Random(localSeed);
            double factor = 1d + ((random.NextDouble() * 2d - 1d) * percent);
            float jittered = (float)Math.Round(value * factor, 2);
            if (jittered < min)
                jittered = min;
            if (jittered > max)
                jittered = max;
            return jittered;
        }

        private static void ApplyGlobalGenerationRules(GeneratedZoneProfileResult result, ZoneGenerationConfig config)
        {
            var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var protectedCore = new HashSet<string>(config.ProtectedCoreComponents ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            if (result.ForbiddenComponents != null)
            {
                for (int i = 0; i < result.ForbiddenComponents.Count; i++)
                {
                    var component = result.ForbiddenComponents[i];
                    if (string.IsNullOrWhiteSpace(component) || protectedCore.Contains(component.Trim()))
                        continue;

                    forbidden.Add(component.Trim());
                }
            }

            if (config.GlobalGenerationBlacklist != null)
            {
                for (int i = 0; i < config.GlobalGenerationBlacklist.Count; i++)
                {
                    var component = config.GlobalGenerationBlacklist[i];
                    if (string.IsNullOrWhiteSpace(component) || protectedCore.Contains(component.Trim()))
                        continue;

                    forbidden.Add(component.Trim());
                }
            }

            var list = new List<string>(forbidden);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            result.ForbiddenComponents = list;
        }

        private static List<ZoneProfileVariantDefinition> GetVariants(ZoneGenerationConfig config, string profileId)
        {
            var custom = GetCustomVariants(config, profileId);
            if (custom.Count > 0)
                return custom;

            if (config != null && !config.UseBuiltInProfilesAsFallback)
                return new List<ZoneProfileVariantDefinition>();

            return GetBuiltInVariants(profileId);
        }

        private static List<ZoneProfileVariantDefinition> GetCustomVariants(ZoneGenerationConfig config, string profileId)
        {
            if (config == null || config.CustomProfiles == null || string.IsNullOrWhiteSpace(profileId))
                return new List<ZoneProfileVariantDefinition>();

            for (int i = 0; i < config.CustomProfiles.Count; i++)
            {
                var profile = config.CustomProfiles[i];
                if (profile == null || string.IsNullOrWhiteSpace(profile.ProfileId))
                    continue;

                if (!string.Equals(profile.ProfileId.Trim(), profileId.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                return CloneProfileVariants(profile.Variants);
            }

            return new List<ZoneProfileVariantDefinition>();
        }

        private static List<ZoneProfileVariantDefinition> GetBuiltInVariants(string profileId)
        {
            var builtInProfiles = CreateBuiltInProfileDefinitions();
            for (int i = 0; i < builtInProfiles.Count; i++)
            {
                var profile = builtInProfiles[i];
                if (profile == null || string.IsNullOrWhiteSpace(profile.ProfileId))
                    continue;

                if (string.Equals(profile.ProfileId.Trim(), profileId.Trim(), StringComparison.OrdinalIgnoreCase))
                    return CloneProfileVariants(profile.Variants);
            }

            return new List<ZoneProfileVariantDefinition>();
        }

        private static List<string> CloneComponents(List<string> source)
        {
            return source == null ? new List<string>() : new List<string>(source);
        }

        private static List<ComponentPriceModifierEntry> CloneModifiers(List<ComponentPriceModifierEntry> source)
        {
            var clone = new List<ComponentPriceModifierEntry>();
            if (source == null)
                return clone;

            for (int i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ComponentSubtypeId))
                    continue;

                clone.Add(new ComponentPriceModifierEntry
                {
                    ComponentSubtypeId = entry.ComponentSubtypeId.Trim(),
                    Multiplier = entry.Multiplier
                });
            }

            return clone;
        }
    }
}
