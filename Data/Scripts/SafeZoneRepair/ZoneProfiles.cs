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
        public float? WeldingSpeedRandomMin { get; set; }
        public float? WeldingSpeedRandomMax { get; set; }
        public float? ProjectionWeldingSpeedRandomMin { get; set; }
        public float? ProjectionWeldingSpeedRandomMax { get; set; }
        public float? CostModifierRandomMin { get; set; }
        public float? CostModifierRandomMax { get; set; }
        public float? ProjectionBuildDelayRandomMin { get; set; }
        public float? ProjectionBuildDelayRandomMax { get; set; }
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
        public float? WeldingSpeedRandomMin { get; set; }
        public float? WeldingSpeedRandomMax { get; set; }
        public float? ProjectionWeldingSpeedRandomMin { get; set; }
        public float? ProjectionWeldingSpeedRandomMax { get; set; }
        public float? CostModifierRandomMin { get; set; }
        public float? CostModifierRandomMax { get; set; }
        public float? ProjectionBuildDelayRandomMin { get; set; }
        public float? ProjectionBuildDelayRandomMax { get; set; }
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

            if (config.AllowVariantJitter)
                ApplyJitter(result, variant, config, seed);

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
                            WeldingSpeed = 1.00f,
                            ProjectionWeldingSpeed = 1.00f,
                            CostModifier = 1.00f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 1.00f,
                            PlayerServiceName = "General Service",
                            PlayerRestrictionsSummary = "No major restrictions",
                            PlayerDetailsDescription = "General-purpose service zone with balanced pricing and support for most standard systems.",
                            WeldingSpeedRandomMin = -0.04f,
                            WeldingSpeedRandomMax = 0.06f,
                            ProjectionWeldingSpeedRandomMin = -0.06f,
                            ProjectionWeldingSpeedRandomMax = 0.08f,
                            CostModifierRandomMin = 0.00f,
                            CostModifierRandomMax = 0.06f,
                            ProjectionBuildDelayRandomMin = -0.04f,
                            ProjectionBuildDelayRandomMax = 0.06f,
                            ForbiddenComponents = new List<string>(),
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>()
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "Conservative",
                            WeldingSpeed = 0.92f,
                            ProjectionWeldingSpeed = 0.88f,
                            CostModifier = 0.96f,
                            AllowProjections = false,
                            ProjectionBuildDelay = 1.10f,
                            PlayerServiceName = "General Service",
                            PlayerRestrictionsSummary = "Projection support unavailable",
                            PlayerDetailsDescription = "Conservative utility service focused on routine repairs with reduced projection support.",
                            WeldingSpeedRandomMin = -0.05f,
                            WeldingSpeedRandomMax = 0.04f,
                            ProjectionWeldingSpeedRandomMin = -0.05f,
                            ProjectionWeldingSpeedRandomMax = 0.03f,
                            CostModifierRandomMin = -0.02f,
                            CostModifierRandomMax = 0.05f,
                            ProjectionBuildDelayRandomMin = -0.02f,
                            ProjectionBuildDelayRandomMax = 0.08f,
                            ForbiddenComponents = new List<string> { "Superconductor" },
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>()
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "LightService",
                            WeldingSpeed = 1.04f,
                            ProjectionWeldingSpeed = 0.96f,
                            CostModifier = 1.04f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 1.04f,
                            PlayerServiceName = "General Service",
                            PlayerRestrictionsSummary = "Gravity hardware limited",
                            PlayerDetailsDescription = "Balanced general service with moderate throughput and limited support for gravity systems.",
                            WeldingSpeedRandomMin = -0.03f,
                            WeldingSpeedRandomMax = 0.07f,
                            ProjectionWeldingSpeedRandomMin = -0.04f,
                            ProjectionWeldingSpeedRandomMax = 0.06f,
                            CostModifierRandomMin = 0.00f,
                            CostModifierRandomMax = 0.06f,
                            ProjectionBuildDelayRandomMin = -0.04f,
                            ProjectionBuildDelayRandomMax = 0.05f,
                            ForbiddenComponents = new List<string> { "GravityGenerator" },
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Detector", Multiplier = 1.05f }
                            }
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
                            WeldingSpeed = 0.92f,
                            ProjectionWeldingSpeed = 0.86f,
                            CostModifier = 0.94f,
                            AllowProjections = false,
                            ProjectionBuildDelay = 1.18f,
                            PlayerServiceName = "Civilian Service",
                            PlayerRestrictionsSummary = "Advanced power and gravity parts limited",
                            PlayerDetailsDescription = "Affordable civilian service focused on routine hull and system repairs. Advanced power and gravity systems are limited.",
                            WeldingSpeedRandomMin = -0.05f,
                            WeldingSpeedRandomMax = 0.06f,
                            ProjectionWeldingSpeedRandomMin = -0.05f,
                            ProjectionWeldingSpeedRandomMax = 0.05f,
                            CostModifierRandomMin = -0.02f,
                            CostModifierRandomMax = 0.06f,
                            ProjectionBuildDelayRandomMin = -0.02f,
                            ProjectionBuildDelayRandomMax = 0.08f,
                            ForbiddenComponents = new List<string> { "Superconductor", "GravityGenerator" },
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "PowerCell", Multiplier = 1.12f }
                            }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "Reliable",
                            WeldingSpeed = 1.00f,
                            ProjectionWeldingSpeed = 0.98f,
                            CostModifier = 1.02f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 1.00f,
                            PlayerServiceName = "Civilian Service",
                            PlayerRestrictionsSummary = "Advanced power parts limited",
                            PlayerDetailsDescription = "Reliable port-side service with standard throughput, moderate prices, and support for common civilian systems.",
                            WeldingSpeedRandomMin = -0.04f,
                            WeldingSpeedRandomMax = 0.06f,
                            ProjectionWeldingSpeedRandomMin = -0.04f,
                            ProjectionWeldingSpeedRandomMax = 0.08f,
                            CostModifierRandomMin = 0.00f,
                            CostModifierRandomMax = 0.06f,
                            ProjectionBuildDelayRandomMin = -0.04f,
                            ProjectionBuildDelayRandomMax = 0.05f,
                            ForbiddenComponents = new List<string> { "Superconductor" },
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "PowerCell", Multiplier = 1.08f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Medical", Multiplier = 1.12f }
                            }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "BasicService",
                            WeldingSpeed = 0.84f,
                            ProjectionWeldingSpeed = 0.74f,
                            CostModifier = 0.91f,
                            AllowProjections = false,
                            ProjectionBuildDelay = 1.35f,
                            PlayerServiceName = "Civilian Service",
                            PlayerRestrictionsSummary = "Advanced systems restricted",
                            PlayerDetailsDescription = "Basic civilian repair berth intended for affordable maintenance. Advanced systems, heavy power and gravity hardware are restricted.",
                            WeldingSpeedRandomMin = -0.05f,
                            WeldingSpeedRandomMax = 0.05f,
                            ProjectionWeldingSpeedRandomMin = -0.05f,
                            ProjectionWeldingSpeedRandomMax = 0.04f,
                            CostModifierRandomMin = -0.01f,
                            CostModifierRandomMax = 0.05f,
                            ProjectionBuildDelayRandomMin = -0.02f,
                            ProjectionBuildDelayRandomMax = 0.10f,
                            ForbiddenComponents = new List<string> { "GravityGenerator", "Reactor", "Superconductor" },
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Detector", Multiplier = 1.10f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "RadioCommunication", Multiplier = 1.10f }
                            }
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
                            PlayerServiceName = "Industrial Yard",
                            PlayerRestrictionsSummary = "Gravity hardware limited",
                            PlayerDetailsDescription = "Industrial yard focused on productive repair throughput and practical projection support. Exotic gravity hardware is restricted.",
                            WeldingSpeedRandomMin = -0.03f,
                            WeldingSpeedRandomMax = 0.09f,
                            ProjectionWeldingSpeedRandomMin = -0.02f,
                            ProjectionWeldingSpeedRandomMax = 0.12f,
                            CostModifierRandomMin = 0.03f,
                            CostModifierRandomMax = 0.10f,
                            ProjectionBuildDelayRandomMin = -0.06f,
                            ProjectionBuildDelayRandomMax = 0.04f,
                            ForbiddenComponents = new List<string> { "GravityGenerator" },
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Motor", Multiplier = 0.95f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "MetalGrid", Multiplier = 0.95f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Computer", Multiplier = 0.97f }
                            }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "FastExpensive",
                            WeldingSpeed = 1.50f,
                            ProjectionWeldingSpeed = 1.45f,
                            CostModifier = 1.60f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.70f,
                            PlayerServiceName = "Industrial Yard",
                            PlayerRestrictionsSummary = "High-throughput service at premium rates",
                            PlayerDetailsDescription = "Express industrial repair with strong projection support and premium pricing for priority turnaround.",
                            WeldingSpeedRandomMin = 0.00f,
                            WeldingSpeedRandomMax = 0.12f,
                            ProjectionWeldingSpeedRandomMin = 0.02f,
                            ProjectionWeldingSpeedRandomMax = 0.15f,
                            CostModifierRandomMin = 0.06f,
                            CostModifierRandomMax = 0.16f,
                            ProjectionBuildDelayRandomMin = -0.08f,
                            ProjectionBuildDelayRandomMax = 0.03f,
                            ForbiddenComponents = new List<string>(),
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "PowerCell", Multiplier = 1.15f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Superconductor", Multiplier = 1.20f }
                            }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "ProjectionFriendly",
                            WeldingSpeed = 1.06f,
                            ProjectionWeldingSpeed = 1.48f,
                            CostModifier = 1.34f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.64f,
                            PlayerServiceName = "Industrial Yard",
                            PlayerRestrictionsSummary = "Medical systems limited; strong projection support",
                            PlayerDetailsDescription = "Projection-focused industrial service with rapid construction throughput and moderate premium pricing.",
                            WeldingSpeedRandomMin = -0.02f,
                            WeldingSpeedRandomMax = 0.08f,
                            ProjectionWeldingSpeedRandomMin = 0.04f,
                            ProjectionWeldingSpeedRandomMax = 0.16f,
                            CostModifierRandomMin = 0.03f,
                            CostModifierRandomMax = 0.11f,
                            ProjectionBuildDelayRandomMin = -0.10f,
                            ProjectionBuildDelayRandomMax = 0.02f,
                            ForbiddenComponents = new List<string> { "Medical" },
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Construction", Multiplier = 0.96f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "SmallTube", Multiplier = 0.96f }
                            }
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
                            WeldingSpeed = 1.40f,
                            ProjectionWeldingSpeed = 1.15f,
                            CostModifier = 1.55f,
                            AllowProjections = false,
                            ProjectionBuildDelay = 0.87f,
                            PlayerServiceName = "Military Service",
                            PlayerRestrictionsSummary = "Medical systems restricted",
                            PlayerDetailsDescription = "Rapid combat-readiness repairs with priority on structural and systems recovery. Civilian medical systems are not serviced here.",
                            WeldingSpeedRandomMin = 0.00f,
                            WeldingSpeedRandomMax = 0.10f,
                            ProjectionWeldingSpeedRandomMin = -0.02f,
                            ProjectionWeldingSpeedRandomMax = 0.08f,
                            CostModifierRandomMin = 0.04f,
                            CostModifierRandomMax = 0.12f,
                            ProjectionBuildDelayRandomMin = -0.06f,
                            ProjectionBuildDelayRandomMax = 0.04f,
                            ForbiddenComponents = new List<string> { "Medical" },
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Thruster", Multiplier = 1.08f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Reactor", Multiplier = 1.08f }
                            }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "RestrictedTech",
                            WeldingSpeed = 1.20f,
                            ProjectionWeldingSpeed = 1.00f,
                            CostModifier = 1.30f,
                            AllowProjections = false,
                            ProjectionBuildDelay = 1.00f,
                            PlayerServiceName = "Military Service",
                            PlayerRestrictionsSummary = "Medical and gravity systems restricted",
                            PlayerDetailsDescription = "Controlled military maintenance service with moderate throughput and tighter subsystem restrictions.",
                            WeldingSpeedRandomMin = -0.02f,
                            WeldingSpeedRandomMax = 0.08f,
                            ProjectionWeldingSpeedRandomMin = -0.03f,
                            ProjectionWeldingSpeedRandomMax = 0.06f,
                            CostModifierRandomMin = 0.02f,
                            CostModifierRandomMax = 0.08f,
                            ProjectionBuildDelayRandomMin = -0.04f,
                            ProjectionBuildDelayRandomMax = 0.06f,
                            ForbiddenComponents = new List<string> { "Medical", "GravityGenerator" },
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Detector", Multiplier = 1.10f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "RadioCommunication", Multiplier = 1.10f }
                            }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "SecureExpensive",
                            WeldingSpeed = 1.25f,
                            ProjectionWeldingSpeed = 1.10f,
                            CostModifier = 1.70f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.90f,
                            PlayerServiceName = "Military Service",
                            PlayerRestrictionsSummary = "Secure service with premium military pricing",
                            PlayerDetailsDescription = "Secure military dock service with projection support for approved hull work and high pricing for restricted access.",
                            WeldingSpeedRandomMin = -0.01f,
                            WeldingSpeedRandomMax = 0.08f,
                            ProjectionWeldingSpeedRandomMin = -0.02f,
                            ProjectionWeldingSpeedRandomMax = 0.08f,
                            CostModifierRandomMin = 0.06f,
                            CostModifierRandomMax = 0.16f,
                            ProjectionBuildDelayRandomMin = -0.05f,
                            ProjectionBuildDelayRandomMax = 0.04f,
                            ForbiddenComponents = new List<string> { "Medical" },
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "PowerCell", Multiplier = 1.12f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Explosives", Multiplier = 1.10f }
                            }
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
                            WeldingSpeed = 1.60f,
                            ProjectionWeldingSpeed = 1.60f,
                            CostModifier = 1.85f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.63f,
                            PlayerServiceName = "Premium Repair Yard",
                            PlayerRestrictionsSummary = "Full-service premium support",
                            PlayerDetailsDescription = "High-end repair yard with excellent throughput, rapid projection service and premium pricing.",
                            WeldingSpeedRandomMin = 0.02f,
                            WeldingSpeedRandomMax = 0.12f,
                            ProjectionWeldingSpeedRandomMin = 0.04f,
                            ProjectionWeldingSpeedRandomMax = 0.14f,
                            CostModifierRandomMin = 0.08f,
                            CostModifierRandomMax = 0.18f,
                            ProjectionBuildDelayRandomMin = -0.10f,
                            ProjectionBuildDelayRandomMax = 0.02f,
                            ForbiddenComponents = new List<string>(),
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Medical", Multiplier = 1.08f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "PowerCell", Multiplier = 1.10f }
                            }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "FullService",
                            WeldingSpeed = 1.45f,
                            ProjectionWeldingSpeed = 1.50f,
                            CostModifier = 1.70f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.67f,
                            PlayerServiceName = "Premium Repair Yard",
                            PlayerRestrictionsSummary = "Extensive support with premium pricing",
                            PlayerDetailsDescription = "Full-service premium dock for advanced repair, projection work and expensive high-tech maintenance.",
                            WeldingSpeedRandomMin = 0.00f,
                            WeldingSpeedRandomMax = 0.10f,
                            ProjectionWeldingSpeedRandomMin = 0.02f,
                            ProjectionWeldingSpeedRandomMax = 0.12f,
                            CostModifierRandomMin = 0.06f,
                            CostModifierRandomMax = 0.16f,
                            ProjectionBuildDelayRandomMin = -0.08f,
                            ProjectionBuildDelayRandomMax = 0.03f,
                            ForbiddenComponents = new List<string>(),
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Superconductor", Multiplier = 1.10f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "GravityGenerator", Multiplier = 1.10f }
                            }
                        },
                        new ZoneProfileVariantDefinition
                        {
                            VariantId = "FastLuxury",
                            WeldingSpeed = 1.75f,
                            ProjectionWeldingSpeed = 1.35f,
                            CostModifier = 2.00f,
                            AllowProjections = true,
                            ProjectionBuildDelay = 0.74f,
                            PlayerServiceName = "Premium Repair Yard",
                            PlayerRestrictionsSummary = "Priority turnaround at luxury rates",
                            PlayerDetailsDescription = "Luxury express service optimized for rapid turnaround and high-end support with top-tier pricing.",
                            WeldingSpeedRandomMin = 0.04f,
                            WeldingSpeedRandomMax = 0.14f,
                            ProjectionWeldingSpeedRandomMin = 0.00f,
                            ProjectionWeldingSpeedRandomMax = 0.10f,
                            CostModifierRandomMin = 0.10f,
                            CostModifierRandomMax = 0.20f,
                            ProjectionBuildDelayRandomMin = -0.06f,
                            ProjectionBuildDelayRandomMax = 0.04f,
                            ForbiddenComponents = new List<string>(),
                            ComponentPriceModifiers = new List<ComponentPriceModifierEntry>
                            {
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Medical", Multiplier = 1.12f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "PowerCell", Multiplier = 1.15f },
                                new ComponentPriceModifierEntry { ComponentSubtypeId = "Thruster", Multiplier = 1.10f }
                            }
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
                    PlayerServiceName = variant.PlayerServiceName,
                    PlayerRestrictionsSummary = variant.PlayerRestrictionsSummary,
                    PlayerDetailsDescription = variant.PlayerDetailsDescription,
                    WeldingSpeedRandomMin = variant.WeldingSpeedRandomMin,
                    WeldingSpeedRandomMax = variant.WeldingSpeedRandomMax,
                    ProjectionWeldingSpeedRandomMin = variant.ProjectionWeldingSpeedRandomMin,
                    ProjectionWeldingSpeedRandomMax = variant.ProjectionWeldingSpeedRandomMax,
                    CostModifierRandomMin = variant.CostModifierRandomMin,
                    CostModifierRandomMax = variant.CostModifierRandomMax,
                    ProjectionBuildDelayRandomMin = variant.ProjectionBuildDelayRandomMin,
                    ProjectionBuildDelayRandomMax = variant.ProjectionBuildDelayRandomMax,
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

        private static void ApplyJitter(GeneratedZoneProfileResult result, ZoneProfileVariantDefinition variant, ZoneGenerationConfig config, int seed)
        {
            if (result == null || config == null)
                return;

            float weldingMin, weldingMax;
            ResolveRandomWindow(variant == null ? null : variant.WeldingSpeedRandomMin, variant == null ? null : variant.WeldingSpeedRandomMax, config.DefaultWeldingSpeedRandomMin, config.DefaultWeldingSpeedRandomMax, config.VariantJitterPercent, out weldingMin, out weldingMax);

            float projectionWeldingMin, projectionWeldingMax;
            ResolveRandomWindow(variant == null ? null : variant.ProjectionWeldingSpeedRandomMin, variant == null ? null : variant.ProjectionWeldingSpeedRandomMax, config.DefaultProjectionWeldingSpeedRandomMin, config.DefaultProjectionWeldingSpeedRandomMax, config.VariantJitterPercent, out projectionWeldingMin, out projectionWeldingMax);

            float costMin, costMax;
            ResolveRandomWindow(variant == null ? null : variant.CostModifierRandomMin, variant == null ? null : variant.CostModifierRandomMax, config.DefaultCostModifierRandomMin, config.DefaultCostModifierRandomMax, config.VariantJitterPercent, out costMin, out costMax);

            float buildDelayMin, buildDelayMax;
            ResolveRandomWindow(variant == null ? null : variant.ProjectionBuildDelayRandomMin, variant == null ? null : variant.ProjectionBuildDelayRandomMax, config.DefaultProjectionBuildDelayRandomMin, config.DefaultProjectionBuildDelayRandomMax, config.VariantJitterPercent, out buildDelayMin, out buildDelayMax);

            result.WeldingSpeed = ClampJitterValue(result.WeldingSpeed, 0.1f, 100f, seed ^ 0x11A37, weldingMin, weldingMax);
            result.ProjectionWeldingSpeed = ClampJitterValue(result.ProjectionWeldingSpeed, 0.1f, 100f, seed ^ 0x34BCD, projectionWeldingMin, projectionWeldingMax);
            result.CostModifier = ClampJitterValue(result.CostModifier, 0f, 100f, seed ^ 0x52F11, costMin, costMax);
            result.ProjectionBuildDelay = ClampJitterValue(result.ProjectionBuildDelay, 0.1f, 100f, seed ^ 0x77E29, buildDelayMin, buildDelayMax);
        }

        private static void ResolveRandomWindow(float? overrideMin, float? overrideMax, float defaultMin, float defaultMax, float legacyPercent, out float resolvedMin, out float resolvedMax)
        {
            float min = overrideMin ?? defaultMin;
            float max = overrideMax ?? defaultMax;

            bool useLegacyFallback = !overrideMin.HasValue && !overrideMax.HasValue && min == 0f && max == 0f && legacyPercent > 0f;
            if (useLegacyFallback)
            {
                min = -legacyPercent;
                max = legacyPercent;
            }

            if (min > max)
            {
                float tmp = min;
                min = max;
                max = tmp;
            }

            min = ClampWindowValue(min);
            max = ClampWindowValue(max);

            if (min > max)
            {
                float tmp = min;
                min = max;
                max = tmp;
            }

            resolvedMin = min;
            resolvedMax = max;
        }

        private static float ClampWindowValue(float value)
        {
            if (value < -0.95f)
                return -0.95f;
            if (value > 5f)
                return 5f;
            return value;
        }

        private static float ClampJitterValue(float value, float min, float max, int localSeed, float randomMin, float randomMax)
        {
            if (randomMin == 0f && randomMax == 0f)
                return value;

            var random = new Random(localSeed);
            double factor = 1d + randomMin + ((randomMax - randomMin) * random.NextDouble());
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
