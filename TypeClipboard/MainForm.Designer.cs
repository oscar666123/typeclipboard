namespace TypeClipboard;

public partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private TextBox clipboardTextBox = null!;
    private Button copyClipboardButton = null!;
    private Button typeButton = null!;
    private Button stopButton = null!;
    private CheckBox typeEnterCheckBox = null!;
    private CheckBox hotKeyEnabledCheckBox = null!;
    private ComboBox hotKeyComboBox = null!;
    private NumericUpDown startDelayNumeric = null!;
    private NumericUpDown interkeyDelayNumeric = null!;
    private Label startDelayLabel = null!;
    private Label interkeyDelayLabel = null!;
    private Label hotKeyLabel = null!;
    private Label statusLabel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        clipboardTextBox = new TextBox();
        copyClipboardButton = new Button();
        typeButton = new Button();
        stopButton = new Button();
        typeEnterCheckBox = new CheckBox();
        hotKeyEnabledCheckBox = new CheckBox();
        hotKeyComboBox = new ComboBox();
        startDelayNumeric = new NumericUpDown();
        interkeyDelayNumeric = new NumericUpDown();
        startDelayLabel = new Label();
        interkeyDelayLabel = new Label();
        hotKeyLabel = new Label();
        statusLabel = new Label();
        ((System.ComponentModel.ISupportInitialize)startDelayNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)interkeyDelayNumeric).BeginInit();
        SuspendLayout();
        // 
        // clipboardTextBox
        // 
        clipboardTextBox.AcceptsReturn = true;
        clipboardTextBox.AcceptsTab = true;
        clipboardTextBox.Location = new Point(12, 12);
        clipboardTextBox.Multiline = true;
        clipboardTextBox.Name = "clipboardTextBox";
        clipboardTextBox.ScrollBars = ScrollBars.Vertical;
        clipboardTextBox.Size = new Size(460, 156);
        clipboardTextBox.TabIndex = 0;
        clipboardTextBox.TextChanged += clipboardTextBox_TextChanged;
        // 
        // copyClipboardButton
        // 
        copyClipboardButton.Location = new Point(12, 181);
        copyClipboardButton.Name = "copyClipboardButton";
        copyClipboardButton.Size = new Size(170, 32);
        copyClipboardButton.TabIndex = 1;
        copyClipboardButton.Text = "Refresh clipboard";
        copyClipboardButton.UseVisualStyleBackColor = true;
        copyClipboardButton.Click += copyClipboardButton_Click;
        // 
        // typeButton
        // 
        typeButton.Location = new Point(242, 181);
        typeButton.Name = "typeButton";
        typeButton.Size = new Size(110, 32);
        typeButton.TabIndex = 2;
        typeButton.Text = "Type (Ctrl+T)";
        typeButton.UseVisualStyleBackColor = true;
        typeButton.Click += typeButton_Click;
        // 
        // stopButton
        // 
        stopButton.Enabled = false;
        stopButton.Location = new Point(362, 181);
        stopButton.Name = "stopButton";
        stopButton.Size = new Size(110, 32);
        stopButton.TabIndex = 3;
        stopButton.Text = "Stop (Esc)";
        stopButton.UseVisualStyleBackColor = true;
        stopButton.Click += stopButton_Click;
        // 
        // typeEnterCheckBox
        // 
        typeEnterCheckBox.AutoSize = true;
        typeEnterCheckBox.Checked = true;
        typeEnterCheckBox.CheckState = CheckState.Checked;
        typeEnterCheckBox.Location = new Point(12, 231);
        typeEnterCheckBox.Name = "typeEnterCheckBox";
        typeEnterCheckBox.Size = new Size(82, 19);
        typeEnterCheckBox.TabIndex = 4;
        typeEnterCheckBox.Text = "Type Enter";
        typeEnterCheckBox.UseVisualStyleBackColor = true;
        // 
        // hotKeyEnabledCheckBox
        // 
        hotKeyEnabledCheckBox.AutoSize = true;
        hotKeyEnabledCheckBox.Checked = true;
        hotKeyEnabledCheckBox.CheckState = CheckState.Checked;
        hotKeyEnabledCheckBox.Location = new Point(12, 266);
        hotKeyEnabledCheckBox.Name = "hotKeyEnabledCheckBox";
        hotKeyEnabledCheckBox.Size = new Size(80, 19);
        hotKeyEnabledCheckBox.TabIndex = 5;
        hotKeyEnabledCheckBox.Text = "Hotkey enabled";
        hotKeyEnabledCheckBox.UseVisualStyleBackColor = true;
        hotKeyEnabledCheckBox.CheckedChanged += hotKeyEnabledCheckBox_CheckedChanged;
        // 
        // hotKeyComboBox
        // 
        hotKeyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        hotKeyComboBox.DropDownWidth = 140;
        hotKeyComboBox.FormattingEnabled = true;
        hotKeyComboBox.Location = new Point(120, 264);
        hotKeyComboBox.Name = "hotKeyComboBox";
        hotKeyComboBox.Size = new Size(110, 23);
        hotKeyComboBox.TabIndex = 6;
        hotKeyComboBox.SelectedIndexChanged += hotKeyComboBox_SelectedIndexChanged;
        // 
        // startDelayNumeric
        // 
        startDelayNumeric.Increment = new decimal(new int[] { 100, 0, 0, 0 });
        startDelayNumeric.Location = new Point(352, 229);
        startDelayNumeric.Maximum = new decimal(new int[] { 60000, 0, 0, 0 });
        startDelayNumeric.Name = "startDelayNumeric";
        startDelayNumeric.Size = new Size(120, 23);
        startDelayNumeric.TabIndex = 8;
        startDelayNumeric.Value = new decimal(new int[] { 500, 0, 0, 0 });
        // 
        // interkeyDelayNumeric
        // 
        interkeyDelayNumeric.Location = new Point(352, 264);
        interkeyDelayNumeric.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
        interkeyDelayNumeric.Name = "interkeyDelayNumeric";
        interkeyDelayNumeric.Size = new Size(120, 23);
        interkeyDelayNumeric.TabIndex = 10;
        interkeyDelayNumeric.Value = new decimal(new int[] { 10, 0, 0, 0 });
        // 
        // startDelayLabel
        // 
        startDelayLabel.AutoSize = true;
        startDelayLabel.Location = new Point(241, 232);
        startDelayLabel.Name = "startDelayLabel";
        startDelayLabel.Size = new Size(88, 15);
        startDelayLabel.TabIndex = 7;
        startDelayLabel.Text = "Start delay (ms)";
        // 
        // interkeyDelayLabel
        // 
        interkeyDelayLabel.AutoSize = true;
        interkeyDelayLabel.Location = new Point(241, 266);
        interkeyDelayLabel.Name = "interkeyDelayLabel";
        interkeyDelayLabel.Size = new Size(105, 15);
        interkeyDelayLabel.TabIndex = 9;
        interkeyDelayLabel.Text = "Interkey delay (ms)";
        // 
        // hotKeyLabel
        // 
        hotKeyLabel.AutoSize = true;
        hotKeyLabel.Location = new Point(120, 232);
        hotKeyLabel.Name = "hotKeyLabel";
        hotKeyLabel.Size = new Size(107, 15);
        hotKeyLabel.TabIndex = 11;
        hotKeyLabel.Text = "Emergency hotkey";
        // 
        // statusLabel
        // 
        statusLabel.AutoEllipsis = true;
        statusLabel.BorderStyle = BorderStyle.Fixed3D;
        statusLabel.Location = new Point(12, 307);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(460, 24);
        statusLabel.TabIndex = 12;
        statusLabel.Text = "Ready";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(484, 343);
        Controls.Add(statusLabel);
        Controls.Add(hotKeyLabel);
        Controls.Add(interkeyDelayNumeric);
        Controls.Add(interkeyDelayLabel);
        Controls.Add(startDelayNumeric);
        Controls.Add(startDelayLabel);
        Controls.Add(hotKeyComboBox);
        Controls.Add(hotKeyEnabledCheckBox);
        Controls.Add(typeEnterCheckBox);
        Controls.Add(stopButton);
        Controls.Add(typeButton);
        Controls.Add(copyClipboardButton);
        Controls.Add(clipboardTextBox);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Type Clipboard";
        FormClosing += MainForm_FormClosing;
        Load += MainForm_Load;
        ((System.ComponentModel.ISupportInitialize)startDelayNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)interkeyDelayNumeric).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
}
