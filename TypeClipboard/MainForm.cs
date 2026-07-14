using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TypeClipboard;

public partial class MainForm : Form
{
    private const int WmActivate = 0x0006;
    private const int WmHotKey = 0x0312;
    private const int WmClipboardUpdate = 0x031D;
    private const int WaInactive = 0;
    private const uint GaRoot = 2;
    private const int SwRestore = 9;
    private const int EmergencyHotKeyId = 0x5401;
    private const int TypeShortcutHotKeyId = 0x5402;
    private const int StopShortcutHotKeyId = 0x5403;

    private HotKeyManager? _emergencyHotKeyManager;
    private HotKeyManager? _typeShortcutHotKeyManager;
    private HotKeyManager? _stopShortcutHotKeyManager;
    private System.Windows.Forms.Timer? _clipboardPollTimer;
    private CancellationTokenSource? _typingCancellation;
    private AppSettings _settings = new();
    private string? _lastClipboardText;
    private string? _hotKeyWarning;
    private string? _typeShortcutWarning;
    private string? _stopShortcutWarning;
    private bool? _lastClipboardContainedText;
    private bool _isInitializingShortcuts;
    private bool _isTyping;
    private bool _isClosing;
    private Keys _emergencyShortcut = Keys.F8;
    private Keys _typeShortcut = Keys.F9;
    private Keys _stopShortcut = Keys.F10;
    private ShortcutAction _shortcutCaptureTarget;
    private IntPtr _lastExternalWindow;
    private IntPtr _lastExternalFocusWindow;
    private readonly TypeHistoryStore _historyStore = new();
    private TypeHistoryPanel? _historyPanel;
    private CheckBox? _saveHistoryCheckBox;
    private NumericUpDown? _maximumHistoryNumeric;
    private TableLayoutPanel? _mainLayout;
    private int _mainLayoutPadding;
    private int _collapsedClientWidth;

    public MainForm()
    {
        InitializeComponent();
    }

    private bool IsTyping
    {
        get => _isTyping;
        set
        {
            _isTyping = value;
            typeButton.Enabled = !value;
            stopButton.Enabled = value;
            copyClipboardButton.Enabled = !value;
            UpdateShortcutButtonStates();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmActivate && (m.WParam.ToInt32() & 0xFFFF) != WaInactive)
        {
            RememberExternalWindow(m.LParam);
        }

        if (m.Msg == WmHotKey)
        {
            if (_shortcutCaptureTarget != ShortcutAction.None)
            {
                CaptureShortcut(ShortcutFromHotKeyMessage(m.LParam));
                return;
            }

            int hotKeyId = m.WParam.ToInt32();
            if (hotKeyId == TypeShortcutHotKeyId)
            {
                StartTypingFromShortcut();
                return;
            }

            if (hotKeyId is EmergencyHotKeyId or StopShortcutHotKeyId)
            {
                RequestStop("Stopped");
                return;
            }
        }

        if (m.Msg == WmClipboardUpdate)
        {
            LoadClipboardText("Auto loaded", forceReload: false, showErrors: false);
            return;
        }

        base.WndProc(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_shortcutCaptureTarget != ShortcutAction.None)
        {
            if (NormalizeShortcut(keyData) == Keys.Escape)
            {
                CancelShortcutCapture("Shortcut change cancelled");
                return true;
            }

            CaptureShortcut(keyData);
            return true;
        }

        if (keyData == (Keys.Control | Keys.H))
        {
            OpenHistory();
            return true;
        }

        if (keyData == Keys.Escape && _historyPanel?.Visible == true && !IsTyping)
        {
            ToggleHistory(false);
            return true;
        }

        if (!IsTyping &&
            _settings.TypeShortcutEnabled &&
            NormalizeShortcut(keyData) == _typeShortcut)
        {
            StartTypingFromShortcut();
            return true;
        }

        if (IsTyping &&
            ((_settings.StopShortcutEnabled && NormalizeShortcut(keyData) == _stopShortcut) ||
             (hotKeyEnabledCheckBox.Checked && NormalizeShortcut(keyData) == _emergencyShortcut)))
        {
            RequestStop("Stopped");
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (IsTyping)
        {
            RequestStop("Stopped");
            return;
        }

        LoadClipboardText("Auto loaded", forceReload: false, showErrors: false);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);

        if (_shortcutCaptureTarget != ShortcutAction.None)
        {
            CancelShortcutCapture("Shortcut change cancelled");
        }

        if (!IsTyping && !IsDisposed && IsHandleCreated)
        {
            BeginInvoke(() => RememberExternalWindow(GetForegroundWindow()));
        }
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        _emergencyHotKeyManager = new HotKeyManager(Handle, EmergencyHotKeyId);
        _typeShortcutHotKeyManager = new HotKeyManager(Handle, TypeShortcutHotKeyId);
        _stopShortcutHotKeyManager = new HotKeyManager(Handle, StopShortcutHotKeyId);
        InitializeShortcutButtons();
        InitializeHistoryUi();
        await LoadHistoryAsync();
        RegisterConfiguredHotKeys();
        RegisterClipboardListener();
        StartClipboardPolling();
        LoadClipboardText("Auto loaded", forceReload: true, showErrors: true);
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _isClosing = true;
        RequestStop("Stopped");
        _clipboardPollTimer?.Stop();
        _clipboardPollTimer?.Dispose();
        RemoveClipboardFormatListener(Handle);
        _emergencyHotKeyManager?.Dispose();
        _typeShortcutHotKeyManager?.Dispose();
        _stopShortcutHotKeyManager?.Dispose();
    }

    private void copyClipboardButton_Click(object? sender, EventArgs e)
    {
        LoadClipboardText("Loaded", forceReload: true, showErrors: true);
    }

