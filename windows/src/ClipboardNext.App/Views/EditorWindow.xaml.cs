using ClipboardNext.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinRT.Interop;

namespace ClipboardNext.App;

/// <summary>
/// The queue editor: the ordered list of snippets, the selected snippet's text, the transport
/// controls, Markdown import, and settings. Closing it leaves the app running in the tray.
/// </summary>
public sealed partial class EditorWindow : Window
{
    private const int DefaultWidthDip = 980;
    private const int DefaultHeightDip = 700;
    private const int MinimumWidthDip = 820;
    private const int MinimumHeightDip = 560;

    private readonly AppModel _model;
    private readonly WindowChromeController _chromeController;
    private readonly List<SnippetRow> _rows = [];
    private Guid? _selectedId;
    private LaunchAtLoginState _startupState = LaunchAtLoginState.Unavailable;
    private bool _syncingRows;
    private bool _ready;
    private bool _closed;

    internal EditorWindow(AppModel model)
    {
        _model = model;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        var hwnd = WindowNative.GetWindowHandle(this);
        AppWindowPlacement.CenterInCurrentWorkAreaAtDipSize(AppWindow, hwnd, DefaultWidthDip, DefaultHeightDip);
        _chromeController = new WindowChromeController(this, RootGrid, MinimumWidthDip, MinimumHeightDip);

        Controls.Attach(model.Queue, () => _selectedId);
        ImportPage.Attach(model.Queue, hwnd, ShowQueuePage);
        VersionText.Text = $"Clipboard Next {AppVersionInfo.GetCurrentVersionText()}";
        ApplyStartupState(_startupState);

        model.Queue.Changed += OnStateChanged;
        model.Monitor.Changed += OnStateChanged;
        model.Badge.Changed += OnStateChanged;
        Closed += OnClosed;

        Refresh();
        // Controls raise change events while they are first given their values; from here on a
        // change is the user's.
        _ready = true;
    }

    private DemoQueue Queue => _model.Queue;

