using System.Diagnostics;
using FomodInstaller.Interface;
using FomodInstaller.Interface.ui;

namespace StandaloneFomodInstaller;

// What the FOMOD engine can ask about the game: installed / active plugins, game and script extender versions.
// Answered from the game Data folder the user picked (optional) and plugins.txt.
public sealed class GameState
{
    // Used when the Data folder isn't one of the known games (same as before game detection was added)
    private static readonly string[] SkyrimMasters = { "Skyrim.esm", "Update.esm", "Dawnguard.esm", "HearthFires.esm", "Dragonborn.esm" };

    public string DataFolder { get; }

    // The game's main folder (where its .exe is)
    private string GameFolder { get; }

    private readonly HashSet<string> _activePlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _implicitMasters = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _havePluginsTxt;

    // pluginsTxtOverride: from settings.json; blank = the detected game's plugins.txt
    public GameState(string dataFolder, string pluginsTxtOverride)
    {
        DataFolder = string.IsNullOrWhiteSpace(dataFolder) || !Directory.Exists(dataFolder) ? "" : dataFolder;
        if (DataFolder == "")
        {
            GameFolder = "";
            return;
        }

        var match = GameTable.FindForDataFolder(DataFolder);
        GameFolder = match?.root ?? (Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(DataFolder)) ?? "");
        foreach (string master in match?.game.ImplicitMasters ?? SkyrimMasters)
        {
            _implicitMasters.Add(master);
        }

        string pluginsTxtPath;
        bool usesAsterisk;
        if (!string.IsNullOrWhiteSpace(pluginsTxtOverride))
        {
            pluginsTxtPath = pluginsTxtOverride;
            usesAsterisk = match?.game.PluginsTxtUsesAsterisk ?? true;
        }
        else if (match != null)
        {
            pluginsTxtPath = match.Value.game.FindPluginsTxt();   // null for games without plugins.txt
            usesAsterisk = match.Value.game.PluginsTxtUsesAsterisk;
        }
        else
        {
            pluginsTxtPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Skyrim Special Edition", "plugins.txt");
            usesAsterisk = true;
        }

        if (pluginsTxtPath != null && File.Exists(pluginsTxtPath))
        {
            try
            {
                foreach (var rawLine in File.ReadAllLines(pluginsTxtPath))
                {
                    var line = rawLine.Trim();
                    if (usesAsterisk)
                    {
                        if (line.StartsWith('*'))
                        {
                            _activePlugins.Add(line.Substring(1).Trim());
                        }
                    }
                    else if (line != "" && !line.StartsWith('#'))
                    {
                        _activePlugins.Add(line);   // older games list only the active plugins
                    }
                }
                _havePluginsTxt = true;
            }
            catch
            {
                _havePluginsTxt = false;
            }
        }
    }

    private static bool IsPluginFile(string name)
    {
        var ext = Path.GetExtension(name);
        return ext.Equals(".esp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".esm", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".esl", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsPresent(string relativePath)
    {
        if (DataFolder == "" || string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }
        string full = Path.Combine(DataFolder, relativePath);
        return File.Exists(full) || Directory.Exists(full);
    }

    public bool IsActive(string relativePath)
    {
        if (!IsPresent(relativePath))
        {
            return false;
        }
        if (!IsPluginFile(relativePath))
        {
            return true;   // FOMOD file dependencies on non-plugin files (e.g. SKSE dlls) only need the file
        }
        string fileName = Path.GetFileName(relativePath);
        if (_implicitMasters.Contains(fileName) || !_havePluginsTxt)
        {
            return true;   // without plugins.txt, assume an installed plugin is active
        }
        return _activePlugins.Contains(fileName);
    }

    public string[] GetAllPlugins(bool activeOnly)
    {
        if (DataFolder == "")
        {
            return Array.Empty<string>();
        }
        return Directory.EnumerateFiles(DataFolder)
            .Select(path => Path.GetFileName(path))
            .Where(IsPluginFile)
            .Where(name => !activeOnly || IsActive(name))
            .ToArray();
    }

    private static string GetFileVersion(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return "";
            }
            var info = FileVersionInfo.GetVersionInfo(path);
            return $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}.{info.FilePrivatePart}";
        }
        catch
        {
            return "";
        }
    }

    public string GetGameVersion()
    {
        if (GameFolder == "")
        {
            return "";
        }
        foreach (var exe in GameTable.Games.Select(g => g.Exe))
        {
            var version = GetFileVersion(Path.Combine(GameFolder, exe));
            if (version != "")
            {
                return version;
            }
        }
        return "";
    }

    // extender is the FOMOD's id, e.g. "skse"
    public string GetExtenderVersion(string extender)
    {
        if (GameFolder == "" || string.IsNullOrWhiteSpace(extender))
        {
            return "";
        }
        string[] loaders = { $"{extender}64_loader.exe", $"{extender}vr_loader.exe", $"{extender}_loader.exe" };
        foreach (var loader in loaders)
        {
            var version = GetFileVersion(Path.Combine(GameFolder, loader));
            if (version != "")
            {
                return version;
            }
        }
        return "";
    }
}

