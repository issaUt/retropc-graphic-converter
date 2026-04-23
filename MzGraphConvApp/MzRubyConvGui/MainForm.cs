using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MzRubyConvGui;

public partial class MainForm : Form
{
    private const int RightOptionLabelWidth = 82;
    private const int RightActionButtonWidth = 104;
    private const int RightOptionControlWidth = RightActionButtonWidth;

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MzRubyConvGui");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");
    private const int HistoryLimit = 10;

    private readonly TextBox txtRuby = new();
    private readonly TextBox txtScript = new();
    private readonly Label lblScriptVersion = new();
    private readonly Button btnGetScriptVersion = new();
    private readonly ComboBox txtInput = new();
    private readonly ComboBox txtOutputDir = new();
    private readonly ComboBox txtBaseName = new();
    private readonly ComboBox cmbMode = new();
    private readonly ComboBox cmbFixed = new();
    private readonly ComboBox cmbLayout = new();
    private readonly ComboBox cmbResize = new();
    private readonly ComboBox cmbMethod = new();
    private readonly ComboBox cmbDistance = new();
    private readonly ComboBox cmbRemove = new();
    private readonly ComboBox cmbSort = new();
    private readonly NumericUpDown numStrength = new();
    private readonly CheckBox chkPngOnly = new();
    private readonly Button btnRun = new();
    private readonly Button btnCancel = new();
    private readonly Button btnResetCoreOptions = new();
    private readonly AspectPictureBox picOriginal = new();
    private readonly AspectPictureBox picPreview = new();
    private readonly ComboBox cmbOutputs = new();
    private readonly CheckBox chkPreviewDisplayAspect = new();
    private readonly ListView lstOutputFiles = new();
    private readonly Button btnOpenOutputDir = new();
    private readonly Button btnOpenSelectedOutput = new();
    private readonly TextBox txtLog = new();
    private readonly TextBox txtJson = new();
    private readonly StatusStrip statusStrip = new();
    private readonly ToolStripStatusLabel statusLabel = new();
    private readonly Button focusSink = new();
    private readonly List<Control> controlsDisabledDuringRun = new();
    private readonly List<OutputImageItem> outputImages = new();
    private readonly object processLock = new();
    private Process? currentProcess;
    private string? currentOriginalPath;
    private string? currentPreviewPath;
    private bool cancelRequested;

    public MainForm()
    {
        InitializeComponent();
        BuildUi();
        ApplyDefaults();
        LoadSettings();
        Shown += (_, _) => ClearInitialFocus();
        FormClosing += (_, _) => SaveSettings();
    }

    private void BuildUi()
    {
        Text = "RetroPC Graphic Converter";
        MinimumSize = new Size(840, 680);
        Size = new Size(944, 720);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        Controls.Add(root);

        root.Controls.Add(BuildMainTabs(), 0, 0);
        SetupInputDropTargets();

        statusStrip.Items.Add(statusLabel);
        statusLabel.Text = "Ready";
        root.Controls.Add(statusStrip, 0, 1);

        focusSink.Size = new Size(1, 1);
        focusSink.Location = new Point(-10, -10);
        focusSink.TabStop = false;
        focusSink.FlatStyle = FlatStyle.Flat;
        focusSink.FlatAppearance.BorderSize = 0;
        focusSink.Text = string.Empty;
        Controls.Add(focusSink);
    }

    private Control BuildMainTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        var convertTab = new TabPage("Convert");
        convertTab.Controls.Add(BuildConvertPage());
        tabs.TabPages.Add(convertTab);

        var settingsTab = new TabPage("Settings");
        settingsTab.Controls.Add(BuildEnvironmentSettingsPanel());
        tabs.TabPages.Add(settingsTab);

        return tabs;
    }

    private Control BuildConvertPage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 296));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildSettingsPanel(), 0, 0);
        root.Controls.Add(BuildTabs(), 0, 1);
        return root;
    }

    private Control BuildSettingsPanel()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0, 0, 0, 8)
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        var pathsPanel = BuildPathOptionsPanel();
        var outputPanel = BuildOutputOptionsPanel();
        var ditherPanel = BuildDitherOptionsPanel();
        var actionPanel = BuildActionPanel();

        root.Controls.Add(pathsPanel, 0, 0);
        root.Controls.Add(outputPanel, 0, 1);
        root.Controls.Add(ditherPanel, 0, 2);
        root.Controls.Add(actionPanel, 0, 3);

        cmbMode.SelectedIndexChanged += (_, _) => UpdateOptionState();
        cmbLayout.SelectedIndexChanged += (_, _) =>
        {
            UpdateOptionState();
            UpdatePreviewDisplayAspect();
        };
        cmbResize.SelectedIndexChanged += (_, _) => UpdateOriginalResizePreview();
        txtInput.TextChanged += (_, _) => HandleInputTextChanged();

        controlsDisabledDuringRun.AddRange([pathsPanel, outputPanel, ditherPanel]);

        return root;
    }

    private Control BuildPathOptionsPanel()
    {
        var panel = CreatePathPanel(2);
        AddInputPathRow(panel, 0);
        AddPathRow(panel, 1, "Output Dir", txtOutputDir, "参照...", BrowseOutputDir);
        return panel;
    }

    private Control BuildEnvironmentSettingsPanel()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8, 12, 8, 8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var panel = CreatePathPanel(3);
        AddPathRow(panel, 0, "Ruby", txtRuby, "参照...", BrowseRuby);
        AddPathRow(panel, 1, "Script", txtScript, "参照...", BrowseScript);
        AddScriptVersionRow(panel, 2);
        root.Controls.Add(panel, 0, 0);
        return root;
    }
    private Control BuildOutputOptionsPanel()
    {
        var panel = CreateSixColumnPanel(3);

        panel.Controls.Add(MakeLabel("Base Name"), 0, 0);
        SetupHistoryCombo(txtBaseName);
        panel.Controls.Add(txtBaseName, 1, 0);
        panel.SetColumnSpan(txtBaseName, 3);
        AddRightOptionCombo(panel, "Resize", cmbResize, 0, ["fit", "keep", "cut"]);

        chkPngOnly.Text = "MZ-2500専用ファイルを出力しない";
        chkPngOnly.AutoSize = true;
        chkPngOnly.Dock = DockStyle.Fill;
        chkPngOnly.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(chkPngOnly, 1, 1);
        panel.SetColumnSpan(chkPngOnly, 5);

        panel.Controls.Add(MakeLabel("Mode"), 0, 2);
        SetupCombo(cmbMode, ["8", "16", "512", "4096"]);
        panel.Controls.Add(MakeLeftAlignedControl(cmbMode, 150), 1, 2);
        panel.Controls.Add(MakeLabel("Fixed"), 2, 2);
        SetupCombo(cmbFixed, ["R", "G", "B", "all"]);
        panel.Controls.Add(MakeLeftAlignedControl(cmbFixed, 96), 3, 2);
        AddRightOptionCombo(panel, "Layout", cmbLayout, 2, ["640x400", "640x200", "320x200", "split320x200"]);

        return panel;
    }

    private Control BuildDitherOptionsPanel()
    {
        var panel = CreateSixColumnPanel(2);

        SetupCombo(cmbMethod, ["floyd_steinberg", "stucki", "jarvis", "no_dither"]);
        SetupCombo(cmbDistance, ["rgb", "lab", "oklab"]);
        SetupCombo(cmbRemove, ["no_remove", "removeBB", "removeDW", "removeBBDW"]);
        SetupCombo(cmbSort, ["no_sort", "luminance", "frequency"]);

        numStrength.DecimalPlaces = 2;
        numStrength.Minimum = 0;
        numStrength.Maximum = 1;
        numStrength.Increment = 0.05M;
        numStrength.Dock = DockStyle.Fill;

        panel.Controls.Add(MakeLabel("Method"), 0, 0);
        panel.Controls.Add(MakeLeftAlignedControl(cmbMethod, 150), 1, 0);
        panel.Controls.Add(MakeLabel("Strength"), 2, 0);
        panel.Controls.Add(MakeLeftAlignedControl(numStrength, 96), 3, 0);
        AddRightOptionCombo(panel, "Distance", cmbDistance, 0, ["rgb", "lab", "oklab"]);

        panel.Controls.Add(MakeLabel("Remove"), 0, 1);
        panel.Controls.Add(MakeLeftAlignedControl(cmbRemove, 136), 1, 1);
        panel.Controls.Add(MakeLabel("Sort"), 2, 1);
        panel.Controls.Add(MakeLeftAlignedControl(cmbSort, 136), 3, 1);

        btnResetCoreOptions.Text = "Set Defaults";
        btnResetCoreOptions.Click += (_, _) => ApplyRubyCoreOptionDefaults();
        panel.Controls.Add(MakeRightAlignedControl(btnResetCoreOptions, RightActionButtonWidth), 4, 1);
        panel.SetColumnSpan(btnResetCoreOptions.Parent!, 2);

        return panel;
    }

    private Control BuildActionPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));

        btnRun.Text = "Run";
        btnRun.Dock = DockStyle.Fill;
        btnRun.Click += async (_, _) => await RunConversionAsync();
        panel.Controls.Add(btnRun, 0, 0);

        btnCancel.Text = "Cancel";
        btnCancel.Dock = DockStyle.Fill;
        btnCancel.Enabled = false;
        btnCancel.Click += (_, _) => CancelConversion();
        panel.Controls.Add(btnCancel, 1, 0);

        var openDirButton = new Button
        {
            Text = "Open Dir",
            Dock = DockStyle.Fill
        };
        openDirButton.Click += (_, _) => OpenOutputDirectory();
        panel.Controls.Add(openDirButton, 2, 0);

        return panel;
    }

    private static Control MakeLeftAlignedControl(Control control, int width)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        control.Dock = DockStyle.None;
        control.Width = width;
        control.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        control.Margin = new Padding(0, 4, 0, 4);
        control.Location = new Point(0, 4);
        panel.Controls.Add(control);
        return panel;
    }

    private static Control MakeRightAlignedControl(Control control, int width)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        control.Dock = DockStyle.None;
        control.Width = width;
        control.Height = 28;
        control.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        control.Margin = new Padding(0, 4, 0, 4);
        panel.Resize += (_, _) =>
        {
            control.Location = new Point(Math.Max(0, panel.ClientSize.Width - control.Width), 4);
        };
        control.Location = new Point(0, 4);
        panel.Controls.Add(control);
        return panel;
    }

    private static TableLayoutPanel CreatePathPanel(int rows)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = rows,
            Margin = new Padding(0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        for (var i = 0; i < rows; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        }

        return panel;
    }

    private static TableLayoutPanel CreateSixColumnPanel(int rows)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = rows,
            Margin = new Padding(0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        for (var i = 0; i < rows; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        }
        return panel;
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        var tabCompare = new TabPage("Compare");
        var compareRoot = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        compareRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        compareRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        compareRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        compareRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        compareRoot.Controls.Add(MakeLabel("Original"), 0, 0);
        var previewHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        previewHeader.Controls.Add(MakeLabel("Preview"), 0, 0);
        SetupCombo(cmbOutputs, []);
        cmbOutputs.Enabled = false;
        cmbOutputs.SelectedIndexChanged += (_, _) => ShowSelectedPreview();
        previewHeader.Controls.Add(cmbOutputs, 1, 0);
        chkPreviewDisplayAspect.Text = "Display Aspect";
        chkPreviewDisplayAspect.AutoSize = true;
        chkPreviewDisplayAspect.Dock = DockStyle.Fill;
        chkPreviewDisplayAspect.TextAlign = ContentAlignment.MiddleLeft;
        chkPreviewDisplayAspect.CheckedChanged += (_, _) => UpdatePreviewDisplayAspect();
        previewHeader.Controls.Add(chkPreviewDisplayAspect, 2, 0);
        compareRoot.Controls.Add(previewHeader, 1, 0);

        picOriginal.Dock = DockStyle.Fill;
        picOriginal.SizeMode = PictureBoxSizeMode.Zoom;
        picOriginal.BorderStyle = BorderStyle.FixedSingle;
        picOriginal.Cursor = Cursors.Hand;
        picOriginal.Click += (_, _) => ShowImagePopup("Original", currentOriginalPath);
        compareRoot.Controls.Add(picOriginal, 0, 1);

        picPreview.Dock = DockStyle.Fill;
        picPreview.SizeMode = PictureBoxSizeMode.Zoom;
        picPreview.BorderStyle = BorderStyle.FixedSingle;
        picPreview.Cursor = Cursors.Hand;
        picPreview.Click += (_, _) => ShowImagePopup("Preview", currentPreviewPath);
        compareRoot.Controls.Add(picPreview, 1, 1);

        tabCompare.Controls.Add(compareRoot);
        tabs.TabPages.Add(tabCompare);

        var tabFiles = new TabPage("Files");
        var filesRoot = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        filesRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        filesRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var filesButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        btnOpenOutputDir.Text = "Open Dir";
        btnOpenOutputDir.Width = 96;
        btnOpenOutputDir.Click += (_, _) => OpenOutputDirectory();
        filesButtons.Controls.Add(btnOpenOutputDir);

        btnOpenSelectedOutput.Text = "Open File";
        btnOpenSelectedOutput.Width = 96;
        btnOpenSelectedOutput.Enabled = false;
        btnOpenSelectedOutput.Click += (_, _) => OpenSelectedOutputFile();
        filesButtons.Controls.Add(btnOpenSelectedOutput);
        filesRoot.Controls.Add(filesButtons, 0, 0);

        lstOutputFiles.Dock = DockStyle.Fill;
        lstOutputFiles.View = View.Details;
        lstOutputFiles.FullRowSelect = true;
        lstOutputFiles.MultiSelect = false;
        lstOutputFiles.Columns.Add("Type", 80);
        lstOutputFiles.Columns.Add("Name", 220);
        lstOutputFiles.Columns.Add("Path", 620);
        lstOutputFiles.SelectedIndexChanged += (_, _) => btnOpenSelectedOutput.Enabled = lstOutputFiles.SelectedItems.Count > 0;
        lstOutputFiles.DoubleClick += (_, _) => OpenSelectedOutputFile();
        filesRoot.Controls.Add(lstOutputFiles, 0, 1);

        tabFiles.Controls.Add(filesRoot);
        tabs.TabPages.Add(tabFiles);

        var tabLog = new TabPage("Log");
        txtLog.Dock = DockStyle.Fill;
        txtLog.Multiline = true;
        txtLog.ScrollBars = ScrollBars.Both;
        txtLog.WordWrap = false;
        txtLog.ReadOnly = true;
        tabLog.Controls.Add(txtLog);
        tabs.TabPages.Add(tabLog);

        var tabJson = new TabPage("JSON");
        txtJson.Dock = DockStyle.Fill;
        txtJson.Multiline = true;
        txtJson.ScrollBars = ScrollBars.Both;
        txtJson.WordWrap = false;
        txtJson.ReadOnly = true;
        tabJson.Controls.Add(txtJson);
        tabs.TabPages.Add(tabJson);

        return tabs;
    }

    private void ApplyDefaults()
    {
        txtRuby.Text = "ruby";
        txtScript.Text = string.Empty;
        txtOutputDir.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MzRubyConv");
        txtBaseName.Text = "output";

        cmbMode.SelectedItem = "512";
        cmbFixed.SelectedItem = "all";
        cmbLayout.SelectedItem = "split320x200";
        cmbResize.SelectedItem = "fit";
        cmbMethod.SelectedItem = "floyd_steinberg";
        cmbDistance.SelectedItem = "rgb";
        cmbRemove.SelectedItem = "no_remove";
        cmbSort.SelectedItem = "no_sort";
        chkPngOnly.Checked = false;
        numStrength.Value = 1.0M;
        UpdateOptionState();
    }

    private void ApplyRubyCoreOptionDefaults()
    {
        SetCombo(cmbMode, "8");
        SetCombo(cmbFixed, "R");
        SetCombo(cmbLayout, "640x400");
        SetCombo(cmbResize, "fit");
        SetCombo(cmbMethod, "floyd_steinberg");
        SetCombo(cmbDistance, "rgb");
        SetCombo(cmbRemove, "no_remove");
        SetCombo(cmbSort, "no_sort");
        chkPngOnly.Checked = false;
        numStrength.Value = 1.0M;
        UpdateOptionState();
        UpdatePreviewDisplayAspect();
        UpdateOriginalResizePreview();
        statusLabel.Text = "Options reset to Ruby defaults.";
    }

    private void ClearInitialFocus()
    {
        txtRuby.SelectionLength = 0;
        txtScript.SelectionLength = 0;
        focusSink.Focus();
    }

    private void LoadSettings()
    {
        if (!File.Exists(SettingsPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings is null)
            {
                return;
            }

            ApplyHistory(txtInput, settings.InputHistory);
            ApplyHistory(txtOutputDir, settings.OutputDirHistory);
            ApplyHistory(txtBaseName, settings.BaseNameHistory);

            txtRuby.Text = settings.RubyPath ?? txtRuby.Text;
            txtScript.Text = settings.ScriptPath ?? txtScript.Text;
            _ = RefreshScriptVersionAsync(showErrors: false);
            txtInput.Text = settings.InputPath ?? txtInput.Text;
            txtOutputDir.Text = settings.OutputDir ?? txtOutputDir.Text;
            txtBaseName.Text = settings.BaseName ?? txtBaseName.Text;
            SetCombo(cmbMode, settings.Mode);
            SetCombo(cmbFixed, settings.Fixed);
            SetCombo(cmbLayout, settings.Layout);
            SetCombo(cmbResize, settings.Resize);
            SetCombo(cmbMethod, settings.Method);
            SetCombo(cmbDistance, settings.Distance);
            SetCombo(cmbRemove, settings.Remove);
            SetCombo(cmbSort, settings.Sort);
            chkPngOnly.Checked = settings.PngOnly;
            chkPreviewDisplayAspect.Checked = settings.PreviewDisplayAspect;
            if (settings.Strength >= numStrength.Minimum && settings.Strength <= numStrength.Maximum)
            {
                numStrength.Value = settings.Strength;
            }

            ClearOriginalImage();
            statusLabel.Text = File.Exists(txtInput.Text)
                ? "Input image selected. Press Load or Run."
                : "Ready";
            UpdateOptionState();
        }
        catch (Exception ex)
        {
            txtLog.Text = $"Failed to load settings: {ex.Message}";
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var settings = new AppSettings
            {
                RubyPath = txtRuby.Text,
                ScriptPath = txtScript.Text,
                InputPath = txtInput.Text,
                OutputDir = txtOutputDir.Text,
                BaseName = txtBaseName.Text,
                InputHistory = BuildHistory(txtInput, txtInput.Text),
                OutputDirHistory = BuildHistory(txtOutputDir, txtOutputDir.Text),
                BaseNameHistory = BuildHistory(txtBaseName, txtBaseName.Text),
                Mode = Selected(cmbMode),
                Fixed = Selected(cmbFixed),
                Layout = Selected(cmbLayout),
                Resize = Selected(cmbResize),
                Method = Selected(cmbMethod),
                Strength = numStrength.Value,
                Distance = Selected(cmbDistance),
                Remove = Selected(cmbRemove),
                Sort = Selected(cmbSort),
                PngOnly = chkPngOnly.Checked,
                PreviewDisplayAspect = chkPreviewDisplayAspect.Checked
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json, Encoding.UTF8);
        }
        catch
        {
            // Settings persistence should not block conversion work.
        }
    }

    private void AddPathRow(TableLayoutPanel panel, int row, string label, Control textControl, string buttonText, EventHandler handler)
    {
        panel.Controls.Add(MakeLabel(label), 0, row);
        if (textControl is ComboBox combo)
        {
            SetupHistoryCombo(combo);
        }
        textControl.Dock = DockStyle.Fill;
        panel.Controls.Add(textControl, 1, row);
        panel.SetColumnSpan(textControl, 4);

        var button = new Button { Text = buttonText, Dock = DockStyle.Fill };
        button.Click += handler;
        panel.Controls.Add(button, 5, row);
    }

    private void AddScriptVersionRow(TableLayoutPanel panel, int row)
    {
        panel.Controls.Add(MakeLabel("Script Version"), 0, row);
        lblScriptVersion.Text = "Not checked";
        lblScriptVersion.AutoSize = false;
        lblScriptVersion.Dock = DockStyle.Fill;
        lblScriptVersion.TextAlign = ContentAlignment.MiddleLeft;
        lblScriptVersion.Margin = new Padding(0, 4, 4, 4);
        panel.Controls.Add(lblScriptVersion, 1, row);
        panel.SetColumnSpan(lblScriptVersion, 4);

        btnGetScriptVersion.Text = "Get";
        btnGetScriptVersion.Dock = DockStyle.Fill;
        btnGetScriptVersion.Click += async (_, _) => await RefreshScriptVersionAsync(showErrors: true);
        panel.Controls.Add(btnGetScriptVersion, 5, row);
    }

    private void AddInputPathRow(TableLayoutPanel panel, int row)
    {
        panel.Controls.Add(MakeLabel("Input Image"), 0, row);
        SetupHistoryCombo(txtInput);
        txtInput.Dock = DockStyle.Fill;
        panel.Controls.Add(txtInput, 1, row);
        panel.SetColumnSpan(txtInput, 3);

        var loadButton = new Button { Text = "Load", Dock = DockStyle.Fill };
        loadButton.Click += (_, _) => LoadOriginalFromCurrentInput(showWarnings: true);
        panel.Controls.Add(loadButton, 4, row);

        var browseButton = new Button { Text = "参照...", Dock = DockStyle.Fill };
        browseButton.Click += BrowseInput;
        panel.Controls.Add(browseButton, 5, row);
    }

    private void AddCombo(TableLayoutPanel panel, string label, ComboBox combo, int col, int row, string[] items)
    {
        panel.Controls.Add(MakeLabel(label), col, row);
        SetupCombo(combo, items);
        panel.Controls.Add(combo, col + 1, row);
    }

    private static void AddRightOptionCombo(TableLayoutPanel panel, string label, ComboBox combo, int row, string[] items)
    {
        var wrapper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, RightOptionLabelWidth));
        wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, RightOptionControlWidth));

        SetupCombo(combo, items);
        wrapper.Controls.Add(MakeLabel(label), 1, 0);
        wrapper.Controls.Add(combo, 2, 0);
        panel.Controls.Add(wrapper, 4, row);
        panel.SetColumnSpan(wrapper, 2);
    }

    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 4, 4)
        };
    }

    private static Label MakeSmallLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(8, 8, 4, 0)
        };
    }

    private static void SetupCombo(ComboBox combo, string[] items)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Items.Clear();
        combo.Items.AddRange(items);
        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
        combo.Dock = DockStyle.Fill;
    }

    private static void SetupHistoryCombo(ComboBox combo)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDown;
        combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        combo.AutoCompleteSource = AutoCompleteSource.ListItems;
        combo.Dock = DockStyle.Fill;
    }

    private static void ApplyHistory(ComboBox combo, List<string>? history)
    {
        combo.Items.Clear();
        if (history is null)
        {
            return;
        }

        foreach (var item in history.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().Take(HistoryLimit))
        {
            combo.Items.Add(item);
        }
    }

    private static List<string> BuildHistory(ComboBox combo, string currentValue)
    {
        var values = new List<string>();
        AddHistoryValue(values, currentValue);
        foreach (var item in combo.Items.Cast<object>().Select(item => item.ToString() ?? string.Empty))
        {
            AddHistoryValue(values, item);
        }

        return values.Take(HistoryLimit).ToList();
    }

    private static void AddHistoryValue(List<string> values, string value)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value) || values.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        values.Add(value);
    }

    private void SetupInputDropTargets()
    {
        AddInputDropTarget(this);
        AddInputDropTarget(txtInput);
        AddInputDropTarget(picOriginal);
    }

    private void AddInputDropTarget(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += InputDragEnter;
        control.DragDrop += InputDragDrop;
    }

    private static string? GetDroppedImagePath(IDataObject? data)
    {
        if (data is null || !data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        if (data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return null;
        }

        return paths.FirstOrDefault(path =>
            File.Exists(path) &&
            IsSupportedInputImage(path));
    }

    private static bool IsSupportedInputImage(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".jpe";
    }

    private void InputDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = GetDroppedImagePath(e.Data) is null ? DragDropEffects.None : DragDropEffects.Copy;
    }

    private void InputDragDrop(object? sender, DragEventArgs e)
    {
        var path = GetDroppedImagePath(e.Data);
        if (path is null)
        {
            return;
        }

        SetInputPath(path, updateBaseName: true);
        SaveSettings();
    }

    private void BrowseRuby(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select ruby.exe"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtRuby.Text = dialog.FileName;
        }
    }

    private void BrowseScript(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Ruby script (*.rb)|*.rb|All files (*.*)|*.*",
            Title = "Select converter script"
        };
        if (File.Exists(txtScript.Text))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(txtScript.Text);
        }
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtScript.Text = dialog.FileName;
            _ = RefreshScriptVersionAsync(showErrors: false);
        }
    }

    private void BrowseInput(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|PNG image (*.png)|*.png|JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg|All files (*.*)|*.*",
            Title = "Select source image"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetInputPath(dialog.FileName, updateBaseName: true);
        }
    }

    private void BrowseOutputDir(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select output folder",
            UseDescriptionForTitle = true
        };
        if (Directory.Exists(txtOutputDir.Text))
        {
            dialog.InitialDirectory = txtOutputDir.Text;
        }
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtOutputDir.Text = dialog.SelectedPath;
        }
    }

    private async Task RefreshScriptVersionAsync(bool showErrors)
    {
        if (string.IsNullOrWhiteSpace(txtRuby.Text) || string.IsNullOrWhiteSpace(txtScript.Text))
        {
            lblScriptVersion.Text = "Not configured";
            return;
        }

        if (!File.Exists(txtScript.Text))
        {
            lblScriptVersion.Text = "Script not found";
            return;
        }

        btnGetScriptVersion.Enabled = false;
        lblScriptVersion.Text = "Checking...";
        try
        {
            var result = await RunRubyInfoAsync();
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(result.Log);
            }

            using var document = JsonDocument.Parse(result.StdOut);
            var root = document.RootElement;
            var version = root.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString()
                : null;
            var name = root.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : "Script";

            lblScriptVersion.Text = string.IsNullOrWhiteSpace(version)
                ? "Version unavailable"
                : $"{name} {version}";
        }
        catch (Exception ex)
        {
            lblScriptVersion.Text = "Version unavailable";
            if (showErrors)
            {
                MessageBox.Show(this, ex.Message, "Version Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            btnGetScriptVersion.Enabled = true;
        }
    }

    private async Task<RubyRunResult> RunRubyInfoAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = txtRuby.Text.Trim(),
            WorkingDirectory = Path.GetDirectoryName(txtScript.Text) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add(txtScript.Text.Trim());
        psi.ArgumentList.Add("--json");
        psi.ArgumentList.Add("--info");

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ruby process.");
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;
        var log = new StringBuilder();
        log.AppendLine(BuildCommandPreview(psi));
        if (!string.IsNullOrWhiteSpace(stdErr))
        {
            log.AppendLine();
            log.AppendLine("[stderr]");
            log.AppendLine(stdErr.TrimEnd());
        }

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stdOut))
        {
            log.AppendLine();
            log.AppendLine("[stdout]");
            log.AppendLine(stdOut.TrimEnd());
        }

        return new RubyRunResult(process.ExitCode, stdOut, log.ToString(), false);
    }

    private void HandleInputTextChanged()
    {
        UpdateBaseNameFromInput(updateExisting: false);
        ClearOriginalImage();
        statusLabel.Text = File.Exists(txtInput.Text)
            ? "Input image selected. Press Load or Run."
            : "Ready";
    }

    private void SetInputPath(string path, bool updateBaseName)
    {
        txtInput.Text = path;
        if (updateBaseName)
        {
            UpdateBaseNameFromInput(updateExisting: true);
        }
        if (File.Exists(path))
        {
            LoadOriginalImage(path);
        }
    }

    private bool LoadOriginalFromCurrentInput(bool showWarnings)
    {
        var path = txtInput.Text.Trim();
        if (!File.Exists(path))
        {
            if (showWarnings)
            {
                MessageBox.Show(this, "Input image was not found.", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            ClearOriginalImage();
            statusLabel.Text = "Input image was not found.";
            return false;
        }

        LoadOriginalImage(path);
        SaveSettings();
        statusLabel.Text = "Original image loaded.";
        return true;
    }

    private void UpdateBaseNameFromInput(bool updateExisting)
    {
        if (string.IsNullOrWhiteSpace(txtInput.Text) || !File.Exists(txtInput.Text))
        {
            return;
        }

        if (updateExisting || string.IsNullOrWhiteSpace(txtBaseName.Text) || txtBaseName.Text == "output")
        {
            txtBaseName.Text = Path.GetFileNameWithoutExtension(txtInput.Text);
        }
    }

    private void UpdateOptionState()
    {
        var mode = Selected(cmbMode);
        cmbFixed.Enabled = mode == "512";
        cmbRemove.Enabled = mode == "16";
        cmbSort.Enabled = mode == "4096";

        if (mode == "512" && Selected(cmbLayout) == "640x400")
        {
            cmbLayout.SelectedItem = "320x200";
        }

        if (mode != "512" && Selected(cmbLayout) == "split320x200")
        {
            cmbLayout.SelectedItem = "320x200";
        }
    }

    private async Task RunConversionAsync()
    {
        if (!ValidateInputs())
        {
            return;
        }

        if (!ConfirmOverwriteExistingOutputs())
        {
            return;
        }

        LoadOriginalFromCurrentInput(showWarnings: false);
        SetRunningState(true);
        cancelRequested = false;
        statusLabel.Text = "Running Ruby converter...";
        txtLog.Clear();
        txtJson.Clear();
        ClearPreview();

        try
        {
            var request = BuildRubyRequest();
            var result = await RunRubyConverterAsync(request);
            txtLog.Text = result.Log;
            txtJson.Text = result.StdOut;

            if (result.Canceled)
            {
                statusLabel.Text = "Canceled";
                txtLog.Text = result.Log;
                return;
            }

            if (result.ExitCode != 0)
            {
                statusLabel.Text = $"Failed: ruby exited with {result.ExitCode}";
                MessageBox.Show(this, result.Log, "Conversion failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            LoadOutputsFromJson(result.StdOut);
            txtLog.Text = AppendTimingSummary(txtLog.Text, result.StdOut);
            SaveSettings();
            statusLabel.Text = $"Done: {outputImages.Count} preview image(s)";
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Failed";
            txtLog.Text = ex.ToString();
            MessageBox.Show(this, ex.Message, "Conversion failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetRunningState(false);
        }
    }

    private void SetRunningState(bool running)
    {
        foreach (var control in controlsDisabledDuringRun)
        {
            control.Enabled = !running;
        }

        btnRun.Enabled = !running;
        btnCancel.Enabled = running;
    }

    private void CancelConversion()
    {
        cancelRequested = true;
        statusLabel.Text = "Canceling...";

        lock (processLock)
        {
            if (currentProcess is null || currentProcess.HasExited)
            {
                return;
            }

            try
            {
                currentProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                txtLog.Text = $"Failed to cancel ruby process: {ex.Message}";
            }
        }
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(txtRuby.Text))
        {
            MessageBox.Show(this, "Ruby executable is required. Please set it in the Settings tab.", "Missing Ruby", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (!File.Exists(txtScript.Text))
        {
            MessageBox.Show(this, "Converter script was not found. Please set it in the Settings tab.", "Missing Script", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (!File.Exists(txtInput.Text))
        {
            MessageBox.Show(this, "Input image was not found.", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(txtBaseName.Text))
        {
            MessageBox.Show(this, "Output base name is required.", "Missing Base Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (Selected(cmbMode) == "512" && Selected(cmbLayout) == "640x400")
        {
            MessageBox.Show(this, "512-color mode cannot use the 640x400 layout.", "Unsupported Layout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private bool ConfirmOverwriteExistingOutputs()
    {
        var existing = BuildExpectedOutputPaths()
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (existing.Count == 0)
        {
            return true;
        }

        var preview = string.Join(Environment.NewLine, existing.Take(12).Select(Path.GetFileName));
        if (existing.Count > 12)
        {
            preview += Environment.NewLine + $"... and {existing.Count - 12} more";
        }

        using var dialog = new CenteredConfirmDialog(
            "Overwrite Existing Files",
            $"The following output files already exist and will be overwritten:{Environment.NewLine}{Environment.NewLine}{preview}{Environment.NewLine}{Environment.NewLine}Continue?");
        return dialog.ShowDialogCentered(this) == DialogResult.Yes;
    }

    private RubyRunRequest BuildRubyRequest()
    {
        var arguments = BuildRubyArguments().ToList();
        return new RubyRunRequest(
            txtRuby.Text.Trim(),
            Path.GetDirectoryName(txtScript.Text) ?? Environment.CurrentDirectory,
            arguments
        );
    }

    private async Task<RubyRunResult> RunRubyConverterAsync(RubyRunRequest request)
    {
        var psi = new ProcessStartInfo
        {
            FileName = request.RubyPath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in request.Arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ruby process.");
        lock (processLock)
        {
            currentProcess = process;
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync();
        }
        finally
        {
            lock (processLock)
            {
                if (ReferenceEquals(currentProcess, process))
                {
                    currentProcess = null;
                }
            }
        }

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        var command = BuildCommandPreview(psi);
        var log = new StringBuilder();
        log.AppendLine(command);
        if (cancelRequested)
        {
            log.AppendLine();
            log.AppendLine("Canceled by user. Partial output files may remain in the output folder.");
        }

        if (!string.IsNullOrWhiteSpace(stdErr))
        {
            log.AppendLine();
            log.AppendLine("[stderr]");
            log.AppendLine(stdErr.TrimEnd());
        }
        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stdOut))
        {
            log.AppendLine();
            log.AppendLine("[stdout]");
            log.AppendLine(stdOut.TrimEnd());
        }

        return new RubyRunResult(process.ExitCode, stdOut, log.ToString(), cancelRequested);
    }

    private IEnumerable<string> BuildRubyArguments()
    {
        yield return txtScript.Text.Trim();
        yield return "--json";
        yield return "--quiet";
        if (chkPngOnly.Checked)
        {
            yield return "--png-only";
        }

        yield return "-m";
        yield return Selected(cmbMode);

        if (Selected(cmbMode) == "512")
        {
            yield return "-f";
            yield return Selected(cmbFixed);
        }

        if (Selected(cmbMode) == "16" && Selected(cmbRemove) != "no_remove")
        {
            yield return "-r";
            yield return Selected(cmbRemove);
        }

        if (Selected(cmbMode) == "4096" && Selected(cmbSort) != "no_sort")
        {
            yield return "-s";
            yield return Selected(cmbSort);
        }

        yield return "-d";
        yield return Selected(cmbMethod);
        yield return "--strength";
        yield return numStrength.Value.ToString("0.00", CultureInfo.InvariantCulture);
        yield return "--distance";
        yield return Selected(cmbDistance);
        yield return "--layout";
        yield return Selected(cmbLayout);
        yield return "--resize";
        yield return Selected(cmbResize);

        if (!string.IsNullOrWhiteSpace(txtOutputDir.Text))
        {
            yield return "--out-dir";
            yield return txtOutputDir.Text.Trim();
        }

        yield return txtInput.Text.Trim();
        yield return txtBaseName.Text.Trim();
    }

    private IEnumerable<string> BuildExpectedOutputPaths()
    {
        var outputDir = CurrentOutputDirectory();
        var baseStem = OutputBaseStem();
        var mode = Selected(cmbMode);
        var layout = Selected(cmbLayout);

        if (mode == "512")
        {
            foreach (var fixedChannel in FixedChannelsForOutput())
            {
                var stem = $"{baseStem}_fixed{fixedChannel}";
                if (layout == "split320x200")
                {
                    yield return Path.Combine(outputDir, $"{stem}_u.png");
                    yield return Path.Combine(outputDir, $"{stem}_l.png");
                    if (chkPngOnly.Checked)
                    {
                        continue;
                    }

                    yield return Path.Combine(outputDir, $"{stem}_u.brd");
                    yield return Path.Combine(outputDir, $"{stem}_l.brd");
                    yield return Path.Combine(outputDir, $"{stem}_ul.bas.bsd");
                }
                else
                {
                    yield return Path.Combine(outputDir, $"{stem}.png");
                    if (chkPngOnly.Checked)
                    {
                        continue;
                    }

                    yield return Path.Combine(outputDir, $"{stem}.brd");
                    yield return Path.Combine(outputDir, $"{stem}.bas.bsd");
                }
            }

            yield break;
        }

        yield return Path.Combine(outputDir, $"{baseStem}.png");
        if (chkPngOnly.Checked)
        {
            yield break;
        }

        yield return Path.Combine(outputDir, $"{baseStem}.brd");
        yield return Path.Combine(outputDir, $"{baseStem}.bas.bsd");
        if (mode == "4096")
        {
            yield return Path.Combine(outputDir, $"{baseStem}.palette");
        }
    }

    private IEnumerable<string> FixedChannelsForOutput()
    {
        return Selected(cmbFixed) == "all" ? ["R", "G", "B"] : [Selected(cmbFixed)];
    }

    private string CurrentOutputDirectory()
    {
        if (!string.IsNullOrWhiteSpace(txtOutputDir.Text))
        {
            return txtOutputDir.Text.Trim();
        }

        return Path.GetDirectoryName(txtInput.Text.Trim()) ?? Environment.CurrentDirectory;
    }

    private string OutputBaseStem()
    {
        var baseName = txtBaseName.Text.Trim();
        if (string.IsNullOrWhiteSpace(Path.GetExtension(baseName)))
        {
            baseName += ".png";
        }

        return Path.GetFileNameWithoutExtension(baseName);
    }

    private static string BuildCommandPreview(ProcessStartInfo psi)
    {
        var args = psi.ArgumentList.Select(QuoteArg);
        return QuoteArg(psi.FileName) + " " + string.Join(" ", args);
    }

    private static string QuoteArg(string value)
    {
        return value.Contains(' ') || value.Contains('\\') ? $"\"{value}\"" : value;
    }

    private void LoadOutputsFromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("ok", out var okElement) && !okElement.GetBoolean())
        {
            var error = root.TryGetProperty("error", out var errorElement) ? errorElement.GetString() : "Unknown error";
            throw new InvalidOperationException(error);
        }

        if (!root.TryGetProperty("outputs", out var outputs) ||
            !outputs.TryGetProperty("png", out var pngArray))
        {
            throw new InvalidOperationException("JSON result did not include outputs.png.");
        }

        foreach (var item in pngArray.EnumerateArray())
        {
            var path = item.GetString();
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                outputImages.Add(new OutputImageItem(path));
            }
        }

        AddOutputFiles(outputs, "png", "PNG");
        AddOutputFiles(outputs, "brd", "BRD");
        AddOutputFiles(outputs, "bsd", "BSD");
        AddOutputFiles(outputs, "palette", "Palette");

        cmbOutputs.Items.Clear();
        cmbOutputs.Items.AddRange(outputImages.Cast<object>().ToArray());
        cmbOutputs.Enabled = cmbOutputs.Items.Count > 0;
        if (cmbOutputs.Items.Count > 0)
        {
            cmbOutputs.SelectedIndex = 0;
        }
    }

    private void AddOutputFiles(JsonElement outputs, string jsonName, string displayName)
    {
        if (!outputs.TryGetProperty(jsonName, out var array))
        {
            return;
        }

        foreach (var item in array.EnumerateArray())
        {
            var path = item.GetString();
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var listItem = new ListViewItem(displayName);
            listItem.SubItems.Add(Path.GetFileName(path));
            listItem.SubItems.Add(path);
            listItem.Tag = path;
            lstOutputFiles.Items.Add(listItem);
        }
    }

    private static string AppendTimingSummary(string log, string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("timing", out var timing))
            {
                return log;
            }

            var builder = new StringBuilder(log);
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            if (timing.TryGetProperty("total_seconds", out var total))
            {
                builder.AppendLine($"Elapsed: {total.GetDouble():0.0000}s");
            }

            if (timing.TryGetProperty("channels", out var channels))
            {
                foreach (var channel in channels.EnumerateArray())
                {
                    var fixedChannel = channel.GetProperty("fixed_channel").GetString();
                    var channelTotal = channel.GetProperty("total_seconds").GetDouble();
                    builder.AppendLine($"  {fixedChannel}: {channelTotal:0.0000}s");

                    if (channel.TryGetProperty("image_timings", out var images))
                    {
                        foreach (var image in images.EnumerateArray())
                        {
                            var path = Path.GetFileName(image.GetProperty("path").GetString());
                            var dither = image.GetProperty("dither_seconds").GetDouble();
                            var saveBrd = image.GetProperty("save_brd_seconds").GetDouble();
                            var imageTotal = image.GetProperty("total_seconds").GetDouble();
                            builder.AppendLine($"    {path}: total {imageTotal:0.0000}s, dither {dither:0.0000}s, brd {saveBrd:0.0000}s");
                        }
                    }
                }
            }

            return builder.ToString();
        }
        catch
        {
            return log;
        }
    }

    private void LoadOriginalImage(string path)
    {
        currentOriginalPath = path;
        SetPicture(picOriginal, path);
        UpdateOriginalResizePreview();
        UpdatePreviewDisplayAspect();
    }

    private void ClearOriginalImage()
    {
        currentOriginalPath = null;
        SetPicture(picOriginal, null);
        UpdateOriginalResizePreview();
        UpdatePreviewDisplayAspect();
    }

    private void ShowSelectedPreview()
    {
        if (cmbOutputs.SelectedItem is OutputImageItem item)
        {
            currentPreviewPath = item.Path;
            SetPicture(picPreview, item.Path);
            UpdatePreviewDisplayAspect();
        }
    }

    private void ClearPreview()
    {
        outputImages.Clear();
        cmbOutputs.Items.Clear();
        cmbOutputs.Enabled = false;
        lstOutputFiles.Items.Clear();
        btnOpenSelectedOutput.Enabled = false;
        currentPreviewPath = null;
        SetPicture(picPreview, null);
        UpdatePreviewDisplayAspect();
    }

    private void ShowImagePopup(string title, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(currentOriginalPath) &&
            !string.IsNullOrWhiteSpace(currentPreviewPath) &&
            File.Exists(currentOriginalPath) &&
            File.Exists(currentPreviewPath))
        {
            using var originalPreviewImage = CreateOriginalResizePreviewBitmap();
            if (TryGetSplitPreviewPair(currentPreviewPath, out var upperPath, out var lowerPath))
            {
                using var splitViewer = SyncedImageForm.CreateSplit(currentOriginalPath, upperPath, lowerPath, ViewerInitialSize(), originalPreviewImage);
                splitViewer.ShowDialog(this);
                return;
            }

            using var syncViewer = SyncedImageForm.CreatePair(currentOriginalPath, currentPreviewPath, ViewerInitialSize(), originalPreviewImage);
            syncViewer.ShowDialog(this);
            return;
        }

        using var originalOnlyPreviewImage = string.Equals(path, currentOriginalPath, StringComparison.OrdinalIgnoreCase)
            ? CreateOriginalResizePreviewBitmap()
            : null;
        using var viewer = new ZoomImageForm(title, path, ViewerInitialSize(), originalOnlyPreviewImage);
        viewer.ShowDialog(this);
    }

    private Bitmap? CreateOriginalResizePreviewBitmap()
    {
        if (string.IsNullOrWhiteSpace(currentOriginalPath) || !File.Exists(currentOriginalPath))
        {
            return null;
        }

        using var source = Image.FromFile(currentOriginalPath);
        var bitmap = new Bitmap(640, 400);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.Clear(Color.Black);

        switch (Selected(cmbResize))
        {
            case "keep":
                graphics.DrawImage(source, GetFitRectangle(new Rectangle(0, 0, 640, 400), source.Width / (double)source.Height));
                break;
            case "cut":
                graphics.DrawImage(source, new Rectangle(0, 0, 640, 400), GetCenterCropSourceRectangle(source, 640.0 / 400), GraphicsUnit.Pixel);
                break;
            default:
                graphics.DrawImage(source, new Rectangle(0, 0, 640, 400));
                break;
        }

        return bitmap;
    }

    private static Rectangle GetFitRectangle(Rectangle bounds, double aspectRatio)
    {
        var width = bounds.Width;
        var height = (int)Math.Round(width / aspectRatio);
        if (height > bounds.Height)
        {
            height = bounds.Height;
            width = (int)Math.Round(height * aspectRatio);
        }

        return new Rectangle(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            Math.Max(1, width),
            Math.Max(1, height));
    }

    private static Rectangle GetCenterCropSourceRectangle(Image image, double targetAspectRatio)
    {
        var srcAspectRatio = image.Width / (double)image.Height;
        if (srcAspectRatio > targetAspectRatio)
        {
            var width = Math.Max(1, (int)Math.Round(image.Height * targetAspectRatio));
            var x = Math.Max(0, (image.Width - width) / 2);
            return new Rectangle(x, 0, Math.Min(width, image.Width - x), image.Height);
        }

        var height = Math.Max(1, (int)Math.Round(image.Width / targetAspectRatio));
        var y = Math.Max(0, (image.Height - height) / 2);
        return new Rectangle(0, y, image.Width, Math.Min(height, image.Height - y));
    }

    private Size ViewerInitialSize()
    {
        var screen = Screen.FromControl(this).WorkingArea;
        var width = Math.Clamp((int)Math.Round(Width * 0.8), 520, Math.Max(520, screen.Width));
        var height = Math.Clamp((int)Math.Round(Height * 0.8), 420, Math.Max(420, screen.Height));
        return new Size(width, height);
    }

    private void OpenOutputDirectory()
    {
        try
        {
            var dir = CurrentOutputDirectory();
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Failed to open output folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSelectedOutputFile()
    {
        if (lstOutputFiles.SelectedItems.Count == 0 ||
            lstOutputFiles.SelectedItems[0].Tag is not string path ||
            !File.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Failed to open output file", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TryGetSplitPreviewPair(string selectedPath, out string upperPath, out string lowerPath)
    {
        upperPath = string.Empty;
        lowerPath = string.Empty;
        var name = Path.GetFileNameWithoutExtension(selectedPath);
        var dir = Path.GetDirectoryName(selectedPath) ?? string.Empty;
        string baseName;

        if (name.EndsWith("_u", StringComparison.OrdinalIgnoreCase))
        {
            baseName = name[..^2];
        }
        else if (name.EndsWith("_l", StringComparison.OrdinalIgnoreCase))
        {
            baseName = name[..^2];
        }
        else
        {
            return false;
        }

        upperPath = FindOutputImagePath(Path.Combine(dir, $"{baseName}_u.png"));
        lowerPath = FindOutputImagePath(Path.Combine(dir, $"{baseName}_l.png"));
        return File.Exists(upperPath) && File.Exists(lowerPath);
    }

    private string FindOutputImagePath(string expectedPath)
    {
        return outputImages.FirstOrDefault(
            item => string.Equals(item.Path, expectedPath, StringComparison.OrdinalIgnoreCase))?.Path ?? expectedPath;
    }

    private static void SetPicture(PictureBox pictureBox, string? path)
    {
        pictureBox.Image?.Dispose();
        pictureBox.Image = null;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        using var image = Image.FromFile(path);
        pictureBox.Image = new Bitmap(image);
    }

    private void UpdatePreviewDisplayAspect()
    {
        if (!chkPreviewDisplayAspect.Checked || picPreview.Image is null)
        {
            picPreview.DisplayAspectRatio = null;
            return;
        }

        picPreview.DisplayAspectRatio = GetPreviewDisplayAspectRatio(currentPreviewPath);
    }

    private void UpdateOriginalResizePreview()
    {
        picOriginal.BaseResizeMode = picOriginal.Image is null ? null : Selected(cmbResize);
    }

    private double? GetPreviewDisplayAspectRatio(string? previewPath)
    {
        if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(currentOriginalPath) && File.Exists(currentOriginalPath))
        {
            using var original = Image.FromFile(currentOriginalPath);
            if (original.Width <= 0 || original.Height <= 0)
            {
                return null;
            }

            if (TryGetSplitPreviewPair(previewPath, out _, out _))
            {
                return original.Width / Math.Max(1.0, original.Height / 2.0);
            }

            return original.Width / (double)original.Height;
        }

        using var preview = Image.FromFile(previewPath);
        if (preview.Width <= 0 || preview.Height <= 0)
        {
            return null;
        }

        return Selected(cmbLayout) switch
        {
            "640x200" => preview.Width / Math.Max(1.0, preview.Height * 2.0),
            "split320x200" => preview.Width * 2.0 / preview.Height,
            _ => preview.Width / (double)preview.Height
        };
    }

    private static string Selected(ComboBox combo)
    {
        return combo.SelectedItem?.ToString() ?? string.Empty;
    }

    private static void SetCombo(ComboBox combo, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var index = combo.Items.IndexOf(value);
        if (index >= 0)
        {
            combo.SelectedIndex = index;
        }
    }

}
