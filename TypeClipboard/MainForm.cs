using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TypeClipboard;

public partial class MainForm : Form
{
    private const int WmHotKey = 0x0312;
    private const int WmClipboardUpdate = 0x031D;

    private readonly HotKeyOption[] _hotKeyOptions =
    [
        new("F8", HotKeyModifiers.None, Keys.F8),
        new("Ctrl+Alt+F8", HotKeyModifiers.Control | HotKeyModifiers.Alt, Keys.F8),
        new("Pause/Break", HotKeyModifiers.None, Keys.Pause)
    ];

    private HotKeyManager? _hotKeyManager;
    private System.Windows.Forms.Timer? _clipboardPollTimer;
    private CancellationTokenSource? _typingCancellation;
    private string? _lastClipboardText;
    private string? _hotKeyWarning;
    private bool? _lastClipboardContainedText;
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
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey)
        {
            RequestStop("Stopped");
            return;
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
        if (keyData == (Keys.Control | Keys.T))
        {
            StartTypingFromShortcut();
            return true;
        }

        if (keyData == Keys.Escape)
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
        _hotKeyManager = new HotKeyManager(Handle);
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
        _hotKeyManager?.Dispose();
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
            _hotKeyManager?.Unregister();
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

    private void clipboardTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!IsTyping)
        {
            UpdateCharacterCountStatus();
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
            _hotKeyManager is null ||
            hotKeyComboBox.SelectedItem is not HotKeyOption option)
        {
            return;
        }

        try
        {
            _hotKeyManager.Register(option);
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
        statusLabel.Text = _hotKeyWarning is null
            ? status
            : $"{_hotKeyWarning} | {status}";
    }

    private sealed class TargetWindowChangedException(string message) : Exception(message);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
