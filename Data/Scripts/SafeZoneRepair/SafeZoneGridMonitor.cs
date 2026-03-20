using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Common.ObjectBuilders;
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

        private TimeSpan _lastZoneConfigCleanupCheck = TimeSpan.Zero;
        private static readonly TimeSpan zoneConfigCleanupInterval = TimeSpan.FromSeconds(60);
        private const int MissingZoneConfigRemovalThreshold = 3;

        private TimeSpan _lastZoneConfigPersistCheck = TimeSpan.Zero;
        private static readonly TimeSpan zoneConfigPersistInterval = TimeSpan.FromSeconds(15);

        private bool initialized = false;
        private float weldingSpeed = 3f;
        private float costModifier = 1f;
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
        private const float DeformationRepairStepMultiplier = 3f;

        public const ushort TerminalActionSyncId = 2915;
        public const ushort TerminalStatusSyncId = 2916;
        public const ushort RepairUiStateSyncId = 2917;
        public const ushort AdminZoneConfigRequestSyncId = 2918;
        public const ushort AdminZoneConfigUpdateSyncId = 2919;
        public const ushort AdminZoneConfigStateSyncId = 2920;

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
        private Dictionary<long, int> _missingZoneConfigChecks = new Dictionary<long, int>();

        // Флаг для предотвращения повторной регистрации обработчиков на клиенте
        private static bool _clientHandlersRegistered = false;
        private RepairUiStateMessage _clientUiState = new RepairUiStateMessage { InRepairZone = false, ZoneName = "Repair Zone", RepairEnabled = false, StatusText = "Waiting for zone state", LastRepairText = "No repairs performed yet.", EstimatedRepairCost = 0, CurrentScanText = "Current scan: -" };
        private Dictionary<long, long> _lastControlledGridByPlayer = new Dictionary<long, long>();
        private Dictionary<long, string> _currentScanBlockByPlayer = new Dictionary<long, string>();
        private Dictionary<long, DateTime> _currentScanUntilByPlayer = new Dictionary<long, DateTime>();
        private bool _manualHudRequested = false;
        private bool _zoneDetailsHudRequested = false;
        private bool _cockpitHudSuppressed = false;
        private bool _cockpitInteractiveRequested = false;
        private static readonly MyKeys HudToggleKey = MyKeys.J;
        private static readonly MyKeys RepairToggleKey = MyKeys.R;
        private static readonly MyKeys CockpitHudSuppressKey = MyKeys.N;
        private static readonly MyKeys AdminMenuKey = MyKeys.O;
        private static readonly MyKeys ForceRescanKey = MyKeys.L;
        private static readonly MyKeys ZoneDetailsKey = MyKeys.M;

        private static bool _rhfBindsRegistered = false;
        private static bool _rhfTerminalPagesRegistered = false;
        private static IBindGroup _rhfBindGroup;
        private static IBind _hudToggleBind;
        private static IBind _repairToggleBind;
        private static IBind _zoneDetailsBind;
        private static TerminalPageCategory _rhfTerminalCategory;
        private static RebindPage _rhfKeybindPage;
        private static TextPage _rhfOverviewPage;

        private bool _adminPanelRequested = false;
        private bool _adminComponentsViewRequested = false;
        private int _adminComponentsScrollOffset = 0;
        private readonly HashSet<string> _adminForbiddenComponentsLocal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private AdminZoneConfigStateMessage _adminZoneState = new AdminZoneConfigStateMessage();
        private const int AdminComponentsPageSize = 8;
        private const int MouseWheelStepSize = 120;
        private const int AdminZoneListPageSize = 5;
        private const string ZoneCreationTypeAdmin = "Admin";
        private const string ZoneCreationTypeSafeZoneBlock = "SafeZoneBlock";

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
                    MyAPIGateway.Multiplayer.RegisterMessageHandler(AdminZoneConfigRequestSyncId, HandleAdminZoneConfigRequest);
                    MyAPIGateway.Multiplayer.RegisterMessageHandler(AdminZoneConfigUpdateSyncId, HandleAdminZoneConfigUpdate);
                    MyAPIGateway.Entities.OnEntityAdd += OnEntityAdded;
                    MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
                    LogGeneral("Loaded on server");
                    LoadZoneGenerationConfig();
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
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(AdminZoneConfigStateSyncId, HandleAdminZoneConfigState);

                    MyAPIGateway.Multiplayer.RegisterMessageHandler(SyncId, HandleRepairNotification);
                    MyAPIGateway.Multiplayer.RegisterMessageHandler(TerminalStatusSyncId, HandleTerminalStatus);
                    MyAPIGateway.Multiplayer.RegisterMessageHandler(RepairUiStateSyncId, HandleRepairUiState);
                    MyAPIGateway.Multiplayer.RegisterMessageHandler(AdminZoneConfigStateSyncId, HandleAdminZoneConfigState);
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
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(AdminZoneConfigRequestSyncId, HandleAdminZoneConfigRequest);
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(AdminZoneConfigUpdateSyncId, HandleAdminZoneConfigUpdate);
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
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(AdminZoneConfigStateSyncId, HandleAdminZoneConfigState);
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
                _zoneGenerationConfig = ZoneGenerationConfig.CreateDefault();
                _completedBlockKeys.Clear();
                _lastPlayerStatusText.Clear();
                _lastPlayerStatusTime.Clear();
                _estimatedRepairCostCache.Clear();
                _missingZoneConfigChecks.Clear();
                _lastControlledGridByPlayer.Clear();
                _currentScanBlockByPlayer.Clear();
                _currentScanUntilByPlayer.Clear();
                _manualHudRequested = false;
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
                        var storage = DeserializeZoneConfigCollection(xml);
                        var list = storage != null ? storage.Zones : null;
                        if (list != null)
                        {
                            bool configDirty = false;
                            foreach (var cfg in list)
                            {
                                NormalizeZoneConfig(cfg);
                                if (RefreshZoneCreationTypeFromLiveZone(cfg))
                                    configDirty = true;

                                if (EnsureZoneProfileMetadata(cfg))
                                    configDirty = true;

                                if (cfg.ZoneEntityId != 0)
                                    zoneConfigs[cfg.ZoneEntityId] = cfg;
                            }

                            if (configDirty)
                                SaveConfig(new List<SafeZoneConfig>(zoneConfigs.Values));
                        }
                    }
                }
                else
                {
                    PersistenceDebugLog($"Config file '{ConfigFileName}' not found, creating defaults");
                    SaveDefaultConfig();
                }
                PersistenceDebugLog($"LoadConfig completed with runtime zoneConfigs={zoneConfigs.Count}");
                EnsureZoneConfigFilePersisted("LoadConfig complete");
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

        private static List<string> CreateDefaultForbiddenComponents()
        {
            return new List<string>
            {
                "PrototechCapacitor",
                "PrototechCircuitry",
                "PrototechCoolingUnit",
                "PrototechFrame",
                "PrototechMachinery",
                "PrototechPanel",
                "PrototechPropulsionUnit"
            };
        }

        private static List<string> NormalizeForbiddenComponentList(IEnumerable<string> components, bool includeBaseDefaults)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (components != null)
            {
                foreach (var component in components)
                {
                    if (string.IsNullOrWhiteSpace(component))
                        continue;

                    set.Add(component.Trim());
                }
            }

            if (includeBaseDefaults)
            {
                foreach (var component in CreateDefaultForbiddenComponents())
                    set.Add(component);
            }

            var list = new List<string>(set);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private void SyncAdminForbiddenComponentsFromState()
        {
            _adminForbiddenComponentsLocal.Clear();
            if (_adminZoneState == null || _adminZoneState.ForbiddenComponents == null)
                return;

            foreach (var component in _adminZoneState.ForbiddenComponents)
            {
                if (!string.IsNullOrWhiteSpace(component))
                    _adminForbiddenComponentsLocal.Add(component.Trim());
            }
        }

        private List<string> GetAdminForbiddenComponentsSnapshot()
        {
            return NormalizeForbiddenComponentList(_adminForbiddenComponentsLocal, false);
        }


        private static string NormalizeZoneCreationTypeValue(string value)
        {
            return string.Equals(value, ZoneCreationTypeAdmin, StringComparison.OrdinalIgnoreCase)
                ? ZoneCreationTypeAdmin
                : ZoneCreationTypeSafeZoneBlock;
        }

        private static string GetZoneCreationTypeLabel(string value)
        {
            return NormalizeZoneCreationTypeValue(value) == ZoneCreationTypeAdmin ? "Admin" : "Block";
        }

        private static string DetectZoneCreationType(MySafeZone zone)
        {
            if (zone == null)
                return ZoneCreationTypeSafeZoneBlock;

            try
            {
                var objectBuilder = zone.GetObjectBuilder() as MyObjectBuilder_SafeZone;
                if (objectBuilder != null && objectBuilder.SafeZoneBlockId != 0L)
                    return ZoneCreationTypeSafeZoneBlock;
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[SafeZoneRepair] DetectZoneCreationType failed for zone " + zone.EntityId + ": " + ex);
            }

            return ZoneCreationTypeAdmin;
        }

        private bool RefreshZoneCreationTypeFromLiveZone(SafeZoneConfig cfg)
        {
            if (cfg == null || cfg.ZoneEntityId == 0L)
                return false;

            MySafeZone zone;
            if (!TryGetSafeZoneByEntityId(cfg.ZoneEntityId, out zone) || zone == null)
                return false;

            string detected = NormalizeZoneCreationTypeValue(DetectZoneCreationType(zone));
            if (string.Equals(cfg.ZoneCreationType, detected, StringComparison.OrdinalIgnoreCase))
                return false;

            cfg.ZoneCreationType = detected;
            return true;
        }

        private static void NormalizeZoneConfig(SafeZoneConfig cfg)
        {
            if (cfg == null)
                return;

            if (string.IsNullOrWhiteSpace(cfg.ZoneName))
                cfg.ZoneName = "Repair Zone";

            if (string.IsNullOrWhiteSpace(cfg.DisplayName))
                cfg.DisplayName = cfg.ZoneName;

            cfg.ForbiddenComponents = NormalizeForbiddenComponentList(cfg.ForbiddenComponents, false);

            if (cfg.ComponentPriceModifiers == null)
                cfg.ComponentPriceModifiers = new List<ComponentPriceModifierEntry>();
            else
                NormalizeComponentPriceModifiers(cfg.ComponentPriceModifiers);

            cfg.ZoneCreationType = NormalizeZoneCreationTypeValue(cfg.ZoneCreationType);

            if (cfg.ProjectionWeldingSpeed < 0.001f)
            {
                if (cfg.ProjectionBuildDelay >= 0.001f)
                    cfg.ProjectionWeldingSpeed = (float)Math.Round(1f / Math.Max(0.001f, cfg.ProjectionBuildDelay), 2);
                else
                    cfg.ProjectionWeldingSpeed = 1f;
            }

            if (cfg.ProjectionBuildDelay < 0.001f)
                cfg.ProjectionBuildDelay = (float)Math.Round(1f / Math.Max(0.001f, cfg.ProjectionWeldingSpeed), 2);
        }

        private static void NormalizeComponentPriceModifiers(List<ComponentPriceModifierEntry> modifiers)
        {
            if (modifiers == null)
                return;

            var normalized = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in modifiers)
            {
                if (entry == null)
                    continue;

                string normalizedKey = NormalizeComponentModifierKey(entry.ComponentSubtypeId);
                if (string.IsNullOrWhiteSpace(normalizedKey))
                    continue;

                float value = entry.Multiplier;
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    value = 0f;

                normalized[normalizedKey] = (float)Math.Round(value, 3);
            }

            modifiers.Clear();
            foreach (var pair in normalized)
            {
                modifiers.Add(new ComponentPriceModifierEntry
                {
                    ComponentSubtypeId = pair.Key,
                    Multiplier = pair.Value
                });
            }
        }

        private static string NormalizeComponentModifierKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            key = key.Trim();
            const string prefix = "MyObjectBuilder_Component/";
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                key = key.Substring(prefix.Length);

            return string.IsNullOrWhiteSpace(key) ? null : key;
        }

        private static List<ComponentPriceModifierEntry> CloneComponentPriceModifiers(List<ComponentPriceModifierEntry> source)
        {
            var clone = new List<ComponentPriceModifierEntry>();
            if (source == null)
                return clone;

            foreach (var entry in source)
            {
                if (entry == null)
                    continue;

                string key = NormalizeComponentModifierKey(entry.ComponentSubtypeId);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                float value = entry.Multiplier;
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    value = 0f;

                clone.Add(new ComponentPriceModifierEntry
                {
                    ComponentSubtypeId = key,
                    Multiplier = (float)Math.Round(value, 3)
                });
            }

            NormalizeComponentPriceModifiers(clone);
            return clone;
        }

        private static bool TryGetComponentPriceModifier(SafeZoneConfig zoneCfg, MyDefinitionId id, out float modifier)
        {
            modifier = 1f;
            if (zoneCfg == null || zoneCfg.ComponentPriceModifiers == null || zoneCfg.ComponentPriceModifiers.Count == 0)
                return false;

            string subtype = id.SubtypeId.String;
            if (string.IsNullOrWhiteSpace(subtype))
                return false;

            string fullKey = "MyObjectBuilder_Component/" + subtype;
            foreach (var entry in zoneCfg.ComponentPriceModifiers)
            {
                if (entry == null)
                    continue;

                string key = NormalizeComponentModifierKey(entry.ComponentSubtypeId);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (string.Equals(key, subtype, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, fullKey, StringComparison.OrdinalIgnoreCase))
                {
                    modifier = Math.Max(0f, entry.Multiplier);
                    return true;
                }
            }

            return false;
        }

        private void SaveDefaultConfig()
        {
            var list = new List<SafeZoneConfig>();
            if (safeZones.Count == 0)
            {
                list.Add(CreateExampleZoneConfig());
            }
            else
            {
                foreach (var zone in safeZones)
                    list.Add(CreateDefaultZoneConfig(zone));
            }

            zoneConfigs.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var cfg = list[i];
                if (cfg != null && cfg.ZoneEntityId != 0)
                    zoneConfigs[cfg.ZoneEntityId] = cfg;
            }

            PersistenceDebugLog($"SaveDefaultConfig prepared {list.Count} configs, runtime zoneConfigs={zoneConfigs.Count}");
            SaveConfig(list);
        }


        private SafeZoneConfigCollection DeserializeZoneConfigCollection(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                return new SafeZoneConfigCollection();

            try
            {
                var storage = MyAPIGateway.Utilities.SerializeFromXML<SafeZoneConfigCollection>(xml);
                if (storage != null)
                    return storage;
            }
            catch (Exception ex)
            {
                PersistenceDebugLog($"DeserializeZoneConfigCollection wrapper format failed: {ex.Message}");
            }

            try
            {
                var legacyList = MyAPIGateway.Utilities.SerializeFromXML<List<SafeZoneConfig>>(xml);
                return new SafeZoneConfigCollection
                {
                    Zones = legacyList ?? new List<SafeZoneConfig>()
                };
            }
            catch (Exception ex)
            {
                PersistenceDebugLog($"DeserializeZoneConfigCollection legacy list format failed: {ex.Message}");
            }

            return new SafeZoneConfigCollection();
        }

        private void SaveConfig(List<SafeZoneConfig> list)
        {
            try
            {
                var saveList = list ?? new List<SafeZoneConfig>();
                for (int i = 0; i < saveList.Count; i++)
                {
                    var cfg = saveList[i];
                    if (cfg == null)
                        continue;

                    NormalizeZoneConfig(cfg);
                }

                PersistenceDebugLog($"SaveConfig start: file={ConfigFileName}, count={saveList.Count}");

                var storage = new SafeZoneConfigCollection
                {
                    Zones = saveList
                };

                string xml = MyAPIGateway.Utilities.SerializeToXML(storage);
                if (string.IsNullOrWhiteSpace(xml))
                    throw new Exception("SerializeToXML returned empty XML for zone config storage");

                PersistenceDebugLog($"SaveConfig xml length: {xml.Length}");

                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(ConfigFileName, typeof(SafeZoneGridMonitor)))
                {
                    writer.Write(xml);
                    writer.Flush();
                }

                if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(ConfigFileName, typeof(SafeZoneGridMonitor)))
                {
                    PersistenceDebugLog($"SaveConfig wrote XML but file '{ConfigFileName}' is still missing after write; retrying once");
                    using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(ConfigFileName, typeof(SafeZoneGridMonitor)))
                    {
                        writer.Write(xml);
                        writer.Flush();
                    }
                }

                bool existsAfterSave = MyAPIGateway.Utilities.FileExistsInWorldStorage(ConfigFileName, typeof(SafeZoneGridMonitor));
                string readBackInfo = "readback skipped";
                if (existsAfterSave)
                {
                    try
                    {
                        using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(ConfigFileName, typeof(SafeZoneGridMonitor)))
                        {
                            var readBackXml = reader.ReadToEnd();
                            readBackInfo = string.IsNullOrWhiteSpace(readBackXml) ? "empty" : ("len=" + readBackXml.Length);
                        }
                    }
                    catch (Exception readEx)
                    {
                        readBackInfo = "readback error: " + readEx.Message;
                    }
                }

                PersistenceDebugLog($"SaveConfig completed: file={ConfigFileName}, exists={existsAfterSave}, count={saveList.Count}, {readBackInfo}");
            }
            catch (Exception ex)
            {
                PersistenceDebugLog($"SaveConfig error: {ex}");
            }
        }

        private void EnsureZoneConfigFilePersisted(string reason)
        {
            try
            {
                bool exists = MyAPIGateway.Utilities.FileExistsInWorldStorage(ConfigFileName, typeof(SafeZoneGridMonitor));
                if (exists)
                    return;

                PersistenceDebugLog($"Zone config file '{ConfigFileName}' is missing during '{reason}', forcing save for {zoneConfigs.Count} zones");
                SaveConfig(new List<SafeZoneConfig>(zoneConfigs.Values));
            }
            catch (Exception ex)
            {
                PersistenceDebugLog($"EnsureZoneConfigFilePersisted error ({reason}): {ex}");
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
        private void PersistenceDebugLog(string msg) { MyLog.Default.WriteLine($"[SafeZoneRepair:PERSIST] {msg}"); }

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
                    RefreshUiCursorState();
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
                    RefreshUiCursorState();

                    if (_clientUiState != null)
                        UpdateRichHudState(_clientUiState);
                    else
                        ShowHud();

                    return;
                }

                if (_clientUiState == null || !_clientUiState.InRepairZone)
                {
                    _manualHudRequested = false;
                    RefreshUiCursorState();
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

        private bool IsForceRescanHotkeyPressed()
        {
            var input = MyAPIGateway.Input;
            return input != null && input.IsAnyCtrlKeyPressed() && input.IsNewKeyPressed(ForceRescanKey);
        }

        private void ForceRescanForLocalContext(IMyCubeGrid sourceGrid = null)
        {
            try
            {
                var shipController = GetLocalControlledShipController();
                IMyCubeGrid grid = shipController?.CubeGrid;

                if (grid == null)
                    return;

                if (sourceGrid != null && sourceGrid.EntityId != grid.EntityId)
                    return;

                ForceRescanGrid(grid);
            }
            catch (Exception ex)
            {
                LogError($"ForceRescanForLocalContext error: {ex}");
            }
        }

        private void ForceRescanGrid(IMyCubeGrid grid)
        {
            try
            {
                if (grid == null)
                    return;

                if (!MyAPIGateway.Multiplayer.IsServer)
                {
                    var msg = new TerminalActionMessage { GridEntityId = grid.EntityId, Action = "Rescan" };
                    byte[] data = MyAPIGateway.Utilities.SerializeToBinary(msg);
                    MyAPIGateway.Multiplayer.SendMessageToServer(TerminalActionSyncId, data);
                    return;
                }

                var player = GetPlayerControllingGrid(grid);
                long playerId = player?.IdentityId ?? 0;

                RemoveGridFromQueue(grid);

                var keysToRemove = new List<string>();
                string prefix = grid.EntityId.ToString() + ":";
                foreach (var key in _completedBlockKeys)
                {
                    if (!string.IsNullOrWhiteSpace(key) && key.StartsWith(prefix))
                        keysToRemove.Add(key);
                }

                foreach (var key in keysToRemove)
                    _completedBlockKeys.Remove(key);

                InvalidateEstimatedRepairCostCache(playerId, grid.EntityId);

                if (playerId != 0)
                {
                    _currentScanBlockByPlayer[playerId] = "Current scan: rescan queued";
                    _currentScanUntilByPlayer[playerId] = DateTime.UtcNow.AddSeconds(8);
                }

                if (GridIsInSafeZone(grid))
                {
                    HandleGridInSafeZone(grid, playerId, grid.EntityId);
                    SendTerminalStatusToPlayer(player, "Forced grid rescan queued", "Blue", 2500);
                    SendRepairUiStateToPlayer(player, true, grid, GetSafeZoneForGrid(grid), "Repair ready");
                }
                else
                {
                    SendTerminalStatusToPlayer(player, "Rescan unavailable for current grid", "Red", 2500);
                }
            }
            catch (Exception ex)
            {
                LogError($"ForceRescanGrid error: {ex}");
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

        private void ToggleZoneDetailsForLocalContext(IMyCubeGrid sourceGrid = null)
        {
            try
            {
                if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
                    return;

                IMyCubeGrid grid = sourceGrid ?? GetLocalActionGrid();
                MySafeZone zone = grid != null ? GetSafeZoneForGrid(grid) : null;

                if (zone == null)
                {
                    var player = GetLocalPlayer();
                    var character = player?.Character;
                    if (character != null)
                        zone = GetSafeZoneForPosition(character.GetPosition());
                }

                if (zone == null)
                {
                    _zoneDetailsHudRequested = false;
                    MyAPIGateway.Utilities.ShowMessage("SZR", "No repair zone details available.");
                    if (_clientUiState != null)
                        UpdateRichHudState(_clientUiState);
                    return;
                }

                SafeZoneConfig cfg;
                if (!zoneConfigs.TryGetValue(zone.EntityId, out cfg) || cfg == null)
                {
                    _zoneDetailsHudRequested = false;
                    MyAPIGateway.Utilities.ShowMessage("SZR", "Zone details are not available yet.");
                    if (_clientUiState != null)
                        UpdateRichHudState(_clientUiState);
                    return;
                }

                _zoneDetailsHudRequested = !_zoneDetailsHudRequested;

                if (_clientUiState != null)
                    UpdateRichHudState(_clientUiState);
            }
            catch (Exception ex)
            {
                LogError($"ToggleZoneDetailsForLocalContext error: {ex}");
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
                    RegisterOrGetBind("Zone Details", new ControlHandle[] { RichHudControls.Control, RichHudControls.M }, out _zoneDetailsBind);

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
                    _rhfOverviewPage.Text = new RichText("Ctrl+J toggles the repair HUD.\nCtrl+R toggles repair mode only while controlling a ship controller.\nCtrl+M toggles RHF zone details for the current service area.\nCtrl+N hides or restores the cockpit HUD.\nCockpit toolbar actions remain available for HUD and repair mode.");

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
                new BindDefinition("Toggle Repair", new[] { "Control", "R" }),
                new BindDefinition("Zone Details", new[] { "Control", "M" })
            };
        }

        private bool IsAdminMenuHotkeyPressed()
        {
            var input = MyAPIGateway.Input;
            return input != null && input.IsAnyCtrlKeyPressed() && input.IsNewKeyPressed(AdminMenuKey);
        }

        private bool IsPlayerAdmin(IMyPlayer player)
        {
            if (player == null)
                return false;

            var level = player.PromoteLevel;
            return level == MyPromoteLevel.Admin || level == MyPromoteLevel.Owner || level == MyPromoteLevel.SpaceMaster;
        }

        private bool TryGetPlayerPosition(IMyPlayer player, out Vector3D position)
        {
            position = Vector3D.Zero;
            if (player == null)
                return false;

            var controlled = player.Controller?.ControlledEntity?.Entity as IMyEntity;
            if (controlled != null)
            {
                position = controlled.GetPosition();
                return true;
            }

            if (player.Character != null)
            {
                position = player.Character.GetPosition();
                return true;
            }

            return false;
        }

        private SafeZoneConfig EnsureZoneConfig(MySafeZone zone)
        {
            if (zone == null)
                return null;

            SafeZoneConfig cfg;
            if (!zoneConfigs.TryGetValue(zone.EntityId, out cfg) || cfg == null)
            {
                cfg = CreateDefaultZoneConfig(zone);
                zoneConfigs[zone.EntityId] = cfg;
                PersistenceDebugLog($"EnsureZoneConfig created runtime config for zone {zone.EntityId} ('{zone.DisplayName}')");
                SaveConfig(new List<SafeZoneConfig>(zoneConfigs.Values));
                EnsureZoneConfigFilePersisted("EnsureZoneConfig created new zone");
            }

            RefreshZoneCreationTypeFromLiveZone(cfg);
            EnsureZoneConfigFilePersisted("EnsureZoneConfig completed");
            return cfg;
        }

        private bool TryGetSafeZoneByEntityId(long zoneEntityId, out MySafeZone zone)
        {
            zone = null;
            if (zoneEntityId == 0)
                return false;

            for (int i = safeZones.Count - 1; i >= 0; i--)
            {
                var candidate = safeZones[i];
                if (!IsSafeZoneEntityAlive(candidate))
                {
                    safeZones.RemoveAt(i);
                    continue;
                }

                if (candidate.EntityId == zoneEntityId)
                {
                    zone = candidate;
                    return true;
                }
            }

            IMyEntity entity;
            if (MyAPIGateway.Entities != null && MyAPIGateway.Entities.TryGetEntityById(zoneEntityId, out entity))
            {
                zone = entity as MySafeZone;
                if (IsSafeZoneEntityAlive(zone))
                    return true;
            }

            return false;
        }

        private string GetAdminZoneDisplayName(MySafeZone zone)
        {
            var cfg = EnsureZoneConfig(zone);
            if (cfg != null)
            {
                if (!string.IsNullOrWhiteSpace(cfg.DisplayName))
                    return cfg.DisplayName;
                if (!string.IsNullOrWhiteSpace(cfg.ZoneName))
                    return cfg.ZoneName;
            }

            return GetSafeZoneDefaultName(zone);
        }

        private List<MySafeZone> GetSortedSafeZones()
        {
            var list = new List<MySafeZone>();
            var seenZoneIds = new HashSet<long>();
            for (int i = 0; i < safeZones.Count; i++)
            {
                var zone = safeZones[i];
                if (zone == null || zone.MarkedForClose)
                    continue;

                if (!seenZoneIds.Add(zone.EntityId))
                    continue;

                list.Add(zone);
            }

            list.Sort((a, b) =>
            {
                string nameA = GetAdminZoneDisplayName(a).ToLowerInvariant();
                string nameB = GetAdminZoneDisplayName(b).ToLowerInvariant();
                int byName = string.Compare(nameA, nameB, StringComparison.Ordinal);
                if (byName != 0)
                    return byName;
                return a.EntityId.CompareTo(b.EntityId);
            });

            return list;
        }

        private string FormatAdminZoneListName(string zoneName, long zoneEntityId, Dictionary<string, int> duplicateNameCounts)
        {
            string safeName = string.IsNullOrWhiteSpace(zoneName) ? "Unnamed zone" : zoneName.Trim();
            int duplicateCount;
            if (duplicateNameCounts != null && duplicateNameCounts.TryGetValue(safeName, out duplicateCount) && duplicateCount > 1)
            {
                string idText = Math.Abs(zoneEntityId).ToString();
                if (idText.Length > 4)
                    idText = idText.Substring(idText.Length - 4);
                safeName += " [" + idText + "]";
            }

            return safeName;
        }

        private List<AdminZoneListEntryMessage> BuildAdminZoneList(long selectedZoneEntityId, long playerZoneEntityId)
        {
            var entries = new List<AdminZoneListEntryMessage>();
            var zones = GetSortedSafeZones();
            var duplicateNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < zones.Count; i++)
            {
                string baseName = GetAdminZoneDisplayName(zones[i]);
                string key = string.IsNullOrWhiteSpace(baseName) ? "Unnamed zone" : baseName.Trim();
                int count;
                duplicateNameCounts.TryGetValue(key, out count);
                duplicateNameCounts[key] = count + 1;
            }

            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                entries.Add(new AdminZoneListEntryMessage
                {
                    ZoneEntityId = zone.EntityId,
                    ZoneName = FormatAdminZoneListName(GetAdminZoneDisplayName(zone), zone.EntityId, duplicateNameCounts),
                    IsPlayerInside = playerZoneEntityId != 0 && zone.EntityId == playerZoneEntityId
                });
            }

            return entries;
        }

        private bool TryResolveAdminZone(IMyPlayer player, long requestedZoneEntityId, out MySafeZone zone, out SafeZoneConfig cfg, out long playerZoneEntityId)
        {
            zone = null;
            cfg = null;
            playerZoneEntityId = 0L;

            if (!IsPlayerAdmin(player))
                return false;

            Vector3D pos;
            if (TryGetPlayerPosition(player, out pos))
            {
                var currentZone = GetSafeZoneForPosition(pos);
                if (currentZone != null)
                    playerZoneEntityId = currentZone.EntityId;
            }

            if (requestedZoneEntityId != 0 && TryGetSafeZoneByEntityId(requestedZoneEntityId, out zone))
            {
                cfg = EnsureZoneConfig(zone);
                return zone != null && cfg != null;
            }

            if (playerZoneEntityId != 0 && TryGetSafeZoneByEntityId(playerZoneEntityId, out zone))
            {
                cfg = EnsureZoneConfig(zone);
                return zone != null && cfg != null;
            }

            var zones = GetSortedSafeZones();
            if (zones.Count == 0)
                return false;

            zone = zones[0];
            cfg = EnsureZoneConfig(zone);
            return zone != null && cfg != null;
        }

        private bool TryGetAdminContext(IMyPlayer player, out MySafeZone zone, out SafeZoneConfig cfg)
        {
            long playerZoneEntityId;
            return TryResolveAdminZone(player, 0L, out zone, out cfg, out playerZoneEntityId);
        }

        private void ToggleAdminPanelForLocalContext()
        {
            try
            {
                if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
                    return;

                var player = GetLocalPlayer();
                if (!IsPlayerAdmin(player))
                    return;

                if (_adminPanelRequested)
                {
                    _adminPanelRequested = false;
                    RefreshUiCursorState();
                    UpdateAdminPanelState();
                    return;
                }

                _adminPanelRequested = true;
                MarkAdminPanelDirty();
                RequestAdminZoneConfig(false, _adminZoneState != null ? _adminZoneState.SelectedZoneEntityId : 0L);
                RefreshUiCursorState();
                UpdateAdminPanelState();
            }
            catch (Exception ex)
            {
                LogError($"ToggleAdminPanelForLocalContext error: {ex}");
            }
        }

        private void RefreshUiCursorState()
        {
            SetInteractiveCursorEnabled(_cockpitInteractiveRequested || _adminPanelRequested || _adminPriceModsPanelRequested);
        }

        private void RequestAdminZoneConfig(bool reload, long targetZoneEntityId = 0L)
        {
            try
            {
                var player = GetLocalPlayer();
                if (player == null)
                    return;

                var msg = new AdminZoneConfigRequestMessage
                {
                    PlayerId = player.IdentityId,
                    ReloadFromDisk = reload,
                    TargetZoneEntityId = targetZoneEntityId
                };
                byte[] data = MyAPIGateway.Utilities.SerializeToBinary(msg);
                MyAPIGateway.Multiplayer.SendMessageToServer(AdminZoneConfigRequestSyncId, data);
            }
            catch (Exception ex)
            {
                LogError($"RequestAdminZoneConfig error: {ex}");
            }
        }

        private void SendAdminZoneConfigUpdateFromClient(string zoneName, bool enabled, float weldingSpeedValue, float costModifierValue, bool allowProjections, float projectionWeldingSpeedValue, bool debugMode, List<string> forbiddenComponents, List<ComponentPriceModifierEntry> componentPriceModifiers)
        {
            try
            {
                var player = GetLocalPlayer();
                if (player == null || _adminZoneState == null || _adminZoneState.ZoneEntityId == 0)
                    return;

                var msg = new AdminZoneConfigUpdateMessage
                {
                    PlayerId = player.IdentityId,
                    ZoneEntityId = _adminZoneState.ZoneEntityId,
                    ZoneName = zoneName,
                    Enabled = enabled,
                    WeldingSpeed = weldingSpeedValue,
                    CostModifier = costModifierValue,
                    AllowProjections = allowProjections,
                    ProjectionWeldingSpeed = projectionWeldingSpeedValue,
                    DebugMode = debugMode,
                    ComponentPriceModifiers = CloneComponentPriceModifiers(componentPriceModifiers),
                    ForbiddenComponents = NormalizeForbiddenComponentList(forbiddenComponents, false)
                };
                byte[] data = MyAPIGateway.Utilities.SerializeToBinary(msg);
                MyAPIGateway.Multiplayer.SendMessageToServer(AdminZoneConfigUpdateSyncId, data);
            }
            catch (Exception ex)
            {
                LogError($"SendAdminZoneConfigUpdateFromClient error: {ex}");
            }
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

        private bool IsZoneDetailsHotkeyPressed()
        {
            if (_zoneDetailsBind != null)
                return _zoneDetailsBind.IsNewPressed;

            var input = MyAPIGateway.Input;
            return input != null && input.IsAnyCtrlKeyPressed() && input.IsNewKeyPressed(ZoneDetailsKey);
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

                if (MyAPIGateway.Gui.IsCursorVisible && !IsCockpitInteractiveHudRequested() && !_adminPanelRequested)
                    return;

                if (GetLocalControlledShipController() != null && IsCockpitHudSuppressHotkeyPressed())
                    ToggleCockpitHudVisibilityForLocalContext();

                if (IsHudHotkeyPressed())
                    ToggleHudForLocalContext();

                if (GetLocalControlledShipController() != null && IsRepairHotkeyPressed())
                    ToggleRepairForLocalContext();

                if (IsZoneDetailsHotkeyPressed())
                    ToggleZoneDetailsForLocalContext();

                if (GetLocalControlledShipController() != null && IsForceRescanHotkeyPressed())
                    ForceRescanForLocalContext();

                if (IsAdminMenuHotkeyPressed())
                    ToggleAdminPanelForLocalContext();
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

                var localPlayer = GetLocalPlayer();
                if (_adminPanelRequested && !IsPlayerAdmin(localPlayer))
                {
                    _adminPanelRequested = false;
                    _adminPriceModsPanelRequested = false;
                    _adminComponentsViewRequested = false;
                    RefreshUiCursorState();
                    UpdateAdminPanelState();
                    UpdateAdminPriceModsPanelState();
                }

                if (_clientUiState == null || !_clientUiState.InRepairZone)
                {
                    _manualHudRequested = false;
                    _cockpitInteractiveRequested = false;
                    RefreshUiCursorState();
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

                    RefreshUiCursorState();
                    ShowHud();
                    return;
                }

                _cockpitInteractiveRequested = false;

                if (_adminPanelRequested)
                {
                    RefreshUiCursorState();
                    UpdateAdminPanelState();
                    if (_panel != null)
                        _panel.Visible = false;
                    return;
                }

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
        private int GetAdminComponentsMaxScrollOffset()
        {
            if (_adminComponentCatalog == null || _adminComponentCatalog.Count <= AdminComponentsPageSize)
                return 0;

            return _adminComponentCatalog.Count - AdminComponentsPageSize;
        }

        private void ClampAdminComponentsScrollOffset()
        {
            if (_adminComponentsScrollOffset < 0)
                _adminComponentsScrollOffset = 0;

            int maxOffset = GetAdminComponentsMaxScrollOffset();
            if (_adminComponentsScrollOffset > maxOffset)
                _adminComponentsScrollOffset = maxOffset;
        }

        private int NormalizeMouseWheelToRowDelta(int wheelDelta)
        {
            if (wheelDelta == 0)
                return 0;

            int stepMagnitude = Math.Abs(wheelDelta) / MouseWheelStepSize;
            if (stepMagnitude <= 0)
                stepMagnitude = 1;

            return wheelDelta > 0 ? -stepMagnitude : stepMagnitude;
        }

        private void ScrollAdminComponentsByRows(int rowDelta)
        {
            if (!_adminPanelRequested || !_adminComponentsViewRequested || rowDelta == 0)
                return;

            EnsureAdminComponentCatalogBuilt();
            if (_adminComponentCatalog == null || _adminComponentCatalog.Count <= 0)
                return;

            int oldOffset = _adminComponentsScrollOffset;
            _adminComponentsScrollOffset += rowDelta;
            ClampAdminComponentsScrollOffset();

            if (oldOffset != _adminComponentsScrollOffset)
                UpdateAdminPanelState();
        }

        private void UpdateAdminComponentsScrollInput()
        {
            try
            {
                if (!_adminPanelRequested || !_adminComponentsViewRequested)
                    return;

                if (MyAPIGateway.Gui == null || MyAPIGateway.Input == null)
                    return;

                if (MyAPIGateway.Gui.ChatEntryVisible)
                    return;

                int wheelDelta = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                int rowDelta = NormalizeMouseWheelToRowDelta(wheelDelta);
                if (rowDelta != 0)
                    ScrollAdminComponentsByRows(rowDelta);
            }
            catch (Exception ex)
            {
                LogError($"UpdateAdminComponentsScrollInput error: {ex}");
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Utilities != null && !MyAPIGateway.Utilities.IsDedicated)
            {
                UpdateClientHotkeys();
                UpdateClientHudVisibility();
                UpdateAdminComponentsScrollInput();
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

            UpdateZoneConfigCleanup(now);
            UpdateZoneConfigPersist(now);

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

                    bool needsRepair = Utils.NeedRepairRobust(block, false) || isProjected;
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


        private void UpdateZoneConfigCleanup(TimeSpan now)
        {
            if (now - _lastZoneConfigCleanupCheck < zoneConfigCleanupInterval)
                return;

            _lastZoneConfigCleanupCheck = now;
            CleanupMissingZoneConfigs();
        }

        private void UpdateZoneConfigPersist(TimeSpan now)
        {
            if (now - _lastZoneConfigPersistCheck < zoneConfigPersistInterval)
                return;

            _lastZoneConfigPersistCheck = now;
            EnsureZoneConfigFilePersisted("periodic persist check");
        }

        private void CleanupMissingZoneConfigs()
        {
            try
            {
                CleanupStaleSafeZoneReferences();

                if (zoneConfigs.Count == 0)
                    return;

                bool changed = false;
                var zoneIdsToRemove = new List<long>();

                foreach (var pair in zoneConfigs)
                {
                    long zoneEntityId = pair.Key;
                    if (DoesSafeZoneStillExist(zoneEntityId))
                    {
                        _missingZoneConfigChecks.Remove(zoneEntityId);
                        continue;
                    }

                    int missingChecks = 0;
                    _missingZoneConfigChecks.TryGetValue(zoneEntityId, out missingChecks);
                    missingChecks++;
                    _missingZoneConfigChecks[zoneEntityId] = missingChecks;

                    LogGeneral($"Zone config cleanup miss {missingChecks}/{MissingZoneConfigRemovalThreshold} for zone {zoneEntityId}");

                    if (missingChecks >= MissingZoneConfigRemovalThreshold)
                        zoneIdsToRemove.Add(zoneEntityId);
                }

                for (int i = 0; i < zoneIdsToRemove.Count; i++)
                {
                    long zoneEntityId = zoneIdsToRemove[i];
                    RemoveZoneConfigAndRuntimeState(zoneEntityId);
                    changed = true;
                    LogGeneral($"Removed stale zone config for missing safe zone {zoneEntityId}");
                }

                if (changed)
                    SaveConfig(new List<SafeZoneConfig>(zoneConfigs.Values));
            }
            catch (Exception ex)
            {
                LogError($"CleanupMissingZoneConfigs error: {ex}");
            }
        }

        private void CleanupStaleSafeZoneReferences()
        {
            for (int i = safeZones.Count - 1; i >= 0; i--)
            {
                var zone = safeZones[i];
                if (!IsSafeZoneEntityAlive(zone))
                {
                    long zoneEntityId = zone != null ? zone.EntityId : 0L;
                    safeZones.RemoveAt(i);
                    if (zoneEntityId != 0L)
                        LogGeneral($"Removed stale safe zone reference {zoneEntityId}");
                }
            }
        }

        private bool DoesSafeZoneStillExist(long zoneEntityId)
        {
            if (zoneEntityId == 0L)
                return false;

            MySafeZone zone;
            return TryGetSafeZoneByEntityId(zoneEntityId, out zone) && IsSafeZoneEntityAlive(zone);
        }

        private static bool IsSafeZoneEntityAlive(MySafeZone zone)
        {
            if (zone == null || zone.MarkedForClose)
                return false;

            IMyEntity entity;
            return MyAPIGateway.Entities != null &&
                   MyAPIGateway.Entities.TryGetEntityById(zone.EntityId, out entity) &&
                   entity is MySafeZone;
        }

        private void RemoveZoneConfigAndRuntimeState(long zoneEntityId)
        {
            zoneConfigs.Remove(zoneEntityId);
            _missingZoneConfigChecks.Remove(zoneEntityId);

            for (int i = safeZones.Count - 1; i >= 0; i--)
            {
                var zone = safeZones[i];
                if (zone != null && zone.EntityId == zoneEntityId)
                    safeZones.RemoveAt(i);
            }

            var cachedCostKeysToRemove = new List<long>();
            foreach (var pair in _estimatedRepairCostCache)
            {
                if (pair.Value != null && pair.Value.ZoneEntityId == zoneEntityId)
                    cachedCostKeysToRemove.Add(pair.Key);
            }

            for (int i = 0; i < cachedCostKeysToRemove.Count; i++)
                _estimatedRepairCostCache.Remove(cachedCostKeysToRemove[i]);
        }

        // --- Инициализация безопасных зон ---
        private void InitializeSafeZones()
        {
            safeZones.Clear();

            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => e is MySafeZone);
            foreach (IMyEntity entity in entities)
            {
                MySafeZone zone = entity as MySafeZone;
                if (!IsSafeZoneEntityAlive(zone))
                    continue;

                safeZones.Add(zone);
                LogGeneral($"Added safe zone: {zone.DisplayName} (ID: {zone.EntityId}, radius {zone.Radius})");
            }

            CleanupStaleSafeZoneReferences();
            LogGeneral($"Total safe zones: {safeZones.Count}");
        }

        private void OnEntityAdded(IMyEntity entity)
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;
            MySafeZone zone = entity as MySafeZone;
            if (zone != null && IsSafeZoneEntityAlive(zone))
            {
                bool alreadyTracked = false;
                for (int i = 0; i < safeZones.Count; i++)
                {
                    var existingZone = safeZones[i];
                    if (existingZone != null && existingZone.EntityId == zone.EntityId)
                    {
                        alreadyTracked = true;
                        break;
                    }
                }

                if (!alreadyTracked)
                    safeZones.Add(zone);

                _missingZoneConfigChecks.Remove(zone.EntityId);
                LogGeneral($"New safe zone added: {zone.DisplayName}");

                if (!zoneConfigs.ContainsKey(zone.EntityId))
                {
                    zoneConfigs[zone.EntityId] = CreateDefaultZoneConfig(zone);
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
                if (isProjected && zoneCfg != null && !zoneCfg.AllowProjections)
                {
                    LogZone($"Block {Utils.BlockName(block)} is a projection but zone {zoneCfg.ZoneName} does not allow projection repair, skipping");
                    continue;
                }

                if (isProjected)
                {
                    IMySlimBlock overlapBlock;
                    bool sameBlock;
                    if (HasProjectedOverlap(block, sourceGridEntityId != 0 ? sourceGridEntityId : grid.EntityId, out overlapBlock, out sameBlock))
                    {
                        if (sameBlock)
                        {
                            LogZone($"Projected block {Utils.BlockName(block)} overlaps matching real block {Utils.BlockName(overlapBlock)}, skipping projected queue entry");
                            continue;
                        }

                        LogZone($"Projected block {Utils.BlockName(block)} is blocked by {Utils.BlockName(overlapBlock)}, skipping projected queue entry until next rescan");
                        continue;
                    }
                }

                bool needsRepair = Utils.NeedRepairRobust(block, false) || isProjected;
                bool alreadyInQueue = blocksInQueue.Contains(block);

                if (needsRepair && !alreadyInQueue)
                {
                    // Если блок ранее был завершён, удаляем его ключ, чтобы разрешить новое уведомление при следующем ремонте
                    string blockKey = $"{block.CubeGrid.EntityId}:{block.Position}";
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
                BlockRepairInfo projectedInfo;
                IMySlimBlock sourceBlock;
                bool sameBlock;
                long sourceGridEntityId = blockRepairInfo.TryGetValue(block, out projectedInfo)
                    ? projectedInfo.SourceGridEntityId
                    : 0;

                if (HasProjectedOverlap(block, sourceGridEntityId, out sourceBlock, out sameBlock))
                {
                    if (sameBlock)
                    {
                        var missing = new Dictionary<string, int>();
                        sourceBlock.GetMissingComponents(missing);
                        LogZone($"Projected block overlaps matching real block: {Utils.BlockName(sourceBlock)}");
                        foreach (var kv in missing)
                        {
                            MyDefinitionId id;
                            if (MyDefinitionId.TryParse("MyObjectBuilder_Component/" + kv.Key, out id))
                            {
                                float compCost = GetComponentCost(id, zoneCfg);
                                LogZone($"Overlap component {kv.Key}: cost={compCost}, missing={kv.Value}");
                                if (compCost < 0) return -1;
                                total += compCost * kv.Value;
                            }
                        }
                    }
                    else
                    {
                        LogZone($"Projected block is blocked by another real block at source position, excluding from payable cost");
                        return 0f;
                    }
                }
                else
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
            float baseCost = def?.MinimalPricePerUnit > 0 ? def.MinimalPricePerUnit : 1f;

            float componentModifier;
            if (TryGetComponentPriceModifier(zoneCfg, id, out componentModifier))
            {
                float finalCost = baseCost * componentModifier;
                LogZone($"Component price override for {id.SubtypeId.String}: base={baseCost}, modifier={componentModifier}, final={finalCost}");
                return finalCost;
            }

            return baseCost;
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
                if (block == null || Utils.IsProjected(block) || !Utils.NeedRepairRobust(block, false))
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
                IMySlimBlock overlapBlock;
                bool sameBlock;
                if (HasProjectedOverlap(block, info.SourceGridEntityId, out overlapBlock, out sameBlock))
                {
                    LogRepair(sameBlock
                        ? $"Projected block {Utils.BlockName(block)} overlaps matching real block {Utils.BlockName(overlapBlock)}, removing projected entry so real block repair can continue"
                        : $"Projected block {Utils.BlockName(block)} is blocked by {Utils.BlockName(overlapBlock)}, removing projected entry until next rescan");
                    blocksRepairQueue.Dequeue();
                    blocksInQueue.Remove(block);
                    blockRepairInfo.Remove(block);
                    return;
                }

                if (projector != null && Utils.CanBuild(block, true))
                {
                    float delay = GetProjectionBuildDelayForZone(currentZone);
                    if ((DateTime.Now - _lastProjectionBuildTime).TotalSeconds < delay)
                    {
                        LogRepair($"Projection build delay {delay}s, skipping this tick");
                        return;
                    }

                    float totalCostNow = CalculateTotalRepairCost(block, zoneCfg);
                    info.TotalCost = (long)Math.Max(0f, totalCostNow);
                    if (info.InitialCost <= 0)
                        info.InitialCost = info.TotalCost;

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
            float remainingBuildIntegrity = Math.Max(0f, block.MaxIntegrity - block.BuildIntegrity);
            bool hasDeformation = block.HasDeformation;
            int missingComponentsBefore = block.GetMissingComponentsTotalCount();
            bool hasMissingComponents = missingComponentsBefore > 0;
            bool pureDeformationOnly = remainingBuildIntegrity <= 0.01f && hasDeformation && !hasMissingComponents;
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
            else if (hasDeformation || hasMissingComponents)
            {
                repairAmount = Math.Max(repairAmount, 1f);
            }

            LogRepair($"repairAmount={repairAmount}, remainingBuildIntegrity={remainingBuildIntegrity}, BuildIntegrity={block.BuildIntegrity}, MaxIntegrity={block.MaxIntegrity}, HasDeformation={hasDeformation}, HasMissingComponents={hasMissingComponents}, MissingComponentsBefore={missingComponentsBefore}, CurrentDamage={currentDamageBefore}, AccumulatedDamage={accumulatedDamageBefore}");

            if (repairAmount <= 0 && !hasDeformation && !hasMissingComponents)
            {
                LogRepair($"repairAmount <= 0 and block has no deformation or missing components, removing from queue");
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
                    float deformationBudget = Math.Max(1f, Math.Max(currentDamageBefore, accumulatedDamageBefore)) * DeformationRepairStepMultiplier;
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
                        float deformationBudget = Math.Max(1f, Math.Max(currentDamageBefore, accumulatedDamageBefore)) * DeformationRepairStepMultiplier;
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
            int missingComponentsAfter = block.GetMissingComponentsTotalCount();
            bool progressMade =
                buildIntegrityAfter > buildIntegrityBefore + 0.001f ||
                currentDamageAfter < currentDamageBefore - 0.001f ||
                accumulatedDamageAfter < accumulatedDamageBefore - 0.001f ||
                (hasDeformation && !hasDeformationAfter) ||
                missingComponentsAfter < missingComponentsBefore;

            SendRepairNotificationToClients(block, false, 0, player.IdentityId);

            if (!Utils.NeedRepairRobust(block, false))
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

        private float GetProjectionWeldingSpeedForZone(MySafeZone zone)
        {
            if (zone == null)
                return 1f;

            SafeZoneConfig cfg;
            if (zoneConfigs.TryGetValue(zone.EntityId, out cfg))
            {
                NormalizeZoneConfig(cfg);
                return Math.Max(0.001f, cfg.ProjectionWeldingSpeed);
            }

            return 1f;
        }

        private float GetProjectionBuildDelayForZone(MySafeZone zone)
        {
            float speed = GetProjectionWeldingSpeedForZone(zone);
            return (float)Math.Round(1f / Math.Max(0.001f, speed), 2);
        }

        private IMyCubeGrid FindGridByEntityId(long entityId)
        {
            if (entityId == 0)
                return null;

            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => e != null && e.EntityId == entityId && e is IMyCubeGrid);
            foreach (var entity in entities)
                return entity as IMyCubeGrid;

            return null;
        }

        private bool TryGetProjectedSourceBlock(IMySlimBlock projectedBlock, long sourceGridEntityId, out IMyCubeGrid sourceGrid, out IMySlimBlock sourceBlock)
        {
            sourceGrid = null;
            sourceBlock = null;

            if (projectedBlock == null || !Utils.IsProjected(projectedBlock))
                return false;

            sourceGrid = FindGridByEntityId(sourceGridEntityId);
            if (sourceGrid == null)
            {
                var projectedGrid = projectedBlock.CubeGrid as MyCubeGrid;
                var projector = projectedGrid?.Projector as IMyProjector;
                sourceGrid = projector?.CubeGrid;
            }

            if (sourceGrid == null)
                return false;

            try
            {
                Vector3D worldPos = projectedBlock.CubeGrid.GridIntegerToWorld(projectedBlock.Position);
                Vector3I sourcePos = sourceGrid.WorldToGridInteger(worldPos);
                sourceBlock = sourceGrid.GetCubeBlock(sourcePos);
                return sourceBlock != null;
            }
            catch (Exception ex)
            {
                LogError($"TryGetProjectedSourceBlock error: {ex}");
                sourceGrid = null;
                sourceBlock = null;
                return false;
            }
        }

        private bool AreProjectionEquivalentBlocks(IMySlimBlock projectedBlock, IMySlimBlock sourceBlock)
        {
            if (projectedBlock == null || sourceBlock == null || projectedBlock.BlockDefinition == null || sourceBlock.BlockDefinition == null)
                return false;

            return projectedBlock.BlockDefinition.Id.TypeId == sourceBlock.BlockDefinition.Id.TypeId &&
                   projectedBlock.BlockDefinition.Id.SubtypeId == sourceBlock.BlockDefinition.Id.SubtypeId;
        }

        private bool HasProjectedOverlap(IMySlimBlock projectedBlock, long sourceGridEntityId, out IMySlimBlock sourceBlock, out bool sameBlock)
        {
            sourceBlock = null;
            sameBlock = false;

            IMyCubeGrid sourceGrid;
            if (!TryGetProjectedSourceBlock(projectedBlock, sourceGridEntityId, out sourceGrid, out sourceBlock) || sourceBlock == null)
                return false;

            sameBlock = AreProjectionEquivalentBlocks(projectedBlock, sourceBlock);
            return true;
        }

        private bool GridHasActiveProjector(IMyCubeGrid grid)
        {
            if (grid == null)
                return false;

            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);
            foreach (var block in blocks)
            {
                var projector = block?.FatBlock as IMyProjector;
                if (projector != null && projector.IsFunctional && projector.IsProjecting)
                    return true;
            }

            return false;
        }

        private string AppendProjectionStatus(string statusText, IMyCubeGrid grid, SafeZoneConfig zoneCfg)
        {
            string text = string.IsNullOrWhiteSpace(statusText) ? "In repair zone" : statusText.Trim();
            if (zoneCfg != null && !zoneCfg.AllowProjections && GridHasActiveProjector(grid))
                text += " | Projection repair unavailable in this zone";

            return text;
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

                    BlockRepairInfo repairInfo;
                    IMyCubeGrid uiGrid = block.CubeGrid;
                    if (blockRepairInfo.TryGetValue(block, out repairInfo) && repairInfo.SourceGridEntityId != 0)
                    {
                        var sourceGrid = FindGridByEntityId(repairInfo.SourceGridEntityId);
                        if (sourceGrid != null)
                            uiGrid = sourceGrid;
                    }

                    var uiZone = GetSafeZoneForGrid(uiGrid);
                    bool inZone = uiGrid != null && GridIsInSafeZone(uiGrid);
                    string nextStatus = uiGrid != null && GetGridRepairSetting(uiGrid) ? "Repair ready" : "Repair disabled for your ship";
                    SendRepairUiStateToPlayer(targetPlayer, inZone, uiGrid, uiZone, nextStatus, $"{msg.BlockName} repaired! Cost: {msg.Cost} SC");
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

            SafeZoneConfig cfg = null;
            var zone = GetSafeZoneForGrid(grid);
            if (zone != null)
                zoneConfigs.TryGetValue(zone.EntityId, out cfg);

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
                bool queuedProjected = Utils.IsProjected(queuedBlock);

                if (ahead <= 0)
                    return queuedProjected
                        ? string.Format("Current repair: projection -> {0}", blockName)
                        : string.Format("Current repair: {0}", blockName);

                return string.Format("Current repair: queued ({0} ahead) -> {1}", ahead, blockName);
            }

            long estimate = GetEstimatedRepairCostForUi(player, true, grid, zone, GetGridRepairSetting(grid));
            if (estimate > 0)
                return "Current repair: queued / waiting for next block";

            return BuildZoneServiceInfoText(cfg);
        }

        private string BuildRepairPhaseForUi(IMyPlayer player, IMyCubeGrid grid)
        {
            if (player == null || grid == null)
                return "Repair phase: idle";

            SafeZoneConfig cfg = null;
            var zone = GetSafeZoneForGrid(grid);
            if (zone != null)
                zoneConfigs.TryGetValue(zone.EntityId, out cfg);

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

                bool queuedProjected = Utils.IsProjected(queuedBlock);
                bool queuedHasBuildWork = queuedBlock.MaxIntegrity - queuedBlock.BuildIntegrity > 0.01f;
                bool queuedHasDeformation = queuedBlock.HasDeformation;

                if (ahead <= 0)
                {
                    if (queuedProjected)
                    {
                        IMySlimBlock overlapBlock;
                        bool sameBlock;
                        if (HasProjectedOverlap(queuedBlock, queuedInfo.SourceGridEntityId, out overlapBlock, out sameBlock) && sameBlock)
                            return "Repair phase: projection welding";

                        return Utils.CanBuild(queuedBlock, true)
                            ? "Repair phase: projection welding"
                            : "Repair phase: projection blocked";
                    }

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

            long estimate = GetEstimatedRepairCostForUi(player, true, grid, zone, GetGridRepairSetting(grid));
            if (estimate > 0)
                return "Repair phase: waiting";

            return BuildZoneRestrictionsText(cfg);
        }

        private string BuildCurrentScanTextForUi(IMyPlayer player, IMyCubeGrid grid, SafeZoneConfig cfg)
        {
            if (player == null)
                return "Current scan: -";

            string text;
            DateTime until;
            if (_currentScanBlockByPlayer.TryGetValue(player.IdentityId, out text) &&
                _currentScanUntilByPlayer.TryGetValue(player.IdentityId, out until) &&
                DateTime.UtcNow < until)
            {
                return string.IsNullOrWhiteSpace(text) ? "Current scan: -" : text;
            }

            return "Current scan: -";
        }

        private string BuildZoneServiceInfoText(SafeZoneConfig cfg)
        {
            string projections = GetPlayerFacingProjectionLabel(cfg);
            string speed = GetPlayerFacingSpeedLabel(cfg);
            string cost = GetPlayerFacingCostLabel(cfg);
            return string.Format("Service info: Proj: {0} | Speed: {1} | Cost: {2}", projections, speed, cost);
        }

        private string BuildZoneRestrictionsText(SafeZoneConfig cfg)
        {
            return string.Format("Restrictions: {0}", GetPlayerFacingRestrictionsSummary(cfg));
        }

        private string BuildZoneServiceStatusText(string baseStatusText, SafeZoneConfig cfg)
        {
            string baseText = string.IsNullOrWhiteSpace(baseStatusText) ? "In repair zone" : baseStatusText.Trim();
            string serviceName = GetPlayerFacingServiceName(cfg);
            if (string.IsNullOrWhiteSpace(serviceName))
                return baseText;
            return string.Format("{0} | Service: {1}", baseText, serviceName);
        }

        private string GetPlayerFacingServiceName(SafeZoneConfig cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg?.PlayerServiceName))
                return cfg.PlayerServiceName.Trim();

            string profileId = cfg?.AssignedProfileId;
            switch ((profileId ?? string.Empty).Trim())
            {
                case ZoneProfileIds.Industrial:
                    return "Industrial Yard";
                case ZoneProfileIds.Civilian:
                    return "Civilian Service";
                case ZoneProfileIds.Military:
                    return "Military Service";
                case ZoneProfileIds.Premium:
                    return "Premium Repair Yard";
                case ZoneProfileIds.Neutral:
                default:
                    return "General Service";
            }
        }

        private string GetPlayerFacingProjectionLabel(SafeZoneConfig cfg)
        {
            return cfg != null && cfg.AllowProjections ? "Available" : "Unavailable";
        }

        private string GetPlayerFacingSpeedLabel(SafeZoneConfig cfg)
        {
            float speed = cfg?.WeldingSpeed ?? 1f;
            if (speed < 0.95f)
                return "Slow";
            if (speed > 1.15f)
                return "Fast";
            return "Standard";
        }

        private string GetPlayerFacingCostLabel(SafeZoneConfig cfg)
        {
            float cost = cfg?.CostModifier ?? 1f;
            if (cost < 0.95f)
                return "Low";
            if (cost > 1.2f)
                return "High";
            return "Medium";
        }

        private string GetPlayerFacingRestrictionsSummary(SafeZoneConfig cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg?.PlayerRestrictionsSummary))
                return cfg.PlayerRestrictionsSummary.Trim();

            if (cfg == null || cfg.ForbiddenComponents == null || cfg.ForbiddenComponents.Count == 0)
                return "No major restrictions";

            var forbidden = new HashSet<string>(cfg.ForbiddenComponents, StringComparer.OrdinalIgnoreCase);
            bool hasPrototech = false;
            bool onlyPrototech = true;
            foreach (var component in forbidden)
            {
                if (string.IsNullOrWhiteSpace(component))
                    continue;

                bool isPrototech = component.IndexOf("Prototech", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isPrototech)
                    hasPrototech = true;
                else
                    onlyPrototech = false;
            }

            if (hasPrototech && onlyPrototech)
                return "Prototech parts blocked";

            bool power = forbidden.Contains("Reactor") || forbidden.Contains("Superconductor") || forbidden.Contains("PowerCell");
            bool gravity = forbidden.Contains("GravityGenerator");
            bool medical = forbidden.Contains("Medical");
            bool propulsion = forbidden.Contains("Thrust") || forbidden.Contains("Thruster") || forbidden.Contains("PrototechPropulsionUnit");

            if (power && gravity)
                return "Advanced power and gravity parts limited";
            if (power)
                return "Advanced power parts limited";
            if (gravity)
                return "Gravity parts limited";
            if (medical)
                return "Medical parts limited";
            if (propulsion)
                return "Propulsion parts limited";
            if (forbidden.Count >= 5)
                return "Advanced systems restricted";

            return "Selected components limited";
        }

        private string GetPlayerFacingRestrictionsDetails(SafeZoneConfig cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg?.PlayerDetailsDescription))
                return cfg.PlayerDetailsDescription.Trim();

            if (cfg == null || cfg.ForbiddenComponents == null || cfg.ForbiddenComponents.Count == 0)
                return "No major restrictions are configured for this zone.";

            var components = new List<string>();
            for (int i = 0; i < cfg.ForbiddenComponents.Count; i++)
            {
                string component = cfg.ForbiddenComponents[i];
                if (string.IsNullOrWhiteSpace(component))
                    continue;

                components.Add(component.Trim());
            }

            if (components.Count == 0)
                return "No major restrictions are configured for this zone.";

            components.Sort(StringComparer.OrdinalIgnoreCase);
            return "Restricted components: " + string.Join(", ", components);
        }

        private string GetPlayerFacingZoneHeaderName(SafeZoneConfig cfg, string fallbackZoneName)
        {
            if (!string.IsNullOrWhiteSpace(cfg?.ProfileSourceName))
                return cfg.ProfileSourceName.Trim();

            if (!string.IsNullOrWhiteSpace(cfg?.DisplayName))
                return cfg.DisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(cfg?.ZoneName))
                return cfg.ZoneName.Trim();

            return string.IsNullOrWhiteSpace(fallbackZoneName) ? "Repair Zone" : fallbackZoneName.Trim();
        }

        private string BuildRestrictedComponentsHudText(SafeZoneConfig cfg, int maxLength = 92)
        {
            if (cfg == null || cfg.ForbiddenComponents == null || cfg.ForbiddenComponents.Count == 0)
                return "Restricted comps: none";

            var components = new List<string>();
            for (int i = 0; i < cfg.ForbiddenComponents.Count; i++)
            {
                string component = cfg.ForbiddenComponents[i];
                if (string.IsNullOrWhiteSpace(component))
                    continue;

                string trimmed = component.Trim();
                if (trimmed.Length == 0)
                    continue;

                components.Add(trimmed);
            }

            if (components.Count == 0)
                return "Restricted comps: none";

            components.Sort(StringComparer.OrdinalIgnoreCase);
            string joined = string.Join(", ", components);
            string prefix = "Restricted comps: ";
            if (joined.Length + prefix.Length <= maxLength)
                return prefix + joined;

            int remaining = Math.Max(0, maxLength - prefix.Length - 3);
            if (remaining <= 0)
                return prefix.TrimEnd();

            return prefix + joined.Substring(0, Math.Min(joined.Length, remaining)) + "...";
        }

        private RepairUiStateMessage BuildZoneDetailsHudState(RepairUiStateMessage source)
        {
            if (source == null)
                return null;

            try
            {
                IMyCubeGrid grid = GetLocalActionGrid();
                MySafeZone zone = grid != null ? GetSafeZoneForGrid(grid) : null;
                if (zone == null)
                {
                    var player = GetLocalPlayer();
                    var character = player?.Character;
                    if (character != null)
                        zone = GetSafeZoneForPosition(character.GetPosition());
                }

                if (zone == null)
                    return source;

                SafeZoneConfig cfg;
                if (!zoneConfigs.TryGetValue(zone.EntityId, out cfg) || cfg == null)
                    return source;

                var details = new RepairUiStateMessage
                {
                    PlayerId = source.PlayerId,
                    InRepairZone = source.InRepairZone,
                    ZoneName = GetPlayerFacingZoneHeaderName(cfg, source.ZoneName),
                    RepairEnabled = source.RepairEnabled,
                    StatusText = string.Format("Zone details | Service: {0}", GetPlayerFacingServiceName(cfg)),
                    LastRepairText = BuildRestrictedComponentsHudText(cfg, 4096),
                    LastEventUtcTicks = source.LastEventUtcTicks,
                    EstimatedRepairCost = source.EstimatedRepairCost,
                    CurrentScanText = string.Format("Current scan: Profile {0} | Variant {1}", string.IsNullOrWhiteSpace(cfg.AssignedProfileId) ? "-" : cfg.AssignedProfileId, string.IsNullOrWhiteSpace(cfg.AssignedVariantId) ? "-" : cfg.AssignedVariantId),
                    CurrentRepairText = string.Format("Service: Proj {0} | Speed {1} | Cost {2}", cfg.AllowProjections ? "On" : "Off", GetPlayerFacingSpeedLabel(cfg), GetPlayerFacingCostLabel(cfg)),
                    RepairPhaseText = string.Format("Restrictions: {0}", GetPlayerFacingRestrictionsSummary(cfg))
                };

                return details;
            }
            catch (Exception ex)
            {
                LogError($"BuildZoneDetailsHudState error: {ex}");
                return source;
            }
        }

        private string BuildZoneDetailsText(SafeZoneConfig cfg)
        {
            if (cfg == null)
                return "No zone details available.";

            var sb = new StringBuilder();
            sb.Append("Service: ").Append(GetPlayerFacingServiceName(cfg));
            sb.Append("\nRepair: ").Append(cfg.Enabled ? "Enabled" : "Disabled");
            sb.Append("\nProjections: ").Append(GetPlayerFacingProjectionLabel(cfg));
            sb.Append("\nRepair speed: ").Append(GetPlayerFacingSpeedLabel(cfg));
            sb.Append("\nCost level: ").Append(GetPlayerFacingCostLabel(cfg));

            if (!string.IsNullOrWhiteSpace(cfg.AssignedProfileId))
                sb.Append("\nProfile: ").Append(cfg.AssignedProfileId);

            if (!string.IsNullOrWhiteSpace(cfg.AssignedVariantId))
                sb.Append("\nVariant: ").Append(cfg.AssignedVariantId);

            sb.Append("\n\nRestrictions:");
            sb.Append("\n").Append(GetPlayerFacingRestrictionsSummary(cfg));
            sb.Append("\n").Append(GetPlayerFacingRestrictionsDetails(cfg));

            if (cfg.ComponentPriceModifiers != null && cfg.ComponentPriceModifiers.Count > 0)
            {
                sb.Append("\n\nPrice modifiers:");
                for (int i = 0; i < cfg.ComponentPriceModifiers.Count; i++)
                {
                    var entry = cfg.ComponentPriceModifiers[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.ComponentSubtypeId))
                        continue;

                    sb.Append("\n- ").Append(entry.ComponentSubtypeId.Trim()).Append(": x").Append(entry.Multiplier.ToString("0.##"));
                }
            }

            sb.Append("\n\nHotkey: Ctrl+M");
            return sb.ToString();
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
            string uiStatusText = string.IsNullOrWhiteSpace(statusText) ? (inRepairZone ? "In repair zone" : "Outside repair zone") : statusText;
            uiStatusText = AppendProjectionStatus(uiStatusText, grid, cfg);
            uiStatusText = BuildZoneServiceStatusText(uiStatusText, cfg);

            var msg = new RepairUiStateMessage
            {
                PlayerId = player.IdentityId,
                InRepairZone = inRepairZone,
                ZoneName = zoneName,
                RepairEnabled = repairEnabled,
                StatusText = uiStatusText,
                LastRepairText = string.IsNullOrWhiteSpace(lastRepairText) ? _clientUiState.LastRepairText : lastRepairText,
                LastEventUtcTicks = DateTime.UtcNow.Ticks,
                EstimatedRepairCost = estimatedRepairCost,
                CurrentRepairText = inRepairZone ? BuildCurrentRepairTextForUi(player, grid) : "Current repair: -",
                RepairPhaseText = inRepairZone ? BuildRepairPhaseForUi(player, grid) : "Repair phase: idle",
                CurrentScanText = inRepairZone ? BuildCurrentScanTextForUi(player, grid, cfg) : "Current scan: -"
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

                if (!msg.InRepairZone)
                    _zoneDetailsHudRequested = false;

                if (GetLocalControlledShipController() != null && !IsCockpitHudVisible())
                {
                    HideHud();
                    return;
                }

                var displayMsg = _zoneDetailsHudRequested ? BuildZoneDetailsHudState(msg) : msg;
                UpdateRichHudState(displayMsg);
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

        private string BuildAdminMissingSummary(Dictionary<string, int> missing, int maxItems = 3)
        {
            if (missing == null || missing.Count == 0)
                return "-";

            var sb = new StringBuilder();
            int added = 0;
            foreach (var kv in missing)
            {
                if (kv.Value <= 0)
                    continue;

                if (added > 0)
                    sb.Append(", ");

                sb.Append(kv.Key).Append(":").Append(kv.Value);
                added++;
                if (added >= maxItems)
                    break;
            }

            return added == 0 ? "-" : sb.ToString();
        }

        private string BuildAdminDebugText(IMyPlayer player, MySafeZone zone, SafeZoneConfig cfg)
        {
            if (player == null)
                return "Debug context: player unavailable.";

            if (zone == null || cfg == null)
                return "Debug context: zone unavailable.";

            if (!cfg.DebugMode)
                return "Debug mode is OFF. Toggle it on and press Apply to capture a fresh snapshot.";

            var sb = new StringBuilder();
            sb.Append("Debug mode: ON");
            sb.Append("\nZone entity: ").Append(zone.EntityId);

            var shipController = GetControlledShipController(player);
            var grid = shipController != null ? shipController.CubeGrid : null;
            if (grid == null)
            {
                sb.Append("\nControlled grid: -");
                sb.Append("\nTip: sit in a cockpit/remote control and press Load cfg to refresh.");
                return sb.ToString();
            }

            bool inZone = IsGridInSafeZone(grid, zone);
            bool repairEnabled = GetGridRepairSetting(grid);
            sb.Append("\nGrid: ").Append(grid.DisplayName ?? "-");
            sb.Append("\nGrid entity: ").Append(grid.EntityId);
            sb.Append("\nIn zone: ").Append(inZone ? "yes" : "no");
            sb.Append(" | repair enabled: ").Append(repairEnabled ? "yes" : "no");

            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);
            int robustCandidates = 0;
            int partialBlocks = 0;
            int deformedBlocks = 0;
            int damagedBlocks = 0;
            int projectedBlocks = 0;
            IMySlimBlock samplePartial = null;

            foreach (var block in blocks)
            {
                if (block == null)
                    continue;

                bool projected = Utils.IsProjected(block);
                if (projected)
                    projectedBlocks++;

                if (Utils.NeedRepairRobust(block, false))
                    robustCandidates++;

                bool hasMissingComponents = false;
                if (block.BuildLevelRatio < 0.999f)
                {
                    hasMissingComponents = true;
                }
                else
                {
                    var missing = new Dictionary<string, int>();
                    block.GetMissingComponents(missing);
                    foreach (var kv in missing)
                    {
                        if (kv.Value > 0)
                        {
                            hasMissingComponents = true;
                            break;
                        }
                    }
                }

                if (hasMissingComponents)
                {
                    partialBlocks++;
                    if (samplePartial == null && !projected)
                        samplePartial = block;
                }

                if (block.HasDeformation)
                    deformedBlocks++;

                if (block.MaxIntegrity - block.BuildIntegrity > 0.01f)
                    damagedBlocks++;
            }

            int queuedForGrid = 0;
            int projectedQueuedForGrid = 0;
            foreach (var queued in blocksRepairQueue)
            {
                if (queued == null)
                    continue;

                if (queued.CubeGrid == grid)
                {
                    queuedForGrid++;
                    if (Utils.IsProjected(queued))
                        projectedQueuedForGrid++;
                }
            }

            sb.Append("\nBlocks: ").Append(blocks.Count)
              .Append(" | candidates: ").Append(robustCandidates)
              .Append(" | queue on grid: ").Append(queuedForGrid);
            sb.Append("\nPartial: ").Append(partialBlocks)
              .Append(" | damaged: ").Append(damagedBlocks)
              .Append(" | deformed: ").Append(deformedBlocks)
              .Append(" | projected: ").Append(projectedBlocks)
              .Append(" | projected queued: ").Append(projectedQueuedForGrid);

            string scanText;
            DateTime scanUntil;
            if (_currentScanBlockByPlayer.TryGetValue(player.IdentityId, out scanText) &&
                _currentScanUntilByPlayer.TryGetValue(player.IdentityId, out scanUntil))
            {
                int ttl = Math.Max(0, (int)Math.Ceiling((scanUntil - DateTime.UtcNow).TotalSeconds));
                sb.Append("\nScan cache: ").Append(string.IsNullOrWhiteSpace(scanText) ? "-" : scanText);
                sb.Append(" | ttl: ").Append(ttl).Append("s");
            }
            else
            {
                sb.Append("\nScan cache: -");
            }

            EstimatedRepairCostCacheEntry cacheEntry;
            if (_estimatedRepairCostCache.TryGetValue(player.IdentityId, out cacheEntry))
            {
                sb.Append("\nEstimate cache: ").Append(cacheEntry.EstimatedRepairCost).Append(" SC");
                sb.Append(" | grid: ").Append(cacheEntry.GridEntityId);
                sb.Append(" | zone: ").Append(cacheEntry.ZoneEntityId);
            }
            else
            {
                sb.Append("\nEstimate cache: -");
            }

            if (samplePartial != null)
            {
                var missing = new Dictionary<string, int>();
                samplePartial.GetMissingComponents(missing);
                sb.Append("\nSample partial: ").Append(TruncateHudBlockName(Utils.BlockName(samplePartial), 52));
                sb.Append("\nBuild ratio: ")
                  .Append(samplePartial.BuildLevelRatio.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(" | missing: ").Append(BuildAdminMissingSummary(missing, 3));
                sb.Append(" | deform: ").Append(samplePartial.HasDeformation ? "yes" : "no");
            }
            else
            {
                sb.Append("\nSample partial: -");
            }

            return sb.ToString();
        }

        private void SendAdminZoneConfigStateToPlayer(IMyPlayer player, bool success, string errorText, MySafeZone zone, SafeZoneConfig cfg, long selectedZoneEntityId, long playerZoneEntityId)
        {
            if (player == null || !MyAPIGateway.Multiplayer.IsServer)
                return;

            if (cfg != null)
                RefreshZoneCreationTypeFromLiveZone(cfg);

            var msg = new AdminZoneConfigStateMessage
            {
                PlayerId = player.IdentityId,
                Success = success,
                ErrorText = errorText ?? string.Empty,
                ZoneEntityId = zone?.EntityId ?? 0L,
                ZoneName = cfg?.DisplayName ?? cfg?.ZoneName ?? string.Empty,
                Enabled = cfg?.Enabled ?? false,
                WeldingSpeed = cfg?.WeldingSpeed ?? 0f,
                CostModifier = cfg?.CostModifier ?? 0f,
                AllowProjections = cfg?.AllowProjections ?? false,
                ProjectionWeldingSpeed = cfg?.ProjectionWeldingSpeed ?? 1f,
                DebugMode = cfg != null && cfg.DebugMode,
                DebugText = BuildAdminDebugText(player, zone, cfg),
                SelectedZoneEntityId = selectedZoneEntityId,
                ZoneEntries = BuildAdminZoneList(selectedZoneEntityId, playerZoneEntityId),
                ZoneCreationType = cfg != null ? NormalizeZoneCreationTypeValue(cfg.ZoneCreationType) : ZoneCreationTypeSafeZoneBlock,
                ComponentPriceModifiers = cfg != null ? CloneComponentPriceModifiers(cfg.ComponentPriceModifiers) : new List<ComponentPriceModifierEntry>(),
                ForbiddenComponents = cfg != null ? NormalizeForbiddenComponentList(cfg.ForbiddenComponents, false) : new List<string>()
            };

            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(msg);
            MyAPIGateway.Multiplayer.SendMessageTo(AdminZoneConfigStateSyncId, data, player.SteamUserId);
        }

        private void HandleAdminZoneConfigRequest(byte[] data)
        {
            try
            {
                var msg = MyAPIGateway.Utilities.SerializeFromBinary<AdminZoneConfigRequestMessage>(data);
                if (msg == null || !MyAPIGateway.Multiplayer.IsServer)
                    return;

                var player = GetPlayerByIdentityId(msg.PlayerId);
                if (player == null)
                    return;

                if (msg.ReloadFromDisk)
                    LoadConfig();

                MySafeZone zone;
                SafeZoneConfig cfg;
                long playerZoneEntityId;
                if (!TryResolveAdminZone(player, msg.TargetZoneEntityId, out zone, out cfg, out playerZoneEntityId))
                {
                    SendAdminZoneConfigStateToPlayer(player, false, "No safe zones available for remote admin editing.", null, null, msg.TargetZoneEntityId, playerZoneEntityId);
                    return;
                }

                SendAdminZoneConfigStateToPlayer(player, true, null, zone, cfg, zone != null ? zone.EntityId : msg.TargetZoneEntityId, playerZoneEntityId);
            }
            catch (Exception ex)
            {
                LogError($"HandleAdminZoneConfigRequest error: {ex}");
            }
        }

        private void HandleAdminZoneConfigUpdate(byte[] data)
        {
            try
            {
                var msg = MyAPIGateway.Utilities.SerializeFromBinary<AdminZoneConfigUpdateMessage>(data);
                if (msg == null || !MyAPIGateway.Multiplayer.IsServer)
                    return;

                var player = GetPlayerByIdentityId(msg.PlayerId);
                if (player == null || !IsPlayerAdmin(player))
                    return;

                MySafeZone zone;
                if (!TryGetSafeZoneByEntityId(msg.ZoneEntityId, out zone) || zone == null)
                {
                    long playerZoneEntityId;
                    Vector3D pos;
                    playerZoneEntityId = TryGetPlayerPosition(player, out pos) && GetSafeZoneForPosition(pos) != null ? GetSafeZoneForPosition(pos).EntityId : 0L;
                    SendAdminZoneConfigStateToPlayer(player, false, "Selected zone no longer exists.", null, null, msg.ZoneEntityId, playerZoneEntityId);
                    return;
                }

                var cfg = EnsureZoneConfig(zone);
                cfg.ZoneName = string.IsNullOrWhiteSpace(msg.ZoneName) ? GetSafeZoneDefaultName(zone) : msg.ZoneName.Trim();
                cfg.DisplayName = cfg.ZoneName;
                cfg.Enabled = msg.Enabled;
                cfg.WeldingSpeed = (float)Math.Round(Math.Max(0.001f, msg.WeldingSpeed), 2);
                cfg.CostModifier = (float)Math.Round(Math.Max(0.001f, msg.CostModifier), 2);
                cfg.AllowProjections = msg.AllowProjections;
                cfg.ProjectionWeldingSpeed = (float)Math.Round(Math.Max(0.001f, msg.ProjectionWeldingSpeed), 2);
                cfg.ProjectionBuildDelay = (float)Math.Round(1f / Math.Max(0.001f, cfg.ProjectionWeldingSpeed), 2);
                cfg.DebugMode = msg.DebugMode;
                cfg.ComponentPriceModifiers = CloneComponentPriceModifiers(msg.ComponentPriceModifiers);
                cfg.ForbiddenComponents = NormalizeForbiddenComponentList(msg.ForbiddenComponents, false);
                cfg.ZoneEntityId = zone.EntityId;
                cfg.ZoneCreationType = DetectZoneCreationType(zone);
                cfg.WasManuallyEdited = true;
                NormalizeZoneConfig(cfg);
                zoneConfigs[zone.EntityId] = cfg;
                SaveConfig(new List<SafeZoneConfig>(zoneConfigs.Values));

                long playerZoneEntityIdAfter;
                Vector3D playerPos;
                var playerZone = TryGetPlayerPosition(player, out playerPos) ? GetSafeZoneForPosition(playerPos) : null;
                playerZoneEntityIdAfter = playerZone != null ? playerZone.EntityId : 0L;
                SendAdminZoneConfigStateToPlayer(player, true, "Zone settings applied.", zone, cfg, zone.EntityId, playerZoneEntityIdAfter);
            }
            catch (Exception ex)
            {
                LogError($"HandleAdminZoneConfigUpdate error: {ex}");
            }
        }

        private void HandleAdminZoneConfigState(byte[] data)
        {
            try
            {
                var msg = MyAPIGateway.Utilities.SerializeFromBinary<AdminZoneConfigStateMessage>(data);
                if (msg == null)
                    return;

                if (msg.PlayerId != 0 && msg.PlayerId != MyAPIGateway.Session?.LocalHumanPlayer?.IdentityId)
                    return;

                if (msg.ZoneEntries == null)
                    msg.ZoneEntries = new List<AdminZoneListEntryMessage>();
                if (msg.ComponentPriceModifiers == null)
                    msg.ComponentPriceModifiers = new List<ComponentPriceModifierEntry>();
                if (msg.ForbiddenComponents == null)
                    msg.ForbiddenComponents = new List<string>();
                _adminZoneState = msg;
                SyncAdminForbiddenComponentsFromState();
                MarkAdminPanelDirty();
                if (_adminPriceModsPanelRequested)
                    SyncAdminPriceModifiersFromState();
                if (!msg.Success)
                {
                    _adminPanelRequested = false;
                    _adminPriceModsPanelRequested = false;
                    RefreshUiCursorState();
                }

                UpdateAdminPanelState();
                UpdateAdminPriceModsPanelState();
            }
            catch (Exception ex)
            {
                LogError($"HandleAdminZoneConfigState error: {ex}");
            }
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
                else if (msg.Action == "Rescan")
                    ForceRescanGrid(grid);
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