    private void LoadClipboardText(string successPrefix, bool forceReload, bool showErrors)
    {
        if (IsTyping)
        {
            return;
        }

        try
        {
            if (!Clipboard.ContainsText())
            {
                bool clipboardStateChanged = _lastClipboardContainedText != false;
                _lastClipboardContainedText = false;
                _lastClipboardText = null;

                if (forceReload || clipboardStateChanged)
                {
                    clipboardTextBox.Clear();
                }

                if (showErrors || clipboardStateChanged)
                {
                    SetIdleStatus("Clipboard has no text");
                }

                return;
            }

            string clipboardText = Clipboard.GetText();
            _lastClipboardContainedText = true;
            if (!forceReload && clipboardText == _lastClipboardText)
            {
                return;
            }

            _lastClipboardText = clipboardText;
            clipboardTextBox.Text = clipboardText;
            SetIdleStatus($"{successPrefix} {clipboardTextBox.TextLength} characters");
        }
        catch (Exception ex) when (ex is ExternalException or ThreadStateException or InvalidOperationException)
        {
            if (showErrors)
            {
                SetIdleStatus($"Error: {ex.Message}");
            }
        }
    }

    private async void typeButton_Click(object? sender, EventArgs e)
    {
        await StartTypingAsync(
            clipboardTextBox.Text,
            _lastExternalWindow,
            preferredFocusWindow: _lastExternalFocusWindow);
    }

    private void StartTypingFromShortcut()
    {
        IntPtr targetWindow = GetExternalRootWindow(GetForegroundWindow());
        IntPtr targetFocusWindow = targetWindow == IntPtr.Zero
            ? _lastExternalFocusWindow
            : GetFocusedWindow(targetWindow);
        _ = StartTypingAsync(
            clipboardTextBox.Text,
            targetWindow,
            _typeShortcut,
            targetFocusWindow);
    }

