using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TypeClipboard;

public partial class MainForm : Form
{
    private const int WmHotKey = 0x0312;
    private const int WmClipboardUpdate = 0x031D;
    private const int EmergencyHotKeyId = 0x5401;
    private const int TypeShortcutHotKeyId = 0x5402;
    private const int StopShortcutHotKeyId = 0x5403;

    private readonly HotKeyOption[] _hotKeyOptions =
    [
        new("F8", HotKeyModifiers.None, Keys.F8),
        new("Ctrl+Alt+F8", HotKeyModifiers.Control | HotKeyModifiers.Alt, Keys.F8),
        new("Pause/Break", HotKeyModifiers.None, Keys.Pause)
    ];

    private readonly ShortcutOption[] _typeShortcutOptions =
    [
        new("ctrl-t", "Ctrl+T local", Keys.Control | Keys.T, IsGlobal: false),
        new("ctrl-shift-t", "Ctrl+Shift+T", Keys.Control | Keys.Shift | Keys.T, IsGlobal: true),
        new("ctrl-alt-t", "Ctrl+Alt+T", Keys.Control | Keys.Alt | Keys.T, IsGlobal: true),
        new("f9", "F9", Keys.F9, IsGlobal: true),
        new("disabled", "Disabled", Keys.None, IsGlobal: false)
    ];

