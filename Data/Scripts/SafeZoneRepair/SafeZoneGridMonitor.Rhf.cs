using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.Definitions;
using RichHudFramework.Client;
using RichHudFramework.UI;
using VRageMath;
using VRage.Utils;

namespace SafeZoneRepair
{
    public partial class SafeZoneGridMonitor
    {
        private static bool _richHudInitialized;
        private static bool _richHudReady;

        private static BorderBox _panel;
        private static TexturedBox _backgroundBox;
        private static Label _titleLabel;
        private static Label _zoneLabel;
        private static Label _modeLabel;
        private static Label _statusLabel;
        private static Label _currentScanLabel;
        private static Label _currentRepairLabel;
        private static Label _phaseLabel;
        private static Label _repairLabel;
        private static Label _hintLabel;
        private static BorderedButton _toggleRepairButton;
        private static BorderedButton _forceRescanButton;
        private static BorderedButton _closeMenuButton;

        private static BorderBox _adminPanel;
        private static Label _adminTitleLabel;
        private static Label _adminZonesListLabel;
        private static Label _adminZoneLabel;
        private static Label _adminStatusLabel;
        private static Label _adminNameLabel;
        private static Label _adminEnabledLabel;
        private static Label _adminSpeedLabel;
        private static Label _adminCostLabel;
        private static Label _adminProjectionSpeedLabel;
        private static Label _adminProjLabel;
        private static Label _adminDebugModeLabel;
        private static Label _adminDebugOutputLabel;
        private static Label _adminDebugTextLabel;
        private static TextField _adminZoneNameField;
        private static TextField _adminWeldingSpeedField;
        private static TextField _adminCostModifierField;
        private static TextField _adminProjectionSpeedField;
        private static BorderedButton[] _adminZoneSelectButtons;
        private static BorderedButton _adminZonePrevButton;
        private static BorderedButton _adminZoneNextButton;
        private static BorderedButton _adminToggleEnabledButton;
        private static BorderedButton _adminToggleProjectionsButton;
        private static BorderedButton _adminApplyButton;
        private static BorderedButton _adminLoadConfigButton;
        private static BorderedButton _adminComponentsButton;
        private static BorderedButton _adminCloseButton;
        private static BorderedButton _adminSpeedMinusButton;
        private static BorderedButton _adminSpeedPlusButton;
        private static BorderedButton _adminCostMinusButton;
        private static BorderedButton _adminCostPlusButton;
        private static BorderedButton _adminProjectionSpeedMinusButton;
        private static BorderedButton _adminProjectionSpeedPlusButton;
        private static BorderedButton _adminToggleDebugModeButton;
        private static BorderedButton _adminOpenPriceModsButton;
        private static Label _adminComponentsLegendLabel;
        private static Label _adminComponentsPageLabel;
        private static Label[] _adminComponentRowLabels;
        private static BorderedButton[] _adminComponentRowButtons;
        private static BorderedButton _adminComponentsPrevButton;
        private static BorderedButton _adminComponentsNextButton;
        private static BorderedButton _adminComponentsApplyButton;
        private static BorderedButton _adminComponentsBackButton;
        private static bool _adminPanelFieldsDirty = true;
        private static int _adminZoneListPage = 0;

        private sealed class AdminComponentCatalogEntry
        {
            public string SubtypeId;
            public string DisplayName;
        }

        private static BorderBox _adminPriceModsPanel;
        private static Label _adminPriceModsTitleLabel;
        private static Label _adminPriceModsZoneLabel;
        private static Label _adminPriceModsSummaryLabel;
        private static Label[] _adminPriceModNameLabels;
        private static Label[] _adminPriceModValueLabels;
        private static BorderedButton[] _adminPriceModMinusButtons;
        private static BorderedButton[] _adminPriceModPlusButtons;
        private static BorderedButton[] _adminPriceModResetButtons;
        private static BorderedButton _adminPriceModsPrevButton;
        private static BorderedButton _adminPriceModsNextButton;
        private static BorderedButton _adminPriceModsApplyButton;
        private static BorderedButton _adminPriceModsBackButton;
        private static BorderedButton _adminPriceModsResetAllButton;
        private static bool _adminPriceModsPanelRequested = false;
        private static bool _adminPriceModsDirty = true;
        private static int _adminPriceModsPage = 0;
        private const int AdminPriceModsPageSize = 7;
        private static readonly List<AdminComponentCatalogEntry> _adminComponentCatalog = new List<AdminComponentCatalogEntry>();
        private static readonly Dictionary<string, float> _adminWorkingComponentPriceModifiers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private static string _stickyLastRepairText;
        private static DateTime _stickyLastRepairUntil = DateTime.MinValue;
        private const double StickyLastRepairSeconds = 12.0;

        private void InitRichHud()
        {
            if (_richHudInitialized)
                return;

            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
                return;

            _richHudInitialized = true;
            RhfLog("InitRichHud called");
            RichHudClient.Init(DebugName, HudInit, ClientReset);
        }

        private void HudInit()
        {
            _richHudReady = true;
            RhfLog("HudInit called");
            EnsureHudCreated();
            EnsureAdminPanelCreated();
            EnsureAdminPriceModsPanelCreated();
            EnsureRhfBindingsAndTerminal();

            if (_panel != null)
                _panel.Visible = false;
            if (_adminPanel != null)
                _adminPanel.Visible = false;
            if (_adminPriceModsPanel != null)
                _adminPriceModsPanel.Visible = false;

            SetHudLines(
                "ZERO's Safe Zone Repair",
                "Zone: -",
                "Repair mode: -",
                "Status: Waiting for zone state",
                "Current scan: -",
                "Current repair: -",
                "Repair phase: idle",
                "Estimated cost: 0 SC",
                "Last repair: No repairs performed yet."
            );
        }

        private void RhfLog(string text)
        {
            try
            {
                MyLog.Default.WriteLineAndConsole("[SafeZoneRepair:RHF] " + text);
            }
            catch
            {
            }
        }

        private void ClientReset()
        {
            _richHudReady = false;

            if (_panel != null)
            {
                _panel.Unregister();
                _panel = null;
            }

            _backgroundBox = null;
            _titleLabel = null;
            _zoneLabel = null;
            _modeLabel = null;
            _statusLabel = null;
            _currentScanLabel = null;
            _currentRepairLabel = null;
            _phaseLabel = null;
            _repairLabel = null;
            _hintLabel = null;
            _toggleRepairButton = null;
            _forceRescanButton = null;
            _closeMenuButton = null;
            _adminPanel = null;
            _adminTitleLabel = null;
            _adminZonesListLabel = null;
            _adminZoneLabel = null;
            _adminStatusLabel = null;
            _adminNameLabel = null;
            _adminEnabledLabel = null;
            _adminSpeedLabel = null;
            _adminCostLabel = null;
            _adminProjectionSpeedLabel = null;
            _adminProjLabel = null;
            _adminDebugModeLabel = null;
            _adminDebugOutputLabel = null;
            _adminDebugTextLabel = null;
            _adminZoneNameField = null;
            _adminZoneSelectButtons = null;
            _adminZonePrevButton = null;
            _adminZoneNextButton = null;
            _adminWeldingSpeedField = null;
            _adminCostModifierField = null;
            _adminProjectionSpeedField = null;
            _adminToggleEnabledButton = null;
            _adminToggleProjectionsButton = null;
            _adminApplyButton = null;
            _adminLoadConfigButton = null;
            _adminComponentsButton = null;
            _adminCloseButton = null;
            _adminSpeedMinusButton = null;
            _adminSpeedPlusButton = null;
            _adminCostMinusButton = null;
            _adminCostPlusButton = null;
            _adminProjectionSpeedMinusButton = null;
            _adminProjectionSpeedPlusButton = null;
            _adminToggleDebugModeButton = null;
            _adminOpenPriceModsButton = null;
            _adminComponentsLegendLabel = null;
            _adminComponentsPageLabel = null;
            _adminComponentRowLabels = null;
            _adminComponentRowButtons = null;
            _adminComponentsPrevButton = null;
            _adminComponentsNextButton = null;
            _adminComponentsApplyButton = null;
            _adminComponentsBackButton = null;
            _adminPriceModsPanel = null;
            _adminPriceModsTitleLabel = null;
            _adminPriceModsZoneLabel = null;
            _adminPriceModsSummaryLabel = null;
            _adminPriceModNameLabels = null;
            _adminPriceModValueLabels = null;
            _adminPriceModMinusButtons = null;
            _adminPriceModPlusButtons = null;
            _adminPriceModResetButtons = null;
            _adminPriceModsPrevButton = null;
            _adminPriceModsNextButton = null;
            _adminPriceModsApplyButton = null;
            _adminPriceModsBackButton = null;
            _adminPriceModsResetAllButton = null;
            _adminPanel = null;
            _adminTitleLabel = null;
            _adminZonesListLabel = null;
            _adminZoneLabel = null;
            _adminStatusLabel = null;
            _adminNameLabel = null;
            _adminEnabledLabel = null;
            _adminSpeedLabel = null;
            _adminCostLabel = null;
            _adminProjectionSpeedLabel = null;
            _adminProjLabel = null;
            _adminDebugModeLabel = null;
            _adminDebugOutputLabel = null;
            _adminDebugTextLabel = null;
            _adminZoneNameField = null;
            _adminWeldingSpeedField = null;
            _adminCostModifierField = null;
            _adminProjectionSpeedField = null;
            _adminToggleEnabledButton = null;
            _adminToggleProjectionsButton = null;
            _adminApplyButton = null;
            _adminLoadConfigButton = null;
            _adminComponentsButton = null;
            _adminCloseButton = null;
            _toggleRepairButton = null;
            _closeMenuButton = null;
            _adminZonesListLabel = null;
            _adminZoneSelectButtons = null;
            _adminZonePrevButton = null;
            _adminZoneNextButton = null;
            _adminZoneListPage = 0;

            _adminPriceModsPanelRequested = false;
            _adminPriceModsDirty = true;
            _adminPriceModsPage = 0;
            _adminWorkingComponentPriceModifiers.Clear();
            _adminComponentsViewRequested = false;
            _stickyLastRepairText = null;
            _stickyLastRepairUntil = DateTime.MinValue;
        }

