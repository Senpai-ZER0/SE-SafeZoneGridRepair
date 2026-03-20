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
        /// Terminal actions are intentionally disabled for now to avoid breaking
        /// vanilla cockpit / remote control toolbar actions such as Park and Handbrake.
        /// Hotkeys and RHF remain the supported interaction path.
        /// </summary>
        private void RegisterTerminalActions()
        {
            if (_terminalActionsRegistered)
                return;

            _terminalActionsRegistered = true;
        }
    }
}
