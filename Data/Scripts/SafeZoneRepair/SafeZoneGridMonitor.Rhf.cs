using System;
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
        private static Label _adminProjectionSpeedLabel;
        private static Label _adminProjLabel;
        private static TextField _adminZoneNameField;
        private static TextField _adminWeldingSpeedField;
        private static TextField _adminCostModifierField;
        private static TextField _adminProjectionSpeedField;
        private static BorderedButton _adminToggleEnabledButton;
        private static BorderedButton _adminToggleProjectionsButton;
        private static BorderedButton _adminApplyButton;
        private static BorderedButton _adminLoadConfigButton;
        private static BorderedButton _adminCloseButton;
        private static BorderedButton _adminSpeedMinusButton;
        private static BorderedButton _adminSpeedPlusButton;
        private static BorderedButton _adminCostMinusButton;
        private static BorderedButton _adminCostPlusButton;
        private static BorderedButton _adminProjectionSpeedMinusButton;
        private static BorderedButton _adminProjectionSpeedPlusButton;
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
            _adminZoneLabel = null;
            _adminStatusLabel = null;
            _adminNameLabel = null;
            _adminEnabledLabel = null;
            _adminSpeedLabel = null;
            _adminCostLabel = null;
            _adminProjectionSpeedLabel = null;
            _adminProjLabel = null;
            _adminZoneNameField = null;
            _adminWeldingSpeedField = null;
            _adminCostModifierField = null;
            _adminProjectionSpeedField = null;
            _adminToggleEnabledButton = null;
            _adminToggleProjectionsButton = null;
            _adminApplyButton = null;
            _adminLoadConfigButton = null;
            _adminCloseButton = null;
            _adminSpeedMinusButton = null;
            _adminSpeedPlusButton = null;
            _adminCostMinusButton = null;
            _adminCostPlusButton = null;
            _adminProjectionSpeedMinusButton = null;
            _adminProjectionSpeedPlusButton = null;
            _adminPanel = null;
            _adminTitleLabel = null;
            _adminZoneLabel = null;
            _adminStatusLabel = null;
            _adminNameLabel = null;
            _adminEnabledLabel = null;
            _adminSpeedLabel = null;
            _adminCostLabel = null;
            _adminProjectionSpeedLabel = null;
            _adminProjLabel = null;
            _adminZoneNameField = null;
            _adminWeldingSpeedField = null;
            _adminCostModifierField = null;
            _adminProjectionSpeedField = null;
            _adminToggleEnabledButton = null;
            _adminToggleProjectionsButton = null;
            _adminApplyButton = null;
            _adminLoadConfigButton = null;
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
                Size = new Vector2(520f, 466f),
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
            _adminProjectionSpeedLabel = CreateAdminLabel(new Vector2(18f, -326f), new Vector2(140f, 24f), 0.80f);
            _adminProjLabel = CreateAdminLabel(new Vector2(18f, -378f), new Vector2(140f, 24f), 0.80f);

            _adminZoneNameField = CreateAdminTextField(new Vector2(170f, -112f), new Vector2(320f, 34f));
            _adminWeldingSpeedField = CreateAdminTextField(new Vector2(170f, -216f), new Vector2(120f, 34f));
            _adminCostModifierField = CreateAdminTextField(new Vector2(170f, -268f), new Vector2(120f, 34f));
            _adminProjectionSpeedField = CreateAdminTextField(new Vector2(170f, -320f), new Vector2(120f, 34f));

            _adminToggleEnabledButton = CreateAdminButton(new Vector2(170f, -164f), new Vector2(140f, 36f), "Toggle");
            _adminToggleProjectionsButton = CreateAdminButton(new Vector2(170f, -372f), new Vector2(140f, 36f), "Toggle");
            _adminSpeedMinusButton = CreateAdminButton(new Vector2(302f, -216f), new Vector2(56f, 34f), "-");
            _adminSpeedPlusButton = CreateAdminButton(new Vector2(366f, -216f), new Vector2(56f, 34f), "+");
            _adminCostMinusButton = CreateAdminButton(new Vector2(302f, -268f), new Vector2(56f, 34f), "-");
            _adminCostPlusButton = CreateAdminButton(new Vector2(366f, -268f), new Vector2(56f, 34f), "+");
            _adminProjectionSpeedMinusButton = CreateAdminButton(new Vector2(302f, -320f), new Vector2(56f, 34f), "-");
            _adminProjectionSpeedPlusButton = CreateAdminButton(new Vector2(366f, -320f), new Vector2(56f, 34f), "+");
            _adminApplyButton = CreateAdminButton(new Vector2(18f, -418f), new Vector2(140f, 34f), "Apply");
            _adminLoadConfigButton = CreateAdminButton(new Vector2(176f, -418f), new Vector2(140f, 34f), "Load cfg");
            _adminCloseButton = CreateAdminButton(new Vector2(350f, -418f), new Vector2(140f, 34f), "Close");

            if (_adminZoneNameField != null)
                _adminZoneNameField.CharFilterFunc = ch => ch >= 32 && ch < 127;
            if (_adminWeldingSpeedField != null)
                _adminWeldingSpeedField.CharFilterFunc = ch => char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-';
            if (_adminCostModifierField != null)
                _adminCostModifierField.CharFilterFunc = ch => char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-';
            if (_adminProjectionSpeedField != null)
                _adminProjectionSpeedField.CharFilterFunc = ch => char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-';

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
            if (_adminProjectionSpeedMinusButton != null)
                _adminProjectionSpeedMinusButton.MouseInput.LeftClicked += AdminProjectionSpeedMinusClicked;
            if (_adminProjectionSpeedPlusButton != null)
                _adminProjectionSpeedPlusButton.MouseInput.LeftClicked += AdminProjectionSpeedPlusClicked;
            if (_adminApplyButton != null)
                _adminApplyButton.MouseInput.LeftClicked += AdminApplyClicked;
            if (_adminLoadConfigButton != null)
                _adminLoadConfigButton.MouseInput.LeftClicked += AdminLoadConfigClicked;
            if (_adminCloseButton != null)
                _adminCloseButton.MouseInput.LeftClicked += AdminCloseClicked;

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
            button.Format = new GlyphFormat(Color.White, TextAlignment.Center, 0.76f);
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

        private void UpdateAdminPanelState()
        {
            if (_adminPanel == null)
                return;

            bool visible = _adminPanelRequested;
            _adminPanel.Visible = visible;
            if (!visible)
                return;

            var state = _adminZoneState ?? new AdminZoneConfigStateMessage();
            if (_adminTitleLabel != null)
                _adminTitleLabel.Text = "ZERO's Safe Zone Admin";
            if (_adminZoneLabel != null)
                _adminZoneLabel.Text = string.IsNullOrWhiteSpace(state.ZoneName) ? "Zone: -" : "Zone: " + state.ZoneName;
            if (_adminStatusLabel != null)
                _adminStatusLabel.Text = state.Success
                    ? "Edit the zone values below, then press Apply. Load config restores the current server configuration."
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
                SendAdminZoneConfigUpdateFromClient(zoneName, _adminZoneState.Enabled, speed, cost, _adminZoneState.AllowProjections, projectionSpeed);
            }
            catch (Exception ex)
            {
                LogError("AdminApplyClicked error: " + ex);
            }
        }

        private void AdminLoadConfigClicked(object sender, EventArgs e)
        {
            MarkAdminPanelDirty();
            RequestAdminZoneConfig(true);
        }

        private void AdminCloseClicked(object sender, EventArgs e)
        {
            _adminPanelRequested = false;
            RefreshUiCursorState();
            UpdateAdminPanelState();
        }

        private void SetInteractiveMenuVisible(bool visible, bool repairEnabled)
        {
            if (_toggleRepairButton != null)
            {
                _toggleRepairButton.Visible = visible;
                _toggleRepairButton.Text = repairEnabled ? "Turn repair OFF" : "Turn repair ON";
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
            SetInteractiveCursorEnabled(interactiveMenuVisible);

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
                repairText = string.Format("{0}\nInteractive menu active  |  Ctrl+J: Close menu  |  Ctrl+R: Repair (cockpit)  |  Ctrl+N: Hide HUD", repairText);
            else if (GetLocalControlledShipController() != null)
                repairText = string.Format("{0}\nCtrl+J: Interactive menu  |  Ctrl+R: Repair (cockpit)  |  Ctrl+N: Hide HUD", repairText);
            else
                repairText = string.Format("{0}\nCtrl+J: Info menu  |  Ctrl+N: Hide cockpit HUD", repairText);

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

