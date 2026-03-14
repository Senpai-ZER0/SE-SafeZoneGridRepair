using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
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
        private static Label _adminZoneLabel;
        private static Label _adminStatusLabel;
        private static Label _adminNameLabel;
        private static Label _adminEnabledLabel;
        private static Label _adminSpeedLabel;
        private static Label _adminCostLabel;
        private static Label _adminProjLabel;
        private static TextField _adminZoneNameField;
        private static TextField _adminWeldingSpeedField;
        private static TextField _adminCostModifierField;
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
        private static Label _adminComponentsLegendLabel;
        private static Label _adminComponentsPageLabel;
        private static Label[] _adminComponentRowLabels;
        private static BorderedButton[] _adminComponentRowButtons;
        private static BorderedButton _adminComponentsPrevButton;
        private static BorderedButton _adminComponentsNextButton;
        private static BorderedButton _adminComponentsApplyButton;
        private static BorderedButton _adminComponentsBackButton;
        private static bool _adminPanelFieldsDirty = true;

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
            EnsureRhfBindingsAndTerminal();

            if (_panel != null)
                _panel.Visible = false;
            if (_adminPanel != null)
                _adminPanel.Visible = false;

            SetHudLines(
                "Safe Zone Repair",
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
            _adminZoneLabel = null;
            _adminStatusLabel = null;
            _adminNameLabel = null;
            _adminEnabledLabel = null;
            _adminSpeedLabel = null;
            _adminCostLabel = null;
            _adminProjLabel = null;
            _adminZoneNameField = null;
            _adminWeldingSpeedField = null;
            _adminCostModifierField = null;
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
            _adminComponentsLegendLabel = null;
            _adminComponentsPageLabel = null;
            _adminComponentRowLabels = null;
            _adminComponentRowButtons = null;
            _adminComponentsPrevButton = null;
            _adminComponentsNextButton = null;
            _adminComponentsApplyButton = null;
            _adminComponentsBackButton = null;
            _adminPanel = null;
            _adminTitleLabel = null;
            _adminZoneLabel = null;
            _adminStatusLabel = null;
            _adminNameLabel = null;
            _adminEnabledLabel = null;
            _adminSpeedLabel = null;
            _adminCostLabel = null;
            _adminProjLabel = null;
            _adminZoneNameField = null;
            _adminWeldingSpeedField = null;
            _adminCostModifierField = null;
            _adminToggleEnabledButton = null;
            _adminToggleProjectionsButton = null;
            _adminApplyButton = null;
            _adminLoadConfigButton = null;
            _adminComponentsButton = null;
            _adminCloseButton = null;
            _toggleRepairButton = null;
            _closeMenuButton = null;

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
                Offset = new Vector2(-240f, -15f),
                Size = new Vector2(620f, 350f),
                Color = new Color(110, 140, 170, 210),
                Visible = true
            };

            _backgroundBox = new TexturedBox(_panel)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Offset = new Vector2(0f, 0f),
                Size = new Vector2(620f, 350f),
                Color = new Color(0, 0, 0, 185),
                Visible = true
            };

            _titleLabel = CreateLabel(new Vector2(24f, -5f), new Vector2(560f, 26f), 1.05f);
            _zoneLabel = CreateLabel(new Vector2(24f, -27f), new Vector2(560f, 22f), 0.88f);
            _modeLabel = CreateLabel(new Vector2(24f, -61f), new Vector2(560f, 22f), 0.88f);
            _statusLabel = CreateLabel(new Vector2(24f, -89f), new Vector2(560f, 22f), 0.88f);
            _currentScanLabel = CreateLabel(new Vector2(24f, -117f), new Vector2(560f, 22f), 0.80f);
            _currentRepairLabel = CreateLabel(new Vector2(24f, -145f), new Vector2(560f, 28f), 0.80f, TextBuilderModes.Wrapped);
            _phaseLabel = CreateLabel(new Vector2(24f, -173f), new Vector2(560f, 22f), 0.78f);
            _repairLabel = CreateLabel(new Vector2(24f, -199f), new Vector2(560f, 24f), 0.84f);
            _hintLabel = CreateLabel(new Vector2(24f, -227f), new Vector2(560f, 44f), 0.78f, TextBuilderModes.Wrapped);

            _toggleRepairButton = CreateMenuButton(new Vector2(24f, -282f), new Vector2(160f, 36f), "Toggle Repair");
            _forceRescanButton = CreateMenuButton(new Vector2(202f, -282f), new Vector2(170f, 36f), "Force Rescan");
            _closeMenuButton = CreateMenuButton(new Vector2(390f, -282f), new Vector2(170f, 36f), "Close Menu");

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

            button.Format = new GlyphFormat(Color.White, TextAlignment.Center, 0.78f);
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
                Size = new Vector2(560f, 600f),
                Color = new Color(8, 14, 20, 225),
                Visible = false
            };

            _adminTitleLabel = CreateAdminLabel(new Vector2(18f, -14f), new Vector2(480f, 24f), 1.0f);
            _adminZoneLabel = CreateAdminLabel(new Vector2(18f, -44f), new Vector2(480f, 20f), 0.82f);
            _adminStatusLabel = CreateAdminLabel(new Vector2(18f, -68f), new Vector2(480f, 34f), 0.74f, TextBuilderModes.Wrapped);
            _adminNameLabel = CreateAdminLabel(new Vector2(18f, -118f), new Vector2(140f, 24f), 0.80f);
            _adminEnabledLabel = CreateAdminLabel(new Vector2(18f, -170f), new Vector2(140f, 24f), 0.80f);
            _adminSpeedLabel = CreateAdminLabel(new Vector2(18f, -222f), new Vector2(140f, 24f), 0.80f);
            _adminCostLabel = CreateAdminLabel(new Vector2(18f, -274f), new Vector2(140f, 24f), 0.80f);
            _adminProjLabel = CreateAdminLabel(new Vector2(18f, -326f), new Vector2(140f, 24f), 0.80f);

            _adminZoneNameField = CreateAdminTextField(new Vector2(170f, -112f), new Vector2(320f, 34f));
            _adminWeldingSpeedField = CreateAdminTextField(new Vector2(170f, -216f), new Vector2(120f, 34f));
            _adminCostModifierField = CreateAdminTextField(new Vector2(170f, -268f), new Vector2(120f, 34f));

            _adminToggleEnabledButton = CreateAdminButton(new Vector2(170f, -164f), new Vector2(140f, 36f), "Toggle");
            _adminToggleProjectionsButton = CreateAdminButton(new Vector2(170f, -320f), new Vector2(140f, 36f), "Toggle");
            _adminSpeedMinusButton = CreateAdminButton(new Vector2(302f, -216f), new Vector2(56f, 34f), "-");
            _adminSpeedPlusButton = CreateAdminButton(new Vector2(366f, -216f), new Vector2(56f, 34f), "+");
            _adminCostMinusButton = CreateAdminButton(new Vector2(302f, -268f), new Vector2(56f, 34f), "-");
            _adminCostPlusButton = CreateAdminButton(new Vector2(366f, -268f), new Vector2(56f, 34f), "+");
            _adminApplyButton = CreateAdminButton(new Vector2(18f, -364f), new Vector2(110f, 34f), "Apply");
            _adminLoadConfigButton = CreateAdminButton(new Vector2(136f, -364f), new Vector2(126f, 34f), "Load cfg");
            _adminComponentsButton = CreateAdminButton(new Vector2(270f, -364f), new Vector2(150f, 34f), "Components");
            _adminCloseButton = CreateAdminButton(new Vector2(428f, -364f), new Vector2(110f, 34f), "Close");

            _adminComponentsLegendLabel = CreateAdminLabel(new Vector2(18f, -116f), new Vector2(520f, 42f), 0.74f, TextBuilderModes.Wrapped);
            _adminComponentsPageLabel = CreateAdminLabel(new Vector2(18f, -464f), new Vector2(300f, 22f), 0.74f);
            _adminComponentRowLabels = new Label[AdminComponentsPageSize];
            _adminComponentRowButtons = new BorderedButton[AdminComponentsPageSize];
            for (int i = 0; i < AdminComponentsPageSize; i++)
            {
                float rowY = -156f - (i * 36f);
                _adminComponentRowLabels[i] = CreateAdminLabel(new Vector2(18f, rowY), new Vector2(398f, 28f), 0.74f);
                _adminComponentRowButtons[i] = CreateAdminButton(new Vector2(426f, rowY - 2f), new Vector2(112f, 30f), "Allowed");
                _adminComponentRowLabels[i].Visible = false;
                _adminComponentRowButtons[i].Visible = false;
            }

            _adminComponentsPrevButton = CreateAdminButton(new Vector2(18f, -500f), new Vector2(110f, 34f), "Prev");
            _adminComponentsNextButton = CreateAdminButton(new Vector2(136f, -500f), new Vector2(110f, 34f), "Next");
            _adminComponentsApplyButton = CreateAdminButton(new Vector2(286f, -500f), new Vector2(118f, 34f), "Apply");
            _adminComponentsBackButton = CreateAdminButton(new Vector2(420f, -500f), new Vector2(118f, 34f), "Back");

            if (_adminZoneNameField != null)
                _adminZoneNameField.CharFilterFunc = ch => ch >= 32 && ch < 127;
            if (_adminWeldingSpeedField != null)
                _adminWeldingSpeedField.CharFilterFunc = ch => char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-';
            if (_adminCostModifierField != null)
                _adminCostModifierField.CharFilterFunc = ch => char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-';

            if (_adminToggleEnabledButton != null)
                _adminToggleEnabledButton.MouseInput.LeftClicked += AdminToggleEnabledClicked;
            if (_adminToggleProjectionsButton != null)
                _adminToggleProjectionsButton.MouseInput.LeftClicked += AdminToggleProjectionsClicked;
            if (_adminSpeedMinusButton != null)
                _adminSpeedMinusButton.MouseInput.LeftClicked += AdminSpeedMinusClicked;
            if (_adminSpeedPlusButton != null)
                _adminSpeedPlusButton.MouseInput.LeftClicked += AdminSpeedPlusClicked;
            if (_adminCostMinusButton != null)
                _adminCostMinusButton.MouseInput.LeftClicked += AdminCostMinusClicked;
            if (_adminCostPlusButton != null)
                _adminCostPlusButton.MouseInput.LeftClicked += AdminCostPlusClicked;
            if (_adminApplyButton != null)
                _adminApplyButton.MouseInput.LeftClicked += AdminApplyClicked;
            if (_adminLoadConfigButton != null)
                _adminLoadConfigButton.MouseInput.LeftClicked += AdminLoadConfigClicked;
            if (_adminComponentsButton != null)
                _adminComponentsButton.MouseInput.LeftClicked += AdminComponentsClicked;
            if (_adminCloseButton != null)
                _adminCloseButton.MouseInput.LeftClicked += AdminCloseClicked;
            if (_adminComponentRowButtons != null)
            {
                for (int i = 0; i < _adminComponentRowButtons.Length; i++)
                {
                    if (_adminComponentRowButtons[i] != null)
                        _adminComponentRowButtons[i].MouseInput.LeftClicked += AdminComponentToggleClicked;
                }
            }
            if (_adminComponentsPrevButton != null)
                _adminComponentsPrevButton.MouseInput.LeftClicked += AdminComponentsPrevClicked;
            if (_adminComponentsNextButton != null)
                _adminComponentsNextButton.MouseInput.LeftClicked += AdminComponentsNextClicked;
            if (_adminComponentsApplyButton != null)
                _adminComponentsApplyButton.MouseInput.LeftClicked += AdminComponentsApplyClicked;
            if (_adminComponentsBackButton != null)
                _adminComponentsBackButton.MouseInput.LeftClicked += AdminComponentsBackClicked;

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
            button.Format = new GlyphFormat(Color.White, TextAlignment.Center, 0.72f);
            button.Color = new Color(24, 40, 54, 230);
            button.HighlightColor = new Color(70, 110, 145, 230);
            button.FocusColor = new Color(120, 180, 210, 230);
            button.BorderColor = new Color(110, 140, 170, 230);
            button.BorderThickness = 1f;
            return button;
        }


        private void MarkAdminPanelDirty()
        {
            _adminPanelFieldsDirty = true;
        }

        private void SetAdminMainControlsVisible(bool visible)
        {
            if (_adminNameLabel != null)
                _adminNameLabel.Visible = visible;
            if (_adminEnabledLabel != null)
                _adminEnabledLabel.Visible = visible;
            if (_adminSpeedLabel != null)
                _adminSpeedLabel.Visible = visible;
            if (_adminCostLabel != null)
                _adminCostLabel.Visible = visible;
            if (_adminProjLabel != null)
                _adminProjLabel.Visible = visible;
            if (_adminZoneNameField != null)
                _adminZoneNameField.Visible = visible;
            if (_adminWeldingSpeedField != null)
                _adminWeldingSpeedField.Visible = visible;
            if (_adminCostModifierField != null)
                _adminCostModifierField.Visible = visible;
            if (_adminToggleEnabledButton != null)
                _adminToggleEnabledButton.Visible = visible;
            if (_adminToggleProjectionsButton != null)
                _adminToggleProjectionsButton.Visible = visible;
            if (_adminSpeedMinusButton != null)
                _adminSpeedMinusButton.Visible = visible;
            if (_adminSpeedPlusButton != null)
                _adminSpeedPlusButton.Visible = visible;
            if (_adminCostMinusButton != null)
                _adminCostMinusButton.Visible = visible;
            if (_adminCostPlusButton != null)
                _adminCostPlusButton.Visible = visible;
            if (_adminApplyButton != null)
                _adminApplyButton.Visible = visible;
            if (_adminLoadConfigButton != null)
                _adminLoadConfigButton.Visible = visible;
            if (_adminComponentsButton != null)
                _adminComponentsButton.Visible = visible;
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
            {
                for (int i = 0; i < _adminComponentRowLabels.Length; i++)
                {
                    if (_adminComponentRowLabels[i] != null)
                        _adminComponentRowLabels[i].Visible = visible;
                }
            }

            if (_adminComponentRowButtons != null)
            {
                for (int i = 0; i < _adminComponentRowButtons.Length; i++)
                {
                    if (_adminComponentRowButtons[i] != null)
                        _adminComponentRowButtons[i].Visible = visible;
                }
            }
        }

        private List<AdminComponentCatalogEntry> GetAdminComponentPageEntries()
        {
            var entries = new List<AdminComponentCatalogEntry>();
            if (_adminComponentCatalog == null || _adminComponentCatalog.Count == 0)
                return entries;

            int startIndex = Math.Max(0, _adminComponentsPage) * AdminComponentsPageSize;
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
                if (_adminComponentRowLabels != null)
                {
                    for (int i = 0; i < _adminComponentRowLabels.Length; i++)
                    {
                        if (_adminComponentRowLabels[i] != null)
                            _adminComponentRowLabels[i].Visible = false;
                        if (_adminComponentRowButtons != null && i < _adminComponentRowButtons.Length && _adminComponentRowButtons[i] != null)
                            _adminComponentRowButtons[i].Visible = false;
                    }
                }
                return;
            }

            int totalPages = Math.Max(1, (_adminComponentCatalog.Count + AdminComponentsPageSize - 1) / AdminComponentsPageSize);
            if (_adminComponentsPage >= totalPages)
                _adminComponentsPage = totalPages - 1;
            if (_adminComponentsPage < 0)
                _adminComponentsPage = 0;

            if (_adminTitleLabel != null)
                _adminTitleLabel.Text = "Components List";
            if (_adminStatusLabel != null)
                _adminStatusLabel.Text = "Checked = allowed. Unchecked = forbidden. Changes stay local until Apply.";
            if (_adminComponentsLegendLabel != null)
                _adminComponentsLegendLabel.Text = "Zone forbidden list is saved per safe zone. Prototech components are added to new configs by default.";
            if (_adminComponentsPageLabel != null)
                _adminComponentsPageLabel.Text = string.Format("Page {0}/{1}  |  Components: {2}  |  Forbidden: {3}", _adminComponentsPage + 1, totalPages, _adminComponentCatalog.Count, _adminForbiddenComponentsLocal.Count);
            if (_adminComponentsPrevButton != null)
                _adminComponentsPrevButton.Text = totalPages > 1 ? "Prev" : "Prev";
            if (_adminComponentsNextButton != null)
                _adminComponentsNextButton.Text = totalPages > 1 ? "Next" : "Next";

            var pageEntries = GetAdminComponentPageEntries();
            for (int i = 0; i < AdminComponentsPageSize; i++)
            {
                var label = _adminComponentRowLabels != null && i < _adminComponentRowLabels.Length ? _adminComponentRowLabels[i] : null;
                var button = _adminComponentRowButtons != null && i < _adminComponentRowButtons.Length ? _adminComponentRowButtons[i] : null;
                if (i >= pageEntries.Count)
                {
                    if (label != null)
                        label.Visible = false;
                    if (button != null)
                        button.Visible = false;
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

        private void UpdateAdminPanelState()
        {
            if (_adminPanel == null)
                return;

            bool visible = _adminPanelRequested;
            _adminPanel.Visible = visible;
            if (!visible)
                return;

            var state = _adminZoneState ?? new AdminZoneConfigStateMessage();
            if (_adminZoneLabel != null)
                _adminZoneLabel.Text = string.IsNullOrWhiteSpace(state.ZoneName) ? "Zone: -" : "Zone: " + state.ZoneName;

            if (_adminCloseButton != null)
                _adminCloseButton.Visible = !_adminComponentsViewRequested;

            if (_adminComponentsViewRequested)
            {
                SetAdminMainControlsVisible(false);
                SetAdminComponentsControlsVisible(true);
                UpdateAdminComponentsPage();
                return;
            }

            SetAdminMainControlsVisible(true);
            SetAdminComponentsControlsVisible(false);

            if (_adminTitleLabel != null)
                _adminTitleLabel.Text = "Safe Zone Admin";
            if (_adminStatusLabel != null)
                _adminStatusLabel.Text = state.Success
                    ? "Edit the zone values below, then press Apply. Components opens the allowed/forbidden list for this zone."
                    : (string.IsNullOrWhiteSpace(state.ErrorText) ? "Admin panel unavailable." : state.ErrorText);
            if (_adminNameLabel != null)
                _adminNameLabel.Text = "Zone name";
            if (_adminEnabledLabel != null)
                _adminEnabledLabel.Text = "Repair enabled";
            if (_adminSpeedLabel != null)
                _adminSpeedLabel.Text = "Welding speed";
            if (_adminCostLabel != null)
                _adminCostLabel.Text = "Cost modifier";
            if (_adminProjLabel != null)
                _adminProjLabel.Text = "Allow projections";
            if (_adminPanelFieldsDirty)
            {
                if (_adminZoneNameField != null && !_adminZoneNameField.InputOpen)
                    _adminZoneNameField.Text = state.ZoneName ?? string.Empty;
                if (_adminWeldingSpeedField != null && !_adminWeldingSpeedField.InputOpen)
                    _adminWeldingSpeedField.Text = state.WeldingSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                if (_adminCostModifierField != null && !_adminCostModifierField.InputOpen)
                    _adminCostModifierField.Text = state.CostModifier.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                _adminPanelFieldsDirty = false;
            }
            if (_adminToggleEnabledButton != null)
                _adminToggleEnabledButton.Text = state.Enabled ? "Enabled" : "Disabled";
            if (_adminToggleProjectionsButton != null)
                _adminToggleProjectionsButton.Text = state.AllowProjections ? "Allowed" : "Blocked";
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
                value = field == _adminCostModifierField ? _adminZoneState.CostModifier : _adminZoneState.WeldingSpeed;

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

        private void AdminApplyClicked(object sender, EventArgs e)
        {
            try
            {
                float speed;
                float cost;
                if (!TryParseAdminFloat(_adminWeldingSpeedField, out speed) || !TryParseAdminFloat(_adminCostModifierField, out cost))
                {
                    _adminZoneState.Success = false;
                    _adminZoneState.ErrorText = "Values must be >= 0.001";
                    UpdateAdminPanelState();
                    return;
                }

                speed = NormalizeAdminFloat(speed);
                cost = NormalizeAdminFloat(cost);
                string zoneName = _adminZoneNameField?.Text.ToString() ?? _adminZoneState.ZoneName ?? string.Empty;
                SendAdminZoneConfigUpdateFromClient(zoneName, _adminZoneState.Enabled, speed, cost, _adminZoneState.AllowProjections, GetAdminForbiddenComponentsSnapshot());
            }
            catch (Exception ex)
            {
                LogError("AdminApplyClicked error: " + ex);
            }
        }

        private void AdminLoadConfigClicked(object sender, EventArgs e)
        {
            _adminComponentsViewRequested = false;
            MarkAdminPanelDirty();
            RequestAdminZoneConfig(true);
        }

        private void AdminComponentsClicked(object sender, EventArgs e)
        {
            EnsureAdminComponentCatalogBuilt();
            _adminComponentsViewRequested = true;
            _adminComponentsPage = 0;
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

            int componentIndex = (_adminComponentsPage * AdminComponentsPageSize) + rowIndex;
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
            if (_adminComponentsPage > 0)
                _adminComponentsPage--;
            UpdateAdminPanelState();
        }

        private void AdminComponentsNextClicked(object sender, EventArgs e)
        {
            int totalPages = _adminComponentCatalog.Count > 0 ? (_adminComponentCatalog.Count + AdminComponentsPageSize - 1) / AdminComponentsPageSize : 1;
            if (_adminComponentsPage + 1 < totalPages)
                _adminComponentsPage++;
            UpdateAdminPanelState();
        }

        private void AdminComponentsApplyClicked(object sender, EventArgs e)
        {
            try
            {
                float speed = _adminZoneState != null ? NormalizeAdminFloat(_adminZoneState.WeldingSpeed) : 1f;
                float cost = _adminZoneState != null ? NormalizeAdminFloat(_adminZoneState.CostModifier) : 1f;
                if (_adminWeldingSpeedField != null)
                {
                    float parsedSpeed;
                    if (TryParseAdminFloat(_adminWeldingSpeedField, out parsedSpeed))
                        speed = parsedSpeed;
                }
                if (_adminCostModifierField != null)
                {
                    float parsedCost;
                    if (TryParseAdminFloat(_adminCostModifierField, out parsedCost))
                        cost = parsedCost;
                }
                string zoneName = _adminZoneNameField?.Text.ToString() ?? _adminZoneState?.ZoneName ?? string.Empty;
                SendAdminZoneConfigUpdateFromClient(zoneName, _adminZoneState != null && _adminZoneState.Enabled, speed, cost, _adminZoneState != null && _adminZoneState.AllowProjections, GetAdminForbiddenComponentsSnapshot());
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

        private void AdminCloseClicked(object sender, EventArgs e)
        {
            _adminPanelRequested = false;
            _adminComponentsViewRequested = false;
            RefreshUiCursorState();
            UpdateAdminPanelState();
        }

        private void SetInteractiveMenuVisible(bool visible, bool repairEnabled)
        {
            if (_toggleRepairButton != null)
            {
                _toggleRepairButton.Visible = visible;
                _toggleRepairButton.Text = repairEnabled ? "Disable repair" : "Enable repair";
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
                SetInteractiveCursorEnabled(false);
                HideHud();
                return;
            }

			ShowHud();

            bool interactiveMenuVisible = GetLocalControlledShipController() != null && IsCockpitInteractiveHudRequested();
            bool shouldEnableInteractiveCursor = interactiveMenuVisible || _adminPanelRequested;
            SetInteractiveCursorEnabled(shouldEnableInteractiveCursor);

			string zoneName = string.IsNullOrWhiteSpace(state.ZoneName) ? "Repair Zone" : state.ZoneName.Trim();
			string modeText = state.RepairEnabled ? "Repair mode: enabled" : "Repair mode: disabled";
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
                repairText = string.Format("{0}\nInteractive menu active  |  Ctrl+J: Close menu  |  Ctrl+R: Repair (cockpit)  |  Ctrl+N: Hide HUD", repairText);
            else if (GetLocalControlledShipController() != null)
                repairText = string.Format("{0}\nCtrl+J: Interactive menu  |  Ctrl+R: Repair (cockpit)  |  Ctrl+N: Hide HUD", repairText);
            else
                repairText = string.Format("{0}\nCtrl+J: Info menu  |  Ctrl+N: Hide cockpit HUD", repairText);

			SetInteractiveMenuVisible(interactiveMenuVisible, state.RepairEnabled);

			SetHudLines(
				"Safe Zone Repair",
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

				if (lower.Contains("disabled") || lower.Contains("forbidden") || lower.Contains("outside") || lower.Contains("denied"))
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
            if (!_adminPanelRequested)
                SetInteractiveCursorEnabled(false);

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

