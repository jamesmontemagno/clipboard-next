using System.Runtime.InteropServices;

namespace ClipboardNext.App;

internal static class AppVersionInfo
{
    private static readonly Version FallbackVersion = new(1, 0, 0, 0);

    public static Version GetCurrentVersion()
    {
        try
        {
            var v = Windows.ApplicationModel.Package.Current.Id.Version;
            return new Version(v.Major, v.Minor, v.Build, v.Revision);
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? FallbackVersion;
        }
    }

    /// <summary>The version as users know it, such as <c>1.0.0</c>. The fourth part belongs to the Store.</summary>
    public static string GetCurrentVersionText() => GetCurrentVersion().ToString(3);
}
