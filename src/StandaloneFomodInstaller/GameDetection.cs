using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;
using SharpCompress.Archives;

namespace StandaloneFomodInstaller;

// ---------------------------------------------------------------------------------------------------------------------
// Known games, read from Games.xml next to the .exe (see the comment at the top of that file for its format).
//
// A FOMOD's file checks (fileDependency) are paths relative to the game's "data folder", which is the folder mod
// managers install that game's mods into: "Data" for most Bethesda games, "Data Files" for Morrowind, the game's own
// folder for Cyberpunk 2077.
// ---------------------------------------------------------------------------------------------------------------------

public sealed class GameFamily
{
    public string Name { get; init; }

    // fileDependency files that only exist for this game (master files)
    public string[] Masters { get; init; } = Array.Empty<string>();

    // Script extender id used by <skseDependency> etc. and by the engine ("skse", "f4se", ...)
    public string Extender { get; init; } = "";

    // Folder names in the mod's files that point to this game, e.g. "skse" matches ".../SKSE/Plugins/x.dll"
    public string[] FolderMarkers { get; init; } = Array.Empty<string>();
}

public sealed class GameInfo
{
    public GameFamily Family { get; init; }
    public string Name { get; init; }

    // Folder name under steamapps\common (several for releases that used different names)
    public string[] SteamFolders { get; init; } = Array.Empty<string>();

    // Relative to the game's main folder. Used to recognize an install.
    public string Exe { get; init; }

    // The data folder, relative to the game's main folder ("" = the main folder itself)
    public string DataSubfolder { get; init; } = "Data";

    // Folders under %LOCALAPPDATA% that may hold plugins.txt (first one found is used)
    public string[] PluginsTxtFolders { get; init; } = Array.Empty<string>();

    // Newer games mark active plugins with '*'; older ones list only the active plugins
    public bool PluginsTxtUsesAsterisk { get; init; } = true;

    // Plugins that are always active and not listed in plugins.txt
    public string[] ImplicitMasters { get; init; } = Array.Empty<string>();

    // HKLM\SOFTWARE\WOW6432Node\Bethesda Softworks\<key> "Installed Path", for older retail / launcher installs
    public string BethesdaRegistryKey { get; init; }

    public string DataFolderFor(string gameRoot) => DataSubfolder == "" ? gameRoot : Path.Combine(gameRoot, DataSubfolder);

    public string DataFolderDescription => DataSubfolder == ""
        ? "the game's main folder (not a subfolder)"
        : $"the \"{DataSubfolder}\" folder inside the game's main folder";

    public string FindPluginsTxt()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (string folder in PluginsTxtFolders)
        {
            string path = Path.Combine(local, folder, "plugins.txt");
            if (File.Exists(path))
            {
                return path;
            }
        }
        return PluginsTxtFolders.Length > 0 ? Path.Combine(local, PluginsTxtFolders[0], "plugins.txt") : null;
    }
}

public static class GameTable
{
    public const string FileName = "Games.xml";

    private sealed class Table
    {
        public List<GameFamily> Families { get; } = new();
        public List<GameInfo> Games { get; } = new();
        public string LoadProblem { get; set; }
    }

    private static readonly Lazy<Table> _table = new(Load);

    public static IReadOnlyList<GameFamily> Families => _table.Value.Families;
    public static IReadOnlyList<GameInfo> Games => _table.Value.Games;

    // Set if Games.xml next to the .exe was missing or couldn't be read (the built-in copy is used instead)
    public static string LoadProblem => _table.Value.LoadProblem;

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    private static Table Load()
    {
        string problem;
        if (File.Exists(FilePath))
        {
            try
            {
                return Parse(XDocument.Load(FilePath, LoadOptions.SetLineInfo));
            }
            catch (Exception ex)
            {
                problem = $"{FileName} couldn't be read ({ex.Message}). Using the built-in game list.";
            }
        }
        else
        {
            problem = $"{FileName} wasn't found next to the program. Using the built-in game list.";
        }

        // Games.xml is also built into the program as a resource
        var table = new Table();
        try
        {
            using var stream = typeof(GameTable).Assembly.GetManifestResourceStream(FileName);
            if (stream != null)
            {
                table = Parse(XDocument.Load(stream));
            }
        }
        catch
        {
            // can't happen unless the build is broken; detection then knows no games
        }
        table.LoadProblem = problem;
        return table;
    }

