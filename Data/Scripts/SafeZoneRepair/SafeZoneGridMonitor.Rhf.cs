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
        private static Label _currentRepairLabel;
        private static Label _repairLabel;
        private static Label _hintLabel;
        private static BorderedButton _toggleRepairButton;
        private static BorderedButton _showStatusButton;
        private static BorderedButton _closeMenuButton;

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
            EnsureRhfBindingsAndTerminal();

            if (_panel != null)
                _panel.Visible = false;

            SetHudLines(
                "Safe Zone Repair",
                "Zone: -",
                "Repair mode: -",
                "Status: Waiting for zone state",
                "Current repair: -",
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
            _currentRepairLabel = null;
            _repairLabel = null;
            _hintLabel = null;
            _toggleRepairButton = null;
            _showStatusButton = null;
            _closeMenuButton = null;
            _toggleRepairButton = null;
            _showStatusButton = null;
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
            _currentRepairLabel = null;
            _repairLabel = null;
            _hintLabel = null;
            _toggleRepairButton = null;
            _showStatusButton = null;
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
            _currentRepairLabel = CreateLabel(new Vector2(24f, -117f), new Vector2(560f, 32f), 0.80f, TextBuilderModes.Wrapped);
            _repairLabel = CreateLabel(new Vector2(24f, -149f), new Vector2(560f, 24f), 0.84f);
            _hintLabel = CreateLabel(new Vector2(24f, -177f), new Vector2(560f, 44f), 0.78f, TextBuilderModes.Wrapped);

            _toggleRepairButton = CreateMenuButton(new Vector2(24f, -234f), new Vector2(170f, 36f), "Toggle repair");
            _showStatusButton = CreateMenuButton(new Vector2(214f, -234f), new Vector2(170f, 36f), "Show status");
            _closeMenuButton = CreateMenuButton(new Vector2(404f, -234f), new Vector2(170f, 36f), "Close menu");

            if (_toggleRepairButton != null)
                _toggleRepairButton.MouseInput.LeftClicked += ToggleRepairButtonClicked;

            if (_showStatusButton != null)
                _showStatusButton.MouseInput.LeftClicked += ShowStatusButtonClicked;

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

        private void SetInteractiveMenuVisible(bool visible, bool repairEnabled)
        {
            if (_toggleRepairButton != null)
            {
                _toggleRepairButton.Visible = visible;
                _toggleRepairButton.Text = repairEnabled ? "Disable repair" : "Enable repair";
            }

            if (_showStatusButton != null)
                _showStatusButton.Visible = visible;

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

        private void ShowStatusButtonClicked(object sender, EventArgs e)
        {
            try
            {
                ShowStatusForLocalContext();
            }
            catch (Exception ex)
            {
                LogError("ShowStatusButtonClicked error: " + ex);
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

        private void SetHudLines(string title, string zone, string mode, string status, string currentRepair, string cost, string repair)
        {
            if (_titleLabel != null)
                _titleLabel.Text = title ?? string.Empty;

            if (_zoneLabel != null)
                _zoneLabel.Text = zone ?? string.Empty;

            if (_modeLabel != null)
                _modeLabel.Text = mode ?? string.Empty;

            if (_statusLabel != null)
                _statusLabel.Text = status ?? string.Empty;

            if (_currentRepairLabel != null)
                _currentRepairLabel.Text = currentRepair ?? string.Empty;

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

			ShowHud();

            bool interactiveMenuVisible = GetLocalControlledShipController() != null && IsCockpitInteractiveHudRequested();
            SetInteractiveCursorEnabled(interactiveMenuVisible);

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
            string currentRepairText = string.IsNullOrWhiteSpace(state.CurrentRepairText) ? "Current repair: -" : state.CurrentRepairText.Trim();
            string costText = string.Format("Estimated cost: {0} SC", estimatedRepairCost);
            if (interactiveMenuVisible)
                repairText = string.Format("{0}\nInteractive menu active  |  Ctrl+J: Close menu  |  Ctrl+R: Repair (cockpit)", repairText);
            else if (GetLocalControlledShipController() != null)
                repairText = string.Format("{0}\nCtrl+J: Interactive menu  |  Ctrl+R: Repair (cockpit)  |  Toolbar actions available", repairText);
            else
                repairText = string.Format("{0}\nCtrl+J: Info menu  |  Toolbar actions available in cockpit", repairText);

			SetInteractiveMenuVisible(interactiveMenuVisible, state.RepairEnabled);

			SetHudLines(
				"Safe Zone Repair",
				"Zone: " + zoneName,
				modeText,
				statusText,
                currentRepairText,
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
            SetInteractiveCursorEnabled(false);

            if (_panel != null)
                _panel.Visible = false;
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
                return shipController != null || IsManualHudAllowed();
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

