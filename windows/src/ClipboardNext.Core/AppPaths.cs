namespace ClipboardNext.Core;

/// <summary>
/// Where Clipboard Next keeps its files. For the packaged (Microsoft Store) app Windows redirects
/// this folder into the package's own storage, so it is removed when the app is uninstalled.
/// </summary>
public static class AppPaths
{
    private const string ApplicationFolderName = "ClipboardNext";

    public static string DataDirectory
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = string.IsNullOrWhiteSpace(localAppData) ? Path.GetTempPath() : localAppData;
            return Path.Combine(root, ApplicationFolderName);
        }
    }

    public static string QueuePath => Path.Combine(DataDirectory, "queue.json");

    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public static string LogsDirectory => Path.Combine(DataDirectory, "Logs");
}
