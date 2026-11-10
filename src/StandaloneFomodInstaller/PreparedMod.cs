using SharpCompress.Archives;
using SharpCompress.Common;

namespace StandaloneFomodInstaller;

// The mod's files on disk: either a folder the user picked, or an archive extracted to a temp folder.
public sealed class PreparedMod : IDisposable
{
    public string RootFolder { get; }

    // Paths relative to RootFolder. Directories end with '\', like an archive listing.
    public List<string> FileList { get; }

    public bool HasFomod { get; }

    private readonly bool _isTempFolder;

    private PreparedMod(string rootFolder, bool isTempFolder)
    {
        RootFolder = rootFolder;
        _isTempFolder = isTempFolder;
        FileList = BuildFileList(rootFolder);
        HasFomod = FileList.Any(IsModuleConfig);
    }

    public static PreparedMod FromPath(string path)
    {
        if (Directory.Exists(path))
        {
            return new PreparedMod(Path.GetFullPath(path), false);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Mod archive or folder not found.", path);
        }

        string tempFolder = Path.Combine(Path.GetTempPath(), "StandaloneFomodInstaller", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            using var archive = ArchiveFactory.Open(path);
            // Extracts solid .7z / .rar archives in one pass. SharpCompress refuses entries that would write
            // outside the destination folder.
            archive.WriteToDirectory(tempFolder, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
        }
        catch
        {
            TryDeleteFolder(tempFolder);
            throw;
        }

        return new PreparedMod(tempFolder, true);
    }

    private static List<string> BuildFileList(string root)
    {
        var list = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            list.Add(Path.GetRelativePath(root, dir) + Path.DirectorySeparatorChar);
        }
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            list.Add(Path.GetRelativePath(root, file));
        }
        return list;
    }

    private static bool IsModuleConfig(string relativePath)
    {
        if (!Path.GetFileName(relativePath).Equals("ModuleConfig.xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        string parent = Path.GetFileName(Path.GetDirectoryName(relativePath) ?? "");
        return parent.Equals("fomod", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
        catch
        {
            // a file may still be open (e.g. an image); Windows cleans %TEMP% eventually
        }
    }

    public void Dispose()
    {
        if (_isTempFolder)
        {
            TryDeleteFolder(RootFolder);
        }
    }
}