    private bool IsRunning => Queue.Status == QueueStatus.Running;

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        Controls.Detach();
        _model.Queue.Changed -= OnStateChanged;
        _model.Monitor.Changed -= OnStateChanged;
        _model.Badge.Changed -= OnStateChanged;
        Closed -= OnClosed;
    }

    private void Refresh()
    {
        if (_closed)
        {
            return;
        }

        var queue = Queue;
        var running = IsRunning;
        var count = queue.Snippets.Count;
        if (_selectedId is { } selected && !queue.Snippets.Any(snippet => snippet.Id == selected))
        {
            _selectedId = null;
        }

        StatusText.Text = $"{queue.Status.DisplayName()} · {queue.Progress}";
        SyncRows();
        EmptyQueueState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var index = SelectedIndex();
        StartHereButton.Visibility = !running && index is not null ? Visibility.Visible : Visibility.Collapsed;
        AddButton.IsEnabled = !running;
        ClearAllButton.IsEnabled = !running && count > 0;
        ClearAllPrompt.Text = count == 1 ? "Clear 1 snippet?" : $"Clear all {count} snippets?";
        MoveUpButton.IsEnabled = !running && index > 0;
        MoveDownButton.IsEnabled = !running && index < count - 1;
        ImportButton.IsEnabled = !running;

        RefreshSnippetEditor(index, running);
        RefreshSettings();
        RefreshNotices();
    }

    // Updates rows in place while the queue holds the same snippets in the same order, and
    // rebuilds the list only when snippets were added, removed, or moved.
    private void SyncRows()
    {
        var snippets = Queue.Snippets;
        var sameOrder = _rows.Count == snippets.Count;
        for (var i = 0; sameOrder && i < snippets.Count; i++)
        {
            sameOrder = _rows[i].Id == snippets[i].Id;
        }

        _syncingRows = true;
        try
        {
            if (!sameOrder)
            {
                SnippetList.Items.Clear();
                _rows.Clear();
                foreach (var snippet in snippets)
                {
                    var row = new SnippetRow(snippet.Id, DeleteSnippet);
                    _rows.Add(row);
                    SnippetList.Items.Add(row.Item);
                }
            }

            for (var i = 0; i < snippets.Count; i++)
            {
                _rows[i].Update(i + 1, snippets[i], Queue.Index == i, Queue.OwnsClipboard, canDelete: !IsRunning);
            }

            var selectedItem = _rows.FirstOrDefault(row => row.Id == _selectedId)?.Item;
            if (!ReferenceEquals(SnippetList.SelectedItem, selectedItem))
            {
                SnippetList.SelectedItem = selectedItem;
            }
        }
        finally
        {
            _syncingRows = false;
        }
    }

    private void RefreshSnippetEditor(int? index, bool running)
    {
        if (index is not { } position)
        {
            SnippetEditor.Visibility = Visibility.Collapsed;
            NoSelectionState.Visibility = Visibility.Visible;
            return;
        }

        var snippet = Queue.Snippets[position];
        SnippetEditor.Visibility = Visibility.Visible;
        NoSelectionState.Visibility = Visibility.Collapsed;
        SnippetCaption.Text = $"SNIPPET {position + 1}";
        // Only touch the boxes when they show something else, so typing never moves the caret.
        if (TitleBox.Text != snippet.Title)
        {
            TitleBox.Text = snippet.Title;
        }

        if (NormalizeLineEndings(SnippetBox.Text) != NormalizeLineEndings(snippet.Text))
        {
            SnippetBox.Text = snippet.Text;
        }

        TitleBox.IsEnabled = !running;
        SnippetBox.IsEnabled = !running;
    }

    private void RefreshSettings()
    {
        var detectionEnabled = _model.Settings.PasteDetectionEnabled;
        DetectionSwitch.IsOn = detectionEnabled;
        TerminalSwitch.IsOn = _model.Monitor.DetectTerminalShortcuts;
        TerminalSwitch.IsEnabled = detectionEnabled;

        var delay = Queue.AdvanceDelayMilliseconds;
        if ((int)Math.Round(DelaySlider.Value) != delay)
        {
            DelaySlider.Value = delay;
        }

        DelayText.Text = $"{delay / 1000.0:0.00} s";
        BadgeSwitch.IsOn = _model.Badge.IsVisible;
        ResetBadgeButton.IsEnabled = _model.Badge.IsVisible;
    }

    private void RefreshNotices()
    {
        var monitor = _model.Monitor;
        DetectionOnIndicator.Visibility = monitor.IsMonitoring ? Visibility.Visible : Visibility.Collapsed;
        DetectionBar.IsOpen = !monitor.IsMonitoring;
        if (monitor.Error is { } error)
        {
            DetectionBar.Severity = InfoBarSeverity.Warning;
            DetectionBar.Title = "Paste detection couldn't start";
            DetectionBar.Message = error;
            DetectionActionButton.Content = "Try again";
        }
        else
        {
            DetectionBar.Severity = InfoBarSeverity.Informational;
            DetectionBar.Title = "Manual mode";
            DetectionBar.Message =
                "Use Next to advance, or turn on paste detection to copy the next snippet when you paste. "
                + "It only listens for the paste shortcut while a queue is running, and never records or blocks what you type.";
            DetectionActionButton.Content = "Turn on paste detection";
        }

        MessageBar.IsOpen = Queue.Message is not null;
        MessageBar.Message = Queue.Message ?? string.Empty;
        StorageBar.IsOpen = Queue.StorageError is not null;
        StorageBar.Message = Queue.StorageError ?? string.Empty;
    }

    private int? SelectedIndex()
    {
        if (_selectedId is not { } id)
        {
            return null;
        }

        for (var i = 0; i < Queue.Snippets.Count; i++)
        {
            if (Queue.Snippets[i].Id == id)
            {
                return i;
            }
        }

        return null;
    }

    private Snippet? SelectedSnippet() => SelectedIndex() is { } index ? Queue.Snippets[index] : null;

    private void ReplaceSelected(Snippet updated) =>
        Queue.Update(Queue.Snippets.Select(snippet => snippet.Id == updated.Id ? updated : snippet).ToList());

    // The text box reports line breaks as CR; snippets keep LF so queue files stay portable.
    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private void OnSnippetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingRows)
        {
            return;
        }

        _selectedId = (SnippetList.SelectedItem as ListViewItem)?.Tag as Guid?;
        Refresh();
    }

    private void OnTitleChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready && SelectedSnippet() is { } snippet && TitleBox.Text != snippet.Title)
        {
            ReplaceSelected(snippet with { Title = TitleBox.Text });
        }
    }

    private void OnSnippetTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready || SelectedSnippet() is not { } snippet)
        {
            return;
        }

        var text = NormalizeLineEndings(SnippetBox.Text);
        if (text != NormalizeLineEndings(snippet.Text))
        {
            ReplaceSelected(snippet with { Text = text });
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var snippet = new Snippet(string.Empty);
        _selectedId = snippet.Id;
        Queue.Update(Queue.Snippets.Append(snippet).ToList());
        SnippetBox.Focus(FocusState.Programmatic);
    }

    private void DeleteSnippet(Guid id) =>
        Queue.Update(Queue.Snippets.Where(snippet => snippet.Id != id).ToList());

    private void OnClearAllConfirmed(object sender, RoutedEventArgs e)
    {
        ClearAllFlyout.Hide();
        Queue.Update([]);
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e) => MoveSelected(-1);

    private void OnMoveDownClick(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int direction)
    {
        if (SelectedIndex() is not { } index)
        {
            return;
        }

        var target = index + direction;
        var items = Queue.Snippets.ToList();
        if (target < 0 || target >= items.Count)
        {
            return;
        }

        (items[index], items[target]) = (items[target], items[index]);
        Queue.Update(items);
    }

    private void OnStartHereClick(object sender, RoutedEventArgs e) => Queue.Start(_selectedId);

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        ImportPage.Begin();
        QueuePage.Visibility = Visibility.Collapsed;
        ImportPage.Visibility = Visibility.Visible;
    }

    private void ShowQueuePage()
    {
        ImportPage.Visibility = Visibility.Collapsed;
        QueuePage.Visibility = Visibility.Visible;
    }

    private void OnReplaceSavedQueueConfirmed(object sender, RoutedEventArgs e)
    {
        ReplaceSavedFlyout.Hide();
        Queue.RetrySave();
    }

    private void OnDetectionActionClick(object sender, RoutedEventArgs e) => _model.Monitor.Enable();

    private void OnDetectionToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready || DetectionSwitch.IsOn == _model.Settings.PasteDetectionEnabled)
        {
            return;
        }

        if (DetectionSwitch.IsOn)
        {
            _model.Monitor.Enable();
        }
        else
        {
            _model.Monitor.Disable();
        }
    }

    private void OnTerminalShortcutsToggled(object sender, RoutedEventArgs e)
    {
        if (_ready && TerminalSwitch.IsOn != _model.Monitor.DetectTerminalShortcuts)
        {
            _model.Monitor.DetectTerminalShortcuts = TerminalSwitch.IsOn;
        }
    }

    private void OnDelayChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        var milliseconds = (int)Math.Round(e.NewValue);
        if (milliseconds != Queue.AdvanceDelayMilliseconds)
        {
            Queue.SetAdvanceDelay(milliseconds);
        }
    }

    private void OnBadgeToggled(object sender, RoutedEventArgs e)
    {
        if (_ready && BadgeSwitch.IsOn != _model.Badge.IsVisible)
        {
            _model.Badge.IsVisible = BadgeSwitch.IsOn;
        }
    }

    private void OnResetBadgeClick(object sender, RoutedEventArgs e) => _model.Badge.ResetPosition();

    // Windows owns the startup setting, so it is read each time the settings open.
    private async void OnSettingsFlyoutOpening(object? sender, object e)
    {
        var state = await LaunchAtLogin.GetStateAsync();
        if (!_closed)
        {
            ApplyStartupState(state);
        }
    }

    private async void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        var wanted = StartupSwitch.IsOn;
        if (!_ready || wanted == LaunchAtLogin.IsOn(_startupState))
        {
            return;
        }

        // Windows may refuse the change, so show the state that actually resulted.
        var state = await LaunchAtLogin.SetEnabledAsync(wanted);
        if (!_closed)
        {
            ApplyStartupState(state);
        }
    }

    private void ApplyStartupState(LaunchAtLoginState state)
    {
        _startupState = state;
        StartupSwitch.IsOn = LaunchAtLogin.IsOn(state);
        StartupSwitch.IsEnabled = LaunchAtLogin.CanChange(state);
        StartupCard.Description = LaunchAtLogin.Describe(state);
    }
}
