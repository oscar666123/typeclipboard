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
    private Button emergencyShortcutButton = null!;
    private NumericUpDown startDelayNumeric = null!;
    private NumericUpDown interkeyDelayNumeric = null!;
    private Label startDelayLabel = null!;
    private Label interkeyDelayLabel = null!;
    private Label hotKeyLabel = null!;
    private Label typeShortcutLabel = null!;
    private Label stopShortcutLabel = null!;
    private Button typeShortcutButton = null!;
    private Button stopShortcutButton = null!;
    private CheckBox alwaysOnTopCheckBox = null!;
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
        emergencyShortcutButton = new Button();
        startDelayNumeric = new NumericUpDown();
        interkeyDelayNumeric = new NumericUpDown();
        startDelayLabel = new Label();
        interkeyDelayLabel = new Label();
        hotKeyLabel = new Label();
        typeShortcutLabel = new Label();
        stopShortcutLabel = new Label();
        typeShortcutButton = new Button();
        stopShortcutButton = new Button();
        alwaysOnTopCheckBox = new CheckBox();
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
        typeButton.Text = "Type";
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
        stopButton.Text = "Stop";
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
        hotKeyEnabledCheckBox.Size = new Size(155, 19);
        hotKeyEnabledCheckBox.TabIndex = 6;
        hotKeyEnabledCheckBox.Text = "Emergency enabled";
        hotKeyEnabledCheckBox.UseVisualStyleBackColor = true;
        hotKeyEnabledCheckBox.CheckedChanged += hotKeyEnabledCheckBox_CheckedChanged;
        // 
        // emergencyShortcutButton
        // 
        emergencyShortcutButton.AutoEllipsis = true;
        emergencyShortcutButton.Location = new Point(352, 264);
        emergencyShortcutButton.Name = "emergencyShortcutButton";
        emergencyShortcutButton.Size = new Size(120, 23);
        emergencyShortcutButton.TabIndex = 7;
        emergencyShortcutButton.Text = "Change: F8";
        emergencyShortcutButton.UseVisualStyleBackColor = true;
        emergencyShortcutButton.Click += emergencyShortcutButton_Click;
        // 
        // startDelayNumeric
        // 
        startDelayNumeric.Increment = new decimal(new int[] { 100, 0, 0, 0 });
        startDelayNumeric.Location = new Point(352, 229);
        startDelayNumeric.Maximum = new decimal(new int[] { 60000, 0, 0, 0 });
        startDelayNumeric.Name = "startDelayNumeric";
        startDelayNumeric.Size = new Size(120, 23);
        startDelayNumeric.TabIndex = 5;
        startDelayNumeric.Value = new decimal(new int[] { 500, 0, 0, 0 });
        // 
        // interkeyDelayNumeric
        // 
        interkeyDelayNumeric.Location = new Point(352, 334);
        interkeyDelayNumeric.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
        interkeyDelayNumeric.Name = "interkeyDelayNumeric";
        interkeyDelayNumeric.Size = new Size(120, 23);
        interkeyDelayNumeric.TabIndex = 11;
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
        interkeyDelayLabel.Location = new Point(241, 336);
        interkeyDelayLabel.Name = "interkeyDelayLabel";
        interkeyDelayLabel.Size = new Size(105, 15);
        interkeyDelayLabel.TabIndex = 9;
        interkeyDelayLabel.Text = "Interkey delay (ms)";
        // 
        // hotKeyLabel
        // 
        hotKeyLabel.AutoSize = true;
        hotKeyLabel.Location = new Point(241, 267);
        hotKeyLabel.Name = "hotKeyLabel";
        hotKeyLabel.Size = new Size(107, 15);
        hotKeyLabel.TabIndex = 11;
        hotKeyLabel.Text = "Emergency key";
        //
        // typeShortcutLabel
        //
        typeShortcutLabel.AutoSize = true;
        typeShortcutLabel.Location = new Point(12, 302);
        typeShortcutLabel.Name = "typeShortcutLabel";
        typeShortcutLabel.Size = new Size(76, 15);
        typeShortcutLabel.TabIndex = 12;
        typeShortcutLabel.Text = "Type shortcut";
        //
        // stopShortcutLabel
        //
        stopShortcutLabel.AutoSize = true;
        stopShortcutLabel.Location = new Point(241, 302);
        stopShortcutLabel.Name = "stopShortcutLabel";
        stopShortcutLabel.Size = new Size(77, 15);
        stopShortcutLabel.TabIndex = 14;
        stopShortcutLabel.Text = "Stop shortcut";
        //
        // typeShortcutButton
        //
        typeShortcutButton.AutoEllipsis = true;
        typeShortcutButton.Location = new Point(120, 299);
        typeShortcutButton.Name = "typeShortcutButton";
        typeShortcutButton.Size = new Size(110, 23);
        typeShortcutButton.TabIndex = 8;
        typeShortcutButton.Text = "Change: F9";
        typeShortcutButton.UseVisualStyleBackColor = true;
        typeShortcutButton.Click += typeShortcutButton_Click;
        //
        // stopShortcutButton
        //
        stopShortcutButton.AutoEllipsis = true;
        stopShortcutButton.Location = new Point(352, 299);
        stopShortcutButton.Name = "stopShortcutButton";
        stopShortcutButton.Size = new Size(120, 23);
        stopShortcutButton.TabIndex = 9;
        stopShortcutButton.Text = "Change: F10";
        stopShortcutButton.UseVisualStyleBackColor = true;
        stopShortcutButton.Click += stopShortcutButton_Click;
        //
        // alwaysOnTopCheckBox
        //
        alwaysOnTopCheckBox.AutoSize = true;
        alwaysOnTopCheckBox.Checked = true;
        alwaysOnTopCheckBox.CheckState = CheckState.Checked;
        alwaysOnTopCheckBox.Location = new Point(12, 336);
        alwaysOnTopCheckBox.Name = "alwaysOnTopCheckBox";
        alwaysOnTopCheckBox.Size = new Size(101, 19);
        alwaysOnTopCheckBox.TabIndex = 10;
        alwaysOnTopCheckBox.Text = "Always on top";
        alwaysOnTopCheckBox.UseVisualStyleBackColor = true;
        alwaysOnTopCheckBox.CheckedChanged += alwaysOnTopCheckBox_CheckedChanged;
        //
        // statusLabel
        //
        statusLabel.AutoEllipsis = true;
        statusLabel.BorderStyle = BorderStyle.Fixed3D;
        statusLabel.Location = new Point(12, 400);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(460, 24);
        statusLabel.TabIndex = 17;
        statusLabel.Text = "Ready";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(484, 436);
        Controls.Add(statusLabel);
        Controls.Add(alwaysOnTopCheckBox);
        Controls.Add(stopShortcutButton);
        Controls.Add(typeShortcutButton);
        Controls.Add(stopShortcutLabel);
        Controls.Add(typeShortcutLabel);
        Controls.Add(hotKeyLabel);
        Controls.Add(interkeyDelayNumeric);
        Controls.Add(interkeyDelayLabel);
        Controls.Add(startDelayNumeric);
        Controls.Add(startDelayLabel);
        Controls.Add(emergencyShortcutButton);
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
