namespace TypeClipboard;

internal sealed record HotKeyOption(string DisplayName, HotKeyModifiers Modifiers, Keys Key)
{
    public override string ToString() => DisplayName;
}
