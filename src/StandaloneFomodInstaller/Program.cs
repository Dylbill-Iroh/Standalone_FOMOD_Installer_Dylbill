namespace StandaloneFomodInstaller;

internal static class Program
{
    // Usage: StandaloneFomodInstaller.exe [mod archive or folder]
    // (dragging an archive onto the .exe passes it as the first argument)
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
    }
}
