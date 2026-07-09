using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TypeClipboard;

internal static class InputSimulator
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private const ushort VkReturn = 0x0D;

    public static void SendCharacter(char character)
    {
        SendInputs(
            CreateUnicodeInput(character, keyUp: false),
            CreateUnicodeInput(character, keyUp: true));
    }

    public static void SendEnter()
    {
        SendInputs(
            CreateVirtualKeyInput(VkReturn, keyUp: false),
            CreateVirtualKeyInput(VkReturn, keyUp: true));
    }

    private static INPUT CreateUnicodeInput(char character, bool keyUp)
    {
        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = character,
                    dwFlags = KeyEventFUnicode | (keyUp ? KeyEventFKeyUp : 0)
                }
            }
        };
    }

    private static INPUT CreateVirtualKeyInput(ushort virtualKey, bool keyUp)
    {
        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = keyUp ? KeyEventFKeyUp : 0
                }
            }
        };
    }

    private static void SendInputs(params INPUT[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput failed");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}
