using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TypeClipboard;

public partial class MainForm : Form
{
    private const int WmHotKey = 0x0312;

    private readonly HotKeyOption[] _hotKeyOptions =
    [
        new("F8", HotKeyModifiers.None, Keys.F8),
        new("Ctrl+Alt+F8", HotKeyModifiers.Control | HotKeyModifiers.Alt, Keys.F8),
        new("Pause/Break", HotKeyModifiers.None, Keys.Pause)
    ];

    private HotKeyManager? _hotKeyManager;
    private CancellationTokenSource? _typingCancellation;
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
            hotKeyComboBox.Enabled = !value && hotKeyEnabledCheckBox.Checked;
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

        base.WndProc(ref m);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (IsTyping)
        {
            RequestStop("Stopped");
        }
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        _hotKeyManager = new HotKeyManager(Handle);
        hotKeyComboBox.Items.AddRange(_hotKeyOptions);
        hotKeyComboBox.SelectedIndex = 0;
        UpdateCharacterCountStatus();
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _isClosing = true;
        RequestStop("Stopped");
        _hotKeyManager?.Dispose();
    }

    private void copyClipboardButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                clipboardTextBox.Clear();
                SetIdleStatus("Clipboard is empty");
                return;
            }

            clipboardTextBox.Text = Clipboard.GetText();
            SetIdleStatus($"Loaded {clipboardTextBox.TextLength} characters");
        }
        catch (Exception ex) when (ex is ExternalException or ThreadStateException or InvalidOperationException)
        {
            SetIdleStatus($"Error: {ex.Message}");
        }
    }

    private async void typeButton_Click(object? sender, EventArgs e)
    {
        if (IsTyping)
        {
            return;
        }

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

            for (int index = 0; index < text.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                ThrowIfThisAppIsForeground(token);

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
            ThrowIfThisAppIsForeground(token);
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
        hotKeyComboBox.Enabled = hotKeyEnabledCheckBox.Checked && !IsTyping;

        if (hotKeyEnabledCheckBox.Checked)
        {
            RegisterSelectedHotKey();
        }
        else
        {
            _hotKeyManager?.Unregister();
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

        token.ThrowIfCancellationRequested();
    }

    private void ThrowIfThisAppIsForeground(CancellationToken token)
    {
        if (GetForegroundWindow() == Handle)
        {
            throw new OperationCanceledException(token);
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
            SetIdleStatus($"{option.DisplayName} hotkey registered");
        }
        catch (Win32Exception ex)
        {
            hotKeyEnabledCheckBox.CheckedChanged -= hotKeyEnabledCheckBox_CheckedChanged;
            hotKeyEnabledCheckBox.Checked = false;
            hotKeyEnabledCheckBox.CheckedChanged += hotKeyEnabledCheckBox_CheckedChanged;
            hotKeyComboBox.Enabled = false;
            SetIdleStatus($"Hotkey unavailable: {option.DisplayName} ({ex.Message})");
        }
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
        statusLabel.Text = status;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
