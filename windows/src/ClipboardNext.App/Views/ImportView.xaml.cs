using ClipboardNext.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ClipboardNext.App;

/// <summary>
/// The Markdown import page of the editor: paste or open presenter notes, preview the snippets
/// found in them, choose which to keep, and append them to the queue or replace it.
/// </summary>
public sealed partial class ImportView : UserControl
{
    private DemoQueue? _queue;
    private nint _ownerWindow;
    private Action? _close;
    private IReadOnlyList<ImportCandidate> _candidates = [];

    public ImportView()
    {
        InitializeComponent();
        UpdateState();
    }

    /// <summary>Connects the page to the queue it imports into.</summary>
    /// <param name="queue">The queue that receives the chosen snippets.</param>
    /// <param name="ownerWindow">The handle of the window hosting the page, for the file picker.</param>
    /// <param name="close">Returns to the queue page.</param>
    internal void Attach(DemoQueue queue, nint ownerWindow, Action close)
    {
        _queue = queue;
        _ownerWindow = ownerWindow;
        _close = close;
    }

    /// <summary>Starts a fresh import: no notes, no preview, lines split into separate snippets.</summary>
    internal void Begin()
    {
        MarkdownBox.Text = string.Empty;
        WholeBlocksCheck.IsChecked = false;
        ClearPreview();
        MarkdownBox.Focus(FocusState.Programmatic);
    }

    private void ClearPreview()
    {
        _candidates = [];
        CandidateList.Items.Clear();
        ErrorBar.IsOpen = false;
        UpdateState();
    }

    private void UpdateState()
    {
        var chosen = _candidates.Count(candidate => candidate.Selected);
        SelectedCountText.Text = $"{chosen} selected";
        PreviewButton.IsEnabled = MarkdownBox.Text.Length > 0;
        ReplaceButton.IsEnabled = chosen > 0;
        AppendButton.IsEnabled = chosen > 0;
        PreviewEmptyState.Visibility = _candidates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    // A preview describes the notes as they were; any edit makes it stale.
    private void OnMarkdownChanged(object sender, TextChangedEventArgs e) => ClearPreview();

    private void OnWholeBlocksChanged(object sender, RoutedEventArgs e) => ClearPreview();

    private void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        ClearPreview();
        try
        {
            _candidates = MarkdownImport.Parse(MarkdownBox.Text, WholeBlocksCheck.IsChecked == true);
        }
        catch (MarkdownImportException ex)
        {
            ShowError(ex.Message);
            return;
        }

        foreach (var candidate in _candidates)
        {
            CandidateList.Items.Add(CreateRow(candidate));
        }

        UpdateState();
    }

    private CheckBox CreateRow(ImportCandidate candidate)
    {
        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(new TextBlock
        {
            Text = candidate.Context,
            Style = AppStyles.Get("SecondaryCaptionTextStyle"),
        });
        content.Children.Add(new TextBlock
        {
            Text = candidate.Snippet.Text,
            MaxLines = 5,
            FontSize = 12,
            Style = AppStyles.Get("SnippetTextStyle"),
        });

        var row = new CheckBox
        {
            Content = content,
            IsChecked = candidate.Selected,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 4, 0, 4),
        };
        row.Checked += (_, _) => SetSelected(candidate, true);
        row.Unchecked += (_, _) => SetSelected(candidate, false);
        return row;
    }

    private void SetSelected(ImportCandidate candidate, bool selected)
    {
        candidate.Selected = selected;
        UpdateState();
    }

    private async void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, _ownerWindow);
            picker.FileTypeFilter.Add(".md");
            picker.FileTypeFilter.Add(".markdown");
            picker.FileTypeFilter.Add(".txt");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            MarkdownBox.Text = MarkdownImport.ReadFile(file.Path);
            ClearPreview();
        }
        catch (Exception ex)
        {
            ShowError($"Could not open Markdown: {ex.Message}");
        }
    }

    private void OnReplaceClick(object sender, RoutedEventArgs e)
    {
        if (_queue is { Snippets.Count: 0 })
        {
            Commit(replace: true);
        }
        else
        {
            FlyoutBase.ShowAttachedFlyout(ReplaceButton);
        }
    }

    private void OnReplaceConfirmed(object sender, RoutedEventArgs e)
    {
        ReplaceFlyout.Hide();
        Commit(replace: true);
    }

    private void OnAppendClick(object sender, RoutedEventArgs e) => Commit(replace: false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => _close?.Invoke();

    private void Commit(bool replace)
    {
        if (_queue is not { } queue)
        {
            return;
        }

        var chosen = _candidates.Where(candidate => candidate.Selected).Select(candidate => candidate.Snippet);
        queue.Update(replace ? chosen.ToList() : queue.Snippets.Concat(chosen).ToList());
        _close?.Invoke();
    }
}
