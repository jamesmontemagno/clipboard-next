using System.Diagnostics;
using ClipboardNext.Core;
using Microsoft.UI.Windowing;

namespace ClipboardNext.App;

/// <summary>
/// Sets the taskbar / Alt+Tab icon of a standard window.
/// <para>
/// <see cref="AppWindow.SetIcon(string)"/> requires a fully qualified path: a relative path is
/// resolved against the process working directory, which is only the package folder when the app
/// is started from Start or Explorer. A launcher that starts the executable with another working
/// directory would otherwise get a <see cref="FileNotFoundException"/> thrown from
/// <c>Window.Activated</c>, which XAML treats as fatal. So the path is resolved against the
/// executable's directory and any failure is logged instead of propagated.
/// </para>
/// </summary>
internal static class WindowIcon
{
    private static readonly Lazy<string?> ResolvedIconPath = new(() => AppAssets.ResolvePath("AppIcon.ico"));

    public static void Apply(AppWindow appWindow)
    {
        var path = ResolvedIconPath.Value;
        if (path is null)
        {
            return;
        }

        try
        {
            appWindow.SetIcon(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppWindow.SetIcon failed for '{path}': {ex}");
            CrashDiagnostics.Log("WindowIcon.Apply", ex, handled: true);
        }
    }
}

/// <summary>Finds files the app ships in its <c>Assets</c> folder.</summary>
internal static class AppAssets
{
    /// <summary>The full path of an asset next to the executable, or <see langword="null"/> when it is missing.</summary>
    public static string? ResolvePath(string fileName)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
            if (File.Exists(path))
            {
                return path;
            }

            CrashDiagnostics.Log(
                "AppAssets.ResolvePath",
                new FileNotFoundException("Asset not found.", path),
                handled: true);
        }
        catch (Exception ex)
        {
            CrashDiagnostics.Log("AppAssets.ResolvePath", ex, handled: true);
        }

        return null;
    }
}
