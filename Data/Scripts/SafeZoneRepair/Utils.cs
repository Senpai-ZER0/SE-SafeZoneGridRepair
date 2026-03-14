using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using Sandbox.Definitions; // обязательно

namespace SafeZoneRepair
{
    public static class Utils
    {
        public static bool NeedRepair(this IMySlimBlock target, bool functionalOnly)
        {
            if (target == null)
                return false;

            var def = target.BlockDefinition as MyCubeBlockDefinition;
            if (def == null)
                return !target.IsDestroyed &&
                       (target.FatBlock == null || !target.FatBlock.Closed) &&
                       (target.BuildIntegrity < target.MaxIntegrity - 0.01f || (!functionalOnly && target.HasDeformation));

            float needed = functionalOnly
                ? target.MaxIntegrity * def.CriticalIntegrityRatio
                : target.MaxIntegrity;

            return !target.IsDestroyed &&
                   (target.FatBlock == null || !target.FatBlock.Closed) &&
                   (target.BuildIntegrity < needed - 0.01f || (!functionalOnly && target.HasDeformation));
        }



        public static int GetMissingComponentsTotalCount(this IMySlimBlock target)
        {
            if (target == null || target.IsDestroyed || (target.FatBlock != null && target.FatBlock.Closed))
                return 0;

            var missing = new Dictionary<string, int>();
            target.GetMissingComponents(missing);

            int totalMissing = 0;
            foreach (var kv in missing)
            {
                if (kv.Value > 0)
                    totalMissing += kv.Value;
            }

            return totalMissing;
        }

        public static bool HasMissingComponents(this IMySlimBlock target)
        {
            return GetMissingComponentsTotalCount(target) > 0;
        }

        public static bool NeedRepairRobust(this IMySlimBlock target, bool functionalOnly)
        {
            if (NeedRepair(target, functionalOnly))
                return true;

            if (target == null || target.IsDestroyed || (target.FatBlock != null && target.FatBlock.Closed))
                return false;

            if (!functionalOnly && target.HasDeformation)
                return true;

            if (!functionalOnly && target.HasMissingComponents())
                return true;

            if (!functionalOnly && target.BuildLevelRatio < 0.999f)
                return true;

            return false;
        }

        public static bool IsProjected(this IMySlimBlock target)
        {
            var grid = target.CubeGrid as MyCubeGrid;
            return grid?.Projector != null;
        }

        public static bool IsProjected(this IMyCubeGrid target)
        {
            var grid = target as MyCubeGrid;
            return grid?.Projector != null;
        }

        public static bool CanBuild(this IMySlimBlock target, bool gui)
        {
            var grid = target.CubeGrid as MyCubeGrid;
            if (grid?.Projector == null) return false;
            return ((IMyProjector)grid.Projector).CanBuild(target, gui) == BuildCheckResult.OK;
        }

        public static string BlockName(this IMySlimBlock b)
        {
            if (b == null) return "(none)";
            var term = b.FatBlock as IMyTerminalBlock;
            if (term != null)
                return $"{term.CubeGrid?.DisplayName ?? "Unknown"}.{term.CustomName}";
            return $"{b.CubeGrid?.DisplayName ?? "Unknown"}.{b.BlockDefinition?.DisplayNameText ?? "?"}";
        }
    }
}
