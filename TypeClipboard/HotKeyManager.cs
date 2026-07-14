using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TypeClipboard;

internal sealed class HotKeyManager : IDisposable
{
    private readonly IntPtr _windowHandle;
    private readonly int _hotKeyId;
    private bool _registered;

    public HotKeyManager(IntPtr windowHandle, int hotKeyId)
    {
        _windowHandle = windowHandle;
        _hotKeyId = hotKeyId;
    }

    public bool IsRegistered => _registered;

    public void Register(Keys keyData, string displayName)
    {
        HotKeyModifiers modifiers = HotKeyModifiers.None;
        if (keyData.HasFlag(Keys.Control))
        {
            modifiers |= HotKeyModifiers.Control;
        }

        if (keyData.HasFlag(Keys.Alt))
        {
            modifiers |= HotKeyModifiers.Alt;
        }

        if (keyData.HasFlag(Keys.Shift))
        {
            modifiers |= HotKeyModifiers.Shift;
        }

        Keys key = keyData & Keys.KeyCode;
        Register(modifiers, key, displayName);
    }

    private void Register(HotKeyModifiers modifiers, Keys key, string displayName)
    {
        Unregister();
        modifiers |= HotKeyModifiers.NoRepeat;

        if (!RegisterHotKey(_windowHandle, _hotKeyId, modifiers, (uint)key))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to register {displayName}");
        }

        _registered = true;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        UnregisterHotKey(_windowHandle, _hotKeyId);
        _registered = false;
    }

    public void Dispose()
    {
        Unregister();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, HotKeyModifiers fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
