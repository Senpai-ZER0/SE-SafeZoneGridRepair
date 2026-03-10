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
using SafeZoneRepair;

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

        // Флаг для предотвращения повторной регистрации обработчиков на клиенте
        private static bool _clientHandlersRegistered = false;
        private RepairUiStateMessage _clientUiState = new RepairUiStateMessage { InRepairZone = false, ZoneName = "Repair Zone", RepairEnabled = false, StatusText = "Waiting for zone state", LastRepairText = "No repairs performed yet." };

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
                var cockpits = new List<IMyCockpit>();
                MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(cockpits);
                if (cockpits.Count > 0)
                {
                    var data = cockpits[0].CustomData;
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
                var cockpits = new List<IMyCockpit>();
                MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(cockpits);
                if (cockpits.Count > 0)
                {
                    var cockpit = cockpits[0];
                    var data = new StringBuilder();
                    data.AppendLine($"AllowRepairsInSafeZones:{value}");
                    cockpit.CustomData = data.ToString();
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
					HideHud();
					return;
				}

				var player = session.Player;
				var controlledEntity = player?.Controller?.ControlledEntity?.Entity;
				var shipController = controlledEntity as IMyShipController;

				if (shipController == null || shipController.CubeGrid == null)
				{
					HideHud();
					return;
				}

				ShowHud();
			}
			catch (Exception ex)
			{
				LogError($"UpdateClientHudVisibility error: {ex}");
			}
		}
        // --- Основной цикл обновления ---
        public override void UpdateAfterSimulation()
		{
			if (!MyAPIGateway.Multiplayer.IsServer)
			{
				UpdateClientHudVisibility();
				return;
			}

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
                IMyCockpit cockpit = player.Controller?.ControlledEntity?.Entity as IMyCockpit;
                if (cockpit != null)
                {
                    IMyCubeGrid grid = cockpit.CubeGrid;
                    if (grid != null)
                    {
                        if (!GetGridRepairSetting(grid))
                        {
                            LogZone($"Grid {grid.DisplayName} has repairs disabled, skipping");
                            if (gridsInSafeZone.Contains(grid))
                            {
                                gridsInSafeZone.Remove(grid);
                                LogZone($"Grid {grid.DisplayName} removed from repair list (disabled)");
                            }
                            continue;
                        }

                        bool inAnySafeZone = false;
                        foreach (MySafeZone zone in safeZones)
                        {
                            if (IsGridInSafeZone(grid, zone))
                            {
                                inAnySafeZone = true;
                                HandleGridInSafeZone(grid, player.IdentityId);
                                SendRepairUiStateToPlayer(player, true, grid, zone, "Entered repair zone");
                            }
                        }
                        if (!inAnySafeZone && gridsInSafeZone.Contains(grid))
                        {
                            gridsInSafeZone.Remove(grid);
                            LogZone($"Grid {grid.DisplayName} left safe zone");
                        }

                        if (!inAnySafeZone)
                            SendRepairUiStateToPlayer(player, false, grid, null, "Outside repair zone");
                    }
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
        private void HandleGridInSafeZone(IMyCubeGrid grid, long pilotId)
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
                    HandleGridInSafeZone(projector.ProjectedGrid, pilotId);
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
                    if (totalCost > 0)
                    {
                        blockRepairInfo[block] = new BlockRepairInfo { TotalCost = (long)totalCost, InitialCost = (long)totalCost, PilotIdentityId = pilotId };
                    }
                    else
                    {
                        LogZone($"Block {Utils.BlockName(block)} totalCost <= 0, not adding to repair info");
                    }
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
            float repairAmount = localWeldingSpeed * (float)deltaTime;
            float remaining = block.MaxIntegrity - block.BuildIntegrity;
            LogRepair($"repairAmount={repairAmount}, remaining={remaining}, BuildIntegrity={block.BuildIntegrity}, MaxIntegrity={block.MaxIntegrity}");
            if (repairAmount > remaining)
                repairAmount = remaining;

            if (repairAmount <= 0)
            {
                LogRepair($"repairAmount <= 0, skipping");
                return;
            }

            float integrityToRestore = block.MaxIntegrity - block.BuildIntegrity + repairAmount;
            float stepCost = info.TotalCost * (repairAmount / integrityToRestore);
            if (stepCost < 1) stepCost = 1;
            LogRepair($"stepCost={stepCost}, integrityToRestore={integrityToRestore}");

            if (balance < stepCost)
            {
                LogCost($"Player cannot afford repair step (need {stepCost}, have {balance})");
                blocksRepairQueue.Dequeue();
                blocksInQueue.Remove(block);
                blockRepairInfo.Remove(block);
                return;
            }

            player.RequestChangeBalance(-(long)stepCost);
            info.TotalCost -= (long)stepCost;
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
                LogRepair($"Calling IncreaseMountLevel with repairAmount={repairAmount}");
                block.IncreaseMountLevel(repairAmount, ownerId, tempInv, 1f);
                LogRepair($"IncreaseMountLevel completed");
            }
            catch (Exception ex)
            {
                LogError($"Repair error: {ex}");
            }

            SendRepairNotificationToClients(block, false, 0, player.IdentityId);

            if (block.MaxIntegrity - block.BuildIntegrity <= 0.1f)
            {
                LogRepair($"Block fully repaired (close enough), removing from queue");
                // Докручиваем до 100%, чтобы избежать 99%
                if (block.MaxIntegrity - block.BuildIntegrity > 0.01f)
                {
                    float remainingFinal = block.MaxIntegrity - block.BuildIntegrity;
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
                        block.IncreaseMountLevel(remainingFinal, ownerId, tempInv, 1f);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Final repair error: {ex}");
                    }
                }
                blocksRepairQueue.Dequeue();
                blocksInQueue.Remove(block);
                blockRepairInfo.Remove(block);
                if (info.TotalCost > 1)
                {
                    player.RequestChangeBalance(-info.TotalCost);
                    info.TotalCost = 0;
                }
                SendRepairNotificationToClients(block, true, info.InitialCost, player.IdentityId);
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

        private void SendRepairUiStateToPlayer(IMyPlayer player, bool inRepairZone, IMyCubeGrid grid, MySafeZone zone, string statusText = null, string lastRepairText = null)
        {
            if (player == null || !MyAPIGateway.Multiplayer.IsServer)
                return;

            SafeZoneConfig cfg = null;
            if (zone != null)
                zoneConfigs.TryGetValue(zone.EntityId, out cfg);

            string zoneName = cfg?.DisplayName ?? cfg?.ZoneName ?? GetSafeZoneDefaultName(zone);

            var msg = new RepairUiStateMessage
            {
                PlayerId = player.IdentityId,
                InRepairZone = inRepairZone,
                ZoneName = zoneName,
                RepairEnabled = grid != null && GetGridRepairSetting(grid),
                StatusText = string.IsNullOrWhiteSpace(statusText) ? (inRepairZone ? "Entered repair zone" : "Outside repair zone") : statusText,
                LastRepairText = string.IsNullOrWhiteSpace(lastRepairText) ? _clientUiState.LastRepairText : lastRepairText,
                LastEventUtcTicks = DateTime.UtcNow.Ticks
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
            LogGeneral("Repair queue cleared");
        }

        // --- Обработка чат-команд ---
        private void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;
            if (!messageText.StartsWith("/")) return;

            sendToOthers = false;

            MyLog.Default.WriteLine($"[SafeZoneRepair] Command received: '{messageText}'");

            string[] parts = messageText.Trim().Split(' ');
            string command = parts[0].ToLower();

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
                var controlled = player.Controller?.ControlledEntity?.Entity;
                var cockpit = controlled as IMyCockpit;
                if (cockpit != null && cockpit.CubeGrid == grid)
                    return player;
            }
            return null;
        }

        private void SendTerminalStatusToPlayer(IMyPlayer player, string text, string color, int time)
        {
            if (player == null) return;

            text = text.Trim();

            // Защита от повторной отправки одинакового сообщения одному игроку (не чаще 1 раза в 5 секунд)
            if (_lastPlayerStatusText.ContainsKey(player.IdentityId))
            {
                if (_lastPlayerStatusText[player.IdentityId] == text && (DateTime.Now - _lastPlayerStatusTime[player.IdentityId]).TotalSeconds < 5)
                    return;
            }
            _lastPlayerStatusText[player.IdentityId] = text;
            _lastPlayerStatusTime[player.IdentityId] = DateTime.Now;

            LogGeneral($"Sending terminal status to player {player.IdentityId}: '{text}'");

            // Если это локальный игрок (в одиночной игре), показываем уведомление напрямую
            if (MyAPIGateway.Session.LocalHumanPlayer != null && player.SteamUserId == MyAPIGateway.Session.LocalHumanPlayer.SteamUserId)
            {
                MyAPIGateway.Utilities.ShowNotification(text, time, color);
                return;
            }
            var msg = new TerminalStatusMessage { Text = text, Color = color, Time = time, PlayerId = player.IdentityId };
            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(msg);
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