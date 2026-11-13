using ClipboardNext.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ClipboardNext.App;

/// <summary>
/// The content of the tray popup: the queue's status, the current and next snippet, the
/// transport buttons, and shortcuts to the badge, the editor, and Exit.
/// </summary>
public sealed partial class TrayPopupView : UserControl
{
    /// <summary>The popup's size in device-independent pixels. Its content is laid out to fit.</summary>
    public const double PopupWidth = 360;
    public const double PopupHeight = 432;

    private readonly AppModel _model;
    private readonly Action _openEditor;
    private readonly Action _exit;
    private readonly Action _dismiss;
    private NoticeAction _noticeAction;

    internal TrayPopupView(AppModel model, Action openEditor, Action exit, Action dismiss)
    {
        _model = model;
        _openEditor = openEditor;
        _exit = exit;
        _dismiss = dismiss;
        InitializeComponent();

        Controls.Attach(model.Queue);
        model.Queue.Changed += OnStateChanged;
        model.Monitor.Changed += OnStateChanged;
        model.Badge.Changed += OnStateChanged;
        Refresh();
    }

    private enum NoticeAction
    {
        None,
        OpenEditor,
        EnablePasteDetection,
    }

    private enum NoticeKind
    {
        Info,
        Warning,
        Ok,
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var queue = _model.Queue;
        StatusText.Text = queue.Status.DisplayName();
        ProgressText.Text = queue.Progress;
        CurrentText.Text = queue.Current?.Text ?? "—";
        NextText.Text = queue.Next?.Text ?? "—";

        var badgeVisible = _model.Badge.IsVisible;
        BadgeToggle.IsChecked = badgeVisible;
        ResetBadgeButton.Visibility = badgeVisible ? Visibility.Visible : Visibility.Collapsed;

        var monitor = _model.Monitor;
        if (queue.StorageError is not null)
        {
            ShowNotice(NoticeKind.Warning, "The queue could not be saved or loaded. Open the editor to fix it.", NoticeAction.OpenEditor, "Open");
        }
        else if (queue.Message is { } message)
        {
            ShowNotice(NoticeKind.Info, message, NoticeAction.None, null);
        }
        else if (monitor.Error is { } error)
        {
            ShowNotice(NoticeKind.Warning, error, NoticeAction.EnablePasteDetection, "Retry");
        }
        else if (!monitor.IsMonitoring)
        {
            ShowNotice(NoticeKind.Info, "Manual mode. Turn on paste detection to advance when you paste.", NoticeAction.EnablePasteDetection, "Turn on");
        }
        else
        {
            ShowNotice(NoticeKind.Ok, "Paste detection is on. Each paste copies the next snippet.", NoticeAction.None, null);
        }
    }

    private void ShowNotice(NoticeKind kind, string text, NoticeAction action, string? actionLabel)
    {
        NoticeInfoIcon.Visibility = kind == NoticeKind.Info ? Visibility.Visible : Visibility.Collapsed;
        NoticeWarningIcon.Visibility = kind == NoticeKind.Warning ? Visibility.Visible : Visibility.Collapsed;
        NoticeOkIcon.Visibility = kind == NoticeKind.Ok ? Visibility.Visible : Visibility.Collapsed;
        NoticeText.Text = text;
        _noticeAction = action;
        NoticeButton.Content = actionLabel;
        NoticeButton.Visibility = action == NoticeAction.None ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnNoticeButtonClick(object sender, RoutedEventArgs e)
    {
        switch (_noticeAction)
        {
            case NoticeAction.OpenEditor:
                OpenEditor();
                break;
            case NoticeAction.EnablePasteDetection:
                _model.Monitor.Enable();
                break;
        }
    }

    private void OnOpenEditorClick(object sender, RoutedEventArgs e) => OpenEditor();

    private void OnBadgeToggleClick(object sender, RoutedEventArgs e) =>
        _model.Badge.IsVisible = BadgeToggle.IsChecked == true;

    private void OnResetBadgeClick(object sender, RoutedEventArgs e) => _model.Badge.ResetPosition();

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        _dismiss();
        _exit();
    }

    private void OpenEditor()
    {
        _dismiss();
        _openEditor();
    }
}
