using ClipboardNext.Core;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ClipboardNext.App;

/// <summary>
/// The import page of the editor: paste or open source text, preview the snippets
/// found in them, choose which to keep, and append them to the queue or replace it.
/// </summary>
public sealed partial class ImportView : UserControl
{
    private DemoQueue? _queue;
    private AppSettings? _settings;
    private nint _ownerWindow;
    private Action? _close;
    private IReadOnlyList<ImportCandidate> _candidates = [];
    private bool _ready;

    public ImportView()
    {
        InitializeComponent();
        _ready = true;
        UpdateFormat();
    }

    /// <summary>Connects the page to the queue it imports into.</summary>
    /// <param name="queue">The queue that receives the chosen snippets.</param>
    /// <param name="settings">The preferences that remember the selected format.</param>
    /// <param name="ownerWindow">The handle of the window hosting the page, for the file picker.</param>
    /// <param name="close">Returns to the queue page.</param>
    internal void Attach(DemoQueue queue, AppSettings settings, nint ownerWindow, Action close)
    {
        _queue = queue;
        _settings = settings;
        _ownerWindow = ownerWindow;
        _close = close;
        _ready = false;
        ImportFormatSelector.SelectedItem = settings.ImportFormat == ImportFormat.PlainText
            ? PlainTextFormatItem : MarkdownFormatItem;
        _ready = true;
        UpdateFormat();
    }

    /// <summary>Starts a fresh import with the remembered format, no source text, and no preview.</summary>
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

    private ImportFormat Format => ImportFormatSelector.SelectedItem == PlainTextFormatItem
        ? ImportFormat.PlainText : ImportFormat.Markdown;

    private void OnFormatChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (!_ready)
        {
            return;
        }

        if (_settings is { } settings)
        {
            settings.ImportFormat = Format;
        }

        UpdateFormat();
    }

    private void UpdateFormat()
    {
        var markdown = Format == ImportFormat.Markdown;
        WholeBlocksCheck.Visibility = markdown ? Visibility.Visible : Visibility.Collapsed;
        MarkdownBox.PlaceholderText = markdown
            ? "Paste your Markdown presenter notes here, or open a file."
            : "Paste plain text here, one item per line, or open a file.";
        AutomationProperties.SetName(MarkdownBox, markdown ? "Markdown presenter notes" : "Plain text, one item per line");
        PreviewGuidanceText.Text = markdown
            ? "Code blocks are selected by default. Blockquoted prompts are optional."
            : "Each nonblank line becomes an untitled snippet, selected by default.";
        ImportHelpText.Text = markdown
            ? "By default, each nonblank code line becomes a snippet. Splitting is not shell-aware: keep whole blocks for continuations or scripts. Review commands before pasting."
            : "Each nonblank line becomes an untitled snippet. Whitespace and repeated lines are kept. Review commands before pasting.";
        ClearPreview();
    }

    // A preview describes the source as it was; any edit makes it stale.
    private void OnMarkdownChanged(object sender, TextChangedEventArgs e) => ClearPreview();

    private void OnWholeBlocksChanged(object sender, RoutedEventArgs e) => ClearPreview();

    private void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        ClearPreview();
        try
        {
            _candidates = Format == ImportFormat.Markdown
                ? MarkdownImport.Parse(MarkdownBox.Text, WholeBlocksCheck.IsChecked == true)
                : PlainTextImport.Parse(MarkdownBox.Text);
        }
        catch (Exception ex) when (ex is MarkdownImportException or PlainTextImportException)
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
        if (!string.IsNullOrEmpty(candidate.Context))
        {
            content.Children.Add(new TextBlock
            {
                Text = candidate.Context,
                Style = AppStyles.Get("SecondaryCaptionTextStyle"),
            });
        }
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
            ShowError($"Could not open file: {ex.Message}");
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
