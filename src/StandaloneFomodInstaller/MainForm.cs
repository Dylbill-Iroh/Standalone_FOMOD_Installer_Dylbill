using FomodInstaller.Interface;
using FomodInstaller.Interface.ui;
using ModInstaller.Lite;
using UiGroup = FomodInstaller.Interface.ui.Group;
using UiOption = FomodInstaller.Interface.ui.Option;

namespace StandaloneFomodInstaller;

public sealed class MainForm : Form
{
    // Folders / files that mark the top of a mod's file tree, used when an archive has no FOMOD installer
    // (same idea as Vortex's stop patterns for Bethesda games).
    private static readonly List<string> StopPatterns = new()
    {
        "(^|/)fonts(/|$)", "(^|/)interface(/|$)", "(^|/)menus(/|$)", "(^|/)meshes(/|$)", "(^|/)music(/|$)",
        "(^|/)scripts(/|$)", "(^|/)shaders(/|$)", "(^|/)sound(/|$)", "(^|/)strings(/|$)", "(^|/)textures(/|$)",
        "(^|/)trees(/|$)", "(^|/)video(/|$)", "(^|/)facegen(/|$)", "(^|/)materials(/|$)", "(^|/)skse(/|$)",
        "(^|/)distantlod(/|$)", "(^|/)lodsettings(/|$)", "(^|/)grass(/|$)", "(^|/)seq(/|$)", "(^|/)mapmarkers(/|$)",
        "(^|/)nemesis_engine(/|$)", "(^|/)source(/|$)", "(^|/)netscriptframework(/|$)", "(^|/)calientetools(/|$)",
        "[^/]*\\.esp$", "[^/]*\\.esm$", "[^/]*\\.esl$", "[^/]*\\.bsa$", "[^/]*\\.ba2$",
    };

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly ToolTip _toolTip = new();

    private PreparedMod _mod;
    private List<Instruction> _instructions = new();
    private List<string> _installFolders = new();   // checked and saved when Start is clicked

    // Set by the FOMOD engine in StartDialog
    private Action<int, int, int[]> _select;
    private Action<bool, int> _cont;
    private Action _cancel;
    private bool _dialogActive;
    private bool _closing;

    private InstallerStep[] _steps = Array.Empty<InstallerStep>();
    private int _currentStep = -1;
    private int _renderedStep = -1;
    private readonly List<List<ButtonBase>> _optionControls = new();
    private bool _updatingControls;
    private (int group, int option) _shownOption = (-1, -1);

