using ClipboardNext.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace ClipboardNext.App;

/// <summary>The queue's transport buttons, shared by the tray popup and the editor.</summary>
public sealed partial class QueueControls : UserControl
{
    private const string PlayGlyph = "\uE768";
    private const string PauseGlyph = "\uE769";

    private DemoQueue? _queue;
    private Func<Guid?>? _selectedId;

    public QueueControls()
    {
        InitializeComponent();
    }

    /// <summary>Connects the buttons to a queue.</summary>
    /// <param name="queue">The queue to control.</param>
    /// <param name="selectedId">The snippet Start begins from, when the host has a selection.</param>
    internal void Attach(DemoQueue queue, Func<Guid?>? selectedId = null)
    {
        Detach();
        _queue = queue;
        _selectedId = selectedId;
        queue.Changed += OnQueueChanged;
        Refresh();
    }

    internal void Detach()
    {
        if (_queue is not null)
        {
            _queue.Changed -= OnQueueChanged;
            _queue = null;
        }
    }

    private void OnQueueChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (_queue is not { } queue)
        {
            return;
        }

        var action = QueuePresentation.PrimaryAction(queue);
        PrimaryIcon.Glyph = action == QueuePrimaryAction.Pause ? PauseGlyph : PlayGlyph;
        PrimaryText.Text = action.ToString();
        AutomationProperties.SetName(PrimaryButton, action.ToString());
        ToolTipService.SetToolTip(PrimaryButton, action switch
        {
            QueuePrimaryAction.Pause => "Stop advancing; the current snippet stays on the clipboard",
            QueuePrimaryAction.Resume => "Copy the current snippet again and keep going",
            _ => "Copy the first snippet, or the selected one, and start the queue",
        });
        BackButton.IsEnabled = queue.CanGoBack;
        NextButton.IsEnabled = QueuePresentation.CanGoNext(queue);
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => _queue?.ManualPrevious();

    private void OnNextClick(object sender, RoutedEventArgs e) => _queue?.ManualNext();

    private void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        if (_queue is not { } queue)
        {
            return;
        }

        switch (QueuePresentation.PrimaryAction(queue))
        {
            case QueuePrimaryAction.Pause:
                queue.Pause();
                break;
            case QueuePrimaryAction.Resume:
                queue.Resume();
                break;
            default:
                queue.Start(_selectedId?.Invoke());
                break;
        }
    }
}
