public class MainForm : Form
{
    private Button selectFolderButton;
    private CheckBox preserveTrimmedCheckbox;
    private CheckBox createSubfolderCheckbox;
    private CheckBox nullifyVariantsCheckbox;
    private Label preserveTrimmedLabel;
    private Label infoLabel;
    private Button convertButton;
    private TextBox logTextBox;
    private ToolTip toolTip;

    private List<string> selectedFiles = new List<string>();
    private string documentsPath;
    private string defaultStadiumPath;

    public MainForm()
    {
        InitializeComponent();
        documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        defaultStadiumPath = Path.Combine(documentsPath, @"Trackmania\Blocks\Stadium");
    }

    private void InitializeComponent()
    {
        this.Text = "Trackmania Macroblock Converter";
        this.Size = new Size(700, 550);
        this.MinimumSize = new Size(600, 400);
        this.StartPosition = FormStartPosition.CenterScreen;

        selectFolderButton = new Button
        {
            Text = "Select Folder",
            Location = new Point(20, 20),
            Size = new Size(150, 35),
            Font = new Font("Segoe UI", 9F)
        };
        selectFolderButton.Click += SelectFolderButtonClick;

        preserveTrimmedCheckbox = new CheckBox
        {
            Location = new Point(20, 70),
            Size = new Size(20, 20),
            Checked = true
        };

        preserveTrimmedLabel = new Label
        {
            Text = "Preserve trimmed macroblocks",
            Location = new Point(45, 72),
            Size = new Size(180, 20),
            Font = new Font("Segoe UI", 9F)
        };

        infoLabel = new Label
        {
            Text = "(?)",
            Location = new Point(230, 72),
            Size = new Size(25, 20),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.Blue,
            Cursor = Cursors.Help
        };

        toolTip = new ToolTip
        {
            AutoPopDelay = 10000,
            InitialDelay = 500,
            ReshowDelay = 100
        };
        toolTip.SetToolTip(infoLabel, "When checked, invalid blocks will be skipped, but the program will still convert the remaining blocks.");

        createSubfolderCheckbox = new CheckBox
        {
            Location = new Point(20, 100),
            Size = new Size(20, 20),
            Checked = true
        };

        Label createConvertedLabel = new Label
        {
            Text = "Create 'Converted' subfolder",
            Location = new Point(45, 102),
            Size = new Size(180, 20),
            Font = new Font("Segoe UI", 9F)
        };

        nullifyVariantsCheckbox = new CheckBox
        {
            Location = new Point(20, 130),
            Size = new Size(20, 20),
            Checked = true
        };

        Label nullifyVariantsLabel = new Label
        {
            Text = "Set base block variants",
            Location = new Point(45, 132),
            Size = new Size(180, 20),
            Font = new Font("Segoe UI", 9F)
        };

        convertButton = new Button
        {
            Text = "Convert",
            Location = new Point(20, 165),
            Size = new Size(150, 35),
            Font = new Font("Segoe UI", 9F),
            Enabled = false
        };
        convertButton.Click += ConvertButtonClick;

        logTextBox = new TextBox
        {
            Location = new Point(20, 215),
            Size = new Size(640, 330),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        this.Controls.Add(selectFolderButton);
        this.Controls.Add(preserveTrimmedCheckbox);
        this.Controls.Add(preserveTrimmedLabel);
        this.Controls.Add(infoLabel);
        this.Controls.Add(createSubfolderCheckbox);
        this.Controls.Add(createConvertedLabel);
        this.Controls.Add(nullifyVariantsCheckbox);
        this.Controls.Add(nullifyVariantsLabel);
        this.Controls.Add(convertButton);
        this.Controls.Add(logTextBox);
    }

    private void SelectFolderButtonClick(object sender, EventArgs e)
    {
        using (var folderDialog = new FolderBrowserDialog())
        {
            if (Directory.Exists(defaultStadiumPath))
            {
                folderDialog.SelectedPath = defaultStadiumPath;
            }
            else if (Directory.Exists(documentsPath))
            {
                folderDialog.SelectedPath = documentsPath;
            }

            folderDialog.Description = "Select folder containing macroblocks to convert";
            folderDialog.ShowNewFolderButton = false;

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                selectedFiles.Clear();
                var files = Directory.GetFiles(folderDialog.SelectedPath, "*.Macroblock.Gbx", SearchOption.AllDirectories);
                selectedFiles.AddRange(files);
                
                LogMessage($"Selected folder: {folderDialog.SelectedPath}");
                LogMessage($"Found {selectedFiles.Count} macroblocks.");
                convertButton.Enabled = selectedFiles.Count > 0;
            }
        }
    }

    private void ConvertButtonClick(object sender, EventArgs e)
    {
        if (selectedFiles.Count == 0)
        {
            LogMessage("No macroblocks found!");
            return;
        }

        LogMessage("\n=== Starting Conversion ===");
        convertButton.Enabled = false;
        selectFolderButton.Enabled = false;

        try
        {
            var converter = new MacroblockConverter(
                selectedFiles, 
                preserveTrimmedCheckbox.Checked,
                createSubfolderCheckbox.Checked,
                nullifyVariantsCheckbox.Checked,
                LogMessage
            );
            
            converter.Convert();
            
            LogMessage("\n=== Conversion Complete ===");
            LogMessage("\n\n!!! Remember to restart your game. !!!");
        }
        catch (Exception ex)
        {
            LogMessage($"\nError during conversion: {ex.Message}");
            LogMessage($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            convertButton.Enabled = true;
            selectFolderButton.Enabled = true;
        }
    }

    public void LogMessage(string message)
    {
        if (logTextBox.InvokeRequired)
        {
            logTextBox.Invoke(new Action<string>(LogMessage), message);
        }
        else
        {
            logTextBox.AppendText(message + Environment.NewLine);
            logTextBox.SelectionStart = logTextBox.Text.Length;
            logTextBox.ScrollToCaret();
        }
    }
}