        private void ResetRichHud()
        {
            _richHudInitialized = false;
            _richHudReady = false;

            if (_panel != null)
            {
                _panel.Unregister();
                _panel = null;
            }

            _backgroundBox = null;
            _titleLabel = null;
            _zoneLabel = null;
            _modeLabel = null;
            _statusLabel = null;
            _currentScanLabel = null;
            _currentRepairLabel = null;
            _phaseLabel = null;
            _repairLabel = null;
            _hintLabel = null;
            _toggleRepairButton = null;
            _forceRescanButton = null;
            _closeMenuButton = null;
            _adminZonesListLabel = null;
            _adminZoneSelectButtons = null;
            _adminZonePrevButton = null;
            _adminZoneNextButton = null;
            _adminZoneListPage = 0;
            _adminPriceModsPanel = null;
            _adminPriceModsPanelRequested = false;
            _adminPriceModsDirty = true;
            _adminPriceModsPage = 0;
            _adminWorkingComponentPriceModifiers.Clear();

            _stickyLastRepairText = null;
            _stickyLastRepairUntil = DateTime.MinValue;
        }

        private void EnsureHudCreated()
        {
            if (_panel != null)
                return;

            _panel = new BorderBox(RichHudFramework.UI.Client.HudMain.HighDpiRoot)
            {
                ParentAlignment = ParentAlignments.InnerTopRight,
                Offset = new Vector2(-210f, -15f),
                Size = new Vector2(560f, 336f),
                Color = new Color(110, 140, 170, 210),
                Visible = true
            };

            _backgroundBox = new TexturedBox(_panel)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = new Vector2(0f, 0f),
                Size = new Vector2(560f, 336f),
                Color = new Color(0, 0, 0, 185),
                Visible = true
            };

            _titleLabel = CreateLabel(new Vector2(22f, -5f), new Vector2(516f, 26f), 1.03f);
            _zoneLabel = CreateLabel(new Vector2(22f, -27f), new Vector2(516f, 22f), 0.86f);
            _modeLabel = CreateLabel(new Vector2(22f, -61f), new Vector2(516f, 22f), 0.88f);
            _statusLabel = CreateLabel(new Vector2(22f, -89f), new Vector2(516f, 22f), 0.88f);
            _currentScanLabel = CreateLabel(new Vector2(22f, -117f), new Vector2(516f, 22f), 0.80f);
            _currentRepairLabel = CreateLabel(new Vector2(22f, -145f), new Vector2(516f, 28f), 0.80f, TextBuilderModes.Wrapped);
            _phaseLabel = CreateLabel(new Vector2(22f, -173f), new Vector2(516f, 22f), 0.78f);
            _repairLabel = CreateLabel(new Vector2(22f, -199f), new Vector2(516f, 24f), 0.84f);
            _hintLabel = CreateLabel(new Vector2(22f, -227f), new Vector2(516f, 42f), 0.76f, TextBuilderModes.Wrapped);

            _toggleRepairButton = CreateMenuButton(new Vector2(22f, -278f), new Vector2(158f, 40f), "Repair OFF");
            _forceRescanButton = CreateMenuButton(new Vector2(190f, -278f), new Vector2(158f, 40f), "Rescan");
            _closeMenuButton = CreateMenuButton(new Vector2(358f, -278f), new Vector2(158f, 40f), "Close");

            if (_toggleRepairButton != null)
                _toggleRepairButton.MouseInput.LeftClicked += ToggleRepairButtonClicked;

            if (_forceRescanButton != null)
                _forceRescanButton.MouseInput.LeftClicked += ForceRescanButtonClicked;

            if (_closeMenuButton != null)
                _closeMenuButton.MouseInput.LeftClicked += CloseMenuButtonClicked;

            SetInteractiveMenuVisible(false, false);

            RhfLog("HUD multilabel panel created");
        }

