using FomodInstaller.Interface;

namespace StandaloneFomodInstaller;

public sealed class InstallReport
{
    public int FilesCopied;
    public int FilesSkipped;
    public int FoldersCreated;
    public List<string> Problems { get; } = new();
}

// Carries out the engine's instructions: copies files from the mod folder to the install folder.
public static class FileInstaller
{
    // The instructions the engine produced, split into what will be installed and what this app can't do.
    public static List<Instruction> GetCopies(IEnumerable<Instruction> instructions) =>
        instructions.Where(i => i.type == "copy").ToList();

    public static List<string> GetNotes(IEnumerable<Instruction> instructions)
    {
        var notes = new List<string>();
        foreach (var i in instructions)
        {
            switch (i.type)
            {
                case "error":
                    notes.Add($"Installer {i.value}: {i.source}");
                    break;
                case "unsupported":
                    notes.Add($"Not supported by this installer: {i.source}");
                    break;
                case "iniedit":
                    notes.Add($"Skipped INI edit: {i.destination} [{i.section}] {i.key}={i.value}");
                    break;
                case "enableplugin":
                    notes.Add($"Enable this plugin in your load order: {i.source}");
                    break;
                case "enableallplugins":
                    notes.Add("Enable all installed plugins in your load order.");
                    break;
            }
        }
        return notes;
    }

    public static bool HasFatalError(IEnumerable<Instruction> instructions) =>
        instructions.Any(i => i.type == "error" && string.Equals(i.value, "fatal", StringComparison.OrdinalIgnoreCase));

    // Resolves a relative destination inside installFolder. Returns null if it would point outside it.
    private static string ResolveInside(string installFolder, string relative)
    {
        string root = Path.GetFullPath(installFolder);
        string rootWithSeparator = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(root, relative ?? ""));
        return full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    public static InstallReport Install(IEnumerable<Instruction> instructions, string modRoot, string installFolder, bool overwrite)
    {
        var report = new InstallReport();
        Directory.CreateDirectory(installFolder);

        var list = instructions.ToList();

        foreach (var i in list.Where(i => i.type == "mkdir"))
        {
            string dir = ResolveInside(installFolder, i.destination);
            if (dir == null)
            {
                report.Problems.Add($"Skipped folder outside the install folder: {i.destination}");
                continue;
            }
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                report.FoldersCreated++;
            }
        }

        // Lower priority first, so higher priority files overwrite them, like a mod manager does.
        foreach (var i in list.Where(i => i.type == "copy").OrderBy(i => i.priority))
        {
            string source = Path.Combine(modRoot, i.source ?? "");
            string dest = ResolveInside(installFolder, i.destination);
            if (dest == null)
            {
                report.Problems.Add($"Skipped file outside the install folder: {i.destination}");
                continue;
            }

            if (Directory.Exists(source))
            {
                Directory.CreateDirectory(dest);
                continue;
            }
            if (!File.Exists(source))
            {
                report.Problems.Add($"Missing in the mod: {i.source}");
                continue;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest) && !overwrite)
                {
                    report.FilesSkipped++;
                    continue;
                }
                File.Copy(source, dest, true);
                report.FilesCopied++;
            }
            catch (Exception ex)
            {
                report.Problems.Add($"Couldn't copy {i.source}: {ex.Message}");
            }
        }

        foreach (var i in list.Where(i => i.type == "generatefile"))
        {
            string dest = ResolveInside(installFolder, i.destination);
            if (dest == null || i.data == null)
            {
                continue;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest) && !overwrite)
                {
                    report.FilesSkipped++;
                    continue;
                }
                File.WriteAllBytes(dest, i.data);
                report.FilesCopied++;
            }
            catch (Exception ex)
            {
                report.Problems.Add($"Couldn't write {i.destination}: {ex.Message}");
            }
        }

        return report;
    }
}
