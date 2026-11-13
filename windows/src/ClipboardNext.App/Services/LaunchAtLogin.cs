using Windows.ApplicationModel;

namespace ClipboardNext.App;

/// <summary>
/// The real launch-at-login state as Windows reports it. The <c>DisabledByUser</c>,
/// <c>DisabledByPolicy</c>, and <c>EnabledByPolicy</c> values let the settings explain when
/// Windows, not the app, owns the switch.
/// </summary>
internal enum LaunchAtLoginState
{
    /// <summary>Off, and the app may turn it on.</summary>
    Disabled,

    /// <summary>On, and the app may turn it off.</summary>
    Enabled,

    /// <summary>The user turned it off in Windows Settings or Task Manager; the app cannot re-enable it.</summary>
    DisabledByUser,

    /// <summary>Group policy forces it off.</summary>
    DisabledByPolicy,

    /// <summary>Group policy forces it on.</summary>
    EnabledByPolicy,

    /// <summary>Launch at login could not be queried, for example when running without package identity.</summary>
    Unavailable,
}

/// <summary>
/// "Start with Windows", backed by the MSIX <see cref="StartupTask"/> declared in
/// <c>Package.appxmanifest</c>. The registration belongs to the package, so it survives Store
/// updates and is removed with the app.
/// </summary>
internal static class LaunchAtLogin
{
    // Must match the TaskId of the windows.startupTask extension in Package.appxmanifest.
    private const string TaskId = "ClipboardNextLaunchAtLogin";

    public static async Task<LaunchAtLoginState> GetStateAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            return Map(task.State);
        }
        catch (Exception)
        {
            return LaunchAtLoginState.Unavailable;
        }
    }

    /// <summary>
    /// Requests the desired state and returns the state that resulted. Windows may refuse the
    /// change, so callers must show the returned state. Enabling can show a one-time consent prompt.
    /// </summary>
    public static async Task<LaunchAtLoginState> SetEnabledAsync(bool enabled)
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (enabled)
            {
                return task.State == StartupTaskState.Disabled
                    ? Map(await task.RequestEnableAsync())
                    : Map(task.State);
            }

            if (task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
            {
                task.Disable();
            }

            // Disable() changes the registration; read it again so the returned state is current.
            return Map((await StartupTask.GetAsync(TaskId)).State);
        }
        catch (Exception)
        {
            return LaunchAtLoginState.Unavailable;
        }
    }

    public static bool IsOn(LaunchAtLoginState state) =>
        state is LaunchAtLoginState.Enabled or LaunchAtLoginState.EnabledByPolicy;

    public static bool CanChange(LaunchAtLoginState state) =>
        state is LaunchAtLoginState.Enabled or LaunchAtLoginState.Disabled;

    /// <summary>Explains a state the app cannot change; the default description otherwise.</summary>
    public static string Describe(LaunchAtLoginState state) => state switch
    {
        LaunchAtLoginState.DisabledByUser =>
            "Turned off in Windows Settings > Apps > Startup. Turn Clipboard Next back on there.",
        LaunchAtLoginState.DisabledByPolicy => "Turned off by your organization.",
        LaunchAtLoginState.EnabledByPolicy => "Turned on by your organization.",
        LaunchAtLoginState.Unavailable => "Not available for this installation.",
        _ => "Open quietly in the notification area when you sign in.",
    };

    private static LaunchAtLoginState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => LaunchAtLoginState.Enabled,
        StartupTaskState.Disabled => LaunchAtLoginState.Disabled,
        StartupTaskState.DisabledByUser => LaunchAtLoginState.DisabledByUser,
        StartupTaskState.DisabledByPolicy => LaunchAtLoginState.DisabledByPolicy,
        StartupTaskState.EnabledByPolicy => LaunchAtLoginState.EnabledByPolicy,
        _ => LaunchAtLoginState.Unavailable,
    };
}