public sealed class StandalonePluginDelegates : PluginDelegates
{
    private readonly GameState _game;
    public StandalonePluginDelegates(GameState game) { _game = game; }

    public override string[] GetAll(bool activeOnly) => _game.GetAllPlugins(activeOnly);
    public override bool IsActive(string pluginName) => _game.IsActive(pluginName);
    public override bool IsPresent(string pluginName) => _game.IsPresent(pluginName);
}

// Only used by C# install scripts, which this app doesn't run
public sealed class StandaloneIniDelegates : IniDelegates
{
    public override Task<string> GetIniString(string iniFileName, string iniSection, string iniKey) => Task.FromResult("");
    public override Task<int> GetIniInt(string iniFileName, string iniSection, string iniKey) => Task.FromResult(0);
}

public sealed class StandaloneContextDelegates : ContextDelegates
{
    private readonly GameState _game;
    public StandaloneContextDelegates(GameState game) { _game = game; }

    // Mod manager version for fommDependency checks
    public override string GetAppVersion() => "1.0.0";
    public override string GetCurrentGameVersion() => _game.GetGameVersion();
    public override string GetExtenderVersion(string extender) => _game.GetExtenderVersion(extender);

    // C# scripts only
    public override Task<bool> IsExtenderPresent() => Task.FromResult(_game.GetExtenderVersion("skse") != "");
    public override Task<bool> CheckIfFileExists(string fileName) => Task.FromResult(_game.IsPresent(fileName));
    public override Task<byte[]> GetExistingDataFile(string dataFile) => Task.FromResult(Array.Empty<byte>());
    public override Task<string[]> GetExistingDataFileList(string folderPath, string searchFilter, bool isRecursive) =>
        Task.FromResult(Array.Empty<string>());
}

// Forwards the engine's dialog calls to the window, on the UI thread.
public sealed class StandaloneUiDelegates : UIDelegates
{
    private readonly MainForm _form;
    public StandaloneUiDelegates(MainForm form) { _form = form; }

    public override void StartDialog(string moduleName, HeaderImage image, Action<int, int, int[]> select, Action<bool, int> cont, Action cancel)
        => _form.RunOnUiThread(() => _form.OnStartDialog(moduleName, image, select, cont, cancel));

    public override void EndDialog()
        => _form.RunOnUiThread(_form.OnEndDialog);

    public override void UpdateState(InstallerStep[] installSteps, int currentStep)
        => _form.RunOnUiThread(() => _form.OnUpdateState(installSteps, currentStep));

    public override void ReportError(string title, string message, string details)
        => _form.RunOnUiThread(() => MessageBox.Show(_form, $"{message}\r\n\r\n{details}", title, MessageBoxButtons.OK, MessageBoxIcon.Error));
}

public sealed class StandaloneCoreDelegates : CoreDelegates
{
    public StandaloneCoreDelegates(GameState game, MainForm form)
    {
        plugin = new StandalonePluginDelegates(game);
        ini = new StandaloneIniDelegates();
        context = new StandaloneContextDelegates(game);
        ui = new StandaloneUiDelegates(form);
    }

    public override PluginDelegates plugin { get; }
    public override IniDelegates ini { get; }
    public override ContextDelegates context { get; }
    public override UIDelegates ui { get; }
}