    // Header
    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
    private readonly Label _titleLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 13f, FontStyle.Bold), Margin = new Padding(10, 8, 10, 4) };
    private readonly PictureBox _headerImage = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Visible = false, Margin = Padding.Empty };

    // Page 1: choose the mod
    private readonly Panel _startPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
    private readonly TextBox _modPathBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    private readonly TextBox _dataFolderBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    private readonly TableLayoutPanel _installFoldersTable = new() { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = Padding.Empty };
    private readonly List<TextBox> _installFolderBoxes = new();
    private readonly Button _startButton = new() { Text = "Start", AutoSize = true };
    private readonly Label _startStatus = new() { AutoSize = true, ForeColor = SystemColors.GrayText };

    // Detect page (opened from page 1)
    private readonly Panel _detectPanel = new() { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(12) };
    private readonly FlowLayoutPanel _detectContent = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly Button _detectBackButton = new() { Text = "< Back", AutoSize = true };
    private int _detectRun;

    // Page 2: the installer's steps
    private readonly Panel _stepPanel = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _stepTitle = new() { AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Margin = new Padding(10, 6, 10, 4) };
    private readonly FlowLayoutPanel _groupsPanel = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(6) };
    private readonly PictureBox _optionImage = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = SystemColors.ControlDark };
    private readonly TextBox _optionDescription = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Window };
    private readonly Button _backButton = new() { Text = "< Back", AutoSize = true };
    private readonly Button _nextButton = new() { Text = "Next >", AutoSize = true };
    private readonly Button _cancelButton = new() { Text = "Cancel", AutoSize = true };

    // Page 3: review and install
    private readonly Panel _finishPanel = new() { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(12) };
    private readonly Label _finishSummary = new() { AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 6) };
    private readonly ListBox _filesList = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly TextBox _notesBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 70, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Visible = false, Margin = new Padding(0, 6, 0, 0) };
    private readonly Label _installTargetsLabel = new() { AutoSize = true, Margin = new Padding(0, 6, 0, 4) };
    private readonly CheckBox _overwriteCheck = new() { Text = "Overwrite existing files", AutoSize = true };
    private readonly Button _installButton = new() { Text = "Install", AutoSize = true };
    private readonly Button _restartButton = new() { Text = "Install another mod", AutoSize = true };
    private readonly Label _finishStatus = new() { AutoSize = true };

    public MainForm(string initialModPath)
    {
        // Sizes below are written for 96 DPI; WinForms scales them to the monitor's DPI.
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = "Standalone FOMOD Installer";
        ClientSize = new Size(1000, 680);
        MinimumSize = new Size(700, 480);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5f);
        AllowDrop = true;

        BuildStartPanel();
        BuildStepPanel();
        BuildFinishPanel();
        BuildDetectPanel();

        // Title, banner image, then the current page below them
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var pageHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        pageHost.Controls.Add(_startPanel);
        pageHost.Controls.Add(_stepPanel);
        pageHost.Controls.Add(_finishPanel);
        pageHost.Controls.Add(_detectPanel);
        _root.Controls.Add(_titleLabel, 0, 0);
        _root.Controls.Add(_headerImage, 0, 1);
        _root.Controls.Add(pageHost, 0, 2);
        Controls.Add(_root);

        _titleLabel.Text = "Choose a mod to install";
        _modPathBox.Text = !string.IsNullOrWhiteSpace(initialModPath) ? initialModPath : _settings.LastModPath;
        _dataFolderBox.Text = _settings.LastGameDataFolder;
        SetInstallFolderRows(_settings.GetInstallFolders());
        _overwriteCheck.Checked = _settings.OverwriteExistingFiles;

        DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true && _startPanel.Visible)
            {
                e.Effect = DragDropEffects.Copy;
            }
        };
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            {
                _modPathBox.Text = paths[0];
            }
        };

        FormClosing += (_, _) =>
        {
            _closing = true;
            if (_dialogActive)
            {
                _dialogActive = false;
                _cancel?.Invoke();
            }
            ClearOptionImage();
            _mod?.Dispose();
        };

        ResumeLayout(false);
        PerformLayout();
    }

    // Posts to the UI thread. Always async, so engine callbacks never rebuild controls in the middle of an event handler.
    public void RunOnUiThread(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }
        BeginInvoke(action);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Layout

    private static Label MakeLabel(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(900, 0), Margin = new Padding(0, 10, 0, 2) };

    private void BuildStartPanel()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var browseArchive = new Button { Text = "Archive...", AutoSize = true };
        var browseModFolder = new Button { Text = "Folder...", AutoSize = true };
        var browseData = new Button { Text = "Browse...", AutoSize = true };

        layout.Controls.Add(MakeLabel("Mod archive (.zip, .7z, .rar) or extracted mod folder. You can also drag a file onto this window."), 0, 0);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 0), 3);
        layout.Controls.Add(_modPathBox, 0, 1);
        layout.Controls.Add(browseArchive, 1, 1);
        layout.Controls.Add(browseModFolder, 2, 1);

        layout.Controls.Add(MakeLabel("Game Data folder (optional). Lets the installer see which plugins and files you already have, for options that depend on them."), 0, 2);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 2), 3);
        var detectData = new Button { Text = "Detect Game Data Folder", AutoSize = true };
        layout.Controls.Add(_dataFolderBox, 0, 3);
        layout.Controls.Add(browseData, 1, 3);
        layout.Controls.Add(detectData, 2, 3);

        layout.Controls.Add(MakeLabel("Install to (for example your game's Data folder, or a mod folder). Click \"+ Add folder\" to copy the files to more than one folder."), 0, 4);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 4), 3);
        _installFoldersTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _installFoldersTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _installFoldersTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(_installFoldersTable, 0, 5);
        layout.SetColumnSpan(_installFoldersTable, 3);

        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 16, 0, 0) };
        buttons.Controls.Add(_startButton);
        buttons.Controls.Add(_startStatus);
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 3);

        _modPathBox.Dock = DockStyle.Fill;
        _dataFolderBox.Dock = DockStyle.Fill;

        browseArchive.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Choose a mod archive",
                Filter = "Mod archives (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar|All files (*.*)|*.*",
            };
            if (File.Exists(_modPathBox.Text))
            {
                dlg.InitialDirectory = Path.GetDirectoryName(_modPathBox.Text);
            }
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _modPathBox.Text = dlg.FileName;
            }
        };
        browseModFolder.Click += (_, _) => BrowseFolder(_modPathBox, "Choose an extracted mod folder");
        browseData.Click += (_, _) => BrowseFolder(_dataFolderBox, "Choose the game's Data folder");
        detectData.Click += async (_, _) => await DetectGameDataFolderAsync();
        _startButton.Click += async (_, _) => await StartAsync();

        _startPanel.Controls.Add(layout);
    }

    // One row per install folder: [path][Browse...][+ Add folder] on the first row, [path][Browse...][Remove] on the others.
    // Rows are rebuilt from the list of paths whenever one is added or removed.
    private void SetInstallFolderRows(List<string> paths)
    {
        if (paths.Count == 0)
        {
            paths.Add("");
        }

        _installFoldersTable.SuspendLayout();
        var old = _installFoldersTable.Controls.Cast<Control>().ToList();
        _installFoldersTable.Controls.Clear();
        foreach (var control in old)
        {
            control.Dispose();
        }
        _installFolderBoxes.Clear();
        _installFoldersTable.RowStyles.Clear();
        _installFoldersTable.RowCount = paths.Count;

        for (int i = 0; i < paths.Count; i++)
        {
            int row = i;
            var box = new TextBox { Dock = DockStyle.Fill, Text = paths[i] };
            var browse = new Button { Text = "Browse...", AutoSize = true };
            browse.Click += (_, _) => BrowseFolder(box, "Choose where to install the mod's files");

            Button extra;
            if (row == 0)
            {
                extra = new Button { Text = "+ Add folder", AutoSize = true };
                extra.Click += (_, _) => AddInstallFolder();
            }
            else
            {
                extra = new Button { Text = "Remove", AutoSize = true };
                extra.Click += (_, _) => RemoveInstallFolder(row);
            }

            _installFoldersTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _installFoldersTable.Controls.Add(box, 0, row);
            _installFoldersTable.Controls.Add(browse, 1, row);
            _installFoldersTable.Controls.Add(extra, 2, row);
            _installFolderBoxes.Add(box);
        }

        _installFoldersTable.ResumeLayout(true);
    }

    private List<string> GetInstallFolderTexts() => _installFolderBoxes.Select(b => b.Text).ToList();

    private void AddInstallFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose another folder to install the mod's files to", UseDescriptionForTitle = true };
        string last = _installFolderBoxes.Select(b => b.Text.Trim().Trim('"')).LastOrDefault(Directory.Exists);
        if (last != null)
        {
            dlg.InitialDirectory = last;
        }
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var paths = GetInstallFolderTexts();
        if (paths.Count == 1 && paths[0].Trim() == "")
        {
            paths[0] = dlg.SelectedPath;   // fill the empty first row instead of adding a second one
        }
        else
        {
            paths.Add(dlg.SelectedPath);
        }
        // Rebuild after this click finishes: the rebuild disposes the button that was clicked.
        RunOnUiThread(() => SetInstallFolderRows(paths));
    }

    private void RemoveInstallFolder(int row)
    {
        var paths = GetInstallFolderTexts();
        if (row > 0 && row < paths.Count)
        {
            paths.RemoveAt(row);
            RunOnUiThread(() => SetInstallFolderRows(paths));   // after the click, as above
        }
    }

    // Checks the install folders. Returns the full paths without duplicates, or null if there's a problem (already shown).
    private List<string> ValidateInstallFolders()
    {
        var folders = new List<string>();
        foreach (string text in GetInstallFolderTexts())
        {
            string folder = text.Trim().Trim('"');
            if (folder == "")
            {
                continue;
            }
            if (!Path.IsPathFullyQualified(folder))
            {
                MessageBox.Show(this, $"This install folder isn't a full path (like C:\\Games\\Skyrim\\Data):\r\n{folder}", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            if (!folders.Contains(full, StringComparer.OrdinalIgnoreCase))
            {
                folders.Add(full);
            }
        }

        if (folders.Count == 0)
        {
            MessageBox.Show(this, "Choose at least one folder to install to.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        var missing = folders.Where(f => !Directory.Exists(f)).ToList();
        if (missing.Count > 0)
        {
            string list = string.Join("\r\n", missing);
            var answer = MessageBox.Show(this,
                (missing.Count == 1 ? "This folder doesn't exist:" : "These folders don't exist:") + $"\r\n{list}\r\n\r\nCreate {(missing.Count == 1 ? "it" : "them")} when installing?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return null;
            }
        }
        return folders;
    }

    private void BuildStepPanel()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, Margin = Padding.Empty };

        split.Panel1.Controls.Add(_groupsPanel);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        right.Controls.Add(_optionImage, 0, 0);
        right.Controls.Add(_optionDescription, 0, 1);
        split.Panel2.Controls.Add(right);

        var nav = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, WrapContents = false, Margin = new Padding(6) };
        nav.Controls.Add(_backButton);
        nav.Controls.Add(_nextButton);
        nav.Controls.Add(_cancelButton);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(_stepTitle, 0, 0);
        table.Controls.Add(split, 0, 1);
        table.Controls.Add(nav, 0, 2);
        _stepPanel.Controls.Add(table);

        _stepPanel.VisibleChanged += (_, _) =>
        {
            if (_stepPanel.Visible && split.Width > 400)
            {
                try
                {
                    split.SplitterDistance = split.Width * 55 / 100;
                }
                catch (ArgumentException)
                {
                    // window too small for the requested split; keep the default
                }
            }
        };
        _groupsPanel.Resize += (_, _) => FitGroupWidths();

        _backButton.Click += (_, _) => Navigate(false);
        _nextButton.Click += (_, _) => Navigate(true);
        _cancelButton.Click += (_, _) =>
        {
            if (_dialogActive)
            {
                _dialogActive = false;
                _cancel?.Invoke();   // StartAsync catches the cancellation and returns to the start page
            }
        };
    }

    private void BuildFinishPanel()
    {
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, Padding = new Padding(0, 8, 0, 0), Margin = Padding.Empty };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        bottom.Controls.Add(_installTargetsLabel, 0, 0);
        bottom.Controls.Add(_overwriteCheck, 0, 1);

        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
        buttons.Controls.Add(_installButton);
        buttons.Controls.Add(_restartButton);
        buttons.Controls.Add(_finishStatus);
        bottom.Controls.Add(buttons, 0, 2);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(_finishSummary, 0, 0);
        table.Controls.Add(_filesList, 0, 1);
        table.Controls.Add(_notesBox, 0, 2);
        table.Controls.Add(bottom, 0, 3);
        _finishPanel.Controls.Add(table);

        _installButton.Click += async (_, _) => await InstallAsync();
        _restartButton.Click += (_, _) => ResetToStart();
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Detect page: which game the FOMOD is for, and which folder its file checks are relative to

    private void BuildDetectPanel()
    {
        var nav = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 0) };
        nav.Controls.Add(_detectBackButton);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(_detectContent, 0, 0);
        table.Controls.Add(nav, 0, 1);
        _detectPanel.Controls.Add(table);

        _detectBackButton.Click += (_, _) => CloseDetectPage();
    }

    private void CloseDetectPage()
    {
        _detectRun++;   // ignore a detection that's still running
        ClearDetectContent();
        _titleLabel.Text = "Choose a mod to install";
        ShowPage(_startPanel);
    }

    private void ClearDetectContent()
    {
        var old = _detectContent.Controls.Cast<Control>().ToList();
        _detectContent.Controls.Clear();
        foreach (var control in old)
        {
            control.Dispose();
        }
    }

    private int DetectTextWidth => Math.Max(LogicalToDeviceUnits(300), _detectContent.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - LogicalToDeviceUnits(16));

    private void AddDetectHeading(string text) => _detectContent.Controls.Add(new Label
    {
        Text = text, AutoSize = true, MaximumSize = new Size(DetectTextWidth, 0),
        Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold), Margin = new Padding(0, 12, 0, 4),
    });

    private void AddDetectText(string text) => _detectContent.Controls.Add(new Label
    {
        Text = text, AutoSize = true, MaximumSize = new Size(DetectTextWidth, 0), Margin = new Padding(0, 2, 0, 2),
    });

    private void AddDetectInstall(DetectedInstall install, int checkedFiles)
    {
        var row = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 4, 0, 4) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        string text = $"{install.Game.Name}\r\n{install.DataFolder}";
        if (checkedFiles > 0)
        {
            text += $"\r\n{install.CheckedFilesFound} of {checkedFiles} checked files are there";
        }
        var label = new Label { Text = text, AutoSize = true, MaximumSize = new Size(Math.Max(LogicalToDeviceUnits(200), DetectTextWidth - LogicalToDeviceUnits(150)), 0), Margin = new Padding(0, 0, 12, 0) };
        var use = new Button { Text = "Use this folder", AutoSize = true, Anchor = AnchorStyles.Left };
        use.Click += (_, _) =>
        {
            _dataFolderBox.Text = install.DataFolder;
            // Nowhere to install to yet: the data folder is almost always the right place
            if (_installFolderBoxes.All(b => b.Text.Trim() == ""))
            {
                _installFolderBoxes[0].Text = install.DataFolder;
            }
            RunOnUiThread(CloseDetectPage);   // after this click: closing disposes this button
        };
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(use, 1, 0);
        _detectContent.Controls.Add(row);
    }

    private async Task DetectGameDataFolderAsync()
    {
        string modPath = _modPathBox.Text.Trim().Trim('"');
        if (modPath == "" || (!File.Exists(modPath) && !Directory.Exists(modPath)))
        {
            MessageBox.Show(this, "Choose a mod archive or folder first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int run = ++_detectRun;
        _titleLabel.Text = "Detect game data folder";
        ClearDetectContent();
        ShowPage(_detectPanel);
        AddDetectText("Reading the mod's installer...");

        var result = await Task.Run(() => GameDetector.Detect(modPath));
        if (_closing || run != _detectRun || !_detectPanel.Visible)
        {
            return;
        }

        _detectContent.SuspendLayout();
        ClearDetectContent();
        ShowDetectResult(result);
        _detectContent.ResumeLayout(true);
    }

    private void ShowDetectResult(GameDetectionResult result)
    {
        string gameNames = string.Join(" or ", result.Families.Select(f => f.Name));

        if (GameTable.LoadProblem != null)
        {
            var warning = new Label { Text = GameTable.LoadProblem, AutoSize = true, MaximumSize = new Size(DetectTextWidth, 0), ForeColor = Color.DarkRed, Margin = new Padding(0, 2, 0, 6) };
            _detectContent.Controls.Add(warning);
        }

        if (result.Error != null)
        {
            AddDetectHeading("Couldn't read the mod");
            AddDetectText(result.Error);
            return;
        }

        if (!result.HasFomod || !result.NeedsDataFolder)
        {
            AddDetectHeading("No game data folder needed");
            AddDetectText(!result.HasFomod
                ? "This mod has no FOMOD installer (fomod\\ModuleConfig.xml), so nothing depends on your game's files."
                : "This installer doesn't check for any of your game's files or versions, so you can leave the Game Data folder empty.");
            if (gameNames != "")
            {
                AddDetectText($"It looks like a mod for {gameNames}.");
            }
            return;
        }

        // Game
        AddDetectHeading(gameNames != "" ? $"Detected game: {gameNames}" : "Couldn't tell which game this installer is for");
        if (result.Reasons.Count > 0)
        {
            AddDetectText("Because the installer " + string.Join(", ", result.Reasons) + ".");
        }

        // Which folder
        var gameInfos = GameTable.Games.Where(g => result.Families.Contains(g.Family)).ToList();
        var dataDescriptions = gameInfos.Select(g => g.DataFolderDescription).Distinct().ToList();
        AddDetectHeading("Game data folder");
        if (dataDescriptions.Count == 1)
        {
            AddDetectText($"For {gameNames}, the game data folder is {dataDescriptions[0]}. The installer's file checks are relative to it.");
        }
        else
        {
            AddDetectText("The game data folder is the folder the files below are in. For most Bethesda games that's the \"Data\" folder inside the game's main folder.");
        }

        // What it checks
        AddDetectHeading("What the installer checks");
        if (result.CheckedFiles.Count > 0)
        {
            const int maxShown = 25;
            var shown = result.CheckedFiles.Take(maxShown).Select(f => "    " + f.Replace('/', '\\'));
            string more = result.CheckedFiles.Count > maxShown ? $"\r\n    ...and {result.CheckedFiles.Count - maxShown} more" : "";
            AddDetectText($"{result.CheckedFiles.Count} file(s):\r\n" + string.Join("\r\n", shown) + more);
        }
        if (result.VersionChecks.Count > 0)
        {
            AddDetectText("Versions: " + string.Join(", ", result.VersionChecks) + " (read from the game's main folder, next to the data folder).");
        }

        // Where it is
        AddDetectHeading("Found on this PC");
        if (result.Installs.Count > 0)
        {
            foreach (var install in result.Installs.OrderByDescending(i => i.CheckedFilesFound))
            {
                AddDetectInstall(install, result.CheckedFiles.Count);
            }
        }
        else if (gameNames != "")
        {
            string where = gameInfos.Count > 0 && gameInfos[0].SteamFolders.Length > 0
                ? $" For a Steam install it's usually ...\\steamapps\\common\\{gameInfos[0].SteamFolders[0]}" + (gameInfos[0].DataSubfolder != "" ? $"\\{gameInfos[0].DataSubfolder}" : "") + "."
                : "";
            AddDetectText($"{gameNames} wasn't found in Steam, GOG, Epic or the registry. Choose its data folder with Browse...{where}");
        }
        else
        {
            AddDetectText("None of the games this app knows has these files. Choose the folder that contains them with Browse...");
        }
    }

    private void BrowseFolder(TextBox target, string description)
    {
        using var dlg = new FolderBrowserDialog { Description = description, UseDescriptionForTitle = true };
        if (Directory.Exists(target.Text))
        {
            dlg.InitialDirectory = target.Text;
        }
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dlg.SelectedPath;
        }
    }

    private void ShowPage(Panel page)
    {
        _startPanel.Visible = page == _startPanel;
        _stepPanel.Visible = page == _stepPanel;
        _finishPanel.Visible = page == _finishPanel;
        _detectPanel.Visible = page == _detectPanel;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Page 1 -> run the installer

    private async Task StartAsync()
    {
        string modPath = _modPathBox.Text.Trim().Trim('"');
        if (modPath == "" || (!File.Exists(modPath) && !Directory.Exists(modPath)))
        {
            MessageBox.Show(this, "Choose a mod archive or folder first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string dataFolder = _dataFolderBox.Text.Trim().Trim('"');
        if (dataFolder != "" && !Directory.Exists(dataFolder))
        {
            MessageBox.Show(this, "The game Data folder doesn't exist. Clear it or choose an existing folder.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var installFolders = ValidateInstallFolders();
        if (installFolders == null)
        {
            return;
        }
        _installFolders = installFolders;
        SetInstallFolderRows(new List<string>(installFolders));   // show them cleaned up (full paths, no blanks or duplicates)

        _settings.LastModPath = modPath;
        _settings.LastGameDataFolder = dataFolder;
        _settings.InstallFolders = new List<string>(installFolders);
        _settings.Save();

        _startButton.Enabled = false;
        _startStatus.Text = File.Exists(modPath) ? "Extracting..." : "Reading folder...";

        try
        {
            _mod?.Dispose();
            _mod = null;
            _mod = await Task.Run(() => PreparedMod.FromPath(modPath));
        }
        catch (Exception ex)
        {
            if (_closing)
            {
                return;
            }
            _startButton.Enabled = true;
            _startStatus.Text = "";
            MessageBox.Show(this, $"Couldn't open the mod:\r\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (_closing)
        {
            _mod.Dispose();
            _mod = null;
            return;
        }

        if (!_mod.HasFomod)
        {
            var answer = MessageBox.Show(this,
                "This mod has no FOMOD installer (fomod\\ModuleConfig.xml).\r\n\r\nInstall all of its files as they are?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                ResetToStart();
                return;
            }
        }

        _startStatus.Text = "Loading installer...";
        _titleLabel.Text = Path.GetFileNameWithoutExtension(modPath);

        var game = new GameState(dataFolder, _settings.PluginsTxtPath);
        var core = new StandaloneCoreDelegates(game, this);
        var mod = _mod;

        try
        {
            var result = await Task.Run(() => Installer.Install(
                mod.FileList, StopPatterns, null, mod.RootFolder, null, false, false, _ => { }, core));

            if (_closing)
            {
                return;
            }
            _dialogActive = false;
            _instructions = result?.Instructions ?? new List<Instruction>();
            ShowFinishPage();
        }
        catch (OperationCanceledException)
        {
            if (!_closing)
            {
                ResetToStart();
            }
        }
        catch (Exception ex)
        {
            if (_closing)
            {
                return;
            }
            MessageBox.Show(this, $"The installer failed:\r\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            ResetToStart();
        }
    }

    private void ResetToStart()
    {
        _dialogActive = false;
        _select = null;
        _cont = null;
        _cancel = null;
        _steps = Array.Empty<InstallerStep>();
        _currentStep = -1;
        _renderedStep = -1;
        ClearGroups();
        ClearOptionImage();
        _optionDescription.Text = "";
        SetHeaderImage(null, 0);
        _mod?.Dispose();
        _mod = null;
        _instructions = new List<Instruction>();

        _titleLabel.Text = "Choose a mod to install";
        _startButton.Enabled = true;
        _startStatus.Text = "";
        ShowPage(_startPanel);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Engine callbacks (already on the UI thread)

    public void OnStartDialog(string moduleName, HeaderImage image, Action<int, int, int[]> select, Action<bool, int> cont, Action cancel)
    {
        _select = select;
        _cont = cont;
        _cancel = cancel;
        _dialogActive = true;

        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            _titleLabel.Text = moduleName;
        }
        SetHeaderImage(image?.path, image?.height ?? 0);
        ShowPage(_stepPanel);
    }

    public void OnEndDialog()
    {
        _dialogActive = false;
    }

    public void OnUpdateState(InstallerStep[] steps, int currentStep)
    {
        if (!_dialogActive || steps == null || currentStep < 0 || currentStep >= steps.Length)
        {
            return;
        }

        _steps = steps;
        _currentStep = currentStep;
        var step = steps[currentStep];
        var groups = step.optionalFileGroups?.group ?? Array.Empty<UiGroup>();

        if (_renderedStep == currentStep && SameShape(groups))
        {
            UpdateControls(groups);   // same page: keep scroll position and focus
        }
        else
        {
            RenderStep(step, groups);
        }

        UpdateNavButtons();
        ShowPage(_stepPanel);
        FitGroupWidths();
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Page 2: steps

    private void RenderStep(InstallerStep step, UiGroup[] groups)
    {
        _stepTitle.Text = step.name;
        _renderedStep = _currentStep;
        _shownOption = (-1, -1);

        _updatingControls = true;
        _groupsPanel.SuspendLayout();
        ClearGroups();

        foreach (var group in groups)
        {
            _groupsPanel.Controls.Add(BuildGroup(group));
        }

        _groupsPanel.ResumeLayout(true);
        _updatingControls = false;
        FitGroupWidths();
        _groupsPanel.AutoScrollPosition = new Point(0, 0);

        // Keep focus off the option list: a radio button that gets focus is checked automatically,
        // which would silently change the installer's default choice.
        ActiveControl = _nextButton;

        // Show the first selected option's description, like other installers do
        for (int g = 0; g < groups.Length; g++)
        {
            var options = groups[g].options ?? Array.Empty<UiOption>();
            int o = Array.FindIndex(options, opt => opt.selected);
            if (o >= 0)
            {
                ShowOption(g, o);
                return;
            }
        }
        if (groups.Length > 0 && (groups[0].options?.Length ?? 0) > 0)
        {
            ShowOption(0, 0);
        }
        else
        {
            ClearOptionImage();
            _optionDescription.Text = "";
        }
    }

    private void ClearGroups()
    {
        var old = _groupsPanel.Controls.Cast<Control>().ToList();
        _groupsPanel.Controls.Clear();
        foreach (var control in old)
        {
            control.Dispose();
        }
        _optionControls.Clear();
    }

    private GroupBox BuildGroup(UiGroup group)
    {
        var box = new GroupBox { Text = group.name + GroupRuleText(group.type), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 4, 8, 6), Margin = new Padding(3, 3, 3, 8) };
        var inner = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        box.Controls.Add(inner);

        int groupIndex = _optionControls.Count;
        var controls = new List<ButtonBase>();
        bool useRadio = group.type == "SelectExactlyOne";
        var options = group.options ?? Array.Empty<UiOption>();

        for (int i = 0; i < options.Length; i++)
        {
            int optionIndex = i;
            ButtonBase button = useRadio ? new RadioButton() : new CheckBox();
            button.AutoSize = true;
            button.Margin = new Padding(3, 2, 3, 2);
            ApplyOptionState(button, options[i], group.type);

            button.MouseEnter += (_, _) => ShowOption(groupIndex, optionIndex);
            button.GotFocus += (_, _) => ShowOption(groupIndex, optionIndex);
            if (button is RadioButton radio)
            {
                radio.CheckAlign = ContentAlignment.TopLeft;
                radio.TextAlign = ContentAlignment.TopLeft;
                radio.CheckedChanged += (_, _) => OnOptionToggled(groupIndex, optionIndex, radio.Checked);
            }
            else if (button is CheckBox check)
            {
                check.CheckAlign = ContentAlignment.TopLeft;
                check.TextAlign = ContentAlignment.TopLeft;
                check.CheckedChanged += (_, _) => OnOptionToggled(groupIndex, optionIndex, check.Checked);
            }

            inner.Controls.Add(button);
            controls.Add(button);
        }

        if (options.Length == 0)
        {
            inner.Controls.Add(new Label { Text = "(no options)", AutoSize = true, ForeColor = SystemColors.GrayText });
        }

        _optionControls.Add(controls);
        return box;
    }

    // The rule the mod author set for the group, shown after its name
    private static string GroupRuleText(string groupType) => groupType switch
    {
        "SelectExactlyOne" => "   (choose one)",
        "SelectAtMostOne" => "   (choose one, or none)",
        "SelectAtLeastOne" => "   (choose at least one)",
        "SelectAll" => "   (all installed)",
        _ => "",
    };

    // Sizes every group to the panel width and wraps long option names onto more lines,
    // so nothing is cut off and there's no horizontal scroll bar.
    private void FitGroupWidths()
    {
        if (_groupsPanel.ClientSize.Width <= 0)
        {
            return;
        }

        int width = Math.Max(LogicalToDeviceUnits(200),
            _groupsPanel.ClientSize.Width - _groupsPanel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - LogicalToDeviceUnits(8));
        int optionWidth = width - LogicalToDeviceUnits(24);
        int glyphWidth = LogicalToDeviceUnits(22);
        int minHeight = LogicalToDeviceUnits(24);

        _groupsPanel.SuspendLayout();
        foreach (Control control in _groupsPanel.Controls)
        {
            control.MinimumSize = new Size(width, 0);
            control.MaximumSize = new Size(width, 0);
        }
        foreach (var controls in _optionControls)
        {
            foreach (var button in controls)
            {
                var text = TextRenderer.MeasureText(button.Text, button.Font,
                    new Size(Math.Max(20, optionWidth - glyphWidth), int.MaxValue), TextFormatFlags.WordBreak);
                button.AutoSize = false;
                button.Size = new Size(optionWidth, Math.Max(minHeight, text.Height + LogicalToDeviceUnits(6)));
            }
        }
        _groupsPanel.ResumeLayout(true);
    }

    private static string OptionText(UiOption option) => option.type switch
    {
        "Recommended" => $"{option.name}   (recommended)",
        "Required" => $"{option.name}   (required)",
        "NotUsable" => $"{option.name}   (not available)",
        _ => option.name,
    };

    private void ApplyOptionState(ButtonBase button, UiOption option, string groupType)
    {
        button.Text = OptionText(option);
        // Locked options stay enabled (greyed text) so hovering still shows their description and tooltip;
        // clicks on them are undone in OnOptionToggled.
        button.ForeColor = IsLocked(option, groupType) ? SystemColors.GrayText : SystemColors.ControlText;

        if (button is RadioButton radio)
        {
            radio.Checked = option.selected;
        }
        else if (button is CheckBox check)
        {
            check.Checked = option.selected;
        }

        _toolTip.SetToolTip(button, option.type == "NotUsable" && !string.IsNullOrWhiteSpace(option.conditionMsg) ? option.conditionMsg : null);
    }

    private static bool IsLocked(UiOption option, string groupType) =>
        option.type == "Required" || option.type == "NotUsable" || groupType == "SelectAll";

    private bool SameShape(UiGroup[] groups)
    {
        if (groups.Length != _optionControls.Count)
        {
            return false;
        }
        for (int g = 0; g < groups.Length; g++)
        {
            if ((groups[g].options?.Length ?? 0) != _optionControls[g].Count)
            {
                return false;
            }
        }
        return true;
    }

    private void UpdateControls(UiGroup[] groups)
    {
        _updatingControls = true;
        for (int g = 0; g < groups.Length; g++)
        {
            var options = groups[g].options ?? Array.Empty<UiOption>();
            for (int o = 0; o < options.Length; o++)
            {
                ApplyOptionState(_optionControls[g][o], options[o], groups[g].type);
            }
        }
        _updatingControls = false;

        if (_shownOption.group >= 0)
        {
            ShowOption(_shownOption.group, _shownOption.option);
        }
    }

    private static bool IsChecked(ButtonBase button) => button switch
    {
        RadioButton radio => radio.Checked,
        CheckBox check => check.Checked,
        _ => false,
    };

    private void OnOptionToggled(int groupIndex, int optionIndex, bool isChecked)
    {
        if (_updatingControls || _select == null || !_dialogActive)
        {
            return;
        }

        var groups = _steps[_currentStep].optionalFileGroups?.group;
        if (groups == null || groupIndex >= groups.Length)
        {
            return;
        }

        var controls = _optionControls[groupIndex];
        var groupOptions = groups[groupIndex].options ?? Array.Empty<UiOption>();

        // Required / not available / select-all options can't be changed: put the controls back as they were.
        if (optionIndex < groupOptions.Length && IsLocked(groupOptions[optionIndex], groups[groupIndex].type))
        {
            UpdateControls(groups);
            ShowOption(groupIndex, optionIndex);
            return;
        }

        // A radio button change fires twice (old one unchecked, new one checked); only send the checked one.
        if (controls[optionIndex] is RadioButton && !isChecked)
        {
            return;
        }

        // "Select at most one" uses check boxes so nothing can be selected; uncheck the others by hand.
        if (groups[groupIndex].type == "SelectAtMostOne" && isChecked)
        {
            _updatingControls = true;
            for (int i = 0; i < controls.Count; i++)
            {
                if (i != optionIndex && controls[i] is CheckBox other && i < groupOptions.Length && !IsLocked(groupOptions[i], groups[groupIndex].type))
                {
                    other.Checked = false;
                }
            }
            _updatingControls = false;
        }

        int[] selected = controls.Select((control, index) => (control, index))
            .Where(t => IsChecked(t.control))
            .Select(t => t.index)
            .ToArray();

        ShowOption(groupIndex, optionIndex);
        _select(_currentStep, groupIndex, selected);   // the engine answers with UpdateState
    }

    private void ShowOption(int groupIndex, int optionIndex)
    {
        if (_currentStep < 0 || _currentStep >= _steps.Length)
        {
            return;
        }
        var groups = _steps[_currentStep].optionalFileGroups?.group;
        if (groups == null || groupIndex < 0 || groupIndex >= groups.Length)
        {
            return;
        }
        var options = groups[groupIndex].options;
        if (options == null || optionIndex < 0 || optionIndex >= options.Length)
        {
            return;
        }

        var option = options[optionIndex];
        bool sameOption = _shownOption == (groupIndex, optionIndex);
        _shownOption = (groupIndex, optionIndex);

        string description = option.description ?? "";
        if (option.type == "NotUsable" && !string.IsNullOrWhiteSpace(option.conditionMsg))
        {
            description = $"Not available: {option.conditionMsg}\n\n{description}";
        }
        _optionDescription.Text = description.Replace("\r\n", "\n").Replace("\n", "\r\n").Trim();

        if (!sameOption || _optionImage.Image == null)
        {
            SetOptionImage(option.image);
        }
    }

    private void UpdateNavButtons()
    {
        bool anyBefore = _steps.Take(_currentStep).Any(s => s.visible);
        bool anyAfter = _steps.Skip(_currentStep + 1).Any(s => s.visible);
        _backButton.Enabled = anyBefore;
        _nextButton.Text = anyAfter ? "Next >" : "Finish";
    }

    private void Navigate(bool forward)
    {
        if (!_dialogActive || _cont == null || _currentStep < 0)
        {
            return;
        }

        if (forward)
        {
            string problem = ValidateStep();
            if (problem != null)
            {
                MessageBox.Show(this, problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        _cont(forward, _currentStep);
    }

    // The engine doesn't enforce group rules, so check them before moving on.
    private string ValidateStep()
    {
        var groups = _steps[_currentStep].optionalFileGroups?.group ?? Array.Empty<UiGroup>();
        foreach (var group in groups)
        {
            var options = group.options ?? Array.Empty<UiOption>();
            int selected = options.Count(o => o.selected);
            bool anyChoosable = options.Any(o => o.type != "NotUsable");

            if (group.type == "SelectExactlyOne" && anyChoosable && selected != 1)
            {
                return $"Choose one option in \"{group.name}\".";
            }
            if (group.type == "SelectAtLeastOne" && anyChoosable && selected < 1)
            {
                return $"Choose at least one option in \"{group.name}\".";
            }
        }
        return null;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Images

    private string ModPath(string relative) =>
        _mod == null || string.IsNullOrWhiteSpace(relative) ? null : Path.Combine(_mod.RootFolder, relative);

    // Loads a copy so the file isn't kept open (the temp folder is deleted later).
    private static Image LoadImage(string path)
    {
        try
        {
            if (path == null || !File.Exists(path))
            {
                return null;
            }
            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;   // unsupported format (e.g. .dds) or broken file
        }
    }

    private void SetOptionImage(string relativePath)
    {
        ClearOptionImage();
        _optionImage.Image = LoadImage(ModPath(relativePath));
    }

    private void ClearOptionImage()
    {
        var old = _optionImage.Image;
        _optionImage.Image = null;
        old?.Dispose();
    }

    private void SetHeaderImage(string path, int height)
    {
        var old = _headerImage.Image;
        _headerImage.Image = null;
        old?.Dispose();

        // The engine already combines the header path with the mod's folder prefix
        var image = LoadImage(ModPath(path));
        _headerImage.Image = image;
        _headerImage.Visible = image != null;
        _root.RowStyles[1] = new RowStyle(SizeType.Absolute, image != null ? LogicalToDeviceUnits(height > 0 ? height : 75) : 0);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Page 3: install

    private void ShowFinishPage()
    {
        ClearOptionImage();
        SetHeaderImage(null, 0);

        var copies = FileInstaller.GetCopies(_instructions);
        var notes = FileInstaller.GetNotes(_instructions);
        bool fatal = FileInstaller.HasFatalError(_instructions);

        var destinations = copies.Select(c => c.destination).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToArray();

        _filesList.BeginUpdate();
        _filesList.Items.Clear();
        _filesList.Items.AddRange(destinations);
        _filesList.EndUpdate();

        _finishSummary.Text = fatal
            ? "The installer stopped with an error."
            : $"{destinations.Length} file(s) will be installed:";

        _notesBox.Text = string.Join("\r\n", notes);
        _notesBox.Visible = notes.Count > 0;

        _installTargetsLabel.Text = (_installFolders.Count == 1 ? "Install to:" : $"Install to {_installFolders.Count} folders:")
            + "\r\n" + string.Join("\r\n", _installFolders.Select(f => "    " + f));

        _installButton.Enabled = !fatal && destinations.Length > 0;
        _finishStatus.Text = "";
        ShowPage(_finishPanel);
    }

    private async Task InstallAsync()
    {
        if (_mod == null || _installFolders.Count == 0)
        {
            return;
        }

        _settings.OverwriteExistingFiles = _overwriteCheck.Checked;
        _settings.Save();

        _installButton.Enabled = false;
        _restartButton.Enabled = false;
        _finishStatus.Text = "Installing...";

        var instructions = _instructions;
        string modRoot = _mod.RootFolder;
        bool overwrite = _overwriteCheck.Checked;
        var folders = new List<string>(_installFolders);

        var reports = new List<(string folder, InstallReport report)>();
        try
        {
            foreach (string folder in folders)
            {
                _finishStatus.Text = folders.Count == 1 ? "Installing..." : $"Installing to {folder}...";
                var report = await Task.Run(() => FileInstaller.Install(instructions, modRoot, folder, overwrite));
                reports.Add((folder, report));
            }
        }
        catch (Exception ex)
        {
            _installButton.Enabled = true;
            _restartButton.Enabled = true;
            _finishStatus.Text = "";
            string done = reports.Count > 0 ? $"\r\n\r\nAlready installed to:\r\n{string.Join("\r\n", reports.Select(r => r.folder))}" : "";
            MessageBox.Show(this, $"Install failed:\r\n{ex.Message}{done}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _restartButton.Enabled = true;

        int copied = reports.Sum(r => r.report.FilesCopied);
        int skipped = reports.Sum(r => r.report.FilesSkipped);
        int problems = reports.Sum(r => r.report.Problems.Count);
        _finishStatus.Text = $"Installed {copied} file(s)" +
            (folders.Count > 1 ? $" to {folders.Count} folders" : "") +
            (skipped > 0 ? $", skipped {skipped} existing" : "") +
            (problems > 0 ? $", {problems} problem(s)" : "") + ".";

        var lines = new List<string>();
        if (folders.Count > 1)
        {
            foreach (var (folder, report) in reports)
            {
                lines.Add($"{folder}: {report.FilesCopied} installed" + (report.FilesSkipped > 0 ? $", {report.FilesSkipped} skipped" : ""));
            }
        }
        foreach (var (folder, report) in reports)
        {
            foreach (string problem in report.Problems)
            {
                lines.Add(folders.Count > 1 ? $"{folder}: {problem}" : problem);
            }
        }
        if (lines.Count > 0)
        {
            _notesBox.Text = string.Join("\r\n", lines.Concat(FileInstaller.GetNotes(_instructions)));
            _notesBox.Visible = true;
        }
    }
}
