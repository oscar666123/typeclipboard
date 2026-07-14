namespace TypeClipboard;

internal sealed class TypeHistoryPanel : UserControl
{
    private readonly TextBox _searchBox = new() { PlaceholderText = "Search history...", Dock = DockStyle.Top };
    private readonly Label _stateLabel = new() { Text = "Type History", AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly Button _pauseButton = new() { Text = "Pause History", AutoSize = true };
    private readonly Button _clearButton = new() { Text = "Clear all", AutoSize = true };
    private readonly Button _menuButton = new() { Text = "⋯", Width = 34 };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = false };
    private readonly ContextMenuStrip _itemMenu = new();
    private readonly ContextMenuStrip _panelMenu = new();
    private IReadOnlyList<TypeHistoryItem> _source = [];
    private bool _suppressLoadClick;

    public event Action<TypeHistoryItem>? LoadRequested;
    public event Action<TypeHistoryItem>? TypeRequested;
    public event Action<TypeHistoryItem>? CopyRequested;
    public event Action<TypeHistoryItem>? PinRequested;
    public event Action<TypeHistoryItem>? DeleteRequested;
    public event Action? ClearRequested;
    public event Action? DeleteAllRequested;
    public event Action<bool>? PauseChanged;
    public bool IsPaused { get; private set; }

    public TypeHistoryPanel()
    {
        Width = 330;
        BorderStyle = BorderStyle.FixedSingle;
        _list.DisplayMember = nameof(TypeHistoryItem.Content);
        _list.ItemHeight = 86;
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.DrawItem += DrawItem;
        _list.Click += (_, _) =>
        {
            if (_suppressLoadClick)
            {
                _suppressLoadClick = false;
                return;
            }

            WithSelected(item => LoadRequested?.Invoke(item));
        };
        _list.DoubleClick += (_, _) => WithSelected(item => TypeRequested?.Invoke(item));
        _list.MouseDown += ListMouseDown;
        _list.KeyDown += ListKeyDown;
        _searchBox.TextChanged += (_, _) => RefreshItems();
        _pauseButton.Click += (_, _) => SetPaused(!IsPaused);
        _clearButton.Click += (_, _) => ClearRequested?.Invoke();
        _menuButton.Click += (_, _) => _panelMenu.Show(_menuButton, new Point(0, _menuButton.Height));

        AddMenuItem(_itemMenu, "Load to textbox", item => LoadRequested?.Invoke(item));
        AddMenuItem(_itemMenu, "Type again", item => TypeRequested?.Invoke(item));
        AddMenuItem(_itemMenu, "Copy to clipboard", item => CopyRequested?.Invoke(item));
        AddMenuItem(_itemMenu, "Pin / Unpin", item => PinRequested?.Invoke(item));
        AddMenuItem(_itemMenu, "Delete", item => DeleteRequested?.Invoke(item));
        _list.ContextMenuStrip = _itemMenu;
        _panelMenu.Items.Add("Delete all history including pinned items", null, (_, _) => DeleteAllRequested?.Invoke());

        TableLayoutPanel header = new()
        {
            Dock = DockStyle.Top,
            Height = 67,
            Padding = new Padding(5),
            ColumnCount = 2,
            RowCount = 2
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _stateLabel.Anchor = AnchorStyles.Left;
        _menuButton.Anchor = AnchorStyles.Right;
        FlowLayoutPanel actions = new() { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        actions.Controls.AddRange([_pauseButton, _clearButton]);
        header.Controls.Add(_stateLabel, 0, 0);
        header.Controls.Add(_menuButton, 1, 0);
        header.Controls.Add(actions, 0, 1);
        header.SetColumnSpan(actions, 2);
        Controls.Add(_list);
        Controls.Add(_searchBox);
        Controls.Add(header);
    }

    public void SetItems(IReadOnlyList<TypeHistoryItem> items)
    {
        _source = items;
        RefreshItems();
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        _stateLabel.Text = paused ? "History paused" : "Type History";
        _pauseButton.Text = paused ? "Resume History" : "Pause History";
        PauseChanged?.Invoke(paused);
    }

    public void FocusHistory()
    {
        if (_list.Items.Count > 0 && _list.SelectedIndex < 0) _list.SelectedIndex = 0;
        _searchBox.Focus();
    }

    private void RefreshItems()
    {
        string query = _searchBox.Text;
        TypeHistoryItem[] items = _source
            .Where(item => query.Length == 0 || item.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.IsPinned)
            .ThenByDescending(item => item.LastUsedAt)
            .ToArray();
        _list.BeginUpdate();
        _list.Items.Clear();
        _list.Items.AddRange(items);
        _list.EndUpdate();
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || _list.Items[e.Index] is not TypeHistoryItem item) return;
        Rectangle bounds = e.Bounds;
        string preview = item.Content.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = preview.Split('\n');
        preview = string.Join(Environment.NewLine, lines.Take(3));
        TextRenderer.DrawText(e.Graphics, preview, Font, new Rectangle(bounds.X + 7, bounds.Y + 5, bounds.Width - 14, 48), ForeColor, TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
        string meta = $"{(item.IsPinned ? "📌 " : "")}{item.LastUsedAt.LocalDateTime:g} · {item.Content.Length} chars" + (item.UseCount > 1 ? $" · used {item.UseCount}x" : "") + $" · {item.Status}  ⋯";
        TextRenderer.DrawText(e.Graphics, meta, Font, new Rectangle(bounds.X + 7, bounds.Bottom - 25, bounds.Width - 14, 20), SystemColors.GrayText, TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private void ListMouseDown(object? sender, MouseEventArgs e)
    {
        int index = _list.IndexFromPoint(e.Location);
        if (index < 0) return;

        _list.SelectedIndex = index;
        if (e.Button == MouseButtons.Left && e.X >= _list.ClientSize.Width - 36)
        {
            _suppressLoadClick = true;
            Rectangle itemBounds = _list.GetItemRectangle(index);
            _itemMenu.Show(_list, new Point(_list.ClientSize.Width - _itemMenu.PreferredSize.Width, itemBounds.Bottom));
        }
    }

    private void ListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && e.Control) WithSelected(item => TypeRequested?.Invoke(item));
        else if (e.KeyCode == Keys.Enter) WithSelected(item => LoadRequested?.Invoke(item));
        else if (e.KeyCode == Keys.Delete) WithSelected(item => DeleteRequested?.Invoke(item));
        else return;
        e.Handled = true;
    }

    private void AddMenuItem(ContextMenuStrip menu, string text, Action<TypeHistoryItem> action) =>
        menu.Items.Add(text, null, (_, _) => WithSelected(action));

    private void WithSelected(Action<TypeHistoryItem> action)
    {
        if (_list.SelectedItem is TypeHistoryItem item) action(item);
    }
}
