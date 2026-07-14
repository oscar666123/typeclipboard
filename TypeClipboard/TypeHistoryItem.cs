namespace TypeClipboard;

internal enum TypeHistoryStatus
{
    Started,
    Completed,
    Stopped,
    Failed
}

internal sealed class TypeHistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public int UseCount { get; set; } = 1;
    public TypeHistoryStatus Status { get; set; }
    public bool IsPinned { get; set; }
}