    private static Table Parse(XDocument doc)
    {
        var table = new Table();
        var root = doc.Root ?? throw new FormatException("the file is empty");

        static string Attr(XElement e, string name) => ((string)e.Attribute(name))?.Trim();
        static string[] Values(XElement e, string name) => e.Elements(name)
            .Select(v => v.Value.Trim()).Where(v => v != "").ToArray();

        foreach (var familyElement in root.Elements("Family"))
        {
            string familyName = Attr(familyElement, "name");
            if (string.IsNullOrEmpty(familyName))
            {
                throw new FormatException($"a <Family> on line {LineOf(familyElement)} has no name");
            }
            var family = new GameFamily
            {
                Name = familyName,
                Extender = Attr(familyElement, "extender") ?? "",
                Masters = Values(familyElement, "Master"),
                FolderMarkers = Values(familyElement, "FolderMarker")
                    .Select(m => m.Replace('\\', '/').Trim('/').ToLowerInvariant()).ToArray(),
            };
            table.Families.Add(family);

            foreach (var gameElement in familyElement.Elements("Game"))
            {
                string name = Attr(gameElement, "name");
                string exe = Attr(gameElement, "exe");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(exe))
                {
                    throw new FormatException($"the <Game> on line {LineOf(gameElement)} needs a name and an exe");
                }
                string asterisk = Attr(gameElement, "pluginsTxtUsesAsterisk");
                table.Games.Add(new GameInfo
                {
                    Family = family,
                    Name = name,
                    Exe = exe.Replace('/', '\\'),
                    DataSubfolder = (Attr(gameElement, "dataFolder") ?? "Data").Replace('/', '\\').Trim('\\'),
                    PluginsTxtUsesAsterisk = asterisk == null || !asterisk.Equals("false", StringComparison.OrdinalIgnoreCase),
                    BethesdaRegistryKey = string.IsNullOrEmpty(Attr(gameElement, "bethesdaRegistryKey")) ? null : Attr(gameElement, "bethesdaRegistryKey"),
                    SteamFolders = Values(gameElement, "SteamFolder"),
                    PluginsTxtFolders = Values(gameElement, "PluginsTxtFolder"),
                    ImplicitMasters = Values(gameElement, "ImplicitMaster"),
                });
            }
        }
        return table;
    }

    private static int LineOf(XElement e) => e is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;

    // The known game a data folder belongs to, and that game's main folder. Null if it isn't one of them.
    public static (GameInfo game, string root)? FindForDataFolder(string dataFolder)
    {
        if (string.IsNullOrWhiteSpace(dataFolder) || !Directory.Exists(dataFolder))
        {
            return null;
        }
        string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataFolder));
        foreach (var game in Games)
        {
            string root;
            if (game.DataSubfolder == "")
            {
                root = folder;
            }
            else if (Path.GetFileName(folder).Equals(game.DataSubfolder, StringComparison.OrdinalIgnoreCase))
            {
                root = Path.GetDirectoryName(folder);
            }
            else
            {
                continue;
            }
            if (root != null && File.Exists(Path.Combine(root, game.Exe)))
            {
                return (game, root);
            }
        }
        return null;
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Finding installed games: Steam libraries, GOG and Epic installs, and Bethesda's registry keys
// ---------------------------------------------------------------------------------------------------------------------

public static class GameLocator
{
    public static List<(GameInfo game, string root)> FindInstalls(IEnumerable<GameInfo> games)
    {
        var gameList = games.ToList();
        var found = new List<(GameInfo game, string root)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(GameInfo game, string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }
            try
            {
                root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                if (File.Exists(Path.Combine(root, game.Exe)) && seen.Add(game.Name + "|" + root))
                {
                    found.Add((game, root));
                }
            }
            catch
            {
                // bad path in a launcher's data
            }
        }

