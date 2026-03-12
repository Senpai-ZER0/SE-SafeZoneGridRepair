using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Text;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace SafeZoneRepair
{
    public partial class SafeZoneGridMonitor
    {
        private static bool _terminalActionsRegistered = false;

        /// <summary>
        /// Регистрирует terminal actions для ship controller, чтобы их можно было
        /// назначать на toolbar / кнопки в кокпите.
        /// Вызывается только на клиенте.
        /// </summary>
        private void RegisterTerminalActions()
        {
            if (_terminalActionsRegistered)
                return;

            if (MyAPIGateway.TerminalControls == null)
                return;

            _terminalActionsRegistered = true;

            try
            {
                RegisterToggleRepairAction();
                RegisterShowRepairStatusAction();
                RegisterToggleRepairHudAction();
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"[SafeZoneRepair] terminal error: {ex}");
            }
        }

        private void RegisterToggleRepairAction()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<IMyShipController>("ToggleRepairInSafeZones");
            action.Name = new StringBuilder("Toggle repair mode");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Action = block =>
            {
                var shipController = block as IMyShipController;
                var grid = shipController?.CubeGrid;
                if (grid != null)
                    ToggleRepair(grid);
            };
            action.Writer = (block, sb) =>
            {
                var shipController = block as IMyShipController;
                var grid = shipController?.CubeGrid;
                if (grid == null)
                {
                    sb.Append("Repair: N/A");
                    return;
                }

                bool enabled = GetGridRepairSetting(grid);
                sb.Append(enabled ? "Repair: ON" : "Repair: OFF");
            };

            MyAPIGateway.TerminalControls.AddAction<IMyShipController>(action);
        }

        private void RegisterShowRepairStatusAction()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<IMyShipController>("ShowRepairStatus");
            action.Name = new StringBuilder("Show repair status");
            action.Icon = @"Textures\GUI\Icons\Actions\SetValue.dds";
            action.Action = block =>
            {
                var shipController = block as IMyShipController;
                var grid = shipController?.CubeGrid;
                if (grid != null)
                    ShowRepairStatus(grid);
            };
            action.Writer = (block, sb) =>
            {
                sb.Append("Repair status");
            };

            MyAPIGateway.TerminalControls.AddAction<IMyShipController>(action);
        }

        private void RegisterToggleRepairHudAction()
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<IMyShipController>("ToggleRepairHud");
            action.Name = new StringBuilder("Toggle repair HUD");
            action.Icon = @"Textures\GUI\Icons\Actions\SetValueAndExecute.dds";
            action.Action = block =>
            {
                var shipController = block as IMyShipController;
                var grid = shipController?.CubeGrid;

                if (grid != null)
                    ToggleHudForLocalContext(grid);
                else
                    ToggleHudForLocalContext();
            };
            action.Writer = (block, sb) =>
            {
                sb.Append("Toggle repair HUD");
            };

            MyAPIGateway.TerminalControls.AddAction<IMyShipController>(action);
        }
    }
}
