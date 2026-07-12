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

    private void MainForm_Load(object? sender, EventArgs e)
    {
        _emergencyHotKeyManager = new HotKeyManager(Handle, EmergencyHotKeyId);
        _typeShortcutHotKeyManager = new HotKeyManager(Handle, TypeShortcutHotKeyId);
        _stopShortcutHotKeyManager = new HotKeyManager(Handle, StopShortcutHotKeyId);
        InitializeShortcutSelectors();
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
        await StartTypingAsync();
    }

    private void StartTypingFromShortcut()
    {
        _ = StartTypingAsync();
    }

    private async Task StartTypingAsync()
    {
        if (IsTyping)
        {
            return;
        }

        LoadClipboardText("Auto loaded", forceReload: false, showErrors: false);

        string text = clipboardTextBox.Text;
        if (text.Length == 0)
        {
            SetIdleStatus("Textbox is empty");
            return;
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
        }
        catch (OperationCanceledException)
        {
            if (!_isClosing)
            {
                SetIdleStatus("Stopped");
            }
        }
        catch (TargetWindowChangedException ex)
        {
            if (!_isClosing)
            {
                SetIdleStatus(ex.Message);
            }
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                SetIdleStatus($"Error: {ex.Message}");
            }
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
