using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TypeClipboard;

internal sealed class HotKeyManager : IDisposable
{
    private const int HotKeyId = 0x5401;
    private readonly IntPtr _windowHandle;
    private bool _registered;

    public HotKeyManager(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public bool IsRegistered => _registered;

    public void Register(HotKeyOption option)
    {
        Unregister();

        if (!RegisterHotKey(_windowHandle, HotKeyId, option.Modifiers, (uint)option.Key))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to register {option.DisplayName}");
        }

        _registered = true;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        UnregisterHotKey(_windowHandle, HotKeyId);
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
