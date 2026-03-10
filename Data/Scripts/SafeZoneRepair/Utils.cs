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
            var def = target.BlockDefinition as MyCubeBlockDefinition;
            float needed = functionalOnly ? target.MaxIntegrity * def.CriticalIntegrityRatio : target.MaxIntegrity;
            return !target.IsDestroyed && (target.FatBlock == null || !target.FatBlock.Closed) &&
                   (target.Integrity < needed - 0.1f); // допуск 0.1, игнорируем деформацию
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