        private Label CreateLabel(Vector2 offset, Vector2 size, float textSize, TextBuilderModes builderMode = TextBuilderModes.Lined)
        {
            return new Label(_panel)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = offset,
                Size = size,
                AutoResize = false,
                VertCenterText = false,
                BuilderMode = builderMode,
                Format = new GlyphFormat(Color.White, TextAlignment.Left, textSize)
            };
        }

        private BorderedButton CreateMenuButton(Vector2 offset, Vector2 size, string text)
        {
            var button = new BorderedButton(_panel)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = offset,
                Size = size,
                Text = text,
                Visible = false
            };

            button.Format = new GlyphFormat(Color.White, TextAlignment.Center, 0.80f);
            button.Color = new Color(24, 40, 54, 230);
            button.HighlightColor = new Color(70, 110, 145, 230);
            button.FocusColor = new Color(120, 180, 210, 230);
            button.BorderColor = new Color(110, 140, 170, 230);
            button.BorderThickness = 1f;

            return button;
        }

        private void EnsureAdminPanelCreated()
        {
            if (_adminPanel != null)
                return;

            _adminPanel = new BorderBox(RichHudFramework.UI.Client.HudMain.HighDpiRoot)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = new Vector2(20f, -20f),
                Size = new Vector2(890f, 720f),
                Color = new Color(8, 14, 20, 225),
                Visible = false
            };

            _adminTitleLabel = CreateAdminLabel(new Vector2(18f, -14f), new Vector2(850f, 24f), 1.0f);
            _adminZonesListLabel = CreateAdminLabel(new Vector2(18f, -48f), new Vector2(236f, 22f), 0.82f);
            _adminZoneLabel = CreateAdminLabel(new Vector2(280f, -48f), new Vector2(570f, 20f), 0.82f);
            _adminStatusLabel = CreateAdminLabel(new Vector2(280f, -74f), new Vector2(570f, 40f), 0.74f, TextBuilderModes.Wrapped);
            _adminNameLabel = CreateAdminLabel(new Vector2(280f, -132f), new Vector2(150f, 24f), 0.80f);
            _adminEnabledLabel = CreateAdminLabel(new Vector2(280f, -184f), new Vector2(150f, 24f), 0.80f);
            _adminSpeedLabel = CreateAdminLabel(new Vector2(280f, -236f), new Vector2(150f, 24f), 0.80f);
            _adminCostLabel = CreateAdminLabel(new Vector2(280f, -288f), new Vector2(150f, 24f), 0.80f);
            _adminProjectionSpeedLabel = CreateAdminLabel(new Vector2(280f, -340f), new Vector2(150f, 24f), 0.80f);
            _adminProjLabel = CreateAdminLabel(new Vector2(280f, -392f), new Vector2(150f, 24f), 0.80f);
            _adminDebugModeLabel = CreateAdminLabel(new Vector2(280f, -444f), new Vector2(150f, 24f), 0.80f);
            _adminDebugOutputLabel = CreateAdminLabel(new Vector2(280f, -548f), new Vector2(200f, 22f), 0.76f);
            _adminDebugTextLabel = CreateAdminLabel(new Vector2(280f, -574f), new Vector2(500f, 104f), 0.64f, TextBuilderModes.Wrapped);

            _adminZoneNameField = CreateAdminTextField(new Vector2(440f, -126f), new Vector2(390f, 36f));
            _adminWeldingSpeedField = CreateAdminTextField(new Vector2(440f, -230f), new Vector2(124f, 36f));
            _adminCostModifierField = CreateAdminTextField(new Vector2(440f, -282f), new Vector2(124f, 36f));
            _adminProjectionSpeedField = CreateAdminTextField(new Vector2(440f, -334f), new Vector2(124f, 36f));

            _adminToggleEnabledButton = CreateAdminButton(new Vector2(440f, -178f), new Vector2(156f, 40f), "Toggle");
            _adminToggleProjectionsButton = CreateAdminButton(new Vector2(440f, -386f), new Vector2(156f, 40f), "Toggle");
            _adminToggleDebugModeButton = CreateAdminButton(new Vector2(440f, -438f), new Vector2(156f, 40f), "Toggle");
            _adminSpeedMinusButton = CreateAdminButton(new Vector2(610f, -230f), new Vector2(64f, 36f), "-");
            _adminSpeedPlusButton = CreateAdminButton(new Vector2(682f, -230f), new Vector2(64f, 36f), "+");
            _adminCostMinusButton = CreateAdminButton(new Vector2(610f, -282f), new Vector2(64f, 36f), "-");
            _adminCostPlusButton = CreateAdminButton(new Vector2(682f, -282f), new Vector2(64f, 36f), "+");
            _adminProjectionSpeedMinusButton = CreateAdminButton(new Vector2(610f, -334f), new Vector2(64f, 36f), "-");
            _adminProjectionSpeedPlusButton = CreateAdminButton(new Vector2(682f, -334f), new Vector2(64f, 36f), "+");
            _adminApplyButton = CreateAdminButton(new Vector2(280f, -494f), new Vector2(118f, 40f), "Apply");
            _adminLoadConfigButton = CreateAdminButton(new Vector2(412f, -494f), new Vector2(118f, 40f), "Load");
            _adminComponentsButton = CreateAdminButton(new Vector2(544f, -494f), new Vector2(118f, 40f), "Comps");
            _adminOpenPriceModsButton = CreateAdminButton(new Vector2(674f, -494f), new Vector2(118f, 40f), "Prices");
            _adminCloseButton = CreateAdminButton(new Vector2(778f, -494f), new Vector2(94f, 40f), "Close");

            _adminComponentsLegendLabel = CreateAdminLabel(new Vector2(280f, -132f), new Vector2(560f, 42f), 0.74f, TextBuilderModes.Wrapped);
            _adminComponentsPageLabel = CreateAdminLabel(new Vector2(280f, -500f), new Vector2(560f, 22f), 0.74f);
            _adminComponentRowLabels = new Label[AdminComponentsPageSize];
            _adminComponentRowButtons = new BorderedButton[AdminComponentsPageSize];
            for (int i = 0; i < AdminComponentsPageSize; i++)
            {
                float rowY = -182f - (i * 36f);
                _adminComponentRowLabels[i] = CreateAdminLabel(new Vector2(280f, rowY), new Vector2(404f, 30f), 0.72f, TextBuilderModes.Wrapped);
                _adminComponentRowButtons[i] = CreateAdminButton(new Vector2(696f, rowY - 2f), new Vector2(144f, 32f), "Allowed");
            }
            _adminComponentsPrevButton = CreateAdminButton(new Vector2(280f, -540f), new Vector2(104f, 40f), "Prev");
            _adminComponentsNextButton = CreateAdminButton(new Vector2(396f, -540f), new Vector2(104f, 40f), "Next");
            _adminComponentsApplyButton = CreateAdminButton(new Vector2(618f, -540f), new Vector2(108f, 40f), "Apply");
            _adminComponentsBackButton = CreateAdminButton(new Vector2(738f, -540f), new Vector2(102f, 40f), "Back");

            _adminZonePrevButton = CreateAdminButton(new Vector2(18f, -298f), new Vector2(108f, 36f), "Prev");
            _adminZoneNextButton = CreateAdminButton(new Vector2(146f, -298f), new Vector2(108f, 36f), "Next");
            _adminZoneSelectButtons = new BorderedButton[AdminZoneListPageSize];
            for (int i = 0; i < AdminZoneListPageSize; i++)
            {
                float y = -84f - (i * 42f);
                _adminZoneSelectButtons[i] = CreateAdminButton(new Vector2(18f, y), new Vector2(236f, 38f), "-");
                if (_adminZoneSelectButtons[i] != null)
                    _adminZoneSelectButtons[i].Format = new GlyphFormat(Color.White, TextAlignment.Left, 0.68f);
            }

            if (_adminZoneNameField != null)
                _adminZoneNameField.CharFilterFunc = ch => ch >= 32 && ch < 127;
            if (_adminWeldingSpeedField != null)
                _adminWeldingSpeedField.CharFilterFunc = ch => char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-';
            if (_adminCostModifierField != null)
                _adminCostModifierField.CharFilterFunc = ch => char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-';
            if (_adminProjectionSpeedField != null)
                _adminProjectionSpeedField.CharFilterFunc = ch => char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-';

            if (_adminZonePrevButton != null)
                _adminZonePrevButton.MouseInput.LeftClicked += AdminZonePrevClicked;
            if (_adminZoneNextButton != null)
                _adminZoneNextButton.MouseInput.LeftClicked += AdminZoneNextClicked;
            if (_adminZoneSelectButtons != null)
            {
                for (int i = 0; i < _adminZoneSelectButtons.Length; i++)
                {
                    int slot = i;
                    if (_adminZoneSelectButtons[i] != null)
                        _adminZoneSelectButtons[i].MouseInput.LeftClicked += (sender, args) => AdminZoneSelectClicked(slot);
                }
            }
            if (_adminToggleEnabledButton != null)
                _adminToggleEnabledButton.MouseInput.LeftClicked += AdminToggleEnabledClicked;
            if (_adminToggleProjectionsButton != null)
                _adminToggleProjectionsButton.MouseInput.LeftClicked += AdminToggleProjectionsClicked;
            if (_adminToggleDebugModeButton != null)
                _adminToggleDebugModeButton.MouseInput.LeftClicked += AdminToggleDebugModeClicked;
            if (_adminSpeedMinusButton != null)
                _adminSpeedMinusButton.MouseInput.LeftClicked += AdminSpeedMinusClicked;
            if (_adminSpeedPlusButton != null)
                _adminSpeedPlusButton.MouseInput.LeftClicked += AdminSpeedPlusClicked;
            if (_adminCostMinusButton != null)
                _adminCostMinusButton.MouseInput.LeftClicked += AdminCostMinusClicked;
            if (_adminCostPlusButton != null)
                _adminCostPlusButton.MouseInput.LeftClicked += AdminCostPlusClicked;
            if (_adminProjectionSpeedMinusButton != null)
                _adminProjectionSpeedMinusButton.MouseInput.LeftClicked += AdminProjectionSpeedMinusClicked;
            if (_adminProjectionSpeedPlusButton != null)
                _adminProjectionSpeedPlusButton.MouseInput.LeftClicked += AdminProjectionSpeedPlusClicked;
            if (_adminApplyButton != null)
                _adminApplyButton.MouseInput.LeftClicked += AdminApplyClicked;
            if (_adminLoadConfigButton != null)
                _adminLoadConfigButton.MouseInput.LeftClicked += AdminLoadConfigClicked;
            if (_adminComponentsButton != null)
                _adminComponentsButton.MouseInput.LeftClicked += AdminComponentsClicked;
            if (_adminCloseButton != null)
                _adminCloseButton.MouseInput.LeftClicked += AdminCloseClicked;
            if (_adminOpenPriceModsButton != null)
                _adminOpenPriceModsButton.MouseInput.LeftClicked += AdminOpenPriceModsClicked;
            if (_adminComponentsPrevButton != null)
                _adminComponentsPrevButton.MouseInput.LeftClicked += AdminComponentsPrevClicked;
            if (_adminComponentsNextButton != null)
                _adminComponentsNextButton.MouseInput.LeftClicked += AdminComponentsNextClicked;
            if (_adminComponentsApplyButton != null)
                _adminComponentsApplyButton.MouseInput.LeftClicked += AdminComponentsApplyClicked;
            if (_adminComponentsBackButton != null)
                _adminComponentsBackButton.MouseInput.LeftClicked += AdminComponentsBackClicked;
            if (_adminComponentRowButtons != null)
            {
                for (int i = 0; i < _adminComponentRowButtons.Length; i++)
                {
                    if (_adminComponentRowButtons[i] != null)
                        _adminComponentRowButtons[i].MouseInput.LeftClicked += AdminComponentToggleClicked;
                }
            }

            _adminPanelFieldsDirty = true;
            UpdateAdminPanelState();
        }

        private Label CreateAdminLabel(Vector2 offset, Vector2 size, float textSize, TextBuilderModes builderMode = TextBuilderModes.Lined)
        {
            return new Label(_adminPanel)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = offset,
                Size = size,
                AutoResize = false,
                VertCenterText = false,
                BuilderMode = builderMode,
                Format = new GlyphFormat(new Color(220, 230, 240), TextAlignment.Left, textSize)
            };
        }

        private TextField CreateAdminTextField(Vector2 offset, Vector2 size)
        {
            var field = new TextField(_adminPanel)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = offset,
                Size = size,
                EnableEditing = true,
                AutoResize = false
            };
            field.Format = new GlyphFormat(Color.White, TextAlignment.Left, 0.78f);
            field.Color = new Color(24, 40, 54, 230);
            field.BorderColor = new Color(110, 140, 170, 230);
            return field;
        }

        private BorderedButton CreateAdminButton(Vector2 offset, Vector2 size, string text)
        {
            var button = new BorderedButton(_adminPanel)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = offset,
                Size = size,
                Text = text,
                Visible = true
            };
            button.Format = new GlyphFormat(Color.White, TextAlignment.Center, 0.78f);
            button.Color = new Color(24, 40, 54, 230);
            button.HighlightColor = new Color(70, 110, 145, 230);
            button.FocusColor = new Color(120, 180, 210, 230);
            button.BorderColor = new Color(110, 140, 170, 230);
            button.BorderThickness = 1f;
            return button;
        }

        private void EnsureAdminComponentCatalogBuilt()
        {
            if (_adminComponentCatalog.Count > 0)
                return;

            try
            {
                var defs = MyDefinitionManager.Static?.GetAllDefinitions();
                if (defs == null)
                    return;

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var def in defs)
                {
                    var compDef = def as MyComponentDefinition;
                    if (compDef == null)
                        continue;

                    string subtype = compDef.Id.SubtypeId.String;
                    if (string.IsNullOrWhiteSpace(subtype) || !seen.Add(subtype))
                        continue;

                    string displayName = compDef.DisplayNameText;
                    if (string.IsNullOrWhiteSpace(displayName))
                        displayName = subtype;

                    _adminComponentCatalog.Add(new AdminComponentCatalogEntry
                    {
                        SubtypeId = subtype,
                        DisplayName = displayName
                    });
                }

                _adminComponentCatalog.Sort((a, b) =>
                {
                    int cmp = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0)
                        return cmp;
                    return string.Compare(a.SubtypeId, b.SubtypeId, StringComparison.OrdinalIgnoreCase);
                });
            }
            catch (Exception ex)
            {
                LogError("EnsureAdminComponentCatalogBuilt error: " + ex);
            }
        }

        private static float NormalizeAdminModifierValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                value = 0f;

            return (float)Math.Round(value, 2);
        }

        private static int CountModifiedAdminComponentPriceModifiers()
        {
            int count = 0;
            foreach (var pair in _adminWorkingComponentPriceModifiers)
            {
                if (Math.Abs(pair.Value - 1f) > 0.0001f)
                    count++;
            }
            return count;
        }

        private static float GetWorkingAdminComponentModifier(string subtypeId)
        {
            float value;
            if (!string.IsNullOrWhiteSpace(subtypeId) && _adminWorkingComponentPriceModifiers.TryGetValue(subtypeId, out value))
                return NormalizeAdminModifierValue(value);
            return 1f;
        }

        private static void SetWorkingAdminComponentModifier(string subtypeId, float value)
        {
            if (string.IsNullOrWhiteSpace(subtypeId))
                return;

            value = NormalizeAdminModifierValue(value);
            if (Math.Abs(value - 1f) <= 0.0001f)
                _adminWorkingComponentPriceModifiers.Remove(subtypeId);
            else
                _adminWorkingComponentPriceModifiers[subtypeId] = value;

            _adminPriceModsDirty = true;
        }

        private static List<ComponentPriceModifierEntry> BuildAdminComponentPriceModifierListForSend()
        {
            var list = new List<ComponentPriceModifierEntry>();
            foreach (var pair in _adminWorkingComponentPriceModifiers)
            {
                string key = pair.Key;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                float value = NormalizeAdminModifierValue(pair.Value);
                if (Math.Abs(value - 1f) <= 0.0001f)
                    continue;

                list.Add(new ComponentPriceModifierEntry
                {
                    ComponentSubtypeId = key,
                    Multiplier = value
                });
            }

            list.Sort((a, b) => string.Compare(a.ComponentSubtypeId, b.ComponentSubtypeId, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private void SyncAdminPriceModifiersFromState()
        {
            _adminWorkingComponentPriceModifiers.Clear();
            if (_adminZoneState != null && _adminZoneState.ComponentPriceModifiers != null)
            {
                foreach (var entry in _adminZoneState.ComponentPriceModifiers)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.ComponentSubtypeId))
                        continue;

                    float value = NormalizeAdminModifierValue(entry.Multiplier);
                    if (Math.Abs(value - 1f) <= 0.0001f)
                        continue;

                    _adminWorkingComponentPriceModifiers[entry.ComponentSubtypeId.Trim()] = value;
                }
            }

            _adminPriceModsPage = 0;
            _adminPriceModsDirty = true;
        }

        private Label CreatePriceModsLabel(Vector2 offset, Vector2 size, float textSize, TextBuilderModes builderMode = TextBuilderModes.Lined)
        {
            return new Label(_adminPriceModsPanel)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = offset,
                Size = size,
                AutoResize = false,
                VertCenterText = false,
                BuilderMode = builderMode,
                Format = new GlyphFormat(new Color(220, 230, 240), TextAlignment.Left, textSize)
            };
        }

        private BorderedButton CreatePriceModsButton(Vector2 offset, Vector2 size, string text)
        {
            var button = new BorderedButton(_adminPriceModsPanel)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = offset,
                Size = size,
                Text = text,
                Visible = true
            };
            button.Format = new GlyphFormat(Color.White, TextAlignment.Center, 0.76f);
            button.Color = new Color(24, 40, 54, 230);
            button.HighlightColor = new Color(70, 110, 145, 230);
            button.FocusColor = new Color(120, 180, 210, 230);
            button.BorderColor = new Color(110, 140, 170, 230);
            button.BorderThickness = 1f;
            return button;
        }

        private void EnsureAdminPriceModsPanelCreated()
        {
            if (_adminPriceModsPanel != null)
                return;

            EnsureAdminComponentCatalogBuilt();

            _adminPriceModsPanel = new BorderBox(RichHudFramework.UI.Client.HudMain.HighDpiRoot)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = new Vector2(40f, -30f),
                Size = new Vector2(960f, 664f),
                Color = new Color(8, 14, 20, 225),
                Visible = false
            };

            _adminPriceModsTitleLabel = CreatePriceModsLabel(new Vector2(18f, -14f), new Vector2(920f, 24f), 1.0f);
            _adminPriceModsZoneLabel = CreatePriceModsLabel(new Vector2(18f, -46f), new Vector2(920f, 22f), 0.82f);
            _adminPriceModsSummaryLabel = CreatePriceModsLabel(new Vector2(18f, -74f), new Vector2(920f, 22f), 0.74f);

            _adminPriceModNameLabels = new Label[AdminPriceModsPageSize];
            _adminPriceModValueLabels = new Label[AdminPriceModsPageSize];
            _adminPriceModMinusButtons = new BorderedButton[AdminPriceModsPageSize];
            _adminPriceModPlusButtons = new BorderedButton[AdminPriceModsPageSize];
            _adminPriceModResetButtons = new BorderedButton[AdminPriceModsPageSize];
            for (int i = 0; i < AdminPriceModsPageSize; i++)
            {
                float y = -118f - (i * 58f);
                _adminPriceModNameLabels[i] = CreatePriceModsLabel(new Vector2(18f, y), new Vector2(446f, 42f), 0.76f, TextBuilderModes.Wrapped);
                _adminPriceModValueLabels[i] = CreatePriceModsLabel(new Vector2(472f, y + 6f), new Vector2(90f, 24f), 0.78f);
                _adminPriceModMinusButtons[i] = CreatePriceModsButton(new Vector2(570f, y), new Vector2(92f, 36f), "-10%");
                _adminPriceModPlusButtons[i] = CreatePriceModsButton(new Vector2(670f, y), new Vector2(92f, 36f), "+10%");
                _adminPriceModResetButtons[i] = CreatePriceModsButton(new Vector2(770f, y), new Vector2(120f, 36f), "Reset");

                int slot = i;
                if (_adminPriceModMinusButtons[i] != null)
                    _adminPriceModMinusButtons[i].MouseInput.LeftClicked += (sender, args) => AdminPriceModAdjustClicked(slot, -0.10f);
                if (_adminPriceModPlusButtons[i] != null)
                    _adminPriceModPlusButtons[i].MouseInput.LeftClicked += (sender, args) => AdminPriceModAdjustClicked(slot, 0.10f);
                if (_adminPriceModResetButtons[i] != null)
                    _adminPriceModResetButtons[i].MouseInput.LeftClicked += (sender, args) => AdminPriceModResetClicked(slot);
            }

            _adminPriceModsPrevButton = CreatePriceModsButton(new Vector2(18f, -568f), new Vector2(108f, 40f), "Prev");
            _adminPriceModsNextButton = CreatePriceModsButton(new Vector2(136f, -568f), new Vector2(108f, 40f), "Next");
            _adminPriceModsResetAllButton = CreatePriceModsButton(new Vector2(558f, -568f), new Vector2(136f, 40f), "Reset all");
            _adminPriceModsApplyButton = CreatePriceModsButton(new Vector2(708f, -568f), new Vector2(108f, 40f), "Apply");
            _adminPriceModsBackButton = CreatePriceModsButton(new Vector2(828f, -568f), new Vector2(108f, 40f), "Back");

            if (_adminPriceModsPrevButton != null)
                _adminPriceModsPrevButton.MouseInput.LeftClicked += AdminPriceModsPrevClicked;
            if (_adminPriceModsNextButton != null)
                _adminPriceModsNextButton.MouseInput.LeftClicked += AdminPriceModsNextClicked;
            if (_adminPriceModsResetAllButton != null)
                _adminPriceModsResetAllButton.MouseInput.LeftClicked += AdminPriceModsResetAllClicked;
            if (_adminPriceModsApplyButton != null)
                _adminPriceModsApplyButton.MouseInput.LeftClicked += AdminPriceModsApplyClicked;
            if (_adminPriceModsBackButton != null)
                _adminPriceModsBackButton.MouseInput.LeftClicked += AdminPriceModsBackClicked;

            UpdateAdminPriceModsPanelState();
        }

        private void UpdateAdminPriceModsPanelState()
        {
            if (_adminPriceModsPanel == null)
                return;

            bool visible = _adminPanelRequested && _adminPriceModsPanelRequested;
            _adminPriceModsPanel.Visible = visible;
            if (!visible)
                return;

            EnsureAdminComponentCatalogBuilt();

            if (_adminPriceModsTitleLabel != null)
                _adminPriceModsTitleLabel.Text = "ZERO's Price Modifiers";
            if (_adminPriceModsZoneLabel != null)
            {
                string zoneName = _adminZoneState != null && !string.IsNullOrWhiteSpace(_adminZoneState.ZoneName) ? _adminZoneState.ZoneName : "-";
                _adminPriceModsZoneLabel.Text = "Selected zone: " + zoneName + " | Default = 100%";
            }

            int totalPages = Math.Max(1, (_adminComponentCatalog.Count + AdminPriceModsPageSize - 1) / AdminPriceModsPageSize);
            if (_adminPriceModsPage >= totalPages)
                _adminPriceModsPage = totalPages - 1;
            if (_adminPriceModsPage < 0)
                _adminPriceModsPage = 0;

            int start = _adminComponentCatalog.Count == 0 ? 0 : (_adminPriceModsPage * AdminPriceModsPageSize) + 1;
            int end = Math.Min(_adminComponentCatalog.Count, (_adminPriceModsPage + 1) * AdminPriceModsPageSize);
            if (_adminPriceModsSummaryLabel != null)
                _adminPriceModsSummaryLabel.Text = string.Format("Components {0}-{1}/{2} | Modified: {3}", start, end, _adminComponentCatalog.Count, CountModifiedAdminComponentPriceModifiers());

            for (int i = 0; i < AdminPriceModsPageSize; i++)
            {
                int index = (_adminPriceModsPage * AdminPriceModsPageSize) + i;
                bool hasEntry = index >= 0 && index < _adminComponentCatalog.Count;
                if (!hasEntry)
                {
                    if (_adminPriceModNameLabels != null && _adminPriceModNameLabels[i] != null)
                        _adminPriceModNameLabels[i].Text = string.Empty;
                    if (_adminPriceModValueLabels != null && _adminPriceModValueLabels[i] != null)
                        _adminPriceModValueLabels[i].Text = string.Empty;
                    if (_adminPriceModMinusButtons != null && _adminPriceModMinusButtons[i] != null)
                        _adminPriceModMinusButtons[i].Visible = false;
                    if (_adminPriceModPlusButtons != null && _adminPriceModPlusButtons[i] != null)
                        _adminPriceModPlusButtons[i].Visible = false;
                    if (_adminPriceModResetButtons != null && _adminPriceModResetButtons[i] != null)
                        _adminPriceModResetButtons[i].Visible = false;
                    continue;
                }

                var entry = _adminComponentCatalog[index];
                float value = GetWorkingAdminComponentModifier(entry.SubtypeId);
                bool modified = Math.Abs(value - 1f) > 0.0001f;
                if (_adminPriceModNameLabels != null && _adminPriceModNameLabels[i] != null)
                    _adminPriceModNameLabels[i].Text = TruncateText(entry.DisplayName + "\n" + entry.SubtypeId, 52);
                if (_adminPriceModValueLabels != null && _adminPriceModValueLabels[i] != null)
                {
                    _adminPriceModValueLabels[i].Text = string.Format("{0:0}%", value * 100f);
                    _adminPriceModValueLabels[i].Format = new GlyphFormat(modified ? new Color(255, 210, 120) : new Color(220, 230, 240), TextAlignment.Left, 0.78f);
                }
                if (_adminPriceModMinusButtons != null && _adminPriceModMinusButtons[i] != null)
                    _adminPriceModMinusButtons[i].Visible = true;
                if (_adminPriceModPlusButtons != null && _adminPriceModPlusButtons[i] != null)
                    _adminPriceModPlusButtons[i].Visible = true;
                if (_adminPriceModResetButtons != null && _adminPriceModResetButtons[i] != null)
                {
                    _adminPriceModResetButtons[i].Visible = true;
                    _adminPriceModResetButtons[i].Text = modified ? "Reset" : "Default";
                }
            }

            if (_adminPriceModsPrevButton != null)
                _adminPriceModsPrevButton.Visible = _adminComponentCatalog.Count > AdminPriceModsPageSize;
            if (_adminPriceModsNextButton != null)
                _adminPriceModsNextButton.Visible = _adminComponentCatalog.Count > AdminPriceModsPageSize;
        }

        private void AdminOpenPriceModsClicked(object sender, EventArgs e)
        {
            EnsureAdminPriceModsPanelCreated();
            SyncAdminPriceModifiersFromState();
            _adminPriceModsPanelRequested = true;
            RefreshUiCursorState();
            UpdateAdminPanelState();
            UpdateAdminPriceModsPanelState();
        }

        private void AdminPriceModsBackClicked(object sender, EventArgs e)
        {
            _adminPriceModsPanelRequested = false;
            RefreshUiCursorState();
            UpdateAdminPanelState();
            UpdateAdminPriceModsPanelState();
        }

        private void AdminPriceModsPrevClicked(object sender, EventArgs e)
        {
            if (_adminPriceModsPage > 0)
            {
                _adminPriceModsPage--;
                UpdateAdminPriceModsPanelState();
            }
        }

        private void AdminPriceModsNextClicked(object sender, EventArgs e)
        {
            int totalPages = Math.Max(1, (_adminComponentCatalog.Count + AdminPriceModsPageSize - 1) / AdminPriceModsPageSize);
            if (_adminPriceModsPage < totalPages - 1)
            {
                _adminPriceModsPage++;
                UpdateAdminPriceModsPanelState();
            }
        }

        private void AdminPriceModAdjustClicked(int slot, float delta)
        {
            int index = (_adminPriceModsPage * AdminPriceModsPageSize) + slot;
            if (index < 0 || index >= _adminComponentCatalog.Count)
                return;

            var entry = _adminComponentCatalog[index];
            float value = GetWorkingAdminComponentModifier(entry.SubtypeId);
            SetWorkingAdminComponentModifier(entry.SubtypeId, value + delta);
            UpdateAdminPriceModsPanelState();
        }

        private void AdminPriceModResetClicked(int slot)
        {
            int index = (_adminPriceModsPage * AdminPriceModsPageSize) + slot;
            if (index < 0 || index >= _adminComponentCatalog.Count)
                return;

            var entry = _adminComponentCatalog[index];
            SetWorkingAdminComponentModifier(entry.SubtypeId, 1f);
            UpdateAdminPriceModsPanelState();
        }

        private void AdminPriceModsResetAllClicked(object sender, EventArgs e)
        {
            _adminWorkingComponentPriceModifiers.Clear();
            _adminPriceModsDirty = true;
            UpdateAdminPriceModsPanelState();
        }

        private void AdminPriceModsApplyClicked(object sender, EventArgs e)
        {
            try
            {
                float speed;
                float cost;
                float projectionSpeed;
                if (!TryParseAdminFloat(_adminWeldingSpeedField, out speed) || !TryParseAdminFloat(_adminCostModifierField, out cost) || !TryParseAdminFloat(_adminProjectionSpeedField, out projectionSpeed))
                {
                    _adminZoneState.Success = false;
                    _adminZoneState.ErrorText = "Values must be >= 0.001";
                    UpdateAdminPriceModsPanelState();
                    return;
                }

                string zoneName = _adminZoneNameField?.Text.ToString() ?? _adminZoneState.ZoneName ?? string.Empty;
                SendAdminZoneConfigUpdateFromClient(zoneName, _adminZoneState.Enabled, NormalizeAdminFloat(speed), NormalizeAdminFloat(cost), _adminZoneState.AllowProjections, NormalizeAdminFloat(projectionSpeed), _adminZoneState.DebugMode, GetAdminForbiddenComponentsSnapshot(), BuildAdminComponentPriceModifierListForSend());
            }
            catch (Exception ex)
            {
                LogError("AdminPriceModsApplyClicked error: " + ex);
            }
        }


        private void SetAdminMainControlsVisible(bool visible)
        {
            if (_adminZoneLabel != null)
                _adminZoneLabel.Visible = visible;
            if (_adminStatusLabel != null)
                _adminStatusLabel.Visible = visible;
            if (_adminNameLabel != null)
                _adminNameLabel.Visible = visible;
            if (_adminEnabledLabel != null)
                _adminEnabledLabel.Visible = visible;
            if (_adminSpeedLabel != null)
                _adminSpeedLabel.Visible = visible;
            if (_adminCostLabel != null)
                _adminCostLabel.Visible = visible;
            if (_adminProjectionSpeedLabel != null)
                _adminProjectionSpeedLabel.Visible = visible;
            if (_adminProjLabel != null)
                _adminProjLabel.Visible = visible;
            if (_adminDebugModeLabel != null)
                _adminDebugModeLabel.Visible = visible;
            if (_adminDebugOutputLabel != null)
                _adminDebugOutputLabel.Visible = visible;
            if (_adminDebugTextLabel != null)
                _adminDebugTextLabel.Visible = visible;
            if (_adminZoneNameField != null)
                _adminZoneNameField.Visible = visible;
            if (_adminWeldingSpeedField != null)
                _adminWeldingSpeedField.Visible = visible;
            if (_adminCostModifierField != null)
                _adminCostModifierField.Visible = visible;
            if (_adminProjectionSpeedField != null)
                _adminProjectionSpeedField.Visible = visible;
            if (_adminToggleEnabledButton != null)
                _adminToggleEnabledButton.Visible = visible;
            if (_adminToggleProjectionsButton != null)
                _adminToggleProjectionsButton.Visible = visible;
            if (_adminToggleDebugModeButton != null)
                _adminToggleDebugModeButton.Visible = visible;
            if (_adminSpeedMinusButton != null)
                _adminSpeedMinusButton.Visible = visible;
            if (_adminSpeedPlusButton != null)
                _adminSpeedPlusButton.Visible = visible;
            if (_adminCostMinusButton != null)
                _adminCostMinusButton.Visible = visible;
            if (_adminCostPlusButton != null)
                _adminCostPlusButton.Visible = visible;
            if (_adminProjectionSpeedMinusButton != null)
                _adminProjectionSpeedMinusButton.Visible = visible;
            if (_adminProjectionSpeedPlusButton != null)
                _adminProjectionSpeedPlusButton.Visible = visible;
            if (_adminApplyButton != null)
                _adminApplyButton.Visible = visible;
            if (_adminLoadConfigButton != null)
                _adminLoadConfigButton.Visible = visible;
            if (_adminComponentsButton != null)
                _adminComponentsButton.Visible = visible;
            if (_adminOpenPriceModsButton != null)
                _adminOpenPriceModsButton.Visible = visible;
        }

        private void SetAdminComponentsControlsVisible(bool visible)
        {
            if (_adminComponentsLegendLabel != null)
                _adminComponentsLegendLabel.Visible = visible;
            if (_adminComponentsPageLabel != null)
                _adminComponentsPageLabel.Visible = visible;
            if (_adminComponentsPrevButton != null)
                _adminComponentsPrevButton.Visible = visible;
            if (_adminComponentsNextButton != null)
                _adminComponentsNextButton.Visible = visible;
            if (_adminComponentsApplyButton != null)
                _adminComponentsApplyButton.Visible = visible;
            if (_adminComponentsBackButton != null)
                _adminComponentsBackButton.Visible = visible;
            if (_adminComponentRowLabels != null)
                for (int i = 0; i < _adminComponentRowLabels.Length; i++)
                    if (_adminComponentRowLabels[i] != null)
                        _adminComponentRowLabels[i].Visible = visible;
            if (_adminComponentRowButtons != null)
                for (int i = 0; i < _adminComponentRowButtons.Length; i++)
                    if (_adminComponentRowButtons[i] != null)
                        _adminComponentRowButtons[i].Visible = visible;
        }

        private System.Collections.Generic.List<AdminComponentCatalogEntry> GetAdminComponentPageEntries()
        {
            var entries = new System.Collections.Generic.List<AdminComponentCatalogEntry>();
            if (_adminComponentCatalog == null || _adminComponentCatalog.Count == 0)
                return entries;

            ClampAdminComponentsScrollOffset();
            int startIndex = Math.Max(0, _adminComponentsScrollOffset);
            if (startIndex >= _adminComponentCatalog.Count)
                startIndex = 0;

            int endIndex = Math.Min(startIndex + AdminComponentsPageSize, _adminComponentCatalog.Count);
            for (int i = startIndex; i < endIndex; i++)
                entries.Add(_adminComponentCatalog[i]);
            return entries;
        }

        private void UpdateAdminComponentsPage()
        {
            EnsureAdminComponentCatalogBuilt();
            if (_adminComponentCatalog.Count == 0)
            {
                if (_adminComponentsLegendLabel != null)
                    _adminComponentsLegendLabel.Text = "No component definitions found on this client.";
                if (_adminComponentsPageLabel != null)
                    _adminComponentsPageLabel.Text = string.Empty;
                SetAdminComponentsControlsVisible(true);
                return;
            }

            ClampAdminComponentsScrollOffset();
            int visibleCount = Math.Min(AdminComponentsPageSize, _adminComponentCatalog.Count);
            int firstVisibleIndex = Math.Min(_adminComponentsScrollOffset + 1, _adminComponentCatalog.Count);
            int lastVisibleIndex = Math.Min(_adminComponentsScrollOffset + visibleCount, _adminComponentCatalog.Count);
            int pageIndex = (_adminComponentsScrollOffset / AdminComponentsPageSize) + 1;
            int totalPages = Math.Max(1, (_adminComponentCatalog.Count + AdminComponentsPageSize - 1) / AdminComponentsPageSize);

            if (_adminTitleLabel != null)
                _adminTitleLabel.Text = "ZERO's Components List";
            if (_adminStatusLabel != null)
                _adminStatusLabel.Text = "Checked = allowed. Unchecked = forbidden. Changes stay local until Apply.";
            if (_adminComponentsLegendLabel != null)
                _adminComponentsLegendLabel.Text = "Wheel: scroll list. Zone forbidden list is saved per safe zone. Prototech components are added to new configs by default.";
            if (_adminComponentsPageLabel != null)
                _adminComponentsPageLabel.Text = string.Format("Rows {0}-{1}/{2} | Page {3}/{4} | Forbidden: {5}", firstVisibleIndex, lastVisibleIndex, _adminComponentCatalog.Count, pageIndex, totalPages, _adminForbiddenComponentsLocal.Count);

            var pageEntries = GetAdminComponentPageEntries();
            for (int i = 0; i < AdminComponentsPageSize; i++)
            {
                var label = _adminComponentRowLabels != null && i < _adminComponentRowLabels.Length ? _adminComponentRowLabels[i] : null;
                var button = _adminComponentRowButtons != null && i < _adminComponentRowButtons.Length ? _adminComponentRowButtons[i] : null;
                if (i >= pageEntries.Count)
                {
                    if (label != null) label.Visible = false;
                    if (button != null) button.Visible = false;
                    continue;
                }
                var entry = pageEntries[i];
                bool allowed = !_adminForbiddenComponentsLocal.Contains(entry.SubtypeId);
                if (label != null)
                {
                    label.Text = string.Format("{0}  [{1}]", entry.DisplayName, entry.SubtypeId);
                    label.Visible = true;
                }
                if (button != null)
                {
                    button.Text = allowed ? "Allowed" : "Forbid";
                    button.Visible = true;
                }
            }
        }

        private void AdminComponentsClicked(object sender, EventArgs e)
        {
            EnsureAdminComponentCatalogBuilt();
            SyncAdminForbiddenComponentsFromState();
            _adminComponentsViewRequested = true;
            _adminComponentsScrollOffset = 0;
            UpdateAdminPanelState();
        }

        private void AdminComponentToggleClicked(object sender, EventArgs e)
        {
            if (_adminComponentRowButtons == null)
                return;
            int rowIndex = -1;
            for (int i = 0; i < _adminComponentRowButtons.Length; i++)
            {
                if (ReferenceEquals(sender, _adminComponentRowButtons[i]))
                {
                    rowIndex = i;
                    break;
                }
            }
            if (rowIndex < 0)
                return;
            int componentIndex = _adminComponentsScrollOffset + rowIndex;
            if (componentIndex < 0 || componentIndex >= _adminComponentCatalog.Count)
                return;
            string subtypeId = _adminComponentCatalog[componentIndex].SubtypeId;
            if (_adminForbiddenComponentsLocal.Contains(subtypeId))
                _adminForbiddenComponentsLocal.Remove(subtypeId);
            else
                _adminForbiddenComponentsLocal.Add(subtypeId);
            UpdateAdminPanelState();
        }

        private void AdminComponentsPrevClicked(object sender, EventArgs e)
        {
            ScrollAdminComponentsByRows(-AdminComponentsPageSize);
        }

        private void AdminComponentsNextClicked(object sender, EventArgs e)
        {
            ScrollAdminComponentsByRows(AdminComponentsPageSize);
        }

        private void AdminComponentsApplyClicked(object sender, EventArgs e)
        {
            try
            {
                float speed;
                float cost;
                float projectionSpeed;
                if (!TryParseAdminFloat(_adminWeldingSpeedField, out speed) || !TryParseAdminFloat(_adminCostModifierField, out cost) || !TryParseAdminFloat(_adminProjectionSpeedField, out projectionSpeed))
                {
                    _adminZoneState.Success = false;
                    _adminZoneState.ErrorText = "Values must be >= 0.001";
                    UpdateAdminPanelState();
                    return;
                }
                string zoneName = _adminZoneNameField?.Text.ToString() ?? _adminZoneState.ZoneName ?? string.Empty;
                SendAdminZoneConfigUpdateFromClient(zoneName, _adminZoneState.Enabled, NormalizeAdminFloat(speed), NormalizeAdminFloat(cost), _adminZoneState.AllowProjections, NormalizeAdminFloat(projectionSpeed), _adminZoneState.DebugMode, GetAdminForbiddenComponentsSnapshot(), BuildAdminComponentPriceModifierListForSend());
            }
            catch (Exception ex)
            {
                LogError("AdminComponentsApplyClicked error: " + ex);
            }
        }

        private void AdminComponentsBackClicked(object sender, EventArgs e)
        {
            _adminComponentsViewRequested = false;
            MarkAdminPanelDirty();
            UpdateAdminPanelState();
        }

        private void MarkAdminPanelDirty()
        {
            _adminPanelFieldsDirty = true;
        }

        private void UpdateAdminZoneListButtons(AdminZoneConfigStateMessage state)
        {
            var entries = state.ZoneEntries ?? new System.Collections.Generic.List<AdminZoneListEntryMessage>();
            int totalPages = Math.Max(1, (entries.Count + AdminZoneListPageSize - 1) / AdminZoneListPageSize);
            if (_adminZoneListPage >= totalPages)
                _adminZoneListPage = totalPages - 1;
            if (_adminZoneListPage < 0)
                _adminZoneListPage = 0;

            int selectedIndex = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].ZoneEntityId == state.SelectedZoneEntityId)
                {
                    selectedIndex = i;
                    break;
                }
            }
            if (selectedIndex >= 0)
            {
                int pageStart = _adminZoneListPage * AdminZoneListPageSize;
                int pageEnd = pageStart + AdminZoneListPageSize;
                if (selectedIndex < pageStart || selectedIndex >= pageEnd)
                    _adminZoneListPage = selectedIndex / AdminZoneListPageSize;
            }

            if (_adminZonesListLabel != null)
            {
                int start = entries.Count == 0 ? 0 : (_adminZoneListPage * AdminZoneListPageSize) + 1;
                int end = Math.Min(entries.Count, (_adminZoneListPage + 1) * AdminZoneListPageSize);
                _adminZonesListLabel.Text = string.Format("Zones {0}-{1}/{2}", start, end, entries.Count);
            }

            if (_adminZoneSelectButtons != null)
            {
                for (int i = 0; i < _adminZoneSelectButtons.Length; i++)
                {
                    var button = _adminZoneSelectButtons[i];
                    if (button == null)
                        continue;

                    int index = (_adminZoneListPage * AdminZoneListPageSize) + i;
                    if (index < entries.Count)
                    {
                        var entry = entries[index];
                        string prefix = entry.ZoneEntityId == state.SelectedZoneEntityId ? "> " : string.Empty;
                        if (entry.IsPlayerInside)
                            prefix += "[Here] ";
                        button.Text = TruncateText(prefix + (entry.ZoneName ?? "Unnamed zone"), 26);
                        bool isSelected = entry.ZoneEntityId == state.SelectedZoneEntityId;
                        button.Color = isSelected ? new Color(52, 86, 110, 230) : new Color(24, 40, 54, 230);
                        button.BorderColor = isSelected ? new Color(150, 210, 240, 230) : new Color(110, 140, 170, 230);
                        button.Visible = true;
                    }
                    else
                    {
                        button.Text = "-";
                        button.Visible = false;
                    }
                }
            }

            if (_adminZonePrevButton != null)
                _adminZonePrevButton.Visible = entries.Count > AdminZoneListPageSize;
            if (_adminZoneNextButton != null)
                _adminZoneNextButton.Visible = entries.Count > AdminZoneListPageSize;
        }

        private void UpdateAdminPanelState()
        {
            if (_adminPanel == null)
                return;

            bool visible = _adminPanelRequested && !_adminPriceModsPanelRequested;
            _adminPanel.Visible = visible;
            if (!visible)
            {
                SetAdminComponentsControlsVisible(false);
                UpdateAdminPriceModsPanelState();
                return;
            }

            var state = _adminZoneState ?? new AdminZoneConfigStateMessage();
            if (state.ZoneEntries == null)
                state.ZoneEntries = new System.Collections.Generic.List<AdminZoneListEntryMessage>();

            if (_adminComponentsViewRequested)
            {
                SetAdminMainControlsVisible(false);
                SetAdminComponentsControlsVisible(true);
                if (_adminCloseButton != null)
                    _adminCloseButton.Visible = false;
                UpdateAdminComponentsPage();
                UpdateAdminPriceModsPanelState();
                return;
            }

            SetAdminMainControlsVisible(true);
            SetAdminComponentsControlsVisible(false);

            if (_adminTitleLabel != null)
                _adminTitleLabel.Text = "ZERO's Safe Zone Admin";
            if (_adminZoneLabel != null)
            {
                string zoneText = string.IsNullOrWhiteSpace(state.ZoneName) ? "Selected zone: -" : "Selected zone: " + state.ZoneName;
                string typeText = string.IsNullOrWhiteSpace(state.ZoneCreationType) ? string.Empty : " | Type: " + (string.Equals(state.ZoneCreationType, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Block");
                _adminZoneLabel.Text = zoneText + typeText;
            }
            if (_adminStatusLabel != null)
                _adminStatusLabel.Text = state.Success
                    ? "Select any zone on the left to edit it remotely. Apply saves the selected zone. Components opens the allowed/forbidden list."
                    : (string.IsNullOrWhiteSpace(state.ErrorText) ? "Admin panel unavailable." : state.ErrorText);
            if (_adminNameLabel != null)
                _adminNameLabel.Text = "Zone name";
            if (_adminEnabledLabel != null)
                _adminEnabledLabel.Text = "Repair enabled";
            if (_adminSpeedLabel != null)
                _adminSpeedLabel.Text = "Welding speed";
            if (_adminCostLabel != null)
                _adminCostLabel.Text = "Cost modifier";
            if (_adminProjectionSpeedLabel != null)
                _adminProjectionSpeedLabel.Text = "Projection speed";
            if (_adminProjLabel != null)
                _adminProjLabel.Text = "Allow projections";
            if (_adminDebugModeLabel != null)
                _adminDebugModeLabel.Text = "Debug mode";
            if (_adminDebugOutputLabel != null)
                _adminDebugOutputLabel.Text = "Debug output";

            UpdateAdminZoneListButtons(state);

            if (_adminPanelFieldsDirty)
            {
                if (_adminZoneNameField != null && !_adminZoneNameField.InputOpen)
                    _adminZoneNameField.Text = state.ZoneName ?? string.Empty;
                if (_adminWeldingSpeedField != null && !_adminWeldingSpeedField.InputOpen)
                    _adminWeldingSpeedField.Text = state.WeldingSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                if (_adminCostModifierField != null && !_adminCostModifierField.InputOpen)
                    _adminCostModifierField.Text = state.CostModifier.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                if (_adminProjectionSpeedField != null && !_adminProjectionSpeedField.InputOpen)
                    _adminProjectionSpeedField.Text = state.ProjectionWeldingSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                _adminPanelFieldsDirty = false;
            }
            if (_adminToggleEnabledButton != null)
                _adminToggleEnabledButton.Text = state.Enabled ? "Enabled" : "Disabled";
            if (_adminToggleProjectionsButton != null)
                _adminToggleProjectionsButton.Text = state.AllowProjections ? "Allowed" : "Blocked";
            if (_adminToggleDebugModeButton != null)
                _adminToggleDebugModeButton.Text = state.DebugMode ? "Enabled" : "Disabled";
            if (_adminDebugTextLabel != null)
                _adminDebugTextLabel.Text = string.IsNullOrWhiteSpace(state.DebugText) ? (state.DebugMode ? "Debug snapshot is empty." : "Debug mode is OFF.") : state.DebugText;
            if (_adminComponentsButton != null)
                _adminComponentsButton.Text = "Comps";
            if (_adminOpenPriceModsButton != null)
                _adminOpenPriceModsButton.Text = "Prices";

            UpdateAdminPriceModsPanelState();
        }

        private void AdminZonePrevClicked(object sender, EventArgs e)
        {
            if (_adminZoneListPage > 0)
            {
                _adminZoneListPage--;
                UpdateAdminPanelState();
            }
        }

        private void AdminZoneNextClicked(object sender, EventArgs e)
        {
            var entries = _adminZoneState != null && _adminZoneState.ZoneEntries != null
                ? _adminZoneState.ZoneEntries.Count
                : 0;
            int totalPages = Math.Max(1, (entries + AdminZoneListPageSize - 1) / AdminZoneListPageSize);
            if (_adminZoneListPage < totalPages - 1)
            {
                _adminZoneListPage++;
                UpdateAdminPanelState();
            }
        }

        private void AdminZoneSelectClicked(int slot)
        {
            try
            {
                if (_adminZoneState == null || _adminZoneState.ZoneEntries == null)
                    return;

                int index = (_adminZoneListPage * AdminZoneListPageSize) + slot;
                if (index < 0 || index >= _adminZoneState.ZoneEntries.Count)
                    return;

                var entry = _adminZoneState.ZoneEntries[index];
                if (entry == null || entry.ZoneEntityId == 0)
                    return;

                MarkAdminPanelDirty();
                RequestAdminZoneConfig(false, entry.ZoneEntityId);
            }
            catch (Exception ex)
            {
                LogError("AdminZoneSelectClicked error: " + ex);
            }
        }

        private bool TryParseAdminFloat(TextField field, out float value)
        {
            value = 0f;
            if (field == null)
                return false;

            string text = field.Text.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim().Replace(',', '.');
            if (!float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                return false;

            if (value < 0.001f)
                return false;

            value = NormalizeAdminFloat(value);
            return true;
        }

        private float NormalizeAdminFloat(float value)
        {
            return (float)Math.Round(Math.Max(0.001f, value), 2);
        }

        private void SetAdminFloatField(TextField field, float value)
        {
            if (field != null)
                field.Text = NormalizeAdminFloat(value).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void AdjustAdminFloatField(TextField field, float delta)
        {
            float value;
            if (!TryParseAdminFloat(field, out value))
            {
                if (field == _adminCostModifierField)
                    value = _adminZoneState.CostModifier;
                else if (field == _adminProjectionSpeedField)
                    value = _adminZoneState.ProjectionWeldingSpeed;
                else
                    value = _adminZoneState.WeldingSpeed;
            }

            value = NormalizeAdminFloat(value + delta);
            SetAdminFloatField(field, value);
        }

        private void AdminToggleEnabledClicked(object sender, EventArgs e)
        {
            _adminZoneState.Enabled = !_adminZoneState.Enabled;
            UpdateAdminPanelState();
        }

        private void AdminToggleProjectionsClicked(object sender, EventArgs e)
        {
            _adminZoneState.AllowProjections = !_adminZoneState.AllowProjections;
            UpdateAdminPanelState();
        }

        private void AdminToggleDebugModeClicked(object sender, EventArgs e)
        {
            _adminZoneState.DebugMode = !_adminZoneState.DebugMode;
            UpdateAdminPanelState();
        }

        private void AdminSpeedMinusClicked(object sender, EventArgs e)
        {
            AdjustAdminFloatField(_adminWeldingSpeedField, -0.1f);
        }

        private void AdminSpeedPlusClicked(object sender, EventArgs e)
        {
            AdjustAdminFloatField(_adminWeldingSpeedField, 0.1f);
        }

        private void AdminCostMinusClicked(object sender, EventArgs e)
        {
            AdjustAdminFloatField(_adminCostModifierField, -0.1f);
        }

        private void AdminCostPlusClicked(object sender, EventArgs e)
        {
            AdjustAdminFloatField(_adminCostModifierField, 0.1f);
        }

        private void AdminProjectionSpeedMinusClicked(object sender, EventArgs e)
        {
            AdjustAdminFloatField(_adminProjectionSpeedField, -0.1f);
        }

        private void AdminProjectionSpeedPlusClicked(object sender, EventArgs e)
        {
            AdjustAdminFloatField(_adminProjectionSpeedField, 0.1f);
        }

        private void AdminApplyClicked(object sender, EventArgs e)
        {
            try
            {
                float speed;
                float cost;
                float projectionSpeed;
                if (!TryParseAdminFloat(_adminWeldingSpeedField, out speed) || !TryParseAdminFloat(_adminCostModifierField, out cost) || !TryParseAdminFloat(_adminProjectionSpeedField, out projectionSpeed))
                {
                    _adminZoneState.Success = false;
                    _adminZoneState.ErrorText = "Values must be >= 0.001";
                    UpdateAdminPanelState();
                    return;
                }

                speed = NormalizeAdminFloat(speed);
                cost = NormalizeAdminFloat(cost);
                projectionSpeed = NormalizeAdminFloat(projectionSpeed);
                string zoneName = _adminZoneNameField?.Text.ToString() ?? _adminZoneState.ZoneName ?? string.Empty;
                SendAdminZoneConfigUpdateFromClient(zoneName, _adminZoneState.Enabled, speed, cost, _adminZoneState.AllowProjections, projectionSpeed, _adminZoneState.DebugMode, GetAdminForbiddenComponentsSnapshot(), BuildAdminComponentPriceModifierListForSend());
            }
            catch (Exception ex)
            {
                LogError("AdminApplyClicked error: " + ex);
            }
        }

        private void AdminLoadConfigClicked(object sender, EventArgs e)
        {
            MarkAdminPanelDirty();
            RequestAdminZoneConfig(true, _adminZoneState != null ? _adminZoneState.SelectedZoneEntityId : 0L);
        }

        private void AdminCloseClicked(object sender, EventArgs e)
        {
            _adminPanelRequested = false;
            _adminPriceModsPanelRequested = false;
            _adminComponentsViewRequested = false;
            RefreshUiCursorState();
            UpdateAdminPanelState();
            UpdateAdminPriceModsPanelState();
        }

        private void SetInteractiveMenuVisible(bool visible, bool repairEnabled)
        {
            if (_toggleRepairButton != null)
            {
                _toggleRepairButton.Visible = visible;
                _toggleRepairButton.Text = repairEnabled ? "Repair OFF" : "Repair ON";
            }

            if (_forceRescanButton != null)
                _forceRescanButton.Visible = visible;

            if (_closeMenuButton != null)
                _closeMenuButton.Visible = visible;
        }

        private void ToggleRepairButtonClicked(object sender, EventArgs e)
        {
            try
            {
                ToggleRepairForLocalContext();
            }
            catch (Exception ex)
            {
                LogError("ToggleRepairButtonClicked error: " + ex);
            }
        }

        private void ForceRescanButtonClicked(object sender, EventArgs e)
        {
            try
            {
                ForceRescanForLocalContext();
            }
            catch (Exception ex)
            {
                LogError("ForceRescanButtonClicked error: " + ex);
            }
        }

        private void CloseMenuButtonClicked(object sender, EventArgs e)
        {
            try
            {
                _cockpitInteractiveRequested = false;
                if (_clientUiState != null)
                    UpdateRichHudState(_clientUiState);
            }
            catch (Exception ex)
            {
                LogError("CloseMenuButtonClicked error: " + ex);
            }
        }

        private void SetHudLines(string title, string zone, string mode, string status, string currentScan, string currentRepair, string phase, string cost, string repair)
        {
            if (_titleLabel != null)
                _titleLabel.Text = title ?? string.Empty;

            if (_zoneLabel != null)
                _zoneLabel.Text = zone ?? string.Empty;

            if (_modeLabel != null)
                _modeLabel.Text = mode ?? string.Empty;

            if (_statusLabel != null)
                _statusLabel.Text = status ?? string.Empty;

            if (_currentScanLabel != null)
                _currentScanLabel.Text = currentScan ?? string.Empty;

            if (_currentRepairLabel != null)
                _currentRepairLabel.Text = currentRepair ?? string.Empty;

            if (_phaseLabel != null)
                _phaseLabel.Text = phase ?? string.Empty;

            if (_repairLabel != null)
                _repairLabel.Text = cost ?? string.Empty;

            if (_hintLabel != null)
                _hintLabel.Text = repair ?? string.Empty;
        }

        private void UpdateRichHudState(RepairUiStateMessage state)
		{
			if (!_richHudReady || state == null || MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
				return;

			EnsureHudCreated();

			if (!state.InRepairZone)
			{
                _manualHudRequested = false;
                _cockpitHudSuppressed = false;
                _cockpitInteractiveRequested = false;
				HideHud();
				return;
			}

			if (!ShouldShowHudForLocalPlayer())
			{
				HideHud();
				return;
			}

            if (GetLocalControlledShipController() != null && !IsCockpitHudVisible())
            {
                _cockpitInteractiveRequested = false;
                RefreshUiCursorState();
                HideHud();
                return;
            }

			ShowHud();

            bool interactiveMenuVisible = GetLocalControlledShipController() != null && IsCockpitInteractiveHudRequested();
            RefreshUiCursorState();

			string zoneName = string.IsNullOrWhiteSpace(state.ZoneName) ? "Repair Zone" : state.ZoneName.Trim();
			string modeText = state.RepairEnabled ? "Repair mode: ON" : "Repair mode: OFF";
			string statusText = string.IsNullOrWhiteSpace(state.StatusText)
				? "Status: Awaiting status update"
				: "Status: " + state.StatusText.Trim();

			if (!string.IsNullOrWhiteSpace(state.LastRepairText))
			{
				_stickyLastRepairText = state.LastRepairText.Trim();
				_stickyLastRepairUntil = DateTime.UtcNow.AddSeconds(StickyLastRepairSeconds);
			}

			string repairText;
			if (!string.IsNullOrWhiteSpace(_stickyLastRepairText) && DateTime.UtcNow < _stickyLastRepairUntil)
				repairText = "Last repair: " + _stickyLastRepairText;
			else
				repairText = FormatLastRepair(_stickyLastRepairText);

            long estimatedRepairCost = state.EstimatedRepairCost < 0 ? 0 : state.EstimatedRepairCost;
            string currentScanText = string.IsNullOrWhiteSpace(state.CurrentScanText) ? "Current scan: -" : state.CurrentScanText.Trim();
            string currentRepairText = string.IsNullOrWhiteSpace(state.CurrentRepairText) ? "Current repair: -" : state.CurrentRepairText.Trim();
            string phaseText = string.IsNullOrWhiteSpace(state.RepairPhaseText) ? "Repair phase: idle" : state.RepairPhaseText.Trim();
            string costText = string.Format("Estimated cost: {0} SC", estimatedRepairCost);
            if (interactiveMenuVisible)
                repairText = string.Format("{0}\nMenu open | Ctrl+J close | Ctrl+R repair | Ctrl+N hide HUD", repairText);
            else if (GetLocalControlledShipController() != null)
                repairText = string.Format("{0}\nCtrl+J menu | Ctrl+R repair | Ctrl+N hide HUD", repairText);
            else
                repairText = string.Format("{0}\nCtrl+J info | Ctrl+N hide HUD", repairText);

			SetInteractiveMenuVisible(interactiveMenuVisible, state.RepairEnabled);

			SetHudLines(
				"ZERO's Safe Zone Repair",
				"Zone: " + zoneName,
				modeText,
				statusText,
                currentScanText,
                currentRepairText,
                phaseText,
                costText,
                repairText
			);

			if (_modeLabel != null)
			{
				_modeLabel.Format = state.RepairEnabled
					? new GlyphFormat(new Color(120, 220, 120), TextAlignment.Left, 0.88f)
					: new GlyphFormat(new Color(255, 170, 80), TextAlignment.Left, 0.88f);
			}

			if (_statusLabel != null)
			{
				string lower = (state.StatusText ?? string.Empty).ToLowerInvariant();

				if (lower.Contains("projection repair unavailable") || lower.Contains("unavailable in this zone"))
					_statusLabel.Format = new GlyphFormat(new Color(255, 180, 70), TextAlignment.Left, 0.88f);
				else if (lower.Contains("disabled") || lower.Contains("forbidden") || lower.Contains("outside") || lower.Contains("denied"))
					_statusLabel.Format = new GlyphFormat(new Color(255, 140, 140), TextAlignment.Left, 0.88f);
				else if (lower.Contains("enabled") || lower.Contains("entered") || lower.Contains("ready") || lower.Contains("active"))
					_statusLabel.Format = new GlyphFormat(new Color(120, 220, 120), TextAlignment.Left, 0.88f);
				else
					_statusLabel.Format = new GlyphFormat(new Color(210, 225, 240), TextAlignment.Left, 0.88f);
			}

			if (_currentRepairLabel != null)
                _currentRepairLabel.Format = new GlyphFormat(new Color(140, 190, 255), TextAlignment.Left, 0.80f);

			if (_repairLabel != null)
				_repairLabel.Format = new GlyphFormat(new Color(210, 225, 240), TextAlignment.Left, 0.84f);

			if (_hintLabel != null)
                _hintLabel.Format = new GlyphFormat(new Color(210, 225, 240), TextAlignment.Left, 0.78f);
		}

        private void HideHud()
        {
            RefreshUiCursorState();

            if (_panel != null)
                _panel.Visible = false;

            if (_adminPanel != null)
                _adminPanel.Visible = _adminPanelRequested;
        }
		
		private void ShowHud()
        {
            if (_panel != null)
                _panel.Visible = true;
        }
		
        private bool ShouldShowHudForLocalPlayer()
        {
            try
            {
                var shipController = GetLocalControlledShipController();
                return (shipController != null && IsCockpitHudVisible()) || IsManualHudAllowed();
            }
            catch
            {
                return false;
            }
        }
		
		private string TruncateText(string text, int maxLength)
		{
			if (string.IsNullOrWhiteSpace(text))
				return text;

			if (text.Length <= maxLength)
				return text;

			return text.Substring(0, maxLength - 3) + "...";
		}

		private string FormatLastRepair(string blockName)
		{
			if (string.IsNullOrWhiteSpace(blockName))
				return "Last repair: -";

			return $"Last repair: {TruncateText(blockName, 28)}";
		}
    }
}

