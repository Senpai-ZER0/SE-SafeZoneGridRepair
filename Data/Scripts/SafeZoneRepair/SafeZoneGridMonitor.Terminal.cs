using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Text;
using VRage.Game.ModAPI;
using VRage.Utils; // обязательно для MyLog

namespace SafeZoneRepair
{
    public partial class SafeZoneGridMonitor
    {
        private static bool _terminalActionsRegistered = false;

        /// <summary>
        /// Регистрирует действия терминала. Вызывается ТОЛЬКО на клиенте.
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
                var toggleAction = MyAPIGateway.TerminalControls.CreateAction<IMyCockpit>("ToggleRepairInSafeZones");
                toggleAction.Name = new StringBuilder("Toggle repair in safe zones");
                toggleAction.Icon = @"Textures\GUI\Icons\Actions\SwitchOnOff.dds";
                toggleAction.Action = (block) =>
                {
                    if (block?.CubeGrid != null)
                        ToggleRepair(block.CubeGrid);
                };
                MyAPIGateway.TerminalControls.AddAction<IMyCockpit>(toggleAction);

                var statusAction = MyAPIGateway.TerminalControls.CreateAction<IMyCockpit>("ShowRepairStatus");
                statusAction.Name = new StringBuilder("Show repair status");
                statusAction.Icon = @"Textures\GUI\Icons\Actions\Info.dds";
                statusAction.Action = (block) =>
                {
                    if (block?.CubeGrid != null)
                        ShowRepairStatus(block.CubeGrid);
                };
                MyAPIGateway.TerminalControls.AddAction<IMyCockpit>(statusAction);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"SafeZoneRepair terminal error {ex}");
            }
        }
    }
}