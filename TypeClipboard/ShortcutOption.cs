namespace TypeClipboard;

internal sealed record ShortcutOption(string Id, string DisplayName, Keys KeyData, bool IsGlobal)
{
    public bool IsEnabled => KeyData != Keys.None;

    public override string ToString() => DisplayName;
}