    private async Task StartTypingAsync(
        string text,
        IntPtr preferredTarget = default,
        Keys triggeringShortcut = Keys.None,
        IntPtr preferredFocusWindow = default)
    {
        if (IsTyping)
        {
            return;
        }

        LoadClipboardText("Auto loaded", forceReload: false, showErrors: false);

        if (string.IsNullOrWhiteSpace(text))
        {
            SetIdleStatus("Textbox is empty");
            return;
        }

        IntPtr targetWindow;
        try
        {
            targetWindow = ResolveTypingTarget(preferredTarget);
        }
        catch (TargetWindowChangedException ex)
        {
            SetIdleStatus(ex.Message);
            return;
        }

        IntPtr targetFocusWindow = ResolveTypingFocus(targetWindow, preferredFocusWindow);

        clipboardTextBox.Text = text;
        TypeHistoryItem? historyItem = null;
        if (_settings.SaveTypeHistory && _historyPanel?.IsPaused != true)
        {
            historyItem = _historyStore.AddOrReuse(text, _settings.MaximumHistoryItems);
        }

        _typingCancellation?.Dispose();
        _typingCancellation = new CancellationTokenSource();
        CancellationToken token = _typingCancellation.Token;

        IsTyping = true;
        statusLabel.Text = "Typing...";

        try
        {
            if (historyItem is not null)
            {
                await SaveHistoryAsync("History saved");
                statusLabel.Text = "Typing...";
            }

            int startDelay = (int)startDelayNumeric.Value;
            int interkeyDelay = (int)interkeyDelayNumeric.Value;

            token.ThrowIfCancellationRequested();
            ThrowIfAnotherExternalWindowSelected(targetWindow);
            await WaitForShortcutReleaseAsync(triggeringShortcut, token);
            ThrowIfAnotherExternalWindowSelected(targetWindow);
            await ActivateTargetWindowAsync(targetWindow, token);
            await DelayAndCheckCancellation(startDelay, token);
            if (targetFocusWindow == IntPtr.Zero)
            {
                targetFocusWindow = GetFocusedWindow(targetWindow);
            }
            ThrowIfTargetWindowChanged(targetWindow, targetFocusWindow);

            for (int index = 0; index < text.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                ThrowIfTargetWindowChanged(targetWindow, targetFocusWindow);

                char character = text[index];
                if (character == '\r')
                {
                    InputSimulator.SendEnter();
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }
                }
                else if (character == '\n')
                {
                    InputSimulator.SendEnter();
                }
                else
                {
                    InputSimulator.SendCharacter(character);
                }

                await DelayAndCheckCancellation(interkeyDelay, token);
            }

            token.ThrowIfCancellationRequested();
            ThrowIfTargetWindowChanged(targetWindow, targetFocusWindow);
            if (typeEnterCheckBox.Checked)
            {
                InputSimulator.SendEnter();
            }

            SetIdleStatus("Completed");
            await SetHistoryStatusAsync(historyItem, TypeHistoryStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            if (!_isClosing)
            {
                SetIdleStatus("Stopped");
            }
            await SetHistoryStatusAsync(historyItem, TypeHistoryStatus.Stopped);
        }
        catch (TargetWindowChangedException ex)
        {
            if (!_isClosing)
            {
                SetIdleStatus(ex.Message);
            }
            await SetHistoryStatusAsync(historyItem, TypeHistoryStatus.Stopped);
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                SetIdleStatus($"Error: {ex.Message}");
            }
            await SetHistoryStatusAsync(historyItem, TypeHistoryStatus.Failed);
        }
        finally
        {
            if (!_isClosing && !IsDisposed)
            {
                IsTyping = false;
            }

            _typingCancellation?.Dispose();
            _typingCancellation = null;
        }
    }

    private void stopButton_Click(object? sender, EventArgs e)
    {
        RequestStop("Stopped");
    }

    private void hotKeyEnabledCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isInitializingShortcuts)
        {
            return;
        }

        bool previousEnabled = _settings.EmergencyShortcutEnabled;
        bool requestedEnabled = hotKeyEnabledCheckBox.Checked;
        _settings.EmergencyShortcutEnabled = requestedEnabled;
        string? registrationWarning = RegisterShortcutAction(ShortcutAction.Emergency);
        if (requestedEnabled && registrationWarning is not null)
        {
            _settings.EmergencyShortcutEnabled = previousEnabled;
            SetEmergencyShortcutCheckBox(previousEnabled);
            RegisterShortcutAction(ShortcutAction.Emergency);
            SetIdleStatus($"{FormatShortcut(_emergencyShortcut)} is unavailable; emergency shortcut stayed disabled");
            return;
        }

        if (!SaveSettings(requestedEnabled
                ? "Emergency shortcut enabled"
                : "Emergency shortcut disabled"))
        {
            _settings.EmergencyShortcutEnabled = previousEnabled;
            SetEmergencyShortcutCheckBox(previousEnabled);
            RegisterShortcutAction(ShortcutAction.Emergency);
        }
    }

    private void emergencyShortcutButton_Click(object? sender, EventArgs e)
    {
        BeginShortcutCapture(ShortcutAction.Emergency);
    }

    private void typeShortcutButton_Click(object? sender, EventArgs e)
    {
        BeginShortcutCapture(ShortcutAction.Type);
    }

    private void stopShortcutButton_Click(object? sender, EventArgs e)
    {
        BeginShortcutCapture(ShortcutAction.Stop);
    }

    private void SetEmergencyShortcutCheckBox(bool isChecked)
    {
        _isInitializingShortcuts = true;
        hotKeyEnabledCheckBox.Checked = isChecked;
        _isInitializingShortcuts = false;
    }

    private void alwaysOnTopCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        TopMost = alwaysOnTopCheckBox.Checked;

        if (_isInitializingShortcuts)
        {
            return;
        }

        _settings.AlwaysOnTop = alwaysOnTopCheckBox.Checked;
        SaveSettings(alwaysOnTopCheckBox.Checked ? "Always on top enabled" : "Always on top disabled");
    }

    private bool SaveSettings(string successStatus)
    {
        try
        {
            _settings.Save();
            SetIdleStatus(successStatus);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetIdleStatus($"Error saving settings: {ex.Message}");
            return false;
        }
    }

    private void clipboardTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!IsTyping)
        {
            UpdateCharacterCountStatus();
        }
    }

    private void InitializeShortcutButtons()
    {
        _settings = AppSettings.Load();
        _isInitializingShortcuts = true;

        alwaysOnTopCheckBox.Checked = _settings.AlwaysOnTop;
        TopMost = _settings.AlwaysOnTop;
        hotKeyEnabledCheckBox.Checked = _settings.EmergencyShortcutEnabled;
        _emergencyShortcut = LoadShortcut(_settings.EmergencyShortcutKeyData, Keys.F8);
        _typeShortcut = LoadShortcut(_settings.TypeShortcutKeyData, Keys.F9);
        _stopShortcut = LoadShortcut(_settings.StopShortcutKeyData, Keys.F10);
        if (_settings.TypeShortcutEnabled &&
            _settings.EmergencyShortcutEnabled &&
            _typeShortcut == _emergencyShortcut)
        {
            _typeShortcut = FindAvailableFunctionKey(Keys.F9, _emergencyShortcut);
        }

        List<Keys> enabledShortcuts = [];
        if (_settings.EmergencyShortcutEnabled)
        {
            enabledShortcuts.Add(_emergencyShortcut);
        }

        if (_settings.TypeShortcutEnabled)
        {
            enabledShortcuts.Add(_typeShortcut);
        }

        if (_settings.StopShortcutEnabled && enabledShortcuts.Contains(_stopShortcut))
        {
            _stopShortcut = FindAvailableFunctionKey(Keys.F10, enabledShortcuts.ToArray());
        }

        StoreShortcutsInSettings();
        UpdateShortcutButtonText();

        _isInitializingShortcuts = false;
        UpdateShortcutButtonStates();
    }

    private void InitializeHistoryUi()
    {
        _historyPanel = new TypeHistoryPanel
        {
            Dock = DockStyle.Right,
            Visible = false,
            Width = (int)Math.Round(330 * DeviceDpi / 96f)
        };
        _historyPanel.LoadRequested += LoadHistoryItem;
        _historyPanel.TypeRequested += item => _ = StartTypingAsync(item.Content);
        _historyPanel.CopyRequested += CopyHistoryItem;
        _historyPanel.PinRequested += async item => { item.IsPinned = !item.IsPinned; await SaveHistoryAsync("History saved"); };
        _historyPanel.DeleteRequested += DeleteHistoryItem;
        _historyPanel.ClearRequested += ClearUnpinnedHistory;
        _historyPanel.DeleteAllRequested += DeleteAllHistory;
        _historyPanel.PauseChanged += paused => SetIdleStatus(paused ? "History paused" : "History resumed");
        Controls.Add(_historyPanel);
        _historyPanel.BringToFront();

        Button historyButton = new() { Text = "Type History", Location = new Point(120, 332), Size = new Size(110, 27) };
        historyButton.Click += (_, _) => ToggleHistory(_historyPanel?.Visible != true);

        _saveHistoryCheckBox = new CheckBox { Text = "Save Type History", AutoSize = true, Checked = _settings.SaveTypeHistory, Location = new Point(241, 336) };
        _saveHistoryCheckBox.CheckedChanged += (_, _) =>
        {
            _settings.SaveTypeHistory = _saveHistoryCheckBox.Checked;
            SaveSettings(_settings.SaveTypeHistory ? "History saving enabled" : "History saving disabled");
        };

        _maximumHistoryNumeric = new NumericUpDown { Minimum = 20, Maximum = 1000, Value = Math.Clamp(_settings.MaximumHistoryItems, 20, 1000), Location = new Point(352, 364), Size = new Size(120, 23) };
        Label maximumLabel = new() { Text = "Maximum history items", AutoSize = true, Location = new Point(213, 367) };
        _maximumHistoryNumeric.ValueChanged += async (_, _) =>
        {
            _settings.MaximumHistoryItems = (int)_maximumHistoryNumeric.Value;
            _historyStore.Trim(_settings.MaximumHistoryItems);
            SaveSettings("History limit updated");
            await SaveHistoryAsync("History saved");
        };
        ArrangeMainControls(historyButton, maximumLabel);
        _collapsedClientWidth = ClientSize.Width;
    }

    private void ArrangeMainControls(Button historyButton, Label maximumLabel)
    {
        int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96f);

        _mainLayoutPadding = Scale(12);
        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(_mainLayoutPadding),
            ColumnCount = 1,
            RowCount = 8
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(42)));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(31)));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(31)));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(31)));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(31)));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(35)));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(28)));

        clipboardTextBox.Dock = DockStyle.Fill;
        clipboardTextBox.Margin = new Padding(0, 0, 0, Scale(8));
        mainLayout.Controls.Add(clipboardTextBox, 0, 0);

        TableLayoutPanel actions = CreateLayoutRow(3, [40, 30, 30]);
        ConfigureFillButton(copyClipboardButton);
        ConfigureFillButton(typeButton);
        ConfigureFillButton(stopButton);
        actions.Controls.Add(copyClipboardButton, 0, 0);
        actions.Controls.Add(typeButton, 1, 0);
        actions.Controls.Add(stopButton, 2, 0);
        mainLayout.Controls.Add(actions, 0, 1);

        TableLayoutPanel options = CreateLayoutRow(3, [30, 35, 35]);
        ConfigureRowControl(typeEnterCheckBox);
        ConfigureRowControl(alwaysOnTopCheckBox);
        ConfigureRowControl(_saveHistoryCheckBox);
        options.Controls.Add(typeEnterCheckBox, 0, 0);
        options.Controls.Add(alwaysOnTopCheckBox, 1, 0);
        options.Controls.Add(_saveHistoryCheckBox!, 2, 0);
        mainLayout.Controls.Add(options, 0, 2);

        TableLayoutPanel delays = CreateLayoutRow(4, [23, 25, 27, 25]);
        ConfigureRowControl(startDelayLabel);
        ConfigureRowControl(startDelayNumeric);
        ConfigureRowControl(interkeyDelayLabel);
        ConfigureRowControl(interkeyDelayNumeric);
        delays.Controls.Add(startDelayLabel, 0, 0);
        delays.Controls.Add(startDelayNumeric, 1, 0);
        delays.Controls.Add(interkeyDelayLabel, 2, 0);
        delays.Controls.Add(interkeyDelayNumeric, 3, 0);
        mainLayout.Controls.Add(delays, 0, 3);

        TableLayoutPanel emergency = CreateLayoutRow(3, [38, 24, 38]);
        ConfigureRowControl(hotKeyEnabledCheckBox);
        ConfigureRowControl(hotKeyLabel);
        ConfigureFillButton(emergencyShortcutButton);
        emergency.Controls.Add(hotKeyEnabledCheckBox, 0, 0);
        emergency.Controls.Add(hotKeyLabel, 1, 0);
        emergency.Controls.Add(emergencyShortcutButton, 2, 0);
        mainLayout.Controls.Add(emergency, 0, 4);

        TableLayoutPanel shortcuts = CreateLayoutRow(4, [20, 30, 20, 30]);
        ConfigureRowControl(typeShortcutLabel);
        ConfigureFillButton(typeShortcutButton);
        ConfigureRowControl(stopShortcutLabel);
        ConfigureFillButton(stopShortcutButton);
        shortcuts.Controls.Add(typeShortcutLabel, 0, 0);
        shortcuts.Controls.Add(typeShortcutButton, 1, 0);
        shortcuts.Controls.Add(stopShortcutLabel, 2, 0);
        shortcuts.Controls.Add(stopShortcutButton, 3, 0);
        mainLayout.Controls.Add(shortcuts, 0, 5);

        TableLayoutPanel historySettings = CreateLayoutRow(3, [30, 44, 26]);
        ConfigureFillButton(historyButton);
        ConfigureRowControl(maximumLabel);
        ConfigureRowControl(_maximumHistoryNumeric);
        historySettings.Controls.Add(historyButton, 0, 0);
        historySettings.Controls.Add(maximumLabel, 1, 0);
        historySettings.Controls.Add(_maximumHistoryNumeric!, 2, 0);
        mainLayout.Controls.Add(historySettings, 0, 6);

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Margin = Padding.Empty;
        mainLayout.Controls.Add(statusLabel, 0, 7);

        Controls.Add(mainLayout);
        _mainLayout = mainLayout;
        mainLayout.BringToFront();
        _historyPanel?.BringToFront();
    }

    private static TableLayoutPanel CreateLayoutRow(int columns, int[] percentages)
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = columns,
            RowCount = 1,
            Margin = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (int percentage in percentages)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, percentage));
        }

        return layout;
    }

    private static void ConfigureFillButton(Button button)
    {
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(3);
    }

    private static void ConfigureRowControl(Control? control)
    {
        if (control is null) return;
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3);
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            await _historyStore.LoadAsync();
            _historyStore.Trim(_settings.MaximumHistoryItems);
            RefreshHistory();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetIdleStatus($"Failed to load history: {ex.Message}");
        }
    }

    private async Task SetHistoryStatusAsync(TypeHistoryItem? item, TypeHistoryStatus status)
    {
        if (item is null) return;
        item.Status = status;
        await SaveHistoryAsync("History saved");
    }

    private async Task SaveHistoryAsync(string successStatus)
    {
        try
        {
            await _historyStore.SaveAsync();
            RefreshHistory();
            SetIdleStatus(successStatus);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetIdleStatus($"Failed to save history: {ex.Message}");
        }
    }

    private void RefreshHistory() => _historyPanel?.SetItems(_historyStore.Items);

    private void LoadHistoryItem(TypeHistoryItem item)
    {
        clipboardTextBox.Text = item.Content;
        clipboardTextBox.SelectionStart = clipboardTextBox.TextLength;
        clipboardTextBox.Focus();
        SetIdleStatus($"Loaded {item.Content.Length} characters");
    }

    private void CopyHistoryItem(TypeHistoryItem item)
    {
        try
        {
            Clipboard.SetText(item.Content);
            SetIdleStatus("Copied to clipboard");
        }
        catch (Exception ex) when (ex is ExternalException or ThreadStateException or InvalidOperationException)
        {
            SetIdleStatus($"Failed to copy history: {ex.Message}");
        }
    }

    private async void DeleteHistoryItem(TypeHistoryItem item)
    {
        if (MessageBox.Show("Delete this history item?", "Type History", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _historyStore.Items.Remove(item);
        await SaveHistoryAsync("History item deleted");
    }

    private async void ClearUnpinnedHistory()
    {
        if (MessageBox.Show("Clear all unpinned history items? Pinned items will remain.", "Clear Type History", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _historyStore.Items.RemoveAll(item => !item.IsPinned);
        await SaveHistoryAsync("History cleared");
    }

    private async void DeleteAllHistory()
    {
        if (MessageBox.Show("Permanently delete ALL Type History, including pinned items? This cannot be undone.", "Delete All Type History", MessageBoxButtons.YesNo, MessageBoxIcon.Stop) != DialogResult.Yes) return;
        _historyStore.Items.Clear();
        await SaveHistoryAsync("History cleared");
    }

    private void OpenHistory()
    {
        ToggleHistory(true);
        _historyPanel?.FocusHistory();
    }

    private void ToggleHistory(bool visible)
    {
        if (_historyPanel is null) return;
        if (_historyPanel.Visible == visible) return;

        if (visible)
        {
            ClientSize = new Size(_collapsedClientWidth + _historyPanel.Width, ClientSize.Height);
            if (_mainLayout is not null)
            {
                _mainLayout.Padding = new Padding(
                    _mainLayoutPadding,
                    _mainLayoutPadding,
                    _historyPanel.Width + _mainLayoutPadding,
                    _mainLayoutPadding);
            }
            _historyPanel.Visible = true;
        }
        else
        {
            _historyPanel.Visible = false;
            if (_mainLayout is not null)
            {
                _mainLayout.Padding = new Padding(_mainLayoutPadding);
            }
            ClientSize = new Size(_collapsedClientWidth, ClientSize.Height);
        }
    }

    private void BeginShortcutCapture(ShortcutAction action)
    {
        if (IsTyping)
        {
            return;
        }

        if (_shortcutCaptureTarget == action)
        {
            CancelShortcutCapture("Shortcut change cancelled");
            return;
        }

        _shortcutCaptureTarget = action;
        UnregisterShortcutAction(action);
        UpdateShortcutButtonText();
        UpdateShortcutButtonStates();
        GetShortcutButton(action).Focus();
        SetIdleStatus($"Press the new {GetShortcutActionName(action)} shortcut");
    }

    private void CaptureShortcut(Keys keyData)
    {
        Keys shortcut = NormalizeShortcut(keyData);
        if (!ValidateShortcut(shortcut, out string validationMessage))
        {
            SetIdleStatus(validationMessage);
            return;
        }

        ShortcutAction action = _shortcutCaptureTarget;
        if (IsShortcutUsedByAnotherAction(action, shortcut, out string existingAction))
        {
            SetIdleStatus($"{FormatShortcut(shortcut)} is already used by {existingAction}");
            return;
        }

        Keys previousShortcut = GetShortcut(action);
        bool previousEnabled = IsShortcutActionEnabled(action);
        SetShortcut(action, shortcut);
        EnableCapturedShortcut(action);
        string? registrationWarning = RegisterShortcutAction(action, probeDisabledAction: true);
        if (registrationWarning is not null)
        {
            SetShortcut(action, previousShortcut);
            RestoreShortcutEnabled(action, previousEnabled);
            UnregisterShortcutAction(action);
            SetShortcutWarning(action, null);
            SetIdleStatus($"{FormatShortcut(shortcut)} is unavailable; press another shortcut");
            return;
        }

        StoreShortcutsInSettings();
        _shortcutCaptureTarget = ShortcutAction.None;
        UpdateShortcutButtonText();
        UpdateShortcutButtonStates();
        if (!SaveSettings($"{GetShortcutActionName(action)} shortcut set to {FormatShortcut(shortcut)}"))
        {
            SetShortcut(action, previousShortcut);
            RestoreShortcutEnabled(action, previousEnabled);
            StoreShortcutsInSettings();
            UpdateShortcutButtonText();
            RegisterShortcutAction(action);
            string retainedShortcut = previousEnabled
                ? FormatShortcut(previousShortcut)
                : "Disabled";
            SetIdleStatus($"Shortcut save failed; kept {retainedShortcut}");
        }
    }

    private void CancelShortcutCapture(string status)
    {
        ShortcutAction action = _shortcutCaptureTarget;
        _shortcutCaptureTarget = ShortcutAction.None;
        UpdateShortcutButtonText();
        UpdateShortcutButtonStates();
        RegisterShortcutAction(action);
        SetIdleStatus(status);
    }

    private void UpdateShortcutButtonText()
    {
        emergencyShortcutButton.Text = _shortcutCaptureTarget == ShortcutAction.Emergency
            ? "Press shortcut..."
            : $"Change: {FormatShortcut(_emergencyShortcut)}";
        typeShortcutButton.Text = _shortcutCaptureTarget == ShortcutAction.Type
            ? "Press shortcut..."
            : _settings.TypeShortcutEnabled
                ? $"Change: {FormatShortcut(_typeShortcut)}"
                : "Change: Disabled";
        stopShortcutButton.Text = _shortcutCaptureTarget == ShortcutAction.Stop
            ? "Press shortcut..."
            : _settings.StopShortcutEnabled
                ? $"Change: {FormatShortcut(_stopShortcut)}"
                : "Change: Disabled";
    }

    private void UpdateShortcutButtonStates()
    {
        bool canChange = !IsTyping;
        hotKeyEnabledCheckBox.Enabled = canChange && _shortcutCaptureTarget == ShortcutAction.None;
        emergencyShortcutButton.Enabled = canChange &&
            _shortcutCaptureTarget is ShortcutAction.None or ShortcutAction.Emergency;
        typeShortcutButton.Enabled = canChange &&
            _shortcutCaptureTarget is ShortcutAction.None or ShortcutAction.Type;
        stopShortcutButton.Enabled = canChange &&
            _shortcutCaptureTarget is ShortcutAction.None or ShortcutAction.Stop;
    }

    private void RegisterConfiguredHotKeys()
    {
        RegisterShortcutAction(ShortcutAction.Emergency);
        RegisterShortcutAction(ShortcutAction.Type);
        RegisterShortcutAction(ShortcutAction.Stop);
    }

    private string? RegisterShortcutAction(ShortcutAction action, bool probeDisabledAction = false)
    {
        HotKeyManager? manager = GetShortcutManager(action);
        manager?.Unregister();

        bool actionEnabled = IsShortcutActionEnabled(action);
        if (!actionEnabled &&
            !probeDisabledAction)
        {
            SetShortcutWarning(action, null);
            return null;
        }

        string? warning = RegisterShortcutHotKey(
            manager,
            GetShortcut(action),
            GetShortcutActionName(action));
        SetShortcutWarning(action, warning);

        if (!actionEnabled &&
            probeDisabledAction)
        {
            manager?.Unregister();
        }

        return warning;
    }

    private void UnregisterShortcutAction(ShortcutAction action)
    {
        GetShortcutManager(action)?.Unregister();
    }

    private HotKeyManager? GetShortcutManager(ShortcutAction action) => action switch
    {
        ShortcutAction.Emergency => _emergencyHotKeyManager,
        ShortcutAction.Type => _typeShortcutHotKeyManager,
        ShortcutAction.Stop => _stopShortcutHotKeyManager,
        _ => null
    };

    private void SetShortcutWarning(ShortcutAction action, string? warning)
    {
        switch (action)
        {
            case ShortcutAction.Emergency:
                _hotKeyWarning = warning;
                break;
            case ShortcutAction.Type:
                _typeShortcutWarning = warning;
                break;
            case ShortcutAction.Stop:
                _stopShortcutWarning = warning;
                break;
        }
    }

    private bool IsShortcutActionEnabled(ShortcutAction action) => action switch
    {
        ShortcutAction.Emergency => hotKeyEnabledCheckBox.Checked,
        ShortcutAction.Type => _settings.TypeShortcutEnabled,
        ShortcutAction.Stop => _settings.StopShortcutEnabled,
        _ => false
    };

    private void EnableCapturedShortcut(ShortcutAction action)
    {
        if (action == ShortcutAction.Type)
        {
            _settings.TypeShortcutEnabled = true;
        }
        else if (action == ShortcutAction.Stop)
        {
            _settings.StopShortcutEnabled = true;
        }
    }

    private void RestoreShortcutEnabled(ShortcutAction action, bool enabled)
    {
        if (action == ShortcutAction.Type)
        {
            _settings.TypeShortcutEnabled = enabled;
        }
        else if (action == ShortcutAction.Stop)
        {
            _settings.StopShortcutEnabled = enabled;
        }
    }

    private static string? RegisterShortcutHotKey(HotKeyManager? manager, Keys shortcut, string actionName)
    {
        if (manager is null)
        {
            return null;
        }

        string displayName = FormatShortcut(shortcut);
        try
        {
            manager.Register(shortcut, displayName);
            return null;
        }
        catch (Win32Exception ex)
        {
            return $"{actionName} shortcut unavailable: {displayName} ({ex.Message})";
        }
    }

    private static Keys LoadShortcut(int persistedKeyData, Keys fallback)
    {
        Keys shortcut = NormalizeShortcut((Keys)persistedKeyData);
        return ValidateShortcut(shortcut, out _) ? shortcut : fallback;
    }

    private static Keys FindAvailableFunctionKey(Keys preferred, params Keys[] usedShortcuts)
    {
        if (!usedShortcuts.Contains(preferred))
        {
            return preferred;
        }

        for (Keys candidate = Keys.F1; candidate <= Keys.F24; candidate++)
        {
            if (!usedShortcuts.Contains(candidate))
            {
                return candidate;
            }
        }

        return preferred;
    }

    private static Keys NormalizeShortcut(Keys keyData)
    {
        return (keyData & Keys.Modifiers) | (keyData & Keys.KeyCode);
    }

    private static Keys ShortcutFromHotKeyMessage(IntPtr messageData)
    {
        long packedValue = messageData.ToInt64();
        HotKeyModifiers modifiers = (HotKeyModifiers)(packedValue & 0xFFFF);
        Keys shortcut = (Keys)((packedValue >> 16) & 0xFFFF);
        if (modifiers.HasFlag(HotKeyModifiers.Control)) shortcut |= Keys.Control;
        if (modifiers.HasFlag(HotKeyModifiers.Alt)) shortcut |= Keys.Alt;
        if (modifiers.HasFlag(HotKeyModifiers.Shift)) shortcut |= Keys.Shift;
        return NormalizeShortcut(shortcut);
    }

    private static bool ValidateShortcut(Keys shortcut, out string message)
    {
        Keys keyCode = shortcut & Keys.KeyCode;
        Keys modifiers = shortcut & Keys.Modifiers;
        if (keyCode is Keys.None or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
            Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or Keys.Menu or Keys.LMenu or Keys.RMenu or
            Keys.LWin or Keys.RWin)
        {
            message = "Press a complete shortcut, such as F9 or Ctrl+Shift+T";
            return false;
        }

        if (shortcut == (Keys.Control | Keys.H))
        {
            message = "Ctrl+H is reserved for Type History";
            return false;
        }

        bool supportsSingleKey = keyCode is >= Keys.F1 and <= Keys.F24 || keyCode == Keys.Pause;
        bool hasControlOrAlt = modifiers.HasFlag(Keys.Control) || modifiers.HasFlag(Keys.Alt);
        if (!supportsSingleKey && !hasControlOrAlt)
        {
            message = "Use Ctrl or Alt with this key";
            return false;
        }

        if (shortcut is (Keys.Alt | Keys.F4) or
            (Keys.Alt | Keys.Tab) or
            (Keys.Alt | Keys.Escape) or
            (Keys.Control | Keys.Escape))
        {
            message = "This shortcut is reserved by Windows";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool IsShortcutUsedByAnotherAction(ShortcutAction action, Keys shortcut, out string actionName)
    {
        foreach (ShortcutAction candidate in new[] { ShortcutAction.Emergency, ShortcutAction.Type, ShortcutAction.Stop })
        {
            if (candidate != action &&
                IsShortcutActionEnabled(candidate) &&
                GetShortcut(candidate) == shortcut)
            {
                actionName = GetShortcutActionName(candidate);
                return true;
            }
        }

        actionName = string.Empty;
        return false;
    }

    private Keys GetShortcut(ShortcutAction action) => action switch
    {
        ShortcutAction.Emergency => _emergencyShortcut,
        ShortcutAction.Type => _typeShortcut,
        ShortcutAction.Stop => _stopShortcut,
        _ => Keys.None
    };

    private void SetShortcut(ShortcutAction action, Keys shortcut)
    {
        switch (action)
        {
            case ShortcutAction.Emergency:
                _emergencyShortcut = shortcut;
                break;
            case ShortcutAction.Type:
                _typeShortcut = shortcut;
                break;
            case ShortcutAction.Stop:
                _stopShortcut = shortcut;
                break;
        }
    }

    private void StoreShortcutsInSettings()
    {
        _settings.EmergencyShortcutKeyData = (int)_emergencyShortcut;
        _settings.TypeShortcutKeyData = (int)_typeShortcut;
        _settings.StopShortcutKeyData = (int)_stopShortcut;
    }

    private Button GetShortcutButton(ShortcutAction action) => action switch
    {
        ShortcutAction.Emergency => emergencyShortcutButton,
        ShortcutAction.Type => typeShortcutButton,
        ShortcutAction.Stop => stopShortcutButton,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static string GetShortcutActionName(ShortcutAction action) => action switch
    {
        ShortcutAction.Emergency => "Emergency",
        ShortcutAction.Type => "Type",
        ShortcutAction.Stop => "Stop",
        _ => "Shortcut"
    };

    private static string FormatShortcut(Keys shortcut)
    {
        List<string> parts = [];
        if (shortcut.HasFlag(Keys.Control)) parts.Add("Ctrl");
        if (shortcut.HasFlag(Keys.Alt)) parts.Add("Alt");
        if (shortcut.HasFlag(Keys.Shift)) parts.Add("Shift");

        Keys keyCode = shortcut & Keys.KeyCode;
        parts.Add(keyCode switch
        {
            Keys.Escape => "Esc",
            Keys.Return => "Enter",
            Keys.Space => "Space",
            Keys.Prior => "PageUp",
            Keys.Next => "PageDown",
            Keys.Pause => "Pause/Break",
            _ => keyCode.ToString()
        });
        return string.Join("+", parts);
    }

    private static async Task DelayAndCheckCancellation(int millisecondsDelay, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (millisecondsDelay > 0)
        {
            await Task.Delay(millisecondsDelay, token);
        }
        else
        {
            await Task.Yield();
        }

        token.ThrowIfCancellationRequested();
    }

    private IntPtr ResolveTypingTarget(IntPtr preferredTarget)
    {
        IntPtr targetWindow = GetExternalRootWindow(preferredTarget);
        if (targetWindow == IntPtr.Zero)
        {
            targetWindow = GetExternalRootWindow(GetForegroundWindow());
        }

        if (targetWindow == IntPtr.Zero)
        {
            targetWindow = GetExternalRootWindow(_lastExternalWindow);
        }

        if (targetWindow == IntPtr.Zero)
        {
            _lastExternalWindow = IntPtr.Zero;
            throw new TargetWindowChangedException("Stopped: select a target field before starting Type");
        }

        if (_lastExternalWindow != targetWindow)
        {
            _lastExternalFocusWindow = GetForegroundWindow() == targetWindow
                ? GetFocusedWindow(targetWindow)
                : IntPtr.Zero;
        }

        _lastExternalWindow = targetWindow;
        return targetWindow;
    }

    private IntPtr ResolveTypingFocus(IntPtr targetWindow, IntPtr preferredFocusWindow)
    {
        if (preferredFocusWindow != IntPtr.Zero)
        {
            return preferredFocusWindow;
        }

        if (targetWindow == _lastExternalWindow && _lastExternalFocusWindow != IntPtr.Zero)
        {
            return _lastExternalFocusWindow;
        }

        return GetForegroundWindow() == targetWindow
            ? GetFocusedWindow(targetWindow)
            : IntPtr.Zero;
    }

    private async Task ActivateTargetWindowAsync(IntPtr targetWindow, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsWindow(targetWindow))
        {
            throw new TargetWindowChangedException("Stopped: target window closed");
        }

        bool requiresActivation = GetForegroundWindow() != targetWindow;
        if (!requiresActivation)
        {
            return;
        }

        if (IsIconic(targetWindow))
        {
            token.ThrowIfCancellationRequested();
            ShowWindowAsync(targetWindow, SwRestore);
        }

        token.ThrowIfCancellationRequested();
        SetForegroundWindow(targetWindow);
        int consecutiveForegroundChecks = 0;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfAnotherExternalWindowSelected(targetWindow);
            if (GetForegroundWindow() == targetWindow)
            {
                consecutiveForegroundChecks++;
                if (consecutiveForegroundChecks >= 3)
                {
                    return;
                }
            }
            else
            {
                consecutiveForegroundChecks = 0;
            }

            if (attempt == 9)
            {
                SetForegroundWindow(targetWindow);
            }

            await Task.Delay(25, token);
        }

        throw new TargetWindowChangedException("Stopped: target window could not receive focus");
    }

    private static async Task WaitForShortcutReleaseAsync(Keys shortcut, CancellationToken token)
    {
        if (shortcut == Keys.None)
        {
            return;
        }

        Keys keyCode = shortcut & Keys.KeyCode;
        while (IsVirtualKeyPressed(keyCode) ||
               (shortcut.HasFlag(Keys.Control) && IsVirtualKeyPressed(Keys.ControlKey)) ||
               (shortcut.HasFlag(Keys.Alt) && IsVirtualKeyPressed(Keys.Menu)) ||
               (shortcut.HasFlag(Keys.Shift) && IsVirtualKeyPressed(Keys.ShiftKey)))
        {
            await Task.Delay(10, token);
        }
    }

    private static bool IsVirtualKeyPressed(Keys key)
    {
        return (GetAsyncKeyState((int)key) & 0x8000) != 0;
    }

    private static void ThrowIfAnotherExternalWindowSelected(IntPtr targetWindow)
    {
        IntPtr foregroundWindow = GetExternalRootWindow(GetForegroundWindow());
        if (foregroundWindow != IntPtr.Zero && foregroundWindow != targetWindow)
        {
            throw new TargetWindowChangedException("Stopped: target window changed");
        }
    }

    private void RememberExternalWindow(IntPtr window)
    {
        IntPtr externalWindow = GetExternalRootWindow(window);
        if (externalWindow != IntPtr.Zero)
        {
            bool sameWindow = externalWindow == _lastExternalWindow;
            IntPtr focusWindow = GetFocusedWindow(externalWindow);
            _lastExternalWindow = externalWindow;
            if (focusWindow != IntPtr.Zero || !sameWindow)
            {
                _lastExternalFocusWindow = focusWindow;
            }
        }
    }

    private static IntPtr GetExternalRootWindow(IntPtr window)
    {
        if (window == IntPtr.Zero || !IsWindow(window))
        {
            return IntPtr.Zero;
        }

        IntPtr rootWindow = GetAncestor(window, GaRoot);
        if (rootWindow == IntPtr.Zero)
        {
            rootWindow = window;
        }

        GetWindowThreadProcessId(rootWindow, out uint processId);
        return processId == (uint)Environment.ProcessId ? IntPtr.Zero : rootWindow;
    }

    private static IntPtr GetFocusedWindow(IntPtr targetWindow)
    {
        uint threadId = GetWindowThreadProcessId(targetWindow, out _);
        GuiThreadInfo threadInfo = new()
        {
            cbSize = (uint)Marshal.SizeOf<GuiThreadInfo>()
        };
        return GetGUIThreadInfo(threadId, ref threadInfo) ? threadInfo.hwndFocus : IntPtr.Zero;
    }

    private static void ThrowIfTargetWindowChanged(IntPtr targetWindow, IntPtr targetFocusWindow)
    {
        if (GetForegroundWindow() != targetWindow)
        {
            throw new TargetWindowChangedException("Stopped: target window changed");
        }

        if (targetFocusWindow != IntPtr.Zero)
        {
            IntPtr currentFocusWindow = GetFocusedWindow(targetWindow);
            if (currentFocusWindow != targetFocusWindow)
            {
                throw new TargetWindowChangedException("Stopped: target field changed");
            }
        }
    }

    private void RegisterClipboardListener()
    {
        if (!AddClipboardFormatListener(Handle))
        {
            SetIdleStatus("Clipboard auto-load unavailable");
        }
    }

    private void StartClipboardPolling()
    {
        _clipboardPollTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };
        _clipboardPollTimer.Tick += (_, _) => LoadClipboardText("Auto loaded", forceReload: false, showErrors: false);
        _clipboardPollTimer.Start();
    }

    private void RequestStop(string status)
    {
        if (!IsTyping)
        {
            return;
        }

        SetIdleStatus(status);
        _typingCancellation?.Cancel();
    }

    private void UpdateCharacterCountStatus()
    {
        SetIdleStatus($"{clipboardTextBox.TextLength} characters");
    }

    private void SetIdleStatus(string status)
    {
        string[] warnings = new string?[] { _hotKeyWarning, _typeShortcutWarning, _stopShortcutWarning }
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => warning!)
            .ToArray();
        statusLabel.Text = warnings.Length == 0
            ? status
            : $"{string.Join(" | ", warnings)} | {status}";
    }

    private enum ShortcutAction
    {
        None,
        Emergency,
        Type,
        Stop
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public NativeRect rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private sealed class TargetWindowChangedException(string message) : Exception(message);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo threadInfo);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
