using GMKMovementMapper.Controls;
using GMKMovementMapper.Diagnostics;
using GMKMovementMapper.HidHide;
using GMKMovementMapper.Input;
using GMKMovementMapper.Mapping;
using GMKMovementMapper.Output;

namespace GMKMovementMapper;

public sealed class MainForm : Form
{
    private static readonly Color PageColor = Color.FromArgb(242, 245, 249);
    private static readonly Color CardColor = Color.White;
    private static readonly Color TextColor = Color.FromArgb(28, 37, 51);
    private static readonly Color MutedColor = Color.FromArgb(98, 108, 125);
    private static readonly Color PrimaryColor = Color.FromArgb(37, 99, 235);
    private static readonly Color SuccessColor = Color.FromArgb(22, 163, 74);
    private static readonly Color DangerColor = Color.FromArgb(220, 38, 38);

    private readonly ProfileStore _profileStore = new();
    private readonly ApplicationSettings _appSettings = ApplicationSettings.Load();
    private MovementProfile _profile;
    private readonly GmkUsbSource _joystick = new();
    private readonly HidHideBridge _hidHide = new();
    private readonly KeyboardOutput _keyboard = new();
    private readonly VirtualControllerOutput _controllerOutput = new();
    private readonly StickVisualizer _visualizer = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 10) };
    private readonly Button _startButton = new();
    private readonly Button _setupButton = new();
    private readonly Button _helpButton = new();
    private readonly Button _darkButton = new();
    private readonly Button _wizardButton = new();
    private readonly Button _calibrateButton = new();
    private readonly Button _newProfileButton = new();
    private readonly Button _deleteProfileButton = new();
    private readonly Button _applyPresetButton = new();
    private readonly Button _curveButton = new();
    private readonly Button _refreshDiagnosticsButton = new();
    private readonly Button _troubleshootButton = new();
    private readonly Button _advancedToggle = new();
    private readonly Button _dragZonesButton = new();
    private readonly ComboBox _profilePicker = new();
    private readonly ComboBox _presetPicker = new();
    private readonly Label _connectionBanner = new();
    private readonly NotifyIcon _trayIcon = new();
    private readonly Label _deviceStatus = new();
    private readonly Label _runBadge = new();
    private readonly Label _rawValue = new();
    private readonly Label _outputValue = new();
    private readonly Label _aValue = new();
    private readonly Label _deadzoneValue = new();
    private readonly Button _modeToggle = new();
    private readonly Label _modeDescription = new();
    private readonly CheckBox _diagonalLock = new();
    private readonly NumericUpDown _diagonalAngle = new();
    private readonly NumericUpDown _rotation = new();
    private readonly NumericUpDown _diagonalWindow = new();
    private readonly NumericUpDown _diagonalStart = new();
    private readonly NumericUpDown _antiDeadzone = new();
    private readonly NumericUpDown _outerDeadzone = new();
    private readonly ComboBox _controllerAButton = new();
    private readonly ComboBox _controllerSlotPicker = new();
    private readonly Button _refreshSlotsButton = new();
    private readonly Label _controllerSlotStatus = new();
    private readonly Button _openLogButton = new();
    private readonly LinkLabel _updateLink = new();
    private bool _suppressSlotEvent;
    private readonly CheckBox _openWithWindows = new();
    private readonly CheckBox _autoStartMovement = new();
    private readonly CheckBox _lowLatencyMode = new();
    private readonly Label _diagDevice = new();
    private readonly Label _diagHidHide = new();
    private readonly Label _diagViGEm = new();
    private readonly Label _diagOutput = new();
    private readonly Label _diagRate = new();
    private readonly Label _diagProfile = new();
    private readonly Label _diagLatency = new();
    private readonly Label _diagCalibration = new();
    private readonly ToolTip _toolTip = new() { AutomaticDelay = 250, AutoPopDelay = 9000, ReshowDelay = 100 };
    private readonly System.Windows.Forms.Timer _diagnosticTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _profileSaveTimer = new() { Interval = 350 };
    private readonly ToolStripMenuItem _trayMovementItem = new("Start movement");
    private readonly ToolStripMenuItem _trayProfilesItem = new("Profiles");
    private readonly Label _dashboardDevice = new();
    private readonly Label _dashboardMode = new();
    private readonly Label _dashboardLatency = new();
    private readonly CheckBox _linearResponse = new();
    private readonly Label _outputCaption = new();
    private Control? _keyboardSettingsPanel;
    private Control? _controllerSettingsPanel;
    private Panel? _settingsHost;
    private Label? _subtitle;
    private readonly TrackBar _deadzone = new();
    private readonly Button _forwardButton = new();
    private readonly Button _leftButton = new();
    private readonly Button _backwardButton = new();
    private readonly Button _rightButton = new();
    private readonly Button _actionButton = new();
    private readonly List<Button> _mappingButtons;

    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private BindingTarget? _capturing;
    private Control? _page;
    private long _lastInputTick;
    private double _pollingRate;
    private double _latencyMs;
    private bool _advancedVisible;
    private volatile bool _trayPerformanceMode;
    private volatile bool _windowActive = true;
    private string _connectionMessage = "Waiting for GMK";
    private bool _connectionSuccess;
    private bool _connectionWarning;

    private enum BindingTarget { Forward, Left, Backward, Right, Action }
    private readonly record struct KeyBindings(Keys Forward, Keys Left, Keys Backward, Keys Right, Keys Action);

    public MainForm()
    {
        var profileNames = _profileStore.GetNames();
        _profile = _profileStore.Load(profileNames.Contains(_appSettings.LastProfileName, StringComparer.OrdinalIgnoreCase)
            ? profileNames.First(n => string.Equals(n, _appSettings.LastProfileName, StringComparison.OrdinalIgnoreCase))
            : profileNames.First());
        _mappingButtons = [_forwardButton, _leftButton, _backwardButton, _rightButton, _actionButton];
        Text = "GMK Mapper";
        Icon = AppBrand.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1040, 700);
        MinimumSize = new Size(900, 620);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        BackColor = PageColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        KeyPreview = true;

        ConfigureControls();
        _page = BuildPage();
        Controls.Add(_page);
        KeyDown += CaptureKey;
        FormClosing += (_, _) => { _profileSaveTimer.Stop(); _profileStore.Save(_profile); StopMapper(); _trayIcon.Visible = false; _trayIcon.Dispose(); };
        Resize += (_, _) => MinimizeToTray();
        Shown += (_, _) =>
        {
            TitleBarTheme.Apply(this, _profile.Appearance != AppearanceMode.Light);
            if (!_profile.SetupComplete) OpenSetupWizard();
            else if (_appSettings.AutoStartMovement) _ = AutoStartWithRetryAsync();
            CheckForUpdateInBackground();
        };
        RefreshBindingButtons();
        SetRunningState(false);
        ApplyTheme();
        UpdateModeUI();
        _diagnosticTimer.Tick += (_, _) => RefreshDiagnostics();
        _diagnosticTimer.Start();
        _profileSaveTimer.Tick += (_, _) => { _profileSaveTimer.Stop(); _profileStore.Save(_profile); };
    }

    internal void SafeShutdown()
    {
        _keyboard.ReleaseAllInputs();
        _controllerOutput.Dispose();
        _joystick.Dispose();
    }

    private async Task AutoStartWithRetryAsync()
    {
        for (var attempt = 0; attempt < 10 && !IsDisposed && _runCts is null; attempt++)
        {
            StartMapper(true);
            if (_runCts is not null) return;
            await Task.Delay(1000);
        }
        if (!IsDisposed && _runCts is null) SetConnectionState("Automatic start timed out — connect the GMK and press Start movement", false, true);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        PerformLayout();
        _page?.PerformLayout();
        Invalidate(true);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _windowActive = true;
        if (!_trayPerformanceMode) _diagnosticTimer.Start();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        _windowActive = false;
        _diagnosticTimer.Stop();
        base.OnDeactivate(e);
    }

    private void ConfigureControls()
    {
        _profilePicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _profilePicker.Width = 230;
        _profilePicker.Items.AddRange(_profileStore.GetNames().Cast<object>().ToArray());
        _profilePicker.SelectedItem = _profile.Name;
        _profilePicker.SelectedIndexChanged += (_, _) => SwitchProfile();
        StyleSecondaryButton(_newProfileButton, "New"); _newProfileButton.Size = new Size(70, 34); _newProfileButton.Click += (_, _) => CreateProfile();
        StyleSecondaryButton(_deleteProfileButton, "Delete"); _deleteProfileButton.Size = new Size(76, 34); _deleteProfileButton.Click += (_, _) => DeleteProfile();
        _presetPicker.DropDownStyle = ComboBoxStyle.DropDownList; _presetPicker.Width = 190;
        _presetPicker.Items.AddRange(["Fortnite analog 71°", "Legacy GMK 76°", "Raw analog", "Keyboard 8-way"]); _presetPicker.SelectedIndex = 0;
        StyleSecondaryButton(_applyPresetButton, "Apply preset"); _applyPresetButton.Size = new Size(105, 34); _applyPresetButton.Click += (_, _) => ApplyPreset();
        StyleSecondaryButton(_curveButton, "Edit curve…"); _curveButton.Size = new Size(108, 31); _curveButton.Click += (_, _) => OpenCurveEditor();
        StyleSecondaryButton(_refreshDiagnosticsButton, "Refresh diagnostics"); _refreshDiagnosticsButton.Size = new Size(150, 36); _refreshDiagnosticsButton.Click += (_, _) => RefreshDiagnostics();
        StyleSecondaryButton(_troubleshootButton, "Troubleshoot…"); _troubleshootButton.Size = new Size(130, 36); _troubleshootButton.Click += (_, _) => OpenTroubleshooter();
        StyleSecondaryButton(_advancedToggle, "Show advanced"); _advancedToggle.Size = new Size(122, 34); _advancedToggle.Click += (_, _) => ToggleAdvanced();
        StyleSecondaryButton(_dragZonesButton, "Drag zones"); _dragZonesButton.Size = new Size(105, 26); _dragZonesButton.Click += (_, _) => ToggleZoneEditor();

        StyleSecondaryButton(_modeToggle, string.Empty);
        _modeToggle.Size = new Size(280, 42);
        _modeToggle.Click += (_, _) =>
        {
            if (_runCts is not null) return;
            _profile.OutputMode = _profile.OutputMode == MovementOutputMode.Keyboard
                ? MovementOutputMode.Controller : MovementOutputMode.Keyboard;
            ScheduleProfileSave();
            UpdateModeUI();
        };
        _modeDescription.AutoSize = true;
        _modeDescription.MaximumSize = new Size(440, 0);
        _modeDescription.Tag = "muted";
        _modeDescription.ForeColor = MutedColor;

        _diagonalLock.Text = "Enable custom diagonal locking";
        _diagonalLock.AutoSize = true;
        _diagonalLock.Checked = _profile.ControllerDiagonalLock;
        _diagonalLock.CheckedChanged += (_, _) => { _profile.ControllerDiagonalLock = _diagonalLock.Checked; _diagonalAngle.Enabled = _diagonalLock.Checked; _diagonalStart.Enabled = _diagonalLock.Checked; _diagonalWindow.Enabled = _diagonalLock.Checked; _profileStore.Save(_profile); RefreshVisualizerOverlay(); };
        _diagonalAngle.Minimum = 0; _diagonalAngle.Maximum = 90; _diagonalAngle.DecimalPlaces = 1; _diagonalAngle.Increment = 0.5M; _diagonalAngle.Width = 82;
        _diagonalAngle.Value = (decimal)Math.Clamp(_profile.ControllerDiagonalAngle, 0, 90);
        _diagonalAngle.ValueChanged += (_, _) => { _profile.ControllerDiagonalAngle = (double)_diagonalAngle.Value; ScheduleProfileSave(); RefreshVisualizerOverlay(); };
        _rotation.Minimum = -180; _rotation.Maximum = 180; _rotation.DecimalPlaces = 1; _rotation.Increment = 0.5M; _rotation.Width = 82;
        _rotation.Value = (decimal)Math.Clamp(_profile.ControllerRotation, -180, 180);
        _rotation.ValueChanged += (_, _) => { _profile.ControllerRotation = (double)_rotation.Value; ScheduleProfileSave(); RefreshVisualizerOverlay(); };
        _diagonalWindow.Minimum = 0; _diagonalWindow.Maximum = 90; _diagonalWindow.DecimalPlaces = 1; _diagonalWindow.Increment = 0.5M; _diagonalWindow.Width = 82;
        _diagonalWindow.Value = (decimal)Math.Clamp(_profile.ControllerDiagonalWindow, 0, 90);
        _diagonalWindow.ValueChanged += (_, _) => { _profile.ControllerDiagonalWindow = (double)_diagonalWindow.Value; ScheduleProfileSave(); RefreshVisualizerOverlay(); };
        _diagonalStart.Minimum = 0; _diagonalStart.Maximum = 89; _diagonalStart.DecimalPlaces = 1; _diagonalStart.Increment = 0.5M; _diagonalStart.Width = 82;
        _diagonalStart.Value = (decimal)Math.Clamp(_profile.ControllerDiagonalStart, 0, 89);
        _diagonalStart.ValueChanged += (_, _) => { _profile.ControllerDiagonalStart = (double)_diagonalStart.Value; ScheduleProfileSave(); RefreshVisualizerOverlay(); };
        _antiDeadzone.Minimum = 0; _antiDeadzone.Maximum = 95; _antiDeadzone.Width = 82; _antiDeadzone.Value = (decimal)Math.Clamp(_profile.ControllerAntiDeadzone * 100, 0, 95);
        _antiDeadzone.ValueChanged += (_, _) => { _profile.ControllerAntiDeadzone = (double)_antiDeadzone.Value / 100; ScheduleProfileSave(); };
        _outerDeadzone.Minimum = 50; _outerDeadzone.Maximum = 100; _outerDeadzone.Width = 82; _outerDeadzone.Value = (decimal)Math.Clamp(_profile.MaxZone * 100, 50, 100);
        _outerDeadzone.ValueChanged += (_, _) => { _profile.MaxZone = (double)_outerDeadzone.Value / 100; ScheduleProfileSave(); };
        _controllerAButton.DropDownStyle = ComboBoxStyle.DropDownList; _controllerAButton.Width = 132;
        _controllerAButton.DataSource = Enum.GetValues<ControllerButtonMapping>();
        _controllerAButton.SelectedItem = _profile.ControllerAButton;
        _controllerAButton.SelectedIndexChanged += (_, _) => { if (_controllerAButton.SelectedItem is ControllerButtonMapping value) { _profile.ControllerAButton = value; _profileStore.Save(_profile); } };
        _controllerSlotPicker.DropDownStyle = ComboBoxStyle.DropDownList; _controllerSlotPicker.Width = 220;
        _controllerSlotPicker.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressSlotEvent) return;
            _profile.ForcedControllerSlot = _controllerSlotPicker.SelectedIndex - 1; // -1 = Automatic (index 0)
            _profileStore.Save(_profile);
        };
        StyleSecondaryButton(_refreshSlotsButton, "Refresh");
        _refreshSlotsButton.Size = new Size(86, 30);
        _refreshSlotsButton.Click += (_, _) => RefreshControllerSlotPicker();
        StyleStatusLabel(_controllerSlotStatus, "Only needed if another Xbox/XInput controller is plugged in at the same time as the GMK.");
        _toolTip.SetToolTip(_controllerSlotPicker, "Which controller slot to read the GMK from, if it shows up in Xbox-compatible mode. Leave on Automatic unless you also use a real Xbox/XInput controller at the same time.");
        RefreshControllerSlotPicker();
        StyleSecondaryButton(_openLogButton, "Open log file"); _openLogButton.Size = new Size(115, 36); _openLogButton.Click += (_, _) => OpenLogFile();
        _linearResponse.Text = "Linear stick response"; _linearResponse.AutoSize = true; _linearResponse.Checked = _profile.ControllerLinearResponse;
        _linearResponse.CheckedChanged += (_, _) => { _profile.ControllerLinearResponse = _linearResponse.Checked; _profileStore.Save(_profile); };

        _openWithWindows.Text = "Open GMK Mapper when Windows starts"; _openWithWindows.AutoSize = true; _openWithWindows.Checked = StartupManager.IsEnabled();
        _openWithWindows.CheckedChanged += (_, _) => { try { StartupManager.SetEnabled(_openWithWindows.Checked); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Startup setting", MessageBoxButtons.OK, MessageBoxIcon.Error); } };
        _autoStartMovement.Text = "Start movement automatically"; _autoStartMovement.AutoSize = true; _autoStartMovement.Checked = _appSettings.AutoStartMovement;
        _autoStartMovement.CheckedChanged += (_, _) => { _appSettings.AutoStartMovement = _autoStartMovement.Checked; _appSettings.Save(); };
        _toolTip.SetToolTip(_diagonalStart, "Where each forward lock zone begins, measured upward from the horizontal centre line.");
        _toolTip.SetToolTip(_diagonalWindow, "Width of each purple forward lock zone.");
        _toolTip.SetToolTip(_antiDeadzone, "Minimum virtual-stick output after leaving the centre deadzone.");
        _toolTip.SetToolTip(_outerDeadzone, "Physical travel where virtual output reaches 100%.");

        StylePrimaryButton(_startButton, "Start movement");
        _startButton.Height = 46;
        _startButton.Dock = DockStyle.Top;
        _startButton.Click += (_, _) => ToggleMapper();

        StyleSecondaryButton(_setupButton, "Set up HidHide");
        _setupButton.Size = new Size(150, 36);
        _setupButton.Click += (_, _) => SetupHidHide();
        _setupButton.Enabled = _hidHide.IsInstalled;
        StyleSecondaryButton(_wizardButton, "Setup wizard");
        _wizardButton.Size = new Size(126, 36);
        _wizardButton.Click += (_, _) => OpenSetupWizard();
        StyleSecondaryButton(_calibrateButton, "Calibrate stick");
        _calibrateButton.Size = new Size(126, 36);
        _calibrateButton.Click += (_, _) => OpenCalibration();
        StyleSecondaryButton(_helpButton, "Help");
        _helpButton.Size = new Size(86, 36);
        _helpButton.Click += (_, _) => new HelpForm(_profile.DarkMode).ShowDialog(this);
        StyleSecondaryButton(_darkButton, $"Theme: {_profile.Appearance}");
        _darkButton.Size = new Size(118, 34);
        _darkButton.Click += (_, _) =>
        {
            _profile.Appearance = _profile.Appearance switch { AppearanceMode.Light => AppearanceMode.Dark, AppearanceMode.Dark => AppearanceMode.Black, _ => AppearanceMode.Light };
            _profile.DarkMode = _profile.Appearance != AppearanceMode.Light;
            ScheduleProfileSave();
            ApplyTheme();
        };
        StyleStatusLabel(_deviceStatus, _hidHide.IsInstalled
            ? "HidHide detected — setup hides the physical GMK from games."
            : "HidHide is not installed. Movement still works, but games may see the GMK.");
        _connectionBanner.AutoSize = false;
        _connectionBanner.Height = 38;
        _connectionBanner.Dock = DockStyle.Top;
        _connectionBanner.Padding = new Padding(11, 10, 8, 8);
        SetConnectionState("Waiting for GMK", false, false);

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open GMK Movement", null, (_, _) => RestoreFromTray());
        _trayMovementItem.Click += (_, _) => { if (_runCts is null) StartMapper(); else StopMapper(); };
        trayMenu.Items.Add(_trayMovementItem);
        trayMenu.Items.Add(_trayProfilesItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) => Close());
        _trayIcon.Icon = Icon;
        _trayIcon.Text = "GMK Movement";
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        RebuildTrayProfiles();

        _runBadge.AutoSize = true;
        _runBadge.Padding = new Padding(9, 5, 9, 5);
        _runBadge.Font = new Font(Font, FontStyle.Bold);
        ConfigureValueLabel(_rawValue, "0.00, 0.00");
        ConfigureValueLabel(_outputValue, "Centered");
        ConfigureValueLabel(_aValue, "Released");
        ConfigureDashboardChip(_dashboardDevice, "GMK waiting"); ConfigureDashboardChip(_dashboardMode, "Keyboard"); ConfigureDashboardChip(_dashboardLatency, "Latency —");

        _deadzone.Minimum = 0;
        _deadzone.Maximum = 40;
        _deadzone.TickFrequency = 5;
        _deadzone.SmallChange = 1;
        _deadzone.LargeChange = 5;
        _deadzone.Value = Math.Clamp((int)Math.Round(_profile.Deadzone * 100), 0, 40);
        _deadzone.Dock = DockStyle.Fill;
        _deadzone.Margin = new Padding(0);
        _deadzone.ValueChanged += (_, _) =>
        {
            _profile.Deadzone = _deadzone.Value / 100.0;
            _deadzoneValue.Text = $"{_deadzone.Value}%";
            ScheduleProfileSave();
        };
        _deadzoneValue.Text = $"{_deadzone.Value}%";
        _deadzoneValue.AutoSize = true;
        _deadzoneValue.Font = new Font(Font, FontStyle.Bold);
        _deadzoneValue.ForeColor = PrimaryColor;

        ConfigureBindingButton(_forwardButton, BindingTarget.Forward);
        ConfigureBindingButton(_leftButton, BindingTarget.Left);
        ConfigureBindingButton(_backwardButton, BindingTarget.Backward);
        ConfigureBindingButton(_rightButton, BindingTarget.Right);
        ConfigureBindingButton(_actionButton, BindingTarget.Action);
        _lowLatencyMode.Text = "Low-latency performance mode"; _lowLatencyMode.AutoSize = true; _lowLatencyMode.Checked = _profile.LowLatencyMode;
        _lowLatencyMode.CheckedChanged += (_, _) => { _profile.LowLatencyMode = _lowLatencyMode.Checked; _profileStore.Save(_profile); };
        _toolTip.SetToolTip(_lowLatencyMode, "Reduces interface drawing frequency while keeping full-speed input and output processing.");
        _visualizer.DiagonalZoneChanged += (start, width) => BeginInvoke((Action)(() => ApplyDraggedZone(start, width)));
        AttachPreview(_deadzone, "deadzone"); AttachPreview(_deadzoneValue, "deadzone");
        AttachPreview(_diagonalLock, "diagonal"); AttachPreview(_diagonalAngle, "diagonal");
        AttachPreview(_diagonalStart, "diagonal"); AttachPreview(_diagonalWindow, "diagonal"); AttachPreview(_rotation, "diagonal");
        AttachPreview(_outerDeadzone, "outer");
    }

    private void AttachPreview(Control control, string area)
    {
        control.MouseEnter += (_, _) => _visualizer.SetEmphasis(area);
        control.MouseLeave += (_, _) => _visualizer.SetEmphasis(null);
    }

    /// <summary>
    /// Lists Automatic plus every XInput slot (0-3) with its live connection
    /// state, so the user can pick exactly which controller is the GMK when
    /// more than one Xbox-compatible device is plugged in at once.
    /// The native XInput query runs on a background thread and never blocks
    /// the UI — a flaky controller can make XInputGetState take a while (or
    /// never return), and that must not freeze the whole window.
    /// </summary>
    private void RefreshControllerSlotPicker()
    {
        _suppressSlotEvent = true;
        _controllerSlotPicker.Items.Clear();
        _controllerSlotPicker.Items.Add("Automatic (recommended)");
        for (var i = 0; i < 4; i++) _controllerSlotPicker.Items.Add($"Slot {i} — checking…");
        _controllerSlotPicker.SelectedIndex = Math.Clamp(_profile.ForcedControllerSlot + 1, 0, _controllerSlotPicker.Items.Count - 1);
        _suppressSlotEvent = false;

        Task.Run(() => XInputSource.GetSlotStatus()).ContinueWith(task =>
        {
            if (!task.IsCompletedSuccessfully || IsDisposed) return;
            var status = task.Result;
            BeginInvoke((Action)(() =>
            {
                _suppressSlotEvent = true;
                var current = _controllerSlotPicker.SelectedIndex;
                _controllerSlotPicker.Items.Clear();
                _controllerSlotPicker.Items.Add("Automatic (recommended)");
                for (var i = 0; i < status.Count; i++)
                    _controllerSlotPicker.Items.Add($"Slot {i} — {(status[i] ? "connected" : "not connected")}");
                _controllerSlotPicker.SelectedIndex = Math.Clamp(current, 0, _controllerSlotPicker.Items.Count - 1);
                _suppressSlotEvent = false;

                var connectedCount = status.Count(c => c);
                _controllerSlotStatus.Text = connectedCount <= 1
                    ? "Only needed if another Xbox/XInput controller is plugged in at the same time as the GMK."
                    : $"{connectedCount} Xbox-compatible controllers detected right now — pick the GMK's slot below if Automatic grabs the wrong one.";
            }));
        });
    }

    /// <summary>
    /// Fire-and-forget: checks GitHub for a newer release and shows a small
    /// clickable link in the header if one exists. Never shows an error and
    /// never delays startup — a failed or slow network check must be invisible.
    /// </summary>
    private void CheckForUpdateInBackground()
    {
        UpdateChecker.CheckForNewerReleaseAsync().ContinueWith(task =>
        {
            if (!task.IsCompletedSuccessfully || task.Result is null || IsDisposed) return;
            var (tag, _) = task.Result.Value;
            BeginInvoke((Action)(() =>
            {
                _updateLink.Text = $"Update available: {tag} →";
                _updateLink.Links.Clear();
                _updateLink.Links.Add(0, _updateLink.Text.Length);
                _updateLink.LinkClicked += (_, _) => OpenReleasesPage();
                _updateLink.Visible = true;
                Logger.Info($"Update available: {tag} (running {UpdateChecker.CurrentVersion})");
            }));
        });
    }

    private static void OpenReleasesPage()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/thedawnpirate1-spec/GMK-Movement-Mapper/releases") { UseShellExecute = true }); }
        catch { /* opening the browser is best-effort */ }
    }

    private void OpenLogFile()
    {
        try
        {
            var path = Logger.CurrentLogPath;
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "No log entries yet today. The log file is created the first time something worth recording happens (a connection, an error).", "No log file yet", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open log file", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Control BuildPage()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = PageColor, ColumnCount = 1, RowCount = 2, Tag = "page" };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeader(), 0, 0);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(12), BackColor = PageColor, Tag = "page"
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        var settingsCard = BuildSettingsCard();
        settingsCard.Margin = new Padding(0, 0, 8, 0);
        var visualCard = BuildVisualCard();
        visualCard.Margin = new Padding(8, 0, 0, 0);
        content.Controls.Add(settingsCard, 0, 0);
        content.Controls.Add(visualCard, 1, 0);
        root.Controls.Add(content, 0, 1);
        return root;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20,29,44), Padding = new Padding(18,9,18,8), Tag = "header", ColumnCount = 4, RowCount = 1 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        var logo = new BrandLogo { Dock = DockStyle.Fill, Margin = new Padding(0,0,8,0) };
        var textStack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
        textStack.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); textStack.RowStyles.Add(new RowStyle(SizeType.Percent, 30)); textStack.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        var title = new Label { Text = "GMK Mapper", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft, Tag = "header" };
        _subtitle = new Label { Text = $"GMK to Keyboard Movement · v{UpdateChecker.CurrentVersion.ToString(3)}", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(174,187,207), Font = new Font("Segoe UI", 8.7F), TextAlign = ContentAlignment.TopLeft, Tag = "header" };
        _updateLink.Dock = DockStyle.Fill; _updateLink.AutoSize = false; _updateLink.Visible = false;
        _updateLink.Font = new Font("Segoe UI Semibold", 8.3F, FontStyle.Bold); _updateLink.LinkColor = Color.FromArgb(147,197,253); _updateLink.Tag = "header";
        textStack.Controls.Add(title,0,0); textStack.Controls.Add(_subtitle,0,1); textStack.Controls.Add(_updateLink,0,2);
        _darkButton.Anchor = AnchorStyles.None; _runBadge.Anchor = AnchorStyles.None;
        header.Controls.Add(logo,0,0); header.Controls.Add(textStack,1,0); header.Controls.Add(_darkButton,2,0); header.Controls.Add(_runBadge,3,0);
        return header;
    }

    private Control BuildSettingsCard()
    {
        var card = CreateCard();
        card.Padding = new Padding(16);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, AutoScroll = false };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.Controls.Add(_connectionBanner, 0, 0);

        var tabs = new ModernTabControl { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 6) };
        tabs.TabPages.Add(BuildMovementTab());
        tabs.TabPages.Add(BuildSetupTab());
        tabs.TabPages.Add(BuildDiagnosticsTab());
        layout.Controls.Add(tabs, 0, 1);
        _startButton.Dock = DockStyle.Fill;
        _startButton.Margin = new Padding(0, 8, 0, 0);
        layout.Controls.Add(_startButton, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private TabPage BuildMovementTab()
    {
        var page = new TabPage("Movement") { Padding = new Padding(14), Tag = "card" };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
        for (var i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(SectionTitle("Movement mode"), 0, 0);
        var modeRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 3) };
        modeRow.Controls.Add(_modeToggle); modeRow.Controls.Add(_advancedToggle); layout.Controls.Add(modeRow, 0, 1);
        layout.Controls.Add(_modeDescription, 0, 2);
        layout.Controls.Add(SectionTitle("Movement configuration"), 0, 3);

        var keyGrid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 1, 0, 4) };
        keyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); keyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        AddBindingRow(keyGrid, "Move forward", _forwardButton, 0); AddBindingRow(keyGrid, "Move left", _leftButton, 1);
        AddBindingRow(keyGrid, "Move backward", _backwardButton, 2); AddBindingRow(keyGrid, "Move right", _rightButton, 3);
        AddBindingRow(keyGrid, "GMK A button", _actionButton, 4);
        _keyboardSettingsPanel = keyGrid; _controllerSettingsPanel = BuildControllerSettings();
        _settingsHost = new Panel { Dock = DockStyle.Top, Height = 205, Margin = new Padding(0) };
        _settingsHost.Controls.Add(_controllerSettingsPanel); _settingsHost.Controls.Add(_keyboardSettingsPanel);
        layout.Controls.Add(_settingsHost, 0, 4);
        layout.Controls.Add(SectionTitle("Deadzone"), 0, 5);
        var deadzoneRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 3, Margin = new Padding(0) };
        deadzoneRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); deadzoneRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48)); deadzoneRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        deadzoneRow.Controls.Add(_deadzone, 0, 0); deadzoneRow.Controls.Add(_deadzoneValue, 1, 0); _deadzoneValue.Anchor = AnchorStyles.Left;
        deadzoneRow.Controls.Add(_calibrateButton, 2, 0); layout.Controls.Add(deadzoneRow, 0, 6);
        page.Controls.Add(layout); return page;
    }

    private TabPage BuildSetupTab()
    {
        var page = new TabPage("Setup & startup") { Padding = new Padding(14), Tag = "card" };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 13 };
        for (var i = 0; i < 13; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(SectionTitle("Profile"), 0, 0);
        var profileRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 10) };
        profileRow.Controls.AddRange([_profilePicker, _newProfileButton, _deleteProfileButton]);
        layout.Controls.Add(profileRow, 0, 1);
        layout.Controls.Add(SectionTitle("Preset library"), 0, 2);
        var presetRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 10) };
        presetRow.Controls.AddRange([_presetPicker, _applyPresetButton]); layout.Controls.Add(presetRow, 0, 3);
        layout.Controls.Add(SectionTitle("Device setup"), 0, 4);
        var setupRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, MinimumSize = new Size(0, 44), WrapContents = false, Margin = new Padding(0, 4, 0, 4), Padding = new Padding(0, 2, 0, 2) };
        setupRow.Controls.AddRange([_wizardButton, _setupButton, _helpButton]);
        layout.Controls.Add(setupRow, 0, 5); layout.Controls.Add(_deviceStatus, 0, 6);
        layout.Controls.Add(SectionTitle("GMK controller slot (Xbox-compatible mode)"), 0, 7);
        var slotRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        slotRow.Controls.AddRange([_controllerSlotPicker, _refreshSlotsButton]);
        layout.Controls.Add(slotRow, 0, 8);
        layout.Controls.Add(_controllerSlotStatus, 0, 9);
        layout.Controls.Add(SectionTitle("Automatic startup"), 0, 10);
        var startup = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 4, 0, 8) };
        startup.Controls.Add(_openWithWindows); startup.Controls.Add(_autoStartMovement); startup.Controls.Add(_lowLatencyMode); layout.Controls.Add(startup, 0, 11);
        layout.Controls.Add(new Label { Text = "Automatic movement uses the selected profile. Controller mode creates the virtual controller only after movement starts.", AutoSize = true, MaximumSize = new Size(450, 0), Tag = "muted" }, 0, 12);
        page.Controls.Add(layout); return page;
    }

    private Control BuildControllerSettings()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Margin = new Padding(0,3,0,6) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
        for (var i=0;i<6;i++) panel.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
        panel.Controls.Add(_diagonalLock,0,0); panel.SetColumnSpan(_diagonalLock,4);
        AddCompactSetting(panel,"Locked angle",_diagonalAngle,"°",0,1); AddCompactSetting(panel,"Zone starts",_diagonalStart,"°",2,1);
        AddCompactSetting(panel,"Zone width",_diagonalWindow,"°",0,2); AddCompactSetting(panel,"Rotation",_rotation,"°",2,2);
        AddCompactSetting(panel,"Anti-deadzone",_antiDeadzone,"%",0,3); AddCompactSetting(panel,"Outer deadzone",_outerDeadzone,"%",2,3);
        panel.Controls.Add(new Label { Text="GMK A output",AutoSize=true,Anchor=AnchorStyles.Left,Tag="text" },0,4); panel.Controls.Add(_controllerAButton,1,4);
        panel.Controls.Add(new Label { Text="Response curve",AutoSize=true,Anchor=AnchorStyles.Left,Tag="text" },2,4); panel.Controls.Add(_curveButton,3,4);
        panel.Controls.Add(_linearResponse,0,5); panel.SetColumnSpan(_linearResponse,2);
        panel.Controls.Add(_dragZonesButton,3,5);
        return panel;
    }

    private static void AddCompactSetting(TableLayoutPanel panel, string label, NumericUpDown input, string unit, int column, int row)
    {
        panel.Controls.Add(new Label { Text=label,AutoSize=true,Anchor=AnchorStyles.Left,Tag="text" },column,row);
        panel.Controls.Add(UnitControl(input,unit),column+1,row);
    }

    private TabPage BuildDiagnosticsTab()
    {
        var page = new TabPage("Diagnostics") { Padding = new Padding(14), Tag = "card" };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(SectionTitle("System diagnostics"), 0, 0); layout.SetColumnSpan(layout.GetControlFromPosition(0,0)!, 2);
        AddDiagnosticRow(layout, "Physical GMK", _diagDevice, 1); AddDiagnosticRow(layout, "HidHide", _diagHidHide, 2);
        AddDiagnosticRow(layout, "ViGEmBus", _diagViGEm, 3); AddDiagnosticRow(layout, "Movement output", _diagOutput, 4);
        AddDiagnosticRow(layout, "USB read rate", _diagRate, 5); AddDiagnosticRow(layout, "Input processing latency", _diagLatency, 6);
        AddDiagnosticRow(layout, "Calibration health", _diagCalibration, 7); AddDiagnosticRow(layout, "Active profile", _diagProfile, 8);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        actions.Controls.Add(_refreshDiagnosticsButton); actions.Controls.Add(_troubleshootButton); actions.Controls.Add(_openLogButton); layout.Controls.Add(actions, 1, 9);
        page.Controls.Add(layout); RefreshDiagnostics(); return page;
    }

    private static void AddDiagnosticRow(TableLayoutPanel layout, string caption, Label value, int row)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Tag = "muted" }, 0, row);
        value.AutoSize = true; value.Anchor = AnchorStyles.Left; value.Tag = "text"; value.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        layout.Controls.Add(value, 1, row);
    }

    private static Control UnitControl(NumericUpDown input, string unit)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, WrapContents = false, Margin = new Padding(0) };
        row.Controls.Add(input);
        row.Controls.Add(new Label { Text = unit, AutoSize = true, Margin = new Padding(2, 6, 0, 0), Tag = "text", ForeColor = TextColor });
        return row;
    }

    private Control BuildVisualCard()
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(SectionTitle("Live input"), 0, 0);
        var dashboard = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0,0,0,4) };
        dashboard.Controls.AddRange([_dashboardDevice, _dashboardMode, _dashboardLatency]); layout.Controls.Add(dashboard, 0, 1);
        layout.Controls.Add(_visualizer, 0, 2);
        var legend = new Label { Text = "● Physical / calibrated    ● Final output    - - Outer deadzone", AutoSize = true, Tag = "muted", ForeColor = MutedColor, Margin = new Padding(2,0,0,5) };
        layout.Controls.Add(legend, 0, 3);
        layout.Controls.Add(CreateLiveRow("Physical stick", _rawValue), 0, 4);
        _outputCaption.Text = "Keyboard output";
        _outputCaption.AutoSize = true;
        _outputCaption.ForeColor = MutedColor;
        _outputCaption.Anchor = AnchorStyles.Left;
        _outputCaption.Tag = "muted";
        layout.Controls.Add(CreateLiveRow(_outputCaption, _outputValue), 0, 5);
        layout.Controls.Add(CreateLiveRow("GMK A button", _aValue), 0, 6);
        card.Controls.Add(layout);
        return card;
    }

    private static Panel CreateCard() => new() { Dock = DockStyle.Fill, BackColor = CardColor, Padding = new Padding(20), Tag = "card" };
    private static Label SectionTitle(string text) => new() { Text = text, AutoSize = true, ForeColor = TextColor, Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold), Margin = new Padding(0, 3, 0, 7), Tag = "text" };

    private static void AddBindingRow(TableLayoutPanel grid, string label, Button button, int row)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        grid.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = TextColor, Anchor = AnchorStyles.Left, Tag = "text" }, 0, row);
        grid.Controls.Add(button, 1, row);
    }

    private static Control CreateLiveRow(string label, Label value)
        => CreateLiveRow(new Label { Text = label, AutoSize = true, ForeColor = MutedColor, Anchor = AnchorStyles.Left, Tag = "muted" }, value);

    private static Control CreateLiveRow(Label caption, Label value)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 36, ColumnCount = 2, Margin = new Padding(0) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.Controls.Add(caption, 0, 0);
        value.Anchor = AnchorStyles.Right;
        row.Controls.Add(value, 1, 0);
        return row;
    }

    private void ConfigureBindingButton(Button button, BindingTarget target)
    {
        StyleSecondaryButton(button, string.Empty);
        button.Width = 132;
        button.Height = 31;
        button.Anchor = AnchorStyles.Right;
        button.Click += (_, _) => BeginKeyCapture(target, button);
    }

    private static void StylePrimaryButton(Button button, string text)
    {
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = PrimaryColor;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private static void StyleSecondaryButton(Button button, string text)
    {
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(205, 212, 222);
        button.BackColor = Color.FromArgb(249, 250, 252);
        button.ForeColor = TextColor;
        button.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private static void StyleStatusLabel(Label label, string text)
    {
        label.Text = text;
        label.AutoSize = true;
        label.MaximumSize = new Size(430, 0);
        label.ForeColor = MutedColor;
        label.Tag = "muted";
        label.Margin = new Padding(0, 0, 0, 11);
    }

    private static void ConfigureValueLabel(Label label, string text)
    {
        label.Text = text;
        label.AutoSize = true;
        label.ForeColor = TextColor;
        label.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        label.Tag = "text";
    }

    private static void ConfigureDashboardChip(Label label, string text)
    {
        label.Text = text; label.AutoSize = true; label.Padding = new Padding(8,5,8,5); label.Margin = new Padding(0,0,7,4);
        label.BackColor = Color.FromArgb(226,232,240); label.ForeColor = Color.FromArgb(51,65,85); label.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
    }

    private void ToggleMapper() { if (_runCts is null) StartMapper(); else StopMapper(); }

    private void StartMapper(bool quiet = false)
    {
        var validation = ValidateConfiguration();
        if (validation.Count > 0)
        {
            SetConnectionState(validation[0], false, true);
            if (!quiet) MessageBox.Show(this, string.Join("\n", validation.Select(x => "• " + x)), "Check configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try { _joystick.Connect(_profile.ForcedControllerSlot); Logger.Info($"GMK connected: {_joystick.DeviceName}"); }
        catch (Exception ex)
        {
            Logger.Warn($"Connect failed: {ex.Message}");
            SetConnectionState("GMK not connected — reconnect it and close other GMK software", false, true);
            if (!quiet) MessageBox.Show(this, ex.Message, "Could not open GMK", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (_profile.OutputMode == MovementOutputMode.Controller)
        {
            try { _controllerOutput.Connect(); }
            catch (Exception ex)
            {
                _joystick.Dispose();
                if (!quiet) MessageBox.Show(this,
                    "Controller mode could not create the virtual Xbox controller. Make sure ViGEmBus is installed, then restart the app.\n\n" + ex.Message,
                    "Virtual controller unavailable", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        _profile.Deadzone = _deadzone.Value / 100.0;
        ScheduleProfileSave();
        var bindings = GetBindings();
        var mapper = new EightWayMapper(_profile);
        var analogMapper = new AnalogStickMapper(_profile);
        var outputMode = _profile.OutputMode;
        _runCts = new CancellationTokenSource();
        var runCts = _runCts;

        _runTask = Task.Factory.StartNew(() =>
        {
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            var lastVisualUpdate = 0L;
            var lastSuccessfulRead = Environment.TickCount64;
            var rateStarted = Environment.TickCount64;
            var readCount = 0;
            var disconnectShown = false;
            try
            {
                while (!runCts.IsCancellationRequested)
                {
                    if (!_joystick.TryRead(out var x, out var y, out var aPressed))
                    {
                        _keyboard.ReleaseAllInputs();
                        if (outputMode == MovementOutputMode.Controller && _controllerOutput.IsConnected)
                            _controllerOutput.Apply(0, 0, false, _profile.ControllerAButton);
                        if (!disconnectShown && Environment.TickCount64 - lastSuccessfulRead > 1000)
                        {
                            disconnectShown = true;
                            BeginInvoke((Action)(() => SetConnectionState("Connection lost — reconnect the GMK", false, true)));
                        }
                        if (Environment.TickCount64 - lastSuccessfulRead > 1000)
                        {
                            try
                            {
                                _joystick.Dispose(); _joystick.Connect(_profile.ForcedControllerSlot);
                                lastSuccessfulRead = Environment.TickCount64; disconnectShown = false;
                                BeginInvoke((Action)(() => SetConnectionState("GMK reconnected automatically", true, false)));
                            }
                            catch { Thread.Sleep(250); }
                        }
                        continue;
                    }

                    lastSuccessfulRead = Environment.TickCount64;
                    var processingStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    _lastInputTick = lastSuccessfulRead;
                    readCount++;
                    if (lastSuccessfulRead - rateStarted >= 1000)
                    {
                        _pollingRate = readCount * 1000.0 / Math.Max(1, lastSuccessfulRead - rateStarted);
                        readCount = 0; rateStarted = lastSuccessfulRead;
                    }
                    if (disconnectShown)
                    {
                        disconnectShown = false;
                        BeginInvoke((Action)(() => SetConnectionState("GMK connected", true, false)));
                    }

                    (x, y) = _profile.Calibrate(x, y);
                    MovementKeys movement;
                    AnalogStickOutput? analog = null;
                    if (outputMode == MovementOutputMode.Controller)
                    {
                        analog = analogMapper.Map(x, y);
                        _controllerOutput.Apply(analog.Value.X, analog.Value.Y, aPressed, _profile.ControllerAButton);
                        movement = AnalogToMovement(analog.Value);
                    }
                    else
                    {
                        movement = mapper.Map(new StickSample(x, y));
                        _keyboard.Apply(movement, bindings.Forward, bindings.Left, bindings.Backward, bindings.Right);
                        _keyboard.ApplyAction(bindings.Action, aPressed);
                    }
                    var processingMs = System.Diagnostics.Stopwatch.GetElapsedTime(processingStarted).TotalMilliseconds;
                    _latencyMs = _latencyMs == 0 ? processingMs : _latencyMs * 0.92 + processingMs * 0.08;
                    var now = Environment.TickCount64;
                    if (_trayPerformanceMode || !_windowActive) continue;
                    var visualInterval = _profile.LowLatencyMode ? 50 : 16;
                    if (now - lastVisualUpdate < visualInterval) continue;
                    lastVisualUpdate = now;
                    BeginInvoke((Action)(() => UpdateLiveDisplay(x, y, movement, aPressed, bindings, analog)));
                }
            }
            catch (Exception ex)
            {
                _keyboard.ReleaseAllInputs();
                if (!IsDisposed) BeginInvoke((Action)(() =>
                {
                    _deviceStatus.Text = $"Movement stopped: {ex.Message}";
                    SetConnectionState("Movement output stopped unexpectedly", false, true);
                    StopMapper();
                }));
            }
            finally { _keyboard.ReleaseAllInputs(); }
        }, runCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        SetConnectionState("GMK connected", true, false);
        SetRunningState(true);
    }

    private void StopMapper()
    {
        var cts = Interlocked.Exchange(ref _runCts, null);
        if (cts is null) return;
        cts.Cancel();
        _keyboard.ReleaseAllInputs();
        _controllerOutput.Dispose();
        _joystick.Dispose();
        cts.Dispose();
        SetRunningState(false);
    }

    private void SetRunningState(bool running)
    {
        _startButton.Text = running ? "Stop movement" : "Start movement";
        _trayMovementItem.Text = running ? "Stop movement" : "Start movement";
        _startButton.BackColor = running ? DangerColor : PrimaryColor;
        _runBadge.Text = running ? "RUNNING" : "READY";
        _runBadge.BackColor = running ? Color.FromArgb(220, 252, 231) : Color.FromArgb(226, 232, 240);
        _runBadge.ForeColor = running ? SuccessColor : Color.FromArgb(71, 85, 105);
        foreach (var button in _mappingButtons) button.Enabled = !running;
        _modeToggle.Enabled = !running;
        _diagonalLock.Enabled = !running;
        _diagonalAngle.Enabled = !running && _diagonalLock.Checked;
        _diagonalWindow.Enabled = !running && _diagonalLock.Checked;
        _diagonalStart.Enabled = !running && _diagonalLock.Checked;
        _antiDeadzone.Enabled = !running;
        _outerDeadzone.Enabled = !running;
        _rotation.Enabled = !running;
        _controllerAButton.Enabled = !running;
        _linearResponse.Enabled = !running;
        _curveButton.Enabled = !running;
        _presetPicker.Enabled = !running;
        _applyPresetButton.Enabled = !running;
        _advancedToggle.Enabled = !running;
        _dragZonesButton.Enabled = !running;
        _lowLatencyMode.Enabled = !running;
        _setupButton.Enabled = !running && _hidHide.IsInstalled;
        _wizardButton.Enabled = !running;
        _calibrateButton.Enabled = !running;
        _profilePicker.Enabled = !running;
        _newProfileButton.Enabled = !running;
        _deleteProfileButton.Enabled = !running && _profilePicker.Items.Count > 1;
        RebuildTrayProfiles();
    }

    private void UpdateLiveDisplay(double x, double y, MovementKeys movement, bool aPressed, KeyBindings bindings, AnalogStickOutput? analog = null)
    {
        _visualizer.SetState(x, y, _profile.Deadzone, _profile.MaxZone, movement, aPressed, analog?.X, analog?.Y);
        _rawValue.Text = $"{x:+0.00;-0.00;0.00}, {y:+0.00;-0.00;0.00}";
        if (analog is { } stick)
        {
            var angle = Math.Atan2(Math.Abs(stick.X), Math.Abs(stick.Y)) * 180.0 / Math.PI;
            _outputValue.Text = stick.X == 0 && stick.Y == 0 ? "Centered" : $"{angle:0.0}°  ({stick.X:+0.00;-0.00;0.00}, {stick.Y:+0.00;-0.00;0.00})";
        }
        else _outputValue.Text = Describe(movement, bindings);
        _aValue.Text = aPressed
            ? (_profile.OutputMode == MovementOutputMode.Controller ? $"Pressed → Xbox {_profile.ControllerAButton}" : $"Pressed → {DisplayKey(bindings.Action)}")
            : "Released";
        _aValue.ForeColor = aPressed ? SuccessColor : (_profile.DarkMode ? Color.FromArgb(235, 241, 249) : TextColor);
    }

    private static MovementKeys AnalogToMovement(AnalogStickOutput stick)
    {
        const double threshold = 0.05;
        return new MovementKeys(stick.Y > threshold, stick.X < -threshold, stick.Y < -threshold, stick.X > threshold);
    }

    private void SetupHidHide()
    {
        try
        {
            var ids = _hidHide.FindGmkInstanceIds();
            if (ids.Count == 0)
            {
                MessageBox.Show(this, "No GMK device was found. Connect it and try again.", "GMK not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _hidHide.ConfigureForMapper(ids[0]);
            _deviceStatus.Text = "HidHide setup complete. You can start movement now.";
            _deviceStatus.ForeColor = SuccessColor;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "HidHide setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void OpenSetupWizard()
    {
        if (_runCts is not null) return;
        using var wizard = new SetupWizardForm(_profile.DarkMode);
        if (wizard.ShowDialog(this) == DialogResult.OK)
        {
            _profile.SetupComplete = true;
            _profileStore.Save(_profile);
            _deviceStatus.Text = "Setup completed. You can start movement now.";
            _deviceStatus.ForeColor = SuccessColor;
        }
    }

    private void OpenCalibration()
    {
        if (_runCts is not null) return;
        using var calibration = new CalibrationForm(_profile);
        if (calibration.ShowDialog(this) == DialogResult.OK)
        {
            _profileStore.Save(_profile);
            SetConnectionState("Calibration saved for this profile", true, false);
        }
    }

    private void SwitchProfile()
    {
        if (_profilePicker.SelectedItem is not string name || name == _profile.Name) return;
        _profile = _profileStore.Load(name);
        _appSettings.LastProfileName = name; _appSettings.Save();
        LoadProfileControls();
        ApplyTheme();
        SetConnectionState($"Profile “{name}” loaded", true, false);
        RebuildTrayProfiles();
    }

    private void CreateProfile()
    {
        var name = PromptForProfileName();
        if (string.IsNullOrWhiteSpace(name)) return;
        _profile = _profileStore.Create(name, _profile);
        _appSettings.LastProfileName = _profile.Name; _appSettings.Save();
        ReloadProfilePicker(_profile.Name);
        LoadProfileControls();
        SetRunningState(false);
        RebuildTrayProfiles();
    }

    private void DeleteProfile()
    {
        if (_profilePicker.Items.Count <= 1) return;
        if (MessageBox.Show(this, $"Delete the profile “{_profile.Name}”?", "Delete profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _profileStore.Delete(_profile.Name);
        var next = _profileStore.GetNames().First();
        _profile = _profileStore.Load(next);
        _appSettings.LastProfileName = next; _appSettings.Save();
        ReloadProfilePicker(next);
        LoadProfileControls();
        ApplyTheme();
        RebuildTrayProfiles();
    }

    private void ReloadProfilePicker(string selected)
    {
        _profilePicker.Items.Clear();
        _profilePicker.Items.AddRange(_profileStore.GetNames().Cast<object>().ToArray());
        _profilePicker.SelectedItem = selected;
    }

    private void LoadProfileControls()
    {
        _deadzone.Value = Math.Clamp((int)Math.Round(_profile.Deadzone * 100), _deadzone.Minimum, _deadzone.Maximum);
        _diagonalLock.Checked = _profile.ControllerDiagonalLock;
        _diagonalAngle.Value = (decimal)Math.Clamp(_profile.ControllerDiagonalAngle, 0, 90);
        _diagonalWindow.Value = (decimal)Math.Clamp(_profile.ControllerDiagonalWindow, 0, 90);
        _diagonalStart.Value = (decimal)Math.Clamp(_profile.ControllerDiagonalStart, 0, 89);
        _antiDeadzone.Value = (decimal)Math.Clamp(_profile.ControllerAntiDeadzone * 100, 0, 95);
        _outerDeadzone.Value = (decimal)Math.Clamp(_profile.MaxZone * 100, 50, 100);
        _lowLatencyMode.Checked = _profile.LowLatencyMode;
        _rotation.Value = (decimal)Math.Clamp(_profile.ControllerRotation, -180, 180);
        _controllerAButton.SelectedItem = _profile.ControllerAButton;
        _linearResponse.Checked = _profile.ControllerLinearResponse;
        RefreshBindingButtons();
        UpdateModeUI();
    }

    private void UpdateModeUI()
    {
        var controller = _profile.OutputMode == MovementOutputMode.Controller;
        _modeToggle.Text = controller ? "CONTROLLER MOVEMENT   •   Analog" : "KEYBOARD MOVEMENT   •   Safe mode";
        _modeToggle.BackColor = controller ? Color.FromArgb(245, 158, 11) : Color.FromArgb(37, 99, 235);
        _modeToggle.ForeColor = Color.White;
        _modeToggle.FlatAppearance.BorderSize = 0;
        _modeDescription.Text = controller
            ? "Creates a virtual Xbox controller only while movement is running. Use this after Epic fixes mixed mouse/controller input."
            : "Outputs keyboard keys and keeps Fortnite from seeing controller movement. Recommended while the current Fortnite bug exists.";
        if (_keyboardSettingsPanel is not null) _keyboardSettingsPanel.Visible = !controller;
        if (_controllerSettingsPanel is not null) _controllerSettingsPanel.Visible = controller && _advancedVisible;
        if (_settingsHost is not null) _settingsHost.Height = controller ? (_advancedVisible ? 205 : 0) : 185;
        _advancedToggle.Visible = controller;
        _dragZonesButton.Enabled = controller && _runCts is null;
        _outputCaption.Text = controller ? "Controller output" : "Keyboard output";
        if (_subtitle is not null) _subtitle.Text = controller ? "GMK to Controller Movement" : "GMK to Keyboard Movement";
        _diagonalAngle.Enabled = controller && _diagonalLock.Checked && _runCts is null;
        _diagonalWindow.Enabled = controller && _diagonalLock.Checked && _runCts is null;
        _diagonalStart.Enabled = controller && _diagonalLock.Checked && _runCts is null;
        _diagnosticTimer.Interval = _profile.LowLatencyMode ? 2000 : 1000;
        if (!controller) { _visualizer.SetZoneEditing(false); _dragZonesButton.Text = "Drag zones"; }
        RefreshVisualizerOverlay();
    }

    private void RefreshVisualizerOverlay()
        => _visualizer.SetControllerOverlay(_profile.OutputMode == MovementOutputMode.Controller && _profile.ControllerDiagonalLock,
            _profile.ControllerDiagonalStart, _profile.ControllerDiagonalWindow, _profile.ControllerRotation);

    private void ToggleAdvanced()
    {
        _advancedVisible = !_advancedVisible;
        _advancedToggle.Text = _advancedVisible ? "Hide advanced" : "Show advanced";
        UpdateModeUI();
    }

    private void ToggleZoneEditor()
    {
        var enabled = _dragZonesButton.Text != "Finish dragging";
        _dragZonesButton.Text = enabled ? "Finish dragging" : "Drag zones";
        _visualizer.SetZoneEditing(enabled);
        _visualizer.SetEmphasis(enabled ? "diagonal" : null);
        if (enabled) SetConnectionState("Drag either purple boundary on the live preview", true, false);
    }

    private void ApplyDraggedZone(double start, double width)
    {
        _profile.ControllerDiagonalStart = Math.Clamp(start, 0, 89);
        _profile.ControllerDiagonalWindow = Math.Clamp(width, 0, 90);
        _diagonalStart.Value = (decimal)_profile.ControllerDiagonalStart;
        _diagonalWindow.Value = (decimal)_profile.ControllerDiagonalWindow;
        ScheduleProfileSave();
    }

    private void ScheduleProfileSave()
    {
        _profileSaveTimer.Stop();
        _profileSaveTimer.Start();
    }

    private List<string> ValidateConfiguration()
    {
        var issues = new List<string>();
        if (_profile.Deadzone >= _profile.MaxZone - 0.02) issues.Add("The centre deadzone must be smaller than the outer deadzone.");
        if (_profile.OutputMode == MovementOutputMode.Controller && _profile.ControllerDiagonalStart + _profile.ControllerDiagonalWindow > 90.01)
            issues.Add("The diagonal zone extends past forward. Reduce its start angle or width.");
        if (_profile.OutputMode == MovementOutputMode.Keyboard)
        {
            var keys = new[] { _profile.ForwardKey, _profile.LeftKey, _profile.BackwardKey, _profile.RightKey };
            if (keys.Distinct().Count() != keys.Length) issues.Add("Each movement direction must use a different keyboard key.");
        }
        return issues;
    }

    private void RebuildTrayProfiles()
    {
        _trayProfilesItem.DropDownItems.Clear();
        foreach (var profileName in _profileStore.GetNames())
        {
            var item = new ToolStripMenuItem(profileName) { Checked = string.Equals(profileName, _profile.Name, StringComparison.OrdinalIgnoreCase), Enabled = _runCts is null };
            item.Click += (_, _) => { if (_runCts is not null) return; _profilePicker.SelectedItem = profileName; };
            _trayProfilesItem.DropDownItems.Add(item);
        }
    }

    private void OpenCurveEditor()
    {
        if (_runCts is not null) return;
        using var editor = new CurveEditorForm(_profile.ControllerCurveExponent, _profile.DarkMode);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        _profile.ControllerCurveExponent = editor.Exponent;
        _profile.ControllerLinearResponse = Math.Abs(editor.Exponent - 1.0) < 0.001;
        _linearResponse.Checked = _profile.ControllerLinearResponse;
        _profileStore.Save(_profile);
    }

    private void ApplyPreset()
    {
        if (_runCts is not null || _presetPicker.SelectedItem is not string preset) return;
        switch (preset)
        {
            case "Fortnite analog 71°":
                _profile.OutputMode = MovementOutputMode.Controller; _profile.ControllerDiagonalLock = true;
                _profile.ControllerDiagonalAngle = 71; _profile.ControllerDiagonalStart = 0; _profile.ControllerDiagonalWindow = 20; _profile.ControllerRotation = 0;
                _profile.ControllerLinearResponse = true; _profile.ControllerCurveExponent = 1; break;
            case "Legacy GMK 76°":
                _profile.OutputMode = MovementOutputMode.Controller; _profile.ControllerDiagonalLock = true;
                _profile.ControllerDiagonalAngle = 76; _profile.ControllerDiagonalStart = 0; _profile.ControllerDiagonalWindow = 30; _profile.ControllerRotation = 0;
                _profile.ControllerLinearResponse = true; _profile.ControllerCurveExponent = 1; break;
            case "Raw analog":
                _profile.OutputMode = MovementOutputMode.Controller; _profile.ControllerDiagonalLock = false;
                _profile.ControllerRotation = 0; _profile.ControllerLinearResponse = true; _profile.ControllerCurveExponent = 1; break;
            default:
                _profile.OutputMode = MovementOutputMode.Keyboard; break;
        }
        _profileStore.Save(_profile); LoadProfileControls(); SetConnectionState($"Preset “{preset}” applied", true, false);
    }

    private void RefreshDiagnostics()
    {
        if (IsDisposed) return;
        _diagDevice.Text = _joystick.IsConnected ? "Connected and reading" : _runCts is null ? "Not opened — start movement to test" : "Disconnected";
        _diagDevice.ForeColor = _joystick.IsConnected ? SuccessColor : MutedColor;
        _diagHidHide.Text = _hidHide.IsInstalled ? "Installed and available" : "Not installed";
        using var vigemKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ViGEmBus");
        var vigem = vigemKey is not null;
        _diagViGEm.Text = vigem ? "Installed" : "Not detected — required for Controller mode";
        _diagOutput.Text = _runCts is null ? "Stopped" : _profile.OutputMode == MovementOutputMode.Controller ? "Virtual Xbox controller active" : "Keyboard movement active";
        var age = _lastInputTick == 0 ? 0 : Environment.TickCount64 - _lastInputTick;
        _diagRate.Text = _runCts is null ? "Not measuring" : age > 1000 ? "No recent input reports" : $"{_pollingRate:0} reports/second";
        _diagLatency.Text = _runCts is null ? "Start movement to measure" : age > 1000 ? "No recent reports" : $"{_latencyMs:0.000} ms average processing time";
        var xRange = _profile.MaxX - _profile.MinX; var yRange = _profile.MaxY - _profile.MinY;
        var centerOffset = Math.Max(Math.Abs(_profile.CenterX), Math.Abs(_profile.CenterY));
        var healthy = xRange >= 1.7 && yRange >= 1.7 && centerOffset <= 0.12;
        _diagCalibration.Text = healthy ? "Good — centre and axis ranges look healthy" : "Calibration recommended — open Movement and select Calibrate stick";
        _diagCalibration.ForeColor = healthy ? SuccessColor : Color.FromArgb(217,119,6);
        _diagProfile.Text = $"{_profile.Name} — {_profile.OutputMode} mode";
        _dashboardDevice.Text = _joystick.IsConnected ? "● GMK connected" : "○ GMK waiting";
        _dashboardDevice.ForeColor = _joystick.IsConnected ? SuccessColor : MutedColor;
        _dashboardMode.Text = _runCts is null ? $"{_profile.OutputMode} ready" : $"{_profile.OutputMode} active";
        _dashboardLatency.Text = _runCts is null ? "Latency —" : $"{_latencyMs:0.000} ms";
    }

    private void OpenTroubleshooter()
    {
        using var vigemKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ViGEmBus");
        using var form = new TroubleshooterForm(_hidHide.IsInstalled, vigemKey is not null, _joystick.IsConnected, _profile.OutputMode, _profile.DarkMode);
        form.ShowDialog(this);
    }

    private string? PromptForProfileName()
    {
        using var prompt = new Form { Text = "New profile", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(390, 150), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = Font };
        var label = new Label { Text = "Profile name", AutoSize = true, Location = new Point(20, 18) };
        var input = new TextBox { Location = new Point(20, 45), Width = 350 };
        var create = new Button { Text = "Create", DialogResult = DialogResult.OK, Location = new Point(270, 96), Size = new Size(100, 34) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(160, 96), Size = new Size(100, 34) };
        prompt.Controls.AddRange([label, input, create, cancel]); prompt.AcceptButton = create; prompt.CancelButton = cancel;
        return prompt.ShowDialog(this) == DialogResult.OK ? input.Text : null;
    }

    private void SetConnectionState(string message, bool success, bool warning)
    {
        _connectionMessage = message; _connectionSuccess = success; _connectionWarning = warning;
        _connectionBanner.Text = (success ? "●  " : warning ? "▲  " : "●  ") + message;
        var dark = _profile.Appearance != AppearanceMode.Light;
        _connectionBanner.BackColor = success ? (dark ? Color.FromArgb(20,83,61) : Color.FromArgb(220,252,231))
            : warning ? (dark ? Color.FromArgb(127,29,29) : Color.FromArgb(254,226,226))
            : (dark ? Color.FromArgb(30,41,59) : Color.FromArgb(241,245,249));
        _connectionBanner.ForeColor = success ? (dark ? Color.FromArgb(167,243,208) : Color.FromArgb(21,128,61))
            : warning ? (dark ? Color.FromArgb(254,202,202) : Color.FromArgb(185,28,28))
            : (dark ? Color.FromArgb(203,213,225) : Color.FromArgb(71,85,105));
    }

    private void MinimizeToTray()
    {
        if (WindowState != FormWindowState.Minimized) return;
        _trayPerformanceMode = true;
        _diagnosticTimer.Stop();
        _trayIcon.Visible = true;
        _trayIcon.Text = _runCts is null ? "GMK Mapper — ready" : $"GMK Mapper — {_profile.OutputMode} movement active";
        Hide();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        _trayPerformanceMode = false;
        _diagnosticTimer.Start();
        RefreshDiagnostics();
        _trayIcon.Visible = false;
    }

    private void BeginKeyCapture(BindingTarget target, Button button)
    {
        if (_runCts is not null) return;
        _capturing = target;
        foreach (var item in _mappingButtons) item.Text = item == button ? "Press any key…" : BindingButtonText(TargetForButton(item));
        button.Focus();
    }

    private void CaptureKey(object? sender, KeyEventArgs e)
    {
        if (_capturing is not { } target) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        if (e.KeyCode != Keys.Escape) SetProfileKey(target, e.KeyCode);
        _capturing = null;
        _profileStore.Save(_profile);
        RefreshBindingButtons();
    }

    private void RefreshBindingButtons()
    {
        _forwardButton.Text = BindingButtonText(BindingTarget.Forward);
        _leftButton.Text = BindingButtonText(BindingTarget.Left);
        _backwardButton.Text = BindingButtonText(BindingTarget.Backward);
        _rightButton.Text = BindingButtonText(BindingTarget.Right);
        _actionButton.Text = BindingButtonText(BindingTarget.Action);
    }

    private string BindingButtonText(BindingTarget target) => $"{DisplayKey(GetProfileKey(target))}   Change";
    private BindingTarget TargetForButton(Button button) => button == _forwardButton ? BindingTarget.Forward : button == _leftButton ? BindingTarget.Left : button == _backwardButton ? BindingTarget.Backward : button == _rightButton ? BindingTarget.Right : BindingTarget.Action;

    private Keys GetProfileKey(BindingTarget target) => target switch
    {
        BindingTarget.Forward => NormalizeKey(_profile.ForwardKey, Keys.W),
        BindingTarget.Left => NormalizeKey(_profile.LeftKey, Keys.A),
        BindingTarget.Backward => NormalizeKey(_profile.BackwardKey, Keys.S),
        BindingTarget.Right => NormalizeKey(_profile.RightKey, Keys.D),
        _ => NormalizeKey(_profile.SprintKey, Keys.Oemcomma)
    };

    private void SetProfileKey(BindingTarget target, Keys key)
    {
        switch (target)
        {
            case BindingTarget.Forward: _profile.ForwardKey = (int)key; break;
            case BindingTarget.Left: _profile.LeftKey = (int)key; break;
            case BindingTarget.Backward: _profile.BackwardKey = (int)key; break;
            case BindingTarget.Right: _profile.RightKey = (int)key; break;
            case BindingTarget.Action: _profile.SprintKey = (int)key; break;
        }
    }

    private KeyBindings GetBindings() => new(GetProfileKey(BindingTarget.Forward), GetProfileKey(BindingTarget.Left), GetProfileKey(BindingTarget.Backward), GetProfileKey(BindingTarget.Right), GetProfileKey(BindingTarget.Action));
    private static Keys NormalizeKey(int stored, Keys fallback) => stored == 0 ? fallback : (Keys)stored;

    private static string Describe(MovementKeys movement, KeyBindings bindings)
    {
        var keys = new List<string>(2);
        if (movement.Forward) keys.Add(DisplayKey(bindings.Forward));
        if (movement.Left) keys.Add(DisplayKey(bindings.Left));
        if (movement.Backward) keys.Add(DisplayKey(bindings.Backward));
        if (movement.Right) keys.Add(DisplayKey(bindings.Right));
        return keys.Count == 0 ? "Centered" : string.Join(" + ", keys);
    }

    private void ApplyTheme()
    {
        var dark = _profile.Appearance != AppearanceMode.Light;
        _profile.DarkMode = dark;
        var black = _profile.Appearance == AppearanceMode.Black;
        var page = black ? Color.FromArgb(2, 4, 8) : dark ? Color.FromArgb(11, 17, 27) : Color.FromArgb(226, 232, 240);
        var card = black ? Color.FromArgb(8, 11, 16) : dark ? Color.FromArgb(22, 30, 43) : Color.FromArgb(248, 250, 252);
        var text = dark ? Color.FromArgb(235, 241, 249) : TextColor;
        var muted = dark ? Color.FromArgb(156, 169, 188) : MutedColor;
        BackColor = page;
        ForeColor = text;
        if (_page is not null) ThemeTree(_page, page, card, text, muted);
        _visualizer.SetDarkMode(dark);
        _darkButton.Text = $"Theme: {_profile.Appearance}";
        _darkButton.BackColor = dark ? Color.FromArgb(42, 53, 70) : Color.FromArgb(249, 250, 252);
        _darkButton.ForeColor = dark ? Color.White : TextColor;
        foreach (var chip in new[] { _dashboardDevice, _dashboardMode, _dashboardLatency })
        {
            chip.BackColor = black ? Color.FromArgb(17,24,39) : dark ? Color.FromArgb(35,45,61) : Color.FromArgb(226,232,240);
            if (chip != _dashboardDevice || !_joystick.IsConnected) chip.ForeColor = dark ? Color.FromArgb(203,213,225) : Color.FromArgb(51,65,85);
        }
        SetRunningState(_runCts is not null);
        UpdateModeUI();
        SetConnectionState(_connectionMessage, _connectionSuccess, _connectionWarning);
        TitleBarTheme.Apply(this, dark);
        Invalidate(true);
    }

    private void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs) return;
        var dark = _profile.Appearance != AppearanceMode.Light; var selected = e.Index == tabs.SelectedIndex;
        var back = selected ? (dark ? Color.FromArgb(35,45,61) : Color.White) : (dark ? Color.FromArgb(15,22,33) : Color.FromArgb(226,232,240));
        using var brush = new SolidBrush(back); e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, Font, e.Bounds, dark ? Color.White : TextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void ThemeTree(Control control, Color page, Color card, Color text, Color muted)
    {
        if (Equals(control.Tag, "page")) control.BackColor = page;
        else if (Equals(control.Tag, "card")) control.BackColor = card;
        else if (control is TableLayoutPanel or FlowLayoutPanel) control.BackColor = control.Parent?.BackColor ?? card;
        else if (control is TabPage) control.BackColor = card;
        if (control is ModernTabControl modernTabs) modernTabs.SetTheme(_profile.Appearance != AppearanceMode.Light, _profile.Appearance == AppearanceMode.Black);

        if (control is CheckBox checkBox) { checkBox.ForeColor = text; checkBox.BackColor = control.Parent?.BackColor ?? card; }
        if (control is ComboBox comboBox) { comboBox.BackColor = _profile.DarkMode ? Color.FromArgb(35, 45, 61) : Color.White; comboBox.ForeColor = text; }
        if (control is NumericUpDown numeric) { numeric.BackColor = _profile.DarkMode ? Color.FromArgb(35, 45, 61) : Color.White; numeric.ForeColor = text; }

        if (control is Label label && label != _runBadge)
        {
            if (Equals(label.Tag, "text")) label.ForeColor = text;
            else if (Equals(label.Tag, "muted")) label.ForeColor = muted;
        }

        if (control is Button button && button != _startButton && button != _darkButton && button != _modeToggle)
        {
            button.BackColor = _profile.DarkMode ? Color.FromArgb(35, 45, 61) : Color.FromArgb(249, 250, 252);
            button.ForeColor = _profile.DarkMode ? Color.FromArgb(235, 241, 249) : TextColor;
            button.FlatAppearance.BorderColor = _profile.DarkMode ? Color.FromArgb(66, 79, 99) : Color.FromArgb(205, 212, 222);
        }

        foreach (Control child in control.Controls) ThemeTree(child, page, card, text, muted);
    }

    private static string DisplayKey(Keys key) => key switch
    {
        Keys.Oemcomma => ",", Keys.OemPeriod => ".", Keys.OemSemicolon => ";", Keys.OemQuestion => "/",
        Keys.OemQuotes => "'", Keys.OemOpenBrackets => "[", Keys.OemCloseBrackets => "]", Keys.OemMinus => "-",
        Keys.Oemplus => "=", Keys.Oemtilde => "`", Keys.OemPipe => "\\", Keys.Space => "Space",
        Keys.ShiftKey => "Shift", Keys.ControlKey => "Ctrl", Keys.Menu => "Alt", _ => key.ToString()
    };
}
