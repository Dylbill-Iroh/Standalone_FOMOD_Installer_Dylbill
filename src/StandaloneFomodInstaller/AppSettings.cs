using System.Text.Json;

namespace StandaloneFomodInstaller;

// Saved in %APPDATA%\StandaloneFomodInstaller\settings.json
public class AppSettings
{
    public string LastModPath { get; set; } = "";
    public string LastGameDataFolder { get; set; } = "";
    public List<string> InstallFolders { get; set; } = new();

    // Only read, for settings saved before more than one install folder was possible
    public string LastInstallFolder { get; set; } = "";

    // Blank = the plugins.txt of the game the Data folder belongs to (see GameTable)
    public string PluginsTxtPath { get; set; } = "";

    public bool OverwriteExistingFiles { get; set; } = true;

    private static string FolderPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StandaloneFomodInstaller");

    private static string FilePath => Path.Combine(FolderPath, "settings.json");

    // The install folders to show on the first page: the saved list, or the older single folder, or the Data folder.
    public List<string> GetInstallFolders()
    {
        var folders = (InstallFolders ?? new List<string>()).Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
        if (folders.Count == 0 && !string.IsNullOrWhiteSpace(LastInstallFolder))
        {
            folders.Add(LastInstallFolder);
        }
        if (folders.Count == 0 && !string.IsNullOrWhiteSpace(LastGameDataFolder))
        {
            folders.Add(LastGameDataFolder);
        }
        return folders;
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
        }
        catch
        {
            // corrupt settings file: start fresh
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // settings are a convenience; never fail an install over them
        }
    }
}
