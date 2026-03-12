using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Components;
using VRage.Game.ObjectBuilders.Components;
using VRageMath;
using VRage.Game.Entity;
using Sandbox.Game.World.Generator;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Definitions;
using VRage;
using System.Text;
using VRage.Utils;
using VRage.ObjectBuilders;
using VRage.Input;
using SafeZoneRepair;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;

namespace SafeZoneRepair
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public partial class SafeZoneGridMonitor : MySessionComponentBase
    {
        // --- Поля ---
        private List<MySafeZone> safeZones = new List<MySafeZone>();
        private List<IMyCubeGrid> gridsInSafeZone = new List<IMyCubeGrid>();
        private Queue<IMySlimBlock> blocksRepairQueue = new Queue<IMySlimBlock>();
        private HashSet<IMySlimBlock> blocksInQueue = new HashSet<IMySlimBlock>();
        private Dictionary<IMySlimBlock, BlockRepairInfo> blockRepairInfo = new Dictionary<IMySlimBlock, BlockRepairInfo>();
        private TimeSpan lastGridCheck = TimeSpan.Zero;
        private static readonly TimeSpan gridCheckInterval = TimeSpan.FromSeconds(1);
		private TimeSpan _lastClientHudVisibilityCheck = TimeSpan.Zero;
		private static readonly TimeSpan clientHudVisibilityCheckInterval = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan estimatedRepairCostCacheInterval = TimeSpan.FromMilliseconds(750);

        private bool initialized = false;
        private float weldingSpeed = 3f;
        private float costModifier = 0.01f;
        private Stopwatch deltaTimer;
        private double deltaTime = 0;
        public const ushort SyncId = 2914;
        private MyParticleEffect effect;
        private MyEntity3DSoundEmitter soundEmitter;
        private Stopwatch sinceLastMsgTimer = new Stopwatch();

        private Dictionary<long, SafeZoneConfig> zoneConfigs = new Dictionary<long, SafeZoneConfig>();
        private const string ConfigFileName = "SafeZoneRepairConfig.xml";

        private LoggingConfig loggingConfig = new LoggingConfig();
        private const string LoggingConfigFileName = "LoggingConfig.xml";

        private Dictionary<IMyCubeGrid, bool> gridRepairSettings = new Dictionary<IMyCubeGrid, bool>();

        private DateTime _lastProjectionBuildTime = DateTime.MinValue;

        public const ushort TerminalActionSyncId = 2915;
        public const ushort TerminalStatusSyncId = 2916;
        public const ushort RepairUiStateSyncId = 2917;

        private HashSet<string> _completedBlockKeys = new HashSet<string>(); // Ключи завершённых блоков (gridId:position)

        // Защита на клиенте (статические поля, чтобы все экземпляры использовали общий таймер)
        private static string _lastClientMessage = null;
        private static DateTime _lastClientMessageTime = DateTime.MinValue;
        private static string _lastRepairMessage = null;
        private static DateTime _lastRepairMessageTime = DateTime.MinValue;

        // Защита на сервере для статусных сообщений
        private Dictionary<long, string> _lastPlayerStatusText = new Dictionary<long, string>();
        private Dictionary<long, DateTime> _lastPlayerStatusTime = new Dictionary<long, DateTime>();
        private Dictionary<long, EstimatedRepairCostCacheEntry> _estimatedRepairCostCache = new Dictionary<long, EstimatedRepairCostCacheEntry>();

        // Флаг для предотвращения повторной регистрации обработчиков на клиенте
        private static bool _clientHandlersRegistered = false;
        private RepairUiStateMessage _clientUiState = new RepairUiStateMessage { InRepairZone = false, ZoneName = "Repair Zone", RepairEnabled = false, StatusText = "Waiting for zone state", LastRepairText = "No repairs performed yet.", EstimatedRepairCost = 0 };
        private Dictionary<long, long> _lastControlledGridByPlayer = new Dictionary<long, long>();
        private bool _manualHudRequested = false;
        private bool _cockpitHudSuppressed = false;
        private bool _cockpitInteractiveRequested = false;
        private static readonly MyKeys HudToggleKey = MyKeys.J;
        private static readonly MyKeys RepairToggleKey = MyKeys.R;
        private static readonly MyKeys CockpitHudSuppressKey = MyKeys.N;

        private static bool _rhfBindsRegistered = false;
        private static bool _rhfTerminalPagesRegistered = false;
        private static IBindGroup _rhfBindGroup;
        private static IBind _hudToggleBind;
        private static IBind _repairToggleBind;
        private static TerminalPageCategory _rhfTerminalCategory;
        private static RebindPage _rhfKeybindPage;
        private static TextPage _rhfOverviewPage;

        private sealed class EstimatedRepairCostCacheEntry
        {
            public long GridEntityId;
            public long ZoneEntityId;
            public bool InRepairZone;
            public bool RepairEnabled;
            public DateTime CachedAtUtc;
            public long EstimatedRepairCost;
        }

        // --- Загрузка и выгрузка ---
        public override void LoadData()
        {
            try
            {
                deltaTimer = Stopwatch.StartNew();
                sinceLastMsgTimer.Start();

                RegisterTerminalActions();

                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    MyAPIGateway.Multiplayer.RegisterMessageHandler(TerminalActionSyncId, HandleTerminalAction);
                    MyAPIGateway.Entities.OnEntityAdd += OnEntityAdded;
                    MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
                    LogGeneral("Loaded on server");
                    LoadConfig();
                    LoadLoggingConfig();

                    if (MyAPIGateway.Utilities != null)
                        MyAPIGateway.Utilities.ShowMessage("SZ", "SafeZoneRepair loaded");
                }

                if (!MyAPIGateway.Utilities.IsDedicated)
                {
                    InitRichHud();

                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(SyncId, HandleRepairNotification);
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(TerminalStatusSyncId, HandleTerminalStatus);
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(RepairUiStateSyncId, HandleRepairUiState);

                    MyAPIGateway.Multiplayer.RegisterMessageHandler(SyncId, HandleRepairNotification);
                    MyAPIGateway.Multiplayer.RegisterMessageHandler(TerminalStatusSyncId, HandleTerminalStatus);
                    MyAPIGateway.Multiplayer.RegisterMessageHandler(RepairUiStateSyncId, HandleRepairUiState);
                    _clientHandlersRegistered = true;
                }
            }
            catch (Exception ex)
            {
                LogError($"LoadData error: {ex}");
            }
        }

        protected override void UnloadData()
        {
            try
            {
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(TerminalActionSyncId, HandleTerminalAction);
                    MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdded;
                    MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;
                    if (MyAPIGateway.Utilities != null)
                        MyAPIGateway.Utilities.ShowMessage("SZ", "SafeZoneRepair unloaded");
                }

                if (_clientHandlersRegistered)
                {
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(SyncId, HandleRepairNotification);
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(TerminalStatusSyncId, HandleTerminalStatus);
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(RepairUiStateSyncId, HandleRepairUiState);
                    _clientHandlersRegistered = false;
                    ResetRichHud();
                }

                soundEmitter?.StopSound(true);
                soundEmitter = null;
                deltaTimer?.Stop();
                sinceLastMsgTimer?.Stop();
                blocksInQueue.Clear();
                blockRepairInfo.Clear();
                zoneConfigs.Clear();
                gridRepairSettings.Clear();
                _completedBlockKeys.Clear();
                _lastPlayerStatusText.Clear();
                _lastPlayerStatusTime.Clear();
                _estimatedRepairCostCache.Clear();
                _lastControlledGridByPlayer.Clear();
                _manualHudRequested = false;
                _cockpitHudSuppressed = false;
                _cockpitInteractiveRequested = false;

                _rhfBindsRegistered = false;
                _rhfTerminalPagesRegistered = false;
                _rhfBindGroup = null;
                _hudToggleBind = null;
                _repairToggleBind = null;
                _rhfTerminalCategory = null;
                _rhfKeybindPage = null;
                _rhfOverviewPage = null;
                SetInteractiveCursorEnabled(false);
            }
            catch (Exception ex)
            {
                LogError($"UnloadData error: {ex}");
            }
        }

        // --- Конфигурация ---
        private void LoadConfig()
        {
            try
            {
                zoneConfigs.Clear();
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(ConfigFileName, typeof(SafeZoneGridMonitor)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(ConfigFileName, typeof(SafeZoneGridMonitor)))
                    {
                        string xml = reader.ReadToEnd();
                        var list = MyAPIGateway.Utilities.SerializeFromXML<List<SafeZoneConfig>>(xml);
                        if (list != null)
                        {
                            foreach (var cfg in list)
                            {
                                NormalizeZoneConfig(cfg);

                                if (cfg.ZoneEntityId != 0)
                                    zoneConfigs[cfg.ZoneEntityId] = cfg;
                            }
                        }
                    }
                }
                else
                {
                    SaveDefaultConfig();
                }
                LogGeneral($"Loaded {zoneConfigs.Count} zone configs");
            }
            catch (Exception ex)
            {
                LogError($"LoadConfig error: {ex}");
            }
        }


        private static string GetSafeZoneDefaultName(MySafeZone zone)
        {
            if (zone != null && !string.IsNullOrWhiteSpace(zone.DisplayName))
                return zone.DisplayName.Trim();

            return "Repair Zone";
        }

        private static void NormalizeZoneConfig(SafeZoneConfig cfg)
        {
            if (cfg == null)
                return;

            if (string.IsNullOrWhiteSpace(cfg.ZoneName))
                cfg.ZoneName = "Repair Zone";

            if (string.IsNullOrWhiteSpace(cfg.DisplayName))
                cfg.DisplayName = cfg.ZoneName;

            if (cfg.ForbiddenComponents == null)
                cfg.ForbiddenComponents = new List<string>();
        }

        private void SaveDefaultConfig()
        {
            var list = new List<SafeZoneConfig>();
            if (safeZones.Count == 0)
            {
                list.Add(new SafeZoneConfig
                {
                    ZoneName = "Example Zone",
                    DisplayName = "Example Zone",
                    ZoneEntityId = 1234567890,
                    WeldingSpeed = this.weldingSpeed,
                    CostModifier = this.costModifier,
                    Enabled = true,
                    AllowProjections = true,
                    ProjectionBuildDelay = 1f,
                    ForbiddenComponents = new List<string> { "ExampleComponent1", "ExampleComponent2" }
                });
            }
            else
            {
                foreach (var zone in safeZones)
                {
                    string zoneName = GetSafeZoneDefaultName(zone);

                    list.Add(new SafeZoneConfig
                    {
                        ZoneName = zoneName,
                        DisplayName = zoneName,
                        ZoneEntityId = zone.EntityId,
                        WeldingSpeed = this.weldingSpeed,
                        CostModifier = this.costModifier,
                        Enabled = true,
                        AllowProjections = true,
                        ProjectionBuildDelay = 1f,
                        ForbiddenComponents = new List<string>()
                    });
                }
            }
            SaveConfig(list);
        }

        private void SaveConfig(List<SafeZoneConfig> list)
        {
            try
            {
                if (list != null)
                {
                    foreach (var cfg in list)
                        NormalizeZoneConfig(cfg);
                }

                string xml = MyAPIGateway.Utilities.SerializeToXML(list);
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(ConfigFileName, typeof(SafeZoneGridMonitor)))
                {
                    writer.Write(xml);
                }
            }
            catch (Exception ex)
            {
                LogError($"SaveConfig error: {ex}");
            }
        }

        private void ReloadConfig() => LoadConfig();

        // --- Логирование ---
        private void LoadLoggingConfig()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(LoggingConfigFileName, typeof(SafeZoneGridMonitor)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(LoggingConfigFileName, typeof(SafeZoneGridMonitor)))
                    {
                        string xml = reader.ReadToEnd();
                        var cfg = MyAPIGateway.Utilities.SerializeFromXML<LoggingConfig>(xml);
                        if (cfg != null)
                            loggingConfig = cfg;
                    }
                }
                else
                {
                    SaveDefaultLoggingConfig();
                }
            }
            catch (Exception ex)
            {
                LogError($"LoadLoggingConfig error: {ex}");
            }
        }

        private void SaveDefaultLoggingConfig()
        {
            try
            {
                string xml = MyAPIGateway.Utilities.SerializeToXML(new LoggingConfig());
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(LoggingConfigFileName, typeof(SafeZoneGridMonitor)))
                {
                    writer.Write(xml);
                }
            }
            catch (Exception ex)
            {
                LogError($"SaveDefaultLoggingConfig error: {ex}");
            }
        }

        // --- Методы, вызываемые из действий терминала ---
        public void ToggleRepair(IMyCubeGrid grid)
        {
            if (grid == null) return;

            if (!MyAPIGateway.Multiplayer.IsServer)
            {
                var msg = new TerminalActionMessage { GridEntityId = grid.EntityId, Action = "Toggle" };
                byte[] data = MyAPIGateway.Utilities.SerializeToBinary(msg);
                MyAPIGateway.Multiplayer.SendMessageToServer(TerminalActionSyncId, data);
                return;
            }

            IMyPlayer player = GetPlayerControllingGrid(grid);
            LogGeneral($"ToggleRepair called for grid {grid.EntityId}, player {player?.IdentityId}");

            bool current = GetGridRepairSetting(grid);
            SetGridRepairSetting(grid, !current);
            InvalidateEstimatedRepairCostCache(player?.IdentityId ?? 0, grid.EntityId);
            if (current)
            {
                RemoveGridFromQueue(grid);
                if (gridsInSafeZone.Contains(grid))
                {
                    gridsInSafeZone.Remove(grid);
                    LogZone($"Grid {grid.DisplayName} removed from repair list (disabled by action)");
                }
                SendTerminalStatusToPlayer(player, "Repair disabled for your ship", "Red", 3000);
                SendRepairUiStateToPlayer(player, GridIsInSafeZone(grid), grid, GetSafeZoneForGrid(grid), "Repair disabled for your ship");
            }
            else
            {
                SendTerminalStatusToPlayer(player, "Repair enabled for your ship", "Green", 3000);
                SendRepairUiStateToPlayer(player, GridIsInSafeZone(grid), grid, GetSafeZoneForGrid(grid), "Repair enabled for your ship");
            }
        }

        public void ShowRepairStatus(IMyCubeGrid grid)
        {
            if (grid == null) return;

            if (!MyAPIGateway.Multiplayer.IsServer)
            {
                var msg = new TerminalActionMessage { GridEntityId = grid.EntityId, Action = "Status" };
                byte[] data = MyAPIGateway.Utilities.SerializeToBinary(msg);
                MyAPIGateway.Multiplayer.SendMessageToServer(TerminalActionSyncId, data);
                return;
            }

            IMyPlayer player = GetPlayerControllingGrid(grid);
            bool enabled = GetGridRepairSetting(grid);
            LogGeneral($"ShowRepairStatus called for grid {grid.EntityId}, player {player?.IdentityId}, enabled={enabled}");

            string status = enabled ? "enabled" : "disabled";
            string color = enabled ? "Green" : "Red";
            SendTerminalStatusToPlayer(player, $"Repair for your ship is {status}", color, 3000);
            SendRepairUiStateToPlayer(player, GridIsInSafeZone(grid), grid, GetSafeZoneForGrid(grid), $"Repair for your ship is {status}");
        }

        // --- Логирование ---
        private void LogGeneral(string msg) { if (loggingConfig.EnableLogGeneral) MyLog.Default.WriteLine($"[SafeZoneRepair] {msg}"); }
        private void LogZone(string msg) { if (loggingConfig.EnableLogZoneDetection) MyLog.Default.WriteLine($"[SafeZoneRepair] {msg}"); }
        private void LogBlock(string msg) { if (loggingConfig.EnableLogBlockNeedsRepair) MyLog.Default.WriteLine($"[SafeZoneRepair] {msg}"); }
        private void LogRepair(string msg) { if (loggingConfig.EnableLogRepairAction) MyLog.Default.WriteLine($"[SafeZoneRepair] {msg}"); }
        private void LogCost(string msg) { if (loggingConfig.EnableLogCostCalculation) MyLog.Default.WriteLine($"[SafeZoneRepair] {msg}"); }
        private void LogError(string msg) { if (loggingConfig.EnableLogErrors) MyLog.Default.WriteLine($"[SafeZoneRepair] ERROR: {msg}"); }

        // --- Настройки ремонта для сеток ---
        private bool GetGridRepairSetting(IMyCubeGrid grid)
        {
            if (grid == null) return true;
            bool value;
            if (!gridRepairSettings.TryGetValue(grid, out value))
            {
                value = LoadGridSettingFromCustomData(grid);
                gridRepairSettings[grid] = value;
            }
            return value;
        }

        private void SetGridRepairSetting(IMyCubeGrid grid, bool value)
        {
            if (grid == null) return;
            gridRepairSettings[grid] = value;
            SaveGridSettingToCustomData(grid, value);
        }

        private bool LoadGridSettingFromCustomData(IMyCubeGrid grid)
        {
            try
            {
                var shipControllers = new List<IMyShipController>();
                MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(shipControllers);
                if (shipControllers.Count > 0)
                {
                    var data = shipControllers[0].CustomData;
                    if (data.Contains("AllowRepairsInSafeZones:"))
                    {
                        var lines = data.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("AllowRepairsInSafeZones:"))
                            {
                                bool result;
                                if (bool.TryParse(line.Substring("AllowRepairsInSafeZones:".Length).Trim(), out result))
                                    return result;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error loading grid setting: {ex}");
            }
            return true;
        }

        private void SaveGridSettingToCustomData(IMyCubeGrid grid, bool value)
        {
            try
            {
                var shipControllers = new List<IMyShipController>();
                MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(shipControllers);
                if (shipControllers.Count > 0)
                {
                    var shipController = shipControllers[0];
                    var data = new StringBuilder();
                    data.AppendLine($"AllowRepairsInSafeZones:{value}");
                    shipController.CustomData = data.ToString();
                }
            }
            catch (Exception ex)
            {
                LogError($"Error saving grid setting: {ex}");
            }
        }

        private void RemoveGridFromQueue(IMyCubeGrid grid)
        {
            if (grid == null) return;
            var blocksToKeep = new List<IMySlimBlock>();
            int removed = 0;
            while (blocksRepairQueue.Count > 0)
            {
                var block = blocksRepairQueue.Dequeue();
                if (block?.CubeGrid == grid)
                {
                    blocksInQueue.Remove(block);
                    blockRepairInfo.Remove(block);
                    removed++;
                }
                else
                {
                    blocksToKeep.Add(block);
                }
            }
            foreach (var block in blocksToKeep)
            {
                blocksRepairQueue.Enqueue(block);
            }
            if (removed > 0)
                LogGeneral($"Removed {removed} blocks from queue for grid {grid.DisplayName} (repair disabled)");
        }
		
		

        private IMyShipController GetControlledShipController(IMyPlayer player)
        {
            if (player?.Controller == null)
                return null;

            var shipController = player.Controller.ControlledEntity as IMyShipController;
            if (shipController != null && shipController.CubeGrid != null)
                return shipController;

            shipController = player.Controller.ControlledEntity?.Entity as IMyShipController;
            return shipController != null && shipController.CubeGrid != null ? shipController : null;
        }

        private IMyShipController GetLocalControlledShipController()
        {
            var session = MyAPIGateway.Session;
            var player = session?.Player ?? session?.LocalHumanPlayer;
            return GetControlledShipController(player);
        }

        private bool IsManualHudAllowed()
        {
            return _manualHudRequested && _clientUiState != null && _clientUiState.InRepairZone;
        }

        private bool IsCockpitHudVisible()
        {
            return !_cockpitHudSuppressed;
        }

        private bool IsCockpitInteractiveHudRequested()
        {
            return _cockpitInteractiveRequested;
        }

        private bool IsCockpitHudSuppressHotkeyPressed()
        {
            var input = MyAPIGateway.Input;
            return input != null && input.IsAnyCtrlKeyPressed() && input.IsNewKeyPressed(CockpitHudSuppressKey);
        }

        private void ToggleCockpitHudVisibilityForLocalContext()
        {
            try
            {
                if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
                    return;

                var shipController = GetLocalControlledShipController();
                if (shipController?.CubeGrid == null)
                    return;

                _cockpitHudSuppressed = !_cockpitHudSuppressed;

                if (_cockpitHudSuppressed)
                {
                    _cockpitInteractiveRequested = false;
                    SetInteractiveCursorEnabled(false);
                    HideHud();
                }
                else if (_clientUiState != null)
                {
                    UpdateRichHudState(_clientUiState);
                }
                else
                {
                    ShowHud();
                }
            }
            catch (Exception ex)
            {
                LogError($"ToggleCockpitHudVisibilityForLocalContext error: {ex}");
            }
        }

        private void SetInteractiveCursorEnabled(bool enabled)
        {
            try
            {
                if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated || !_richHudReady)
                    return;

                HudMain.EnableCursor = enabled;
            }
            catch (Exception ex)
            {
                LogError($"SetInteractiveCursorEnabled error: {ex}");
            }
        }

        private IMyPlayer GetLocalPlayer()
        {
            var session = MyAPIGateway.Session;
            return session?.Player ?? session?.LocalHumanPlayer;
        }

        private IMyCubeGrid GetLocalActionGrid()
        {
            var shipController = GetLocalControlledShipController();
            if (shipController?.CubeGrid != null)
                return shipController.CubeGrid;

            var player = GetLocalPlayer();
            var character = player?.Character;
            if (character == null)
                return null;

            var zone = GetSafeZoneForPosition(character.GetPosition());
            return GetLastKnownGridForPlayer(player, zone);
        }

        private void ToggleHudForLocalContext(IMyCubeGrid sourceGrid = null)
        {
            try
            {
                if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
                    return;

                var shipController = GetLocalControlledShipController();
                if (shipController?.CubeGrid != null)
                {
                    if (sourceGrid != null && shipController.CubeGrid.EntityId != sourceGrid.EntityId)
                        return;

                    if (_cockpitHudSuppressed)
                    {
                        _cockpitHudSuppressed = false;
                    }

                    _cockpitInteractiveRequested = !_cockpitInteractiveRequested;
                    _manualHudRequested = false;
                    SetInteractiveCursorEnabled(_cockpitInteractiveRequested);

                    if (_clientUiState != null)
                        UpdateRichHudState(_clientUiState);
                    else
                        ShowHud();

                    return;
                }

                if (_clientUiState == null || !_clientUiState.InRepairZone)
                {
                    _manualHudRequested = false;
                    SetInteractiveCursorEnabled(false);
                    HideHud();
                    return;
                }

                _manualHudRequested = !_manualHudRequested;
                if (!_manualHudRequested)
                {
                    HideHud();
                }
                else
                {
                    UpdateRichHudState(_clientUiState);
                    UpdateClientHudVisibility();
                }
            }
            catch (Exception ex)
            {
                LogError($"ToggleHudForLocalContext error: {ex}");
            }
        }

        private void ToggleRepairForLocalContext(IMyCubeGrid sourceGrid = null)
        {
            try
            {
                var shipController = GetLocalControlledShipController();
                IMyCubeGrid grid = shipController?.CubeGrid;

                if (grid == null)
                    return;

                if (sourceGrid != null && sourceGrid.EntityId != grid.EntityId)
                    return;

                ToggleRepair(grid);
            }
            catch (Exception ex)
            {
                LogError($"ToggleRepairForLocalContext error: {ex}");
            }
        }

        private void ShowStatusForLocalContext(IMyCubeGrid sourceGrid = null)
        {
            try
            {
                IMyCubeGrid grid = sourceGrid ?? GetLocalActionGrid();
                if (grid == null)
                    return;

                ShowRepairStatus(grid);
            }
            catch (Exception ex)
            {
                LogError($"ShowStatusForLocalContext error: {ex}");
            }
        }

        private void EnsureRhfBindingsAndTerminal()
        {
            try
            {
                if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated || !_richHudReady)
                    return;

                if (!_rhfBindsRegistered || _rhfBindGroup == null)
                {
                    _rhfBindGroup = BindManager.GetOrCreateGroup("SafeZoneRepair");

                    RegisterOrGetBind("Toggle HUD", new ControlHandle[] { RichHudControls.Control, RichHudControls.J }, out _hudToggleBind);
                    RegisterOrGetBind("Toggle Repair", new ControlHandle[] { RichHudControls.Control, RichHudControls.R }, out _repairToggleBind);

                    _rhfBindsRegistered = true;
                }

                if (!_rhfTerminalPagesRegistered)
                {
                    _rhfTerminalCategory = new TerminalPageCategory
                    {
                        Name = "Safe Zone Repair",
                        Enabled = true
                    };

                    _rhfOverviewPage = new TextPage
                    {
                        Name = "Overview",
                        Enabled = true
                    };
                    _rhfOverviewPage.HeaderText = new RichText("Safe Zone Repair");
                    _rhfOverviewPage.SubHeaderText = new RichText("RHF controls and bindable actions");
                    _rhfOverviewPage.Text = new RichText("Ctrl+J toggles the repair HUD.\nCtrl+R toggles repair mode only while controlling a ship controller.\nCtrl+N hides or restores the cockpit HUD.\nCockpit toolbar actions remain available for HUD and repair mode.");

                    _rhfKeybindPage = new RebindPage
                    {
                        Name = "Keybinds",
                        Enabled = true
                    };
                    _rhfKeybindPage.Add(_rhfBindGroup, GetDefaultRhfBindDefinitions(), false);

                    _rhfTerminalCategory.Add(_rhfOverviewPage);
                    _rhfTerminalCategory.Add(_rhfKeybindPage);
                    RichHudTerminal.Root.Add(_rhfTerminalCategory);

                    _rhfTerminalPagesRegistered = true;
                }
            }
            catch (Exception ex)
            {
                LogError($"EnsureRhfBindingsAndTerminal error: {ex}");
            }
        }

        private void RegisterOrGetBind(string bindName, IReadOnlyList<ControlHandle> combo, out IBind bind)
        {
            bind = null;

            if (_rhfBindGroup == null)
                return;

            if (!_rhfBindGroup.DoesBindExist(bindName))
            {
                IBind registeredBind;
                if (_rhfBindGroup.TryRegisterBind(bindName, out registeredBind, combo))
                {
                    bind = registeredBind;
                    return;
                }
            }

            bind = _rhfBindGroup.GetBind(bindName);
        }

        private BindDefinition[] GetDefaultRhfBindDefinitions()
        {
            return new[]
            {
                new BindDefinition("Toggle HUD", new[] { "Control", "J" }),
                new BindDefinition("Toggle Repair", new[] { "Control", "R" })
            };
        }

        private bool IsHudHotkeyPressed()
        {
            if (_hudToggleBind != null)
                return _hudToggleBind.IsNewPressed;

            var input = MyAPIGateway.Input;
            return input != null && input.IsAnyCtrlKeyPressed() && input.IsNewKeyPressed(HudToggleKey);
        }

        private bool IsRepairHotkeyPressed()
        {
            if (_repairToggleBind != null)
                return _repairToggleBind.IsNewPressed;

            var input = MyAPIGateway.Input;
            return input != null && input.IsAnyCtrlKeyPressed() && input.IsNewKeyPressed(RepairToggleKey);
        }

        private void UpdateClientHotkeys()
        {
            try
            {
                if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
                    return;

                if (MyAPIGateway.Input == null || MyAPIGateway.Session == null)
                    return;

                EnsureRhfBindingsAndTerminal();

                if (MyAPIGateway.Gui == null)
                    return;

                if (MyAPIGateway.Gui.ChatEntryVisible)
                    return;

                if (MyAPIGateway.Gui.IsCursorVisible && !IsCockpitInteractiveHudRequested())
                    return;

                if (GetLocalControlledShipController() != null && IsCockpitHudSuppressHotkeyPressed())
                    ToggleCockpitHudVisibilityForLocalContext();

                if (IsHudHotkeyPressed())
                    ToggleHudForLocalContext();

                if (GetLocalControlledShipController() != null && IsRepairHotkeyPressed())
                    ToggleRepairForLocalContext();
            }
            catch (Exception ex)
            {
                LogError($"UpdateClientHotkeys error: {ex}");
            }
        }

        private MySafeZone GetSafeZoneForPosition(Vector3D position)
        {
            foreach (var zone in safeZones)
            {
                Vector3D center = zone.PositionComp.WorldAABB.Center;
                if (Vector3D.Distance(center, position) <= zone.Radius)
                    return zone;
            }

            return null;
        }

        private IMyCubeGrid GetLastKnownGridForPlayer(IMyPlayer player, MySafeZone zone)
        {
            if (player == null)
                return null;

            long gridEntityId;
            if (!_lastControlledGridByPlayer.TryGetValue(player.IdentityId, out gridEntityId))
                return null;

            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(gridEntityId, out entity))
                return null;

            var grid = entity as IMyCubeGrid;
            if (grid == null)
                return null;

            if (zone != null && !IsGridInSafeZone(grid, zone))
                return null;

            return grid;
        }

        private void UpdateClientHudVisibility()
        {
            try
            {
                if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
                    return;

                if (!_richHudReady)
                    return;

                var session = MyAPIGateway.Session;
                if (session == null)
                    return;

                TimeSpan now = session.ElapsedPlayTime;
                if (now - _lastClientHudVisibilityCheck < clientHudVisibilityCheckInterval)
                    return;

                _lastClientHudVisibilityCheck = now;

                if (_clientUiState == null || !_clientUiState.InRepairZone)
                {
                    _manualHudRequested = false;
                    _cockpitInteractiveRequested = false;
                    SetInteractiveCursorEnabled(false);
                    HideHud();
                    return;
                }

                if (GetLocalControlledShipController() != null)
                {
                    if (_cockpitHudSuppressed)
                    {
                        _cockpitInteractiveRequested = false;
                        SetInteractiveCursorEnabled(false);
                        HideHud();
                        return;
                    }

                    SetInteractiveCursorEnabled(_cockpitInteractiveRequested);
                    ShowHud();
                    return;
                }

                _cockpitInteractiveRequested = false;
                SetInteractiveCursorEnabled(false);

                if (IsManualHudAllowed())
                {
                    ShowHud();
                    return;
                }

                HideHud();
            }
            catch (Exception ex)
            {
                LogError($"UpdateClientHudVisibility error: {ex}");
            }
        }
        // --- Основной цикл обновления ---
        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Utilities != null && !MyAPIGateway.Utilities.IsDedicated)
            {
                UpdateClientHotkeys();
                UpdateClientHudVisibility();
            }

            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            if (!initialized)
            {
                InitializeSafeZones();
                initialized = true;
                if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(ConfigFileName, typeof(SafeZoneGridMonitor)))
                    SaveDefaultConfig();
            }

            if (deltaTimer != null)
            {
                deltaTime = deltaTimer.Elapsed.TotalSeconds;
                deltaTimer.Restart();
            }

            TimeSpan now = MyAPIGateway.Session.ElapsedPlayTime;
            if (now - lastGridCheck > gridCheckInterval)
            {
                lastGridCheck = now;
                CheckAllPilotedGrids();
            }

            LogRepair($"Queue count: {blocksRepairQueue.Count}");
            if (blocksRepairQueue.Count > 0)
            {
                IMySlimBlock block = blocksRepairQueue.Peek();
                if (block != null)
                {
                    bool isProjected = Utils.IsProjected(block);
                    if (isProjected)
                    {
                        var cubeGrid = block.CubeGrid as MyCubeGrid;
                        var projector = cubeGrid?.Projector as IMyProjector;
                        if (projector == null || !projector.IsFunctional || !projector.IsProjecting)
                        {
                            LogRepair($"Projector for {Utils.BlockName(block)} is not functional/active, removing from queue");
                            blocksRepairQueue.Dequeue();
                            blocksInQueue.Remove(block);
                            blockRepairInfo.Remove(block);
                            return;
                        }
                    }

                    bool needsRepair = Utils.NeedRepair(block, false) || isProjected;
                    LogRepair($"Checking block {Utils.BlockName(block)}: needsRepair={needsRepair}");
                    if (needsRepair)
                    {
                        ApplyIncrementalRepair(block);
                    }
                    else
                    {
                        LogRepair($"Removing block {Utils.BlockName(block)} from queue (needsRepair={needsRepair})");
                        blocksRepairQueue.Dequeue();
                        blocksInQueue.Remove(block);
                        blockRepairInfo.Remove(block);
                    }
                }
            }

            if (!MyAPIGateway.Utilities.IsDedicated && sinceLastMsgTimer.ElapsedMilliseconds > 3000)
            {
                effect?.Stop();
                StopWeldingSound();
                sinceLastMsgTimer.Restart();
            }
        }

        // --- Поиск пилотируемых кораблей ---
        private void CheckAllPilotedGrids()
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            foreach (IMyPlayer player in players)
            {
                IMyShipController shipController = GetControlledShipController(player);
                if (shipController != null)
                {
                    IMyCubeGrid grid = shipController.CubeGrid;
                    if (grid == null)
                        continue;

                    _lastControlledGridByPlayer[player.IdentityId] = grid.EntityId;

                    bool repairEnabled = GetGridRepairSetting(grid);
                    if (!repairEnabled)
                    {
                        LogZone($"Grid {grid.DisplayName} has repairs disabled, skipping repair pass");
                        if (gridsInSafeZone.Contains(grid))
                        {
                            gridsInSafeZone.Remove(grid);
                            LogZone($"Grid {grid.DisplayName} removed from repair list (disabled)");
                        }
                    }

                    bool inAnySafeZone = false;
                    foreach (MySafeZone zone in safeZones)
                    {
                        if (IsGridInSafeZone(grid, zone))
                        {
                            inAnySafeZone = true;
                            if (repairEnabled)
                                HandleGridInSafeZone(grid, player.IdentityId, grid.EntityId);

                            string statusText = repairEnabled ? "Repair ready" : "Repair disabled for your ship";
                            SendRepairUiStateToPlayer(player, true, grid, zone, statusText);
                        }
                    }

                    if (!inAnySafeZone && gridsInSafeZone.Contains(grid))
                    {
                        gridsInSafeZone.Remove(grid);
                        LogZone($"Grid {grid.DisplayName} left safe zone");
                    }

                    if (!inAnySafeZone)
                        SendRepairUiStateToPlayer(player, false, grid, null, "Outside repair zone");

                    continue;
                }

                var character = player.Character;
                if (character == null)
                {
                    SendRepairUiStateToPlayer(player, false, null, null, "Outside repair zone");
                    continue;
                }

                MySafeZone playerZone = GetSafeZoneForPosition(character.GetPosition());
                if (playerZone != null)
                {
                    IMyCubeGrid lastGrid = GetLastKnownGridForPlayer(player, playerZone);
                    string statusText = lastGrid != null ? "Viewing repair zone" : "In repair zone";
                    SendRepairUiStateToPlayer(player, true, lastGrid, playerZone, statusText);
                }
                else
                {
                    SendRepairUiStateToPlayer(player, false, null, null, "Outside repair zone");
                }
            }
        }

        // --- Инициализация безопасных зон ---
        private void InitializeSafeZones()
        {
            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => e is MySafeZone);
            foreach (IMyEntity entity in entities)
            {
                MySafeZone zone = entity as MySafeZone;
                if (zone != null)
                {
                    safeZones.Add(zone);
                    LogGeneral($"Added safe zone: {zone.DisplayName} (ID: {zone.EntityId}, radius {zone.Radius})");
                }
            }
            LogGeneral($"Total safe zones: {safeZones.Count}");
        }

        private void OnEntityAdded(IMyEntity entity)
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;
            MySafeZone zone = entity as MySafeZone;
            if (zone != null)
            {
                safeZones.Add(zone);
                LogGeneral($"New safe zone added: {zone.DisplayName}");

                if (!zoneConfigs.ContainsKey(zone.EntityId))
                {
                    string zoneName = GetSafeZoneDefaultName(zone);

                    zoneConfigs[zone.EntityId] = new SafeZoneConfig
                    {
                        ZoneName = zoneName,
                        DisplayName = zoneName,
                        ZoneEntityId = zone.EntityId,
                        WeldingSpeed = this.weldingSpeed,
                        CostModifier = this.costModifier,
                        Enabled = true,
                        AllowProjections = true,
                        ProjectionBuildDelay = 1f,
                        ForbiddenComponents = new List<string>()
                    };
                    SaveConfig(new List<SafeZoneConfig>(zoneConfigs.Values));
                }
            }
        }

        // --- Проверка нахождения в зоне ---
        private bool IsGridInSafeZone(IMyCubeGrid grid, MySafeZone zone)
        {
            Vector3D center = zone.PositionComp.WorldAABB.Center;
            double radius = zone.Radius;
            Vector3D gridPos = grid.PositionComp.WorldAABB.Center;
            double dist = Vector3D.Distance(center, gridPos);
            return dist <= radius;
        }

        private bool GridIsInSafeZone(IMyCubeGrid grid)
        {
            foreach (MySafeZone zone in safeZones)
                if (IsGridInSafeZone(grid, zone))
                    return true;
            return false;
        }

        // --- Обработка входа корабля в зону ---
        private void HandleGridInSafeZone(IMyCubeGrid grid, long pilotId, long sourceGridEntityId = 0)
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;

            if (!gridsInSafeZone.Contains(grid))
            {
                gridsInSafeZone.Add(grid);
                LogZone($"Grid {grid.DisplayName} entered safe zone");
            }

            MySafeZone zoneForGrid = GetSafeZoneForGrid(grid);
            SafeZoneConfig zoneCfg = null;
            if (zoneForGrid != null)
            {
                LogZone($"Zone for grid: {zoneForGrid.DisplayName}, cfg exists: {zoneConfigs.ContainsKey(zoneForGrid.EntityId)}");
                if (zoneConfigs.TryGetValue(zoneForGrid.EntityId, out zoneCfg))
                    LogZone($"  AllowProjections={zoneCfg.AllowProjections}");
            }

            List<IMySlimBlock> allBlocks = new List<IMySlimBlock>();
            grid.GetBlocks(allBlocks);
            int added = 0;
            foreach (IMySlimBlock block in allBlocks)
            {
                var projector = block.FatBlock as IMyProjector;
                if (projector != null && projector.IsFunctional && projector.IsProjecting)
                {
                    LogZone($"Found projector, handling projected grid: {projector.ProjectedGrid?.DisplayName}");
                    HandleGridInSafeZone(projector.ProjectedGrid, pilotId, sourceGridEntityId != 0 ? sourceGridEntityId : grid.EntityId);
                }

                bool isProjected = Utils.IsProjected(block);
                bool needsRepair = Utils.NeedRepair(block, false) || isProjected;
                bool alreadyInQueue = blocksInQueue.Contains(block);

                if (needsRepair && !alreadyInQueue)
                {
                    if (isProjected && zoneCfg != null && !zoneCfg.AllowProjections)
                    {
                        LogZone($"Block {Utils.BlockName(block)} is a projection but zone {zoneCfg.ZoneName} does not allow projection repair, skipping");
                        continue;
                    }

                    // Если блок ранее был завершён, удаляем его ключ, чтобы разрешить новое уведомление при следующем ремонте
                    string blockKey = $"{grid.EntityId}:{block.Position}";
                    if (_completedBlockKeys.Contains(blockKey))
                        _completedBlockKeys.Remove(blockKey);

                    LogZone($"Adding block {Utils.BlockName(block)} to queue (projected={isProjected})");
                    blocksInQueue.Add(block);
                    blocksRepairQueue.Enqueue(block);
                    float totalCost = CalculateTotalRepairCost(block, zoneCfg);
                    LogZone($"Block {Utils.BlockName(block)} totalCost={totalCost}");
                    blockRepairInfo[block] = new BlockRepairInfo
                    {
                        TotalCost = (long)Math.Max(0f, totalCost),
                        InitialCost = (long)Math.Max(0f, totalCost),
                        PilotIdentityId = pilotId,
                        SourceGridEntityId = sourceGridEntityId != 0 ? sourceGridEntityId : grid.EntityId
                    };
                    added++;
                }
                else
                {
                    LogZone($"Block {Utils.BlockName(block)} not added: needsRepair={needsRepair}, alreadyInQueue={alreadyInQueue}");
                }
            }
            if (added > 0)
                LogZone($"Added {added} new blocks to repair queue for grid {grid.DisplayName}");

            foreach (MySafeZone zone in safeZones)
            {
                if (IsGridInSafeZone(grid, zone))
                    AllowWeldingInSafeZone(zone);
            }
        }

        private MySafeZone GetSafeZoneForGrid(IMyCubeGrid grid)
        {
            foreach (var zone in safeZones)
                if (IsGridInSafeZone(grid, zone))
                    return zone;
            return null;
        }

        private void AllowWeldingInSafeZone(MySafeZone zone)
        {
            try
            {
                zone.AllowedActions = CastHax(zone.AllowedActions,
                    (int)zone.AllowedActions | (int)SafeZoneAction.Welding | (int)SafeZoneAction.BuildingProjections);
            }
            catch (Exception ex)
            {
                LogError($"Error allowing welding: {ex}");
            }
        }

        // --- Расчёт стоимости ---
        private float CalculateTotalRepairCost(IMySlimBlock block, SafeZoneConfig zoneCfg)
        {
            float cost = CalculateRepairCost(block, zoneCfg);
            LogZone($"CalculateTotalRepairCost for {Utils.BlockName(block)}: raw cost={cost}");
            if (cost < 0)
            {
                LogZone($"Forbidden component detected, returning -1");
                return -1;
            }
            float finalCost = cost * 100f;
            LogZone($"Final cost *100 = {finalCost}");
            return finalCost;
        }

        private float CalculateRepairCost(IMySlimBlock block, SafeZoneConfig zoneCfg)
        {
            float total = 0f;
            LogZone($"Calculating repair cost for {Utils.BlockName(block)}");

            if (Utils.IsProjected(block))
            {
                var def = block.BlockDefinition as MyCubeBlockDefinition;
                if (def != null)
                {
                    LogZone($"Block is projected, definition: {def.DisplayNameText}");
                    foreach (var comp in def.Components)
                    {
                        float compCost = GetComponentCost(comp.Definition.Id, zoneCfg);
                        LogZone($"Component {comp.Definition.Id.SubtypeName}: cost={compCost}, count={comp.Count}");
                        if (compCost < 0) return -1;
                        total += compCost * comp.Count;
                    }
                }
            }
            else
            {
                var missing = new Dictionary<string, int>();
                block.GetMissingComponents(missing);
                LogZone($"Missing components count: {missing.Count}");
                foreach (var kv in missing)
                {
                    MyDefinitionId id;
                    if (MyDefinitionId.TryParse("MyObjectBuilder_Component/" + kv.Key, out id))
                    {
                        float compCost = GetComponentCost(id, zoneCfg);
                        LogZone($"Component {kv.Key}: cost={compCost}, missing={kv.Value}");
                        if (compCost < 0) return -1;
                        total += compCost * kv.Value;
                    }
                }
            }

            float modifier = zoneCfg != null ? zoneCfg.CostModifier : costModifier;
            LogZone($"Total before modifier: {total}, modifier={modifier}");
            total *= modifier;
            LogZone($"Total after modifier: {total}");
            return total;
        }

        private float GetComponentCost(MyDefinitionId id, SafeZoneConfig zoneCfg)
        {
            if (zoneCfg != null && zoneCfg.ForbiddenComponents != null && zoneCfg.ForbiddenComponents.Contains(id.SubtypeId.String))
                return -1;

            var def = MyDefinitionManager.Static.GetComponentDefinition(id);
            return def?.MinimalPricePerUnit > 0 ? def.MinimalPricePerUnit : 1f;
        }

        private long GetPlayerCreditBalance(IMyPlayer player)
        {
            long balance = 0;
            if (player.TryGetBalanceInfo(out balance))
                return balance;
            return 0;
        }

        private void InvalidateEstimatedRepairCostCache(long playerId = 0, long gridEntityId = 0)
        {
            if (playerId == 0 && gridEntityId == 0)
            {
                _estimatedRepairCostCache.Clear();
                return;
            }

            if (playerId != 0)
            {
                EstimatedRepairCostCacheEntry cached;
                if (_estimatedRepairCostCache.TryGetValue(playerId, out cached) &&
                    (gridEntityId == 0 || cached.GridEntityId == gridEntityId))
                {
                    _estimatedRepairCostCache.Remove(playerId);
                }

                return;
            }

            var playersToInvalidate = new List<long>();
            foreach (var pair in _estimatedRepairCostCache)
            {
                if (pair.Value.GridEntityId == gridEntityId)
                    playersToInvalidate.Add(pair.Key);
            }

            foreach (var cachedPlayerId in playersToInvalidate)
                _estimatedRepairCostCache.Remove(cachedPlayerId);
        }

        private long GetEstimatedRepairCostForUi(IMyPlayer player, bool inRepairZone, IMyCubeGrid grid, MySafeZone zone, bool repairEnabled)
        {
            if (player == null || grid == null || !inRepairZone)
                return 0;

            SafeZoneConfig zoneCfg = null;
            if (zone != null)
                zoneConfigs.TryGetValue(zone.EntityId, out zoneCfg);

            if (zoneCfg != null && !zoneCfg.Enabled)
                return 0;

            long playerId = player.IdentityId;
            long gridEntityId = grid.EntityId;
            long zoneEntityId = zone?.EntityId ?? 0;
            DateTime now = DateTime.UtcNow;

            EstimatedRepairCostCacheEntry cached;
            if (_estimatedRepairCostCache.TryGetValue(playerId, out cached) &&
                cached.GridEntityId == gridEntityId &&
                cached.ZoneEntityId == zoneEntityId &&
                cached.InRepairZone == inRepairZone &&
                cached.RepairEnabled == repairEnabled &&
                now - cached.CachedAtUtc < estimatedRepairCostCacheInterval)
            {
                return cached.EstimatedRepairCost;
            }

            long estimatedRepairCost = CalculateEstimatedRepairCost(grid, zoneCfg);
            _estimatedRepairCostCache[playerId] = new EstimatedRepairCostCacheEntry
            {
                GridEntityId = gridEntityId,
                ZoneEntityId = zoneEntityId,
                InRepairZone = inRepairZone,
                RepairEnabled = repairEnabled,
                CachedAtUtc = now,
                EstimatedRepairCost = estimatedRepairCost
            };

            return estimatedRepairCost;
        }

        private long CalculateEstimatedRepairCost(IMyCubeGrid grid, SafeZoneConfig zoneCfg)
        {
            if (grid == null)
                return 0;

            long totalCost = 0;
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);

            foreach (var block in blocks)
            {
                if (block == null || Utils.IsProjected(block) || !Utils.NeedRepair(block, false))
                    continue;

                float blockCost = CalculateTotalRepairCost(block, zoneCfg);
                if (blockCost <= 0)
                    continue;

                totalCost += (long)blockCost;
            }

            return totalCost;
        }

        // --- Основной метод ремонта ---
        private void ApplyIncrementalRepair(IMySlimBlock block)
        {
            LogRepair($"ApplyIncrementalRepair called for {Utils.BlockName(block)}");
            BlockRepairInfo info;
            if (!blockRepairInfo.TryGetValue(block, out info))
            {
                LogRepair($"Block {Utils.BlockName(block)} has no repair info, removing from queue");
                blocksRepairQueue.Dequeue();
                blocksInQueue.Remove(block);
                return;
            }

            bool isProjected = Utils.IsProjected(block);
            LogRepair($"isProjected={isProjected}, TotalCost={info.TotalCost}, PilotId={info.PilotIdentityId}");

            MySafeZone currentZone = GetSafeZoneForGrid(block.CubeGrid);
            SafeZoneConfig zoneCfg = null;
            if (currentZone != null)
                zoneConfigs.TryGetValue(currentZone.EntityId, out zoneCfg);

            if (zoneCfg != null && !zoneCfg.Enabled)
            {
                LogZone($"Zone {currentZone.DisplayName} is disabled, removing block {Utils.BlockName(block)} from queue");
                blocksRepairQueue.Dequeue();
                blocksInQueue.Remove(block);
                blockRepairInfo.Remove(block);
                return;
            }

            long ownerId = block.BuiltBy;
            IMyProjector projector = null;
            if (isProjected)
            {
                var cubeGrid = block.CubeGrid as MyCubeGrid;
                if (cubeGrid?.Projector != null)
                {
                    ownerId = cubeGrid.Projector.OwnerId;
                    projector = cubeGrid.Projector;
                }
            }

            IMyPlayer player = null;
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            foreach (var p in players)
            {
                if (p.IdentityId == info.PilotIdentityId)
                {
                    player = p;
                    break;
                }
            }
            if (player == null)
            {
                LogRepair($"player not found for pilotId {info.PilotIdentityId}");
                blocksRepairQueue.Dequeue();
                blocksInQueue.Remove(block);
                blockRepairInfo.Remove(block);
                return;
            }

            long balance = GetPlayerCreditBalance(player);
            LogRepair($"player balance: {balance}");

            if (isProjected)
            {
                if (projector != null && Utils.CanBuild(block, true))
                {
                    float delay = zoneCfg != null ? zoneCfg.ProjectionBuildDelay : 1f;
                    if ((DateTime.Now - _lastProjectionBuildTime).TotalSeconds < delay)
                    {
                        LogRepair($"Projection build delay {delay}s, skipping this tick");
                        return;
                    }

                    if (balance < info.TotalCost)
                    {
                        LogCost($"Player cannot afford projected block (need {info.TotalCost}, have {balance})");
                        blocksRepairQueue.Dequeue();
                        blocksInQueue.Remove(block);
                        blockRepairInfo.Remove(block);
                        return;
                    }

                    projector.Build(block, ownerId, ownerId, false, ownerId);
                    player.RequestChangeBalance(-info.TotalCost);
                    SendRepairNotificationToClients(block, true, info.InitialCost, player.IdentityId);
                    blocksRepairQueue.Dequeue();
                    blocksInQueue.Remove(block);
                    blockRepairInfo.Remove(block);
                    _lastProjectionBuildTime = DateTime.Now;
                }
                else
                {
                    LogRepair($"Cannot build projected block (projector null or CanBuild false), moving to back of queue");
                    blocksInQueue.Remove(block);
                    blocksRepairQueue.Dequeue();
                    blocksInQueue.Add(block);
                    blocksRepairQueue.Enqueue(block);
                }
                return;
            }

            float localWeldingSpeed = GetWeldingSpeedForZone(currentZone);
            float remainingBuildIntegrity = block.MaxIntegrity - block.BuildIntegrity;
            bool hasDeformation = block.HasDeformation;
            bool pureDeformationOnly = remainingBuildIntegrity <= 0.01f && hasDeformation;
            float currentDamageBefore = block.CurrentDamage;
            float accumulatedDamageBefore = block.AccumulatedDamage;
            float buildIntegrityBefore = block.BuildIntegrity;

            float repairAmount = localWeldingSpeed * (float)deltaTime;
            if (remainingBuildIntegrity > 0.01f)
            {
                if (repairAmount > remainingBuildIntegrity)
                    repairAmount = remainingBuildIntegrity;

                if (repairAmount < 1f)
                    repairAmount = Math.Min(remainingBuildIntegrity, 1f);
            }
            else if (hasDeformation)
            {
                repairAmount = Math.Max(repairAmount, 1f);
            }

            LogRepair($"repairAmount={repairAmount}, remainingBuildIntegrity={remainingBuildIntegrity}, BuildIntegrity={block.BuildIntegrity}, MaxIntegrity={block.MaxIntegrity}, HasDeformation={hasDeformation}, CurrentDamage={currentDamageBefore}, AccumulatedDamage={accumulatedDamageBefore}");

            if (repairAmount <= 0 && !hasDeformation)
            {
                LogRepair($"repairAmount <= 0 and no deformation, removing block from queue");
                blocksRepairQueue.Dequeue();
                blocksInQueue.Remove(block);
                blockRepairInfo.Remove(block);
                return;
            }

            float stepCost = 0f;
            if (info.TotalCost > 0 && remainingBuildIntegrity > 0.01f)
            {
                float integrityToRestore = remainingBuildIntegrity + repairAmount;
                stepCost = info.TotalCost * (repairAmount / Math.Max(0.001f, integrityToRestore));
                if (stepCost < 1f)
                    stepCost = 1f;
            }
            LogRepair($"stepCost={stepCost}, remainingBuildIntegrity={remainingBuildIntegrity}");

            if (stepCost > 0f && balance < stepCost)
            {
                LogCost($"Player cannot afford repair step (need {stepCost}, have {balance})");
                blocksRepairQueue.Dequeue();
                blocksInQueue.Remove(block);
                blockRepairInfo.Remove(block);
                return;
            }

            if (stepCost > 0f)
            {
                player.RequestChangeBalance(-(long)stepCost);
                info.TotalCost -= (long)stepCost;
                if (info.TotalCost < 0)
                    info.TotalCost = 0;
            }
            LogRepair($"After deduction: new TotalCost={info.TotalCost}");

            try
            {
                var tempInv = new MyInventory(1000000, new Vector3(10, 10, 10), MyInventoryFlags.CanReceive);
                var def = block.BlockDefinition as MyCubeBlockDefinition;
                if (def != null)
                {
                    foreach (var comp in def.Components)
                    {
                        if (comp.Definition?.Id != null && !string.IsNullOrEmpty(comp.Definition.Id.SubtypeName))
                        {
                            var phys = new MyObjectBuilder_PhysicalObject { SubtypeName = comp.Definition.Id.SubtypeName };
                            tempInv.AddItems(1000, phys);
                        }
                    }
                }

                if (pureDeformationOnly)
                {
                    float deformationBudget = Math.Max(1f, Math.Max(currentDamageBefore, accumulatedDamageBefore));
                    LogRepair($"Calling FixBones for deformation-only block with oldDamage={deformationBudget}");
                    block.FixBones(deformationBudget, 0f);
                    block.UpdateVisual();
                    LogRepair("FixBones completed");
                }
                else
                {
                    LogRepair($"Calling IncreaseMountLevel with repairAmount={repairAmount}");
                    block.IncreaseMountLevel(repairAmount, ownerId, tempInv, 0f);
                    LogRepair($"IncreaseMountLevel completed");

                    if (block.HasDeformation && block.MaxIntegrity - block.BuildIntegrity <= 0.01f)
                    {
                        float deformationBudget = Math.Max(1f, Math.Max(currentDamageBefore, accumulatedDamageBefore));
                        LogRepair($"Calling FixBones after weld completion with oldDamage={deformationBudget}");
                        block.FixBones(deformationBudget, 0f);
                        block.UpdateVisual();
                        LogRepair("Post-weld FixBones completed");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Repair error: {ex}");
            }

            float buildIntegrityAfter = block.BuildIntegrity;
            float currentDamageAfter = block.CurrentDamage;
            float accumulatedDamageAfter = block.AccumulatedDamage;
            bool hasDeformationAfter = block.HasDeformation;
            bool progressMade =
                buildIntegrityAfter > buildIntegrityBefore + 0.001f ||
                currentDamageAfter < currentDamageBefore - 0.001f ||
                accumulatedDamageAfter < accumulatedDamageBefore - 0.001f ||
                (hasDeformation && !hasDeformationAfter);

            SendRepairNotificationToClients(block, false, 0, player.IdentityId);

            if (block.MaxIntegrity - block.BuildIntegrity <= 0.01f && !block.HasDeformation)
            {
                LogRepair($"Block fully repaired, removing from queue");
                blocksRepairQueue.Dequeue();
                blocksInQueue.Remove(block);
                blockRepairInfo.Remove(block);
                if (info.TotalCost > 1)
                {
                    player.RequestChangeBalance(-info.TotalCost);
                    info.TotalCost = 0;
                }
                SendRepairNotificationToClients(block, true, info.InitialCost, player.IdentityId);
                return;
            }

            if (!progressMade)
            {
                info.NoProgressPasses++;

                if (info.NoProgressPasses >= 5)
                {
                    LogRepair($"No repair progress detected for {Utils.BlockName(block)} after {info.NoProgressPasses} attempts, removing block from queue");
                    blocksRepairQueue.Dequeue();
                    blocksInQueue.Remove(block);
                    blockRepairInfo.Remove(block);
                    return;
                }

                LogRepair($"No repair progress detected for {Utils.BlockName(block)}, moving block to back of queue (attempt {info.NoProgressPasses})");
                blocksRepairQueue.Dequeue();
                blocksRepairQueue.Enqueue(block);
            }
            else
            {
                info.NoProgressPasses = 0;
            }
        }

        private float GetWeldingSpeedForZone(MySafeZone zone)
        {
            if (zone == null) return weldingSpeed;
            SafeZoneConfig cfg;
            if (zoneConfigs.TryGetValue(zone.EntityId, out cfg))
                return cfg.WeldingSpeed;
            return weldingSpeed;
        }

        private float GetCostModifierForZone(MySafeZone zone)
        {
            if (zone == null) return costModifier;
            SafeZoneConfig cfg;
            if (zoneConfigs.TryGetValue(zone.EntityId, out cfg))
                return cfg.CostModifier;
            return costModifier;
        }

        // --- Сетевые уведомления ---
        private void SendRepairNotificationToClients(IMySlimBlock block, bool complete, float cost, long ownerId)
        {
            try
            {
                InvalidateEstimatedRepairCostCache(ownerId, block?.CubeGrid?.EntityId ?? 0);
                string blockKey = $"{block.CubeGrid.EntityId}:{block.Position}";
                if (complete && _completedBlockKeys.Contains(blockKey))
                    return; // уже отправляли уведомление об этом блоке

                LogGeneral($"Sending repair notification: block {blockKey}, complete={complete}, cost={cost}");

                var msg = new RepairNotificationMessage
                {
                    BlockEntityId = block.FatBlock?.EntityId ?? 0,
                    RepairsComplete = complete,
                    Cost = cost,
                    Position = block.Position,
                    BlockName = Utils.BlockName(block),
                    PlayerId = ownerId
                };
                byte[] data = MyAPIGateway.Utilities.SerializeToBinary(msg);
                var targetPlayer = GetPlayerByIdentityId(ownerId);
                if (targetPlayer != null)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(SyncId, data, targetPlayer.SteamUserId);
                    SendRepairUiStateToPlayer(targetPlayer, true, block.CubeGrid, GetSafeZoneForGrid(block.CubeGrid), "Repair complete", $"{msg.BlockName} repaired! Cost: {msg.Cost} SC");
                }
                else
                {
                    MyAPIGateway.Multiplayer.SendMessageToOthers(SyncId, data);
                }

                if (complete)
                {
                    _completedBlockKeys.Add(blockKey);
                }
            }
            catch (Exception ex)
            {
                LogError($"Send notification error: {ex}");
            }
        }

        private void HandleRepairNotification(byte[] data)
		{
			try
			{
				var msg = MyAPIGateway.Utilities.SerializeFromBinary<RepairNotificationMessage>(data);
				if (msg == null)
					return;

				var localPlayer = MyAPIGateway.Session?.Player;
				long localIdentityId = localPlayer?.IdentityId ?? 0L;

				if (msg.PlayerId != 0 && localIdentityId != 0 && msg.PlayerId != localIdentityId)
					return;

				if (_clientUiState == null)
					_clientUiState = new RepairUiStateMessage();

				if (msg.RepairsComplete)
					_clientUiState.LastRepairText = string.Format("{0} repaired! Cost: {1:0} SC", msg.BlockName, msg.Cost);

				UpdateRichHudState(_clientUiState);

				// Старые сообщения посреди экрана отключены.
			}
			catch (Exception ex)
			{
				LogError("HandleRepairNotification error: " + ex);
			}
		}

        private IMyPlayer GetPlayerByIdentityId(long identityId)
        {
            if (identityId == 0)
                return null;

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var player in players)
            {
                if (player != null && player.IdentityId == identityId)
                    return player;
            }

            return null;
        }

        private string BuildCurrentRepairTextForUi(IMyPlayer player, IMyCubeGrid grid)
        {
            if (player == null || grid == null)
                return "Current repair: -";

            int ahead = 0;
            foreach (var queuedBlock in blocksRepairQueue)
            {
                if (queuedBlock == null)
                {
                    ahead++;
                    continue;
                }

                BlockRepairInfo queuedInfo;
                if (!blockRepairInfo.TryGetValue(queuedBlock, out queuedInfo))
                {
                    ahead++;
                    continue;
                }

                long queuedSourceGridId = queuedInfo.SourceGridEntityId != 0 ? queuedInfo.SourceGridEntityId : (queuedBlock.CubeGrid?.EntityId ?? 0);
                if (queuedSourceGridId != grid.EntityId)
                {
                    ahead++;
                    continue;
                }

                string blockName = TruncateHudBlockName(Utils.BlockName(queuedBlock), 32);

                if (ahead <= 0)
                    return string.Format("Current repair: {0}", blockName);

                return string.Format("Current repair: queued ({0} ahead) -> {1}", ahead, blockName);
            }

            long estimate = GetEstimatedRepairCostForUi(player, true, grid, GetSafeZoneForGrid(grid), GetGridRepairSetting(grid));
            if (estimate > 0)
                return "Current repair: queued / waiting for next block";

            return "Current repair: -";
        }

        private string BuildRepairPhaseForUi(IMyPlayer player, IMyCubeGrid grid)
        {
            if (player == null || grid == null)
                return "Repair phase: idle";

            int ahead = 0;
            foreach (var queuedBlock in blocksRepairQueue)
            {
                if (queuedBlock == null)
                {
                    ahead++;
                    continue;
                }

                BlockRepairInfo queuedInfo;
                if (!blockRepairInfo.TryGetValue(queuedBlock, out queuedInfo))
                {
                    ahead++;
                    continue;
                }

                long queuedSourceGridId = queuedInfo.SourceGridEntityId != 0 ? queuedInfo.SourceGridEntityId : (queuedBlock.CubeGrid?.EntityId ?? 0);
                if (queuedSourceGridId != grid.EntityId)
                {
                    ahead++;
                    continue;
                }

                bool queuedHasBuildWork = queuedBlock.MaxIntegrity - queuedBlock.BuildIntegrity > 0.01f;
                bool queuedHasDeformation = queuedBlock.HasDeformation;

                if (ahead <= 0)
                {
                    if (queuedHasBuildWork && queuedHasDeformation)
                        return string.Format("Repair phase: integrity + deformation ({0:0.0}%)", queuedBlock.BuildLevelRatio * 100f);
                    if (queuedHasBuildWork)
                        return string.Format("Repair phase: integrity ({0:0.0}%)", queuedBlock.BuildLevelRatio * 100f);
                    if (queuedHasDeformation)
                        return "Repair phase: deformation";

                    return "Repair phase: finishing";
                }

                return string.Format("Repair phase: queued ({0} ahead)", ahead);
            }

            long estimate = GetEstimatedRepairCostForUi(player, true, grid, GetSafeZoneForGrid(grid), GetGridRepairSetting(grid));
            if (estimate > 0)
                return "Repair phase: waiting";

            return "Repair phase: idle";
        }

        private string TruncateHudBlockName(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, Math.Max(1, maxLength - 3)) + "...";
        }

        private void SendRepairUiStateToPlayer(IMyPlayer player, bool inRepairZone, IMyCubeGrid grid, MySafeZone zone, string statusText = null, string lastRepairText = null)
        {
            if (player == null || !MyAPIGateway.Multiplayer.IsServer)
                return;

            SafeZoneConfig cfg = null;
            if (zone != null)
                zoneConfigs.TryGetValue(zone.EntityId, out cfg);

            string zoneName = cfg?.DisplayName ?? cfg?.ZoneName ?? GetSafeZoneDefaultName(zone);
            bool repairEnabled = grid != null && GetGridRepairSetting(grid);
            long estimatedRepairCost = GetEstimatedRepairCostForUi(player, inRepairZone, grid, zone, repairEnabled);

            var msg = new RepairUiStateMessage
            {
                PlayerId = player.IdentityId,
                InRepairZone = inRepairZone,
                ZoneName = zoneName,
                RepairEnabled = repairEnabled,
                StatusText = string.IsNullOrWhiteSpace(statusText) ? (inRepairZone ? "In repair zone" : "Outside repair zone") : statusText,
                LastRepairText = string.IsNullOrWhiteSpace(lastRepairText) ? _clientUiState.LastRepairText : lastRepairText,
                LastEventUtcTicks = DateTime.UtcNow.Ticks,
                EstimatedRepairCost = estimatedRepairCost,
                CurrentRepairText = inRepairZone ? BuildCurrentRepairTextForUi(player, grid) : "Current repair: -",
                RepairPhaseText = inRepairZone ? BuildRepairPhaseForUi(player, grid) : "Repair phase: idle"
            };

            byte[] bytes = MyAPIGateway.Utilities.SerializeToBinary(msg);
            MyAPIGateway.Multiplayer.SendMessageTo(RepairUiStateSyncId, bytes, player.SteamUserId);
        }

        private void HandleRepairUiState(byte[] data)
        {
            try
            {
                var msg = MyAPIGateway.Utilities.SerializeFromBinary<RepairUiStateMessage>(data);
                if (msg == null)
                    return;

                if (msg.PlayerId != 0 && msg.PlayerId != MyAPIGateway.Session.LocalHumanPlayer?.IdentityId)
                    return;

                _clientUiState = msg;

                if (GetLocalControlledShipController() != null && !IsCockpitHudVisible())
                {
                    HideHud();
                    return;
                }

                UpdateRichHudState(msg);
            }
            catch (Exception ex)
            {
                LogError($"HandleRepairUiState error: {ex}");
            }
        }

        public static T CastHax<T>(T typeRef, object castObj) => (T)castObj;

        // --- Очередь и команды ---
        private void ClearQueue()
        {
            blocksRepairQueue.Clear();
            blocksInQueue.Clear();
            blockRepairInfo.Clear();
            _estimatedRepairCostCache.Clear();
            LogGeneral("Repair queue cleared");
        }

        // --- Обработка чат-команд ---
        private void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith("/")) return;

            string[] parts = messageText.Trim().Split(' ');
            string command = parts[0].ToLower();

            if (command == "/szhud")
            {
                sendToOthers = false;
                ToggleHudForLocalContext();
                return;
            }

            if (!MyAPIGateway.Multiplayer.IsServer) return;

            sendToOthers = false;

            MyLog.Default.WriteLine($"[SafeZoneRepair] Command received: '{messageText}'");

            if (command == "/szreload")
            {
                ReloadConfig();
                ClearQueue();
                MyAPIGateway.Utilities.ShowMessage("SZ", "Safe Zones Reloaded");
                return;
            }
            if (command == "/szlogreload")
            {
                LoadLoggingConfig();
                MyAPIGateway.Utilities.ShowMessage("SZ", "Logging config reloaded");
                return;
            }
        }

        private void HandleTerminalAction(byte[] data)
        {
            try
            {
                var msg = MyAPIGateway.Utilities.SerializeFromBinary<TerminalActionMessage>(data);
                if (msg == null) return;

                LogGeneral($"HandleTerminalAction: Grid {msg.GridEntityId}, Action {msg.Action}");

                IMyEntity ent;
                if (!MyAPIGateway.Entities.TryGetEntityById(msg.GridEntityId, out ent))
                {
                    LogError($"HandleTerminalAction: Entity not found");
                    return;
                }

                var grid = ent as IMyCubeGrid;
                if (grid == null) return;

                if (msg.Action == "Toggle")
                    ToggleRepair(grid);
                else if (msg.Action == "Status")
                    ShowRepairStatus(grid);
                else if (msg.Action == "ToggleHud")
                    ToggleHudForLocalContext(grid);
                else if (msg.Action == "ToggleRepairLocal")
                    ToggleRepairForLocalContext(grid);
            }
            catch (Exception ex)
            {
                LogError($"HandleTerminalAction error: {ex}");
            }
        }

        // --- Вспомогательные методы для отправки уведомлений ---
        private IMyPlayer GetPlayerControllingGrid(IMyCubeGrid grid)
        {
            if (grid == null) return null;
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            foreach (var player in players)
            {
                var shipController = GetControlledShipController(player);
                if (shipController != null && shipController.CubeGrid == grid)
                    return player;
            }
            return null;
        }

        private void SendTerminalStatusToPlayer(IMyPlayer player, string text, string color, int time)
        {
            if (player == null) return;

            text = text.Trim();

            if (_lastPlayerStatusText.ContainsKey(player.IdentityId))
            {
                if (_lastPlayerStatusText[player.IdentityId] == text && (DateTime.Now - _lastPlayerStatusTime[player.IdentityId]).TotalSeconds < 5)
                    return;
            }
            _lastPlayerStatusText[player.IdentityId] = text;
            _lastPlayerStatusTime[player.IdentityId] = DateTime.Now;

            LogGeneral($"Sending terminal status to player {player.IdentityId}: '{text}'");

            var msg = new TerminalStatusMessage { Text = text, Color = color, Time = time, PlayerId = player.IdentityId };
            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(msg);

            if (MyAPIGateway.Session?.LocalHumanPlayer != null && player.SteamUserId == MyAPIGateway.Session.LocalHumanPlayer.SteamUserId)
            {
                HandleTerminalStatus(data);
                return;
            }

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                ulong steamId = player.SteamUserId;
                MyAPIGateway.Multiplayer.SendMessageTo(TerminalStatusSyncId, data, steamId);
            }
            else
            {
                MyAPIGateway.Multiplayer.SendMessageToServer(TerminalStatusSyncId, data);
            }
        }


        private void HandleTerminalStatus(byte[] data)
		{
			try
			{
				var msg = MyAPIGateway.Utilities.SerializeFromBinary<TerminalStatusMessage>(data);
				if (msg == null)
					return;

				var localPlayer = MyAPIGateway.Session?.Player;
				long localIdentityId = localPlayer?.IdentityId ?? 0L;

				if (msg.PlayerId != 0 && localIdentityId != 0 && msg.PlayerId != localIdentityId)
					return;

				if (_clientUiState == null)
					_clientUiState = new RepairUiStateMessage();

				_clientUiState.StatusText = msg.Text ?? string.Empty;
				UpdateRichHudState(_clientUiState);

				// Старые сообщения посреди экрана отключены.
			}
			catch (Exception ex)
			{
				LogError("HandleTerminalStatus error: " + ex);
			}
		}
    }

    // Вспомогательный класс для битовых операций
    public static class SafeZoneAction
    {
        public static readonly object Damage = 0x1;
        public static readonly object Shooting = 0x2;
        public static readonly object Drilling = 0x4;
        public static readonly object Welding = 0x8;
        public static readonly object Grinding = 0x10;
        public static readonly object VoxelHand = 0x20;
        public static readonly object Building = 0x40;
        public static readonly object LandingGearLock = 0x80;
        public static readonly object ConvertToStation = 0x100;
        public static readonly object BuildingProjections = 0x200;
        public static readonly object All = 0x3FF;
        public static readonly object AdminIgnore = 0x37E;
    }
}