        var candidateRoots = new List<string>();
        foreach (string library in GetSteamLibraries())
        {
            foreach (var game in gameList)
            {
                foreach (string folder in game.SteamFolders)
                {
                    TryAdd(game, Path.Combine(library, "steamapps", "common", folder));
                }
            }
        }
        candidateRoots.AddRange(GetGogInstallFolders());
        candidateRoots.AddRange(GetEpicInstallFolders());
        foreach (string root in candidateRoots)
        {
            foreach (var game in gameList)
            {
                TryAdd(game, root);
            }
        }
        foreach (var game in gameList.Where(g => g.BethesdaRegistryKey != null))
        {
            TryAdd(game, ReadRegistry(Registry.LocalMachine, $@"SOFTWARE\WOW6432Node\Bethesda Softworks\{game.BethesdaRegistryKey}", "Installed Path"));
        }
        return found;
    }

    private static string ReadRegistry(RegistryKey hive, string keyPath, string valueName)
    {
        try
        {
            using var key = hive.OpenSubKey(keyPath);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetSteamLibraries()
    {
        var libraries = new List<string>();
        string steam = ReadRegistry(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath")
            ?? ReadRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        if (string.IsNullOrWhiteSpace(steam))
        {
            return libraries;
        }
        steam = steam.Replace('/', '\\');
        libraries.Add(steam);

        try
        {
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                {
                    libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                }
            }
        }
        catch
        {
            // unreadable library list: just use the main Steam folder
        }
        return libraries.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> GetGogInstallFolders()
    {
        var folders = new List<string>();
        try
        {
            using var games = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
            if (games == null)
            {
                return folders;
            }
            foreach (string id in games.GetSubKeyNames())
            {
                using var game = games.OpenSubKey(id);
                if (game?.GetValue("path") is string path)
                {
                    folders.Add(path);
                }
            }
        }
        catch
        {
            // no access or no GOG
        }
        return folders;
    }

    private static List<string> GetEpicInstallFolders()
    {
        var folders = new List<string>();
        try
        {
            string manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(manifests))
            {
                return folders;
            }
            foreach (string file in Directory.EnumerateFiles(manifests, "*.item"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    if (doc.RootElement.TryGetProperty("InstallLocation", out var location) && location.ValueKind == JsonValueKind.String)
                    {
                        folders.Add(location.GetString());
                    }
                }
                catch
                {
                    // skip a broken manifest
                }
            }
        }
        catch
        {
            // no Epic
        }
        return folders;
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Reading a mod's FOMOD to see what it checks, and guessing the game
// ---------------------------------------------------------------------------------------------------------------------

public sealed class DetectedInstall
{
    public GameInfo Game { get; init; }
    public string Root { get; init; }
    public string DataFolder { get; init; }
    public int CheckedFilesFound { get; init; }
}

public sealed class GameDetectionResult
{
    public string Error { get; set; }
    public bool HasFomod { get; set; }

    // fileDependency paths, relative to the game's data folder
    public List<string> CheckedFiles { get; } = new();

    // e.g. "game version", "SKSE version"
    public List<string> VersionChecks { get; } = new();

    // Best guess; empty if unknown, more than one if it's a tie
    public List<GameFamily> Families { get; } = new();
    public List<string> Reasons { get; } = new();

    public List<DetectedInstall> Installs { get; } = new();

    public bool NeedsDataFolder => CheckedFiles.Count > 0 || VersionChecks.Count > 0;
}

public static class GameDetector
{
    public static GameDetectionResult Detect(string modPath)
    {
        var result = new GameDetectionResult();
        try
        {
            var (xml, modFiles) = ReadModuleConfig(modPath);
            result.HasFomod = xml != null;
            if (xml == null)
            {
                return result;
            }

            var destinations = ReadFomod(xml, result);
            GuessGame(result, modFiles.Concat(destinations).ToList());
            FindInstalls(result);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        return result;
    }

    // fomod\ModuleConfig.xml's text, and the mod's file paths ('/' separated), without extracting the whole archive.
    private static (byte[] xml, List<string> files) ReadModuleConfig(string modPath)
    {
        if (Directory.Exists(modPath))
        {
            var files = Directory.EnumerateFiles(modPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(modPath, f).Replace('\\', '/'))
                .ToList();
            string config = FindModuleConfig(files);
            return (config == null ? null : File.ReadAllBytes(Path.Combine(modPath, config)), files);
        }

        using var archive = ArchiveFactory.Open(modPath);
        var entries = archive.Entries.Where(e => !e.IsDirectory && e.Key != null).ToList();
        var keys = entries.Select(e => e.Key.Replace('\\', '/')).ToList();
        string key = FindModuleConfig(keys);
        if (key == null)
        {
            return (null, keys);
        }
        var entry = entries[keys.IndexOf(key)];
        using var memory = new MemoryStream();
        try
        {
            using var stream = entry.OpenEntryStream();
            stream.CopyTo(memory);
        }
        catch
        {
            // Solid archives (e.g. solid .rar) may only be readable from the start: read through to the entry.
            memory.SetLength(0);
            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.Key?.Replace('\\', '/') == key)
                {
                    reader.WriteEntryTo(memory);
                    break;
                }
            }
        }
        return (memory.ToArray(), keys);
    }

    // The shallowest ".../fomod/ModuleConfig.xml"
    private static string FindModuleConfig(List<string> files) => files
        .Where(f => f.EndsWith("fomod/ModuleConfig.xml", StringComparison.OrdinalIgnoreCase)
            && (f.Length == "fomod/ModuleConfig.xml".Length || f[f.Length - "fomod/ModuleConfig.xml".Length - 1] == '/'))
        .OrderBy(f => f.Count(c => c == '/'))
        .FirstOrDefault();

    private static XDocument LoadXml(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            return XDocument.Load(stream);
        }
        catch (XmlException)
        {
            // Some installers declare an encoding that doesn't match the file. Read the text and drop the declaration.
            using var reader = new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true);
            string text = Regex.Replace(reader.ReadToEnd(), @"^\s*<\?xml[^>]*\?>", "");
            return XDocument.Parse(text);
        }
    }

    // Collects the checks into result and returns the install destinations of the mod's files.
    private static List<string> ReadFomod(byte[] xml, GameDetectionResult result)
    {
        var doc = LoadXml(xml);
        var destinations = new List<string>();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in doc.Descendants())
        {
            string name = element.Name.LocalName;
            switch (name)
            {
                case "fileDependency":
                    string file = (string)element.Attribute("file");
                    if (!string.IsNullOrWhiteSpace(file) && files.Add(file.Trim()))
                    {
                        result.CheckedFiles.Add(file.Trim());
                    }
                    break;
                case "gameDependency":
                case "falloutDependency":
                    versions.Add("game version");
                    break;
                case "skseDependency":
                case "f4seDependency":
                case "nvseDependency":
                case "foseDependency":
                    versions.Add(name.Substring(0, name.Length - "Dependency".Length).ToUpperInvariant() + " version");
                    break;
                case "file":
                case "folder":
                    string destination = (string)element.Attribute("destination") ?? (string)element.Attribute("source");
                    if (!string.IsNullOrWhiteSpace(destination))
                    {
                        destinations.Add(destination.Replace('\\', '/'));
                    }
                    break;
            }
        }
        result.VersionChecks.AddRange(versions);
        return destinations;
    }

    private static void GuessGame(GameDetectionResult result, List<string> modPaths)
    {
        var scores = new Dictionary<GameFamily, int>();
        var reasons = new Dictionary<GameFamily, List<string>>();

        void Add(GameFamily family, int points, string reason)
        {
            scores[family] = scores.GetValueOrDefault(family) + points;
            if (!reasons.TryGetValue(family, out var list))
            {
                reasons[family] = list = new List<string>();
            }
            if (!list.Contains(reason))
            {
                list.Add(reason);
            }
        }

        // Paths with '/' on both ends, so a marker only matches whole folder names
        var paths = modPaths.Select(p => "/" + p.Trim('/').ToLowerInvariant() + "/").ToList();

        foreach (var family in GameTable.Families)
        {
            foreach (string file in result.CheckedFiles)
            {
                string fileName = Path.GetFileName(file.Replace('/', '\\'));
                if (family.Masters.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    Add(family, 3, $"checks for {fileName}");
                }
            }
            if (family.Extender != "" && result.VersionChecks.Contains(family.Extender.ToUpperInvariant() + " version", StringComparer.OrdinalIgnoreCase))
            {
                Add(family, 3, $"checks the {family.Extender.ToUpperInvariant()} version");
            }
            foreach (string marker in family.FolderMarkers)
            {
                if (paths.Any(p => p.Contains("/" + marker + "/")))
                {
                    Add(family, 2, $"installs files to a \"{marker.Replace('/', '\\')}\" folder");
                }
            }
        }

        if (scores.Count == 0)
        {
            return;
        }
        int best = scores.Values.Max();
        foreach (var family in GameTable.Families.Where(f => scores.GetValueOrDefault(f) == best))
        {
            result.Families.Add(family);
            result.Reasons.AddRange(reasons[family]);
        }
    }

    private static void FindInstalls(GameDetectionResult result)
    {
        // Known game: look for its installs. Unknown game: look at every known game and keep the ones
        // that have some of the checked files.
        var games = result.Families.Count > 0
            ? GameTable.Games.Where(g => result.Families.Contains(g.Family))
            : GameTable.Games;

        foreach (var (game, root) in GameLocator.FindInstalls(games))
        {
            string data = game.DataFolderFor(root);
            if (!Directory.Exists(data))
            {
                continue;
            }
            int found = result.CheckedFiles.Count(f =>
            {
                string full = Path.Combine(data, f.Replace('/', '\\'));
                return File.Exists(full) || Directory.Exists(full);
            });
            if (result.Families.Count == 0 && found == 0)
            {
                continue;
            }
            result.Installs.Add(new DetectedInstall { Game = game, Root = root, DataFolder = data, CheckedFilesFound = found });
        }
    }
}