    private readonly ShortcutOption[] _stopShortcutOptions =
    [
        new("escape", "Esc local", Keys.Escape, IsGlobal: false),
        new("ctrl-shift-s", "Ctrl+Shift+S", Keys.Control | Keys.Shift | Keys.S, IsGlobal: true),
        new("ctrl-alt-s", "Ctrl+Alt+S", Keys.Control | Keys.Alt | Keys.S, IsGlobal: true),
        new("f10", "F10", Keys.F10, IsGlobal: true),
        new("disabled", "Disabled", Keys.None, IsGlobal: false)
    ];

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
            hotKeyComboBox.Enabled = !value;
            hotKeyEnabledCheckBox.Enabled = !value;
            typeShortcutComboBox.Enabled = !value;
            stopShortcutComboBox.Enabled = !value;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey)
        {
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

        if (!IsTyping && MatchesShortcut(typeShortcutComboBox, keyData))
        {
            StartTypingFromShortcut();
            return true;
        }

        if (IsTyping && MatchesShortcut(stopShortcutComboBox, keyData))
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

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        _emergencyHotKeyManager = new HotKeyManager(Handle, EmergencyHotKeyId);
        _typeShortcutHotKeyManager = new HotKeyManager(Handle, TypeShortcutHotKeyId);
        _stopShortcutHotKeyManager = new HotKeyManager(Handle, StopShortcutHotKeyId);
        InitializeShortcutSelectors();
        InitializeHistoryUi();
        await LoadHistoryAsync();
        RegisterSelectedShortcutHotKeys();
        hotKeyComboBox.Items.AddRange(_hotKeyOptions);
        hotKeyComboBox.SelectedIndex = 0;
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
        await StartTypingAsync(clipboardTextBox.Text);
    }

    private void StartTypingFromShortcut()
    {
        _ = StartTypingAsync(clipboardTextBox.Text);
    }

    private async Task StartTypingAsync(string text)
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

        clipboardTextBox.Text = text;
        TypeHistoryItem? historyItem = null;
        if (_settings.SaveTypeHistory && _historyPanel?.IsPaused != true)
        {
            historyItem = _historyStore.AddOrReuse(text, _settings.MaximumHistoryItems);
            await SaveHistoryAsync("History saved");
        }

        _typingCancellation?.Dispose();
        _typingCancellation = new CancellationTokenSource();
        CancellationToken token = _typingCancellation.Token;

        IsTyping = true;
        statusLabel.Text = "Typing...";

        try
        {
            int startDelay = (int)startDelayNumeric.Value;
            int interkeyDelay = (int)interkeyDelayNumeric.Value;

            await DelayAndCheckCancellation(startDelay, token);
            IntPtr targetWindow = CaptureTargetWindow();

            for (int index = 0; index < text.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                ThrowIfTargetWindowChanged(targetWindow);

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
            ThrowIfTargetWindowChanged(targetWindow);
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
        hotKeyComboBox.Enabled = !IsTyping;

        if (hotKeyEnabledCheckBox.Checked)
        {
            RegisterSelectedHotKey();
        }
        else
        {
            _emergencyHotKeyManager?.Unregister();
            _hotKeyWarning = null;
            SetIdleStatus("Hotkey disabled");
        }
    }

    private void hotKeyComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (hotKeyEnabledCheckBox.Checked)
        {
            RegisterSelectedHotKey();
        }
    }

    private void shortcutComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isInitializingShortcuts ||
            typeShortcutComboBox.SelectedItem is not ShortcutOption typeShortcut ||
            stopShortcutComboBox.SelectedItem is not ShortcutOption stopShortcut)
        {
            return;
        }

        _settings.TypeShortcutId = typeShortcut.Id;
        _settings.StopShortcutId = stopShortcut.Id;
        RegisterSelectedShortcutHotKeys();
        SaveSettings("Shortcuts updated");
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

    private void SaveSettings(string successStatus)
    {
        try
        {
            _settings.Save();
            SetIdleStatus(successStatus);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetIdleStatus($"Error saving shortcuts: {ex.Message}");
        }
    }

    private void clipboardTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!IsTyping)
        {
            UpdateCharacterCountStatus();
        }
    }

    private void InitializeShortcutSelectors()
    {
        _settings = AppSettings.Load();
        _isInitializingShortcuts = true;

        alwaysOnTopCheckBox.Checked = _settings.AlwaysOnTop;
        TopMost = _settings.AlwaysOnTop;
        typeShortcutComboBox.Items.AddRange(_typeShortcutOptions);
        stopShortcutComboBox.Items.AddRange(_stopShortcutOptions);
        typeShortcutComboBox.SelectedItem = FindShortcut(_typeShortcutOptions, _settings.TypeShortcutId);
        stopShortcutComboBox.SelectedItem = FindShortcut(_stopShortcutOptions, _settings.StopShortcutId);

        _isInitializingShortcuts = false;
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

        TableLayoutPanel delays = CreateLayoutRow(4, [24, 26, 24, 26]);
        ConfigureRowControl(startDelayLabel);
        ConfigureRowControl(startDelayNumeric);
        ConfigureRowControl(interkeyDelayLabel);
        ConfigureRowControl(interkeyDelayNumeric);
        delays.Controls.Add(startDelayLabel, 0, 0);
        delays.Controls.Add(startDelayNumeric, 1, 0);
        delays.Controls.Add(interkeyDelayLabel, 2, 0);
        delays.Controls.Add(interkeyDelayNumeric, 3, 0);
        mainLayout.Controls.Add(delays, 0, 3);

        TableLayoutPanel emergency = CreateLayoutRow(3, [30, 35, 35]);
        ConfigureRowControl(hotKeyEnabledCheckBox);
        ConfigureRowControl(hotKeyLabel);
        ConfigureRowControl(hotKeyComboBox);
        emergency.Controls.Add(hotKeyEnabledCheckBox, 0, 0);
        emergency.Controls.Add(hotKeyLabel, 1, 0);
        emergency.Controls.Add(hotKeyComboBox, 2, 0);
        mainLayout.Controls.Add(emergency, 0, 4);

        TableLayoutPanel shortcuts = CreateLayoutRow(4, [24, 26, 24, 26]);
        ConfigureRowControl(typeShortcutLabel);
        ConfigureRowControl(typeShortcutComboBox);
        ConfigureRowControl(stopShortcutLabel);
        ConfigureRowControl(stopShortcutComboBox);
        shortcuts.Controls.Add(typeShortcutLabel, 0, 0);
        shortcuts.Controls.Add(typeShortcutComboBox, 1, 0);
        shortcuts.Controls.Add(stopShortcutLabel, 2, 0);
        shortcuts.Controls.Add(stopShortcutComboBox, 3, 0);
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

    private static ShortcutOption FindShortcut(ShortcutOption[] options, string selectedId)
    {
        return options.FirstOrDefault(option => option.Id == selectedId) ?? options[0];
    }

    private static bool MatchesShortcut(ComboBox comboBox, Keys keyData)
    {
        return comboBox.SelectedItem is ShortcutOption option &&
               option.IsEnabled &&
               keyData == option.KeyData;
    }

    private void RegisterSelectedShortcutHotKeys()
    {
        _typeShortcutWarning = RegisterShortcutHotKey(
            typeShortcutComboBox,
            _typeShortcutHotKeyManager,
            "Type");
        _stopShortcutWarning = RegisterShortcutHotKey(
            stopShortcutComboBox,
            _stopShortcutHotKeyManager,
            "Stop");
    }

    private static string? RegisterShortcutHotKey(
        ComboBox comboBox,
        HotKeyManager? manager,
        string actionName)
    {
        manager?.Unregister();

        if (manager is null ||
            comboBox.SelectedItem is not ShortcutOption option ||
            !option.IsEnabled ||
            !option.IsGlobal)
        {
            return null;
        }

        try
        {
            manager.Register(option);
            return null;
        }
        catch (Win32Exception ex)
        {
            return $"{actionName} global shortcut unavailable: {option.DisplayName} ({ex.Message})";
        }
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

    private IntPtr CaptureTargetWindow()
    {
        IntPtr targetWindow = GetForegroundWindow();
        if (targetWindow == IntPtr.Zero || targetWindow == Handle)
        {
            throw new TargetWindowChangedException("Stopped: target window unavailable");
        }

        return targetWindow;
    }

    private static void ThrowIfTargetWindowChanged(IntPtr targetWindow)
    {
        if (GetForegroundWindow() != targetWindow)
        {
            throw new TargetWindowChangedException("Stopped: target window changed");
        }
    }

    private void RegisterSelectedHotKey()
    {
        if (!hotKeyEnabledCheckBox.Checked ||
            _emergencyHotKeyManager is null ||
            hotKeyComboBox.SelectedItem is not HotKeyOption option)
        {
            return;
        }

        try
        {
            _emergencyHotKeyManager.Register(option);
            _hotKeyWarning = null;
            SetIdleStatus($"{option.DisplayName} hotkey registered");
        }
        catch (Win32Exception ex)
        {
            hotKeyEnabledCheckBox.CheckedChanged -= hotKeyEnabledCheckBox_CheckedChanged;
            hotKeyEnabledCheckBox.Checked = false;
            hotKeyEnabledCheckBox.CheckedChanged += hotKeyEnabledCheckBox_CheckedChanged;
            hotKeyComboBox.Enabled = !IsTyping;
            _hotKeyWarning = $"Hotkey unavailable: {option.DisplayName} ({ex.Message})";
            SetIdleStatus("Select another emergency hotkey");
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

    private sealed class TargetWindowChangedException(string message) : Exception(message);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
