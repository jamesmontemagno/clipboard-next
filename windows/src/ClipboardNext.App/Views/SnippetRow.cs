using System.Globalization;
using ClipboardNext.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace ClipboardNext.App;

/// <summary>Looks up the shared styles defined in App.xaml and by WinUI.</summary>
internal static class AppStyles
{
    public static Style? Get(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) ? value as Style : null;
}

/// <summary>
/// One row of the editor's queue list: its number, optional title, the first lines of its text,
/// a marker when it is the current snippet, and a delete button. Built in code and updated in
/// place, so typing in the editor never rebuilds the list or loses the selection.
/// </summary>
internal sealed class SnippetRow
{
    private const string PasteGlyph = "\uE77F";
    private const string ForwardGlyph = "\uE72A";
    private const string DeleteGlyph = "\uE74D";

    private readonly TextBlock _number;
    private readonly TextBlock _title;
    private readonly TextBlock _text;
    private readonly FontIcon _onClipboardIcon;
    private readonly FontIcon _currentIcon;
    private readonly Button _delete;

    public SnippetRow(Guid id, Action<Guid> delete)
    {
        Id = id;

        _number = new TextBlock
        {
            Width = 26,
            TextAlignment = TextAlignment.Right,
            Style = AppStyles.Get("SecondaryBodyTextStyle"),
        };
        _title = new TextBlock
        {
            MaxLines = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Style = AppStyles.Get("SecondaryCaptionTextStyle"),
        };
        _text = new TextBlock
        {
            MaxLines = 3,
            Style = AppStyles.Get("SnippetTextStyle"),
        };

        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(_title);
        content.Children.Add(_text);

        _onClipboardIcon = new FontIcon { Glyph = PasteGlyph, Style = AppStyles.Get("AccentIconStyle") };
        ToolTipService.SetToolTip(_onClipboardIcon, "On clipboard");
        _currentIcon = new FontIcon { Glyph = ForwardGlyph, Style = AppStyles.Get("AccentIconStyle") };
        ToolTipService.SetToolTip(_currentIcon, "Current queue position");
        var marker = new Grid { Width = 16, VerticalAlignment = VerticalAlignment.Center };
        marker.Children.Add(_onClipboardIcon);
        marker.Children.Add(_currentIcon);

        _delete = new Button
        {
            Content = new FontIcon { Glyph = DeleteGlyph, FontSize = 14 },
            Padding = new Thickness(6),
            VerticalAlignment = VerticalAlignment.Center,
            Style = AppStyles.Get("SubtleButtonStyle"),
        };
        _delete.Click += (_, _) => delete(Id);

        var layout = new Grid { ColumnSpacing = 10, Padding = new Thickness(0, 6, 0, 6) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        AddToColumn(layout, _number, 0);
        AddToColumn(layout, content, 1);
        AddToColumn(layout, marker, 2);
        AddToColumn(layout, _delete, 3);

        Item = new ListViewItem
        {
            Content = layout,
            Tag = id,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
    }

    public Guid Id { get; }

    public ListViewItem Item { get; }

    public void Update(int number, Snippet snippet, bool isCurrent, bool ownsClipboard, bool canDelete)
    {
        var label = number.ToString(CultureInfo.CurrentCulture);
        _number.Text = label;
        _title.Text = snippet.Title;
        _title.Visibility = snippet.Title.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        _text.Text = snippet.Text.Length == 0 ? "Empty snippet" : snippet.Text;
        _text.Opacity = snippet.Text.Length == 0 ? 0.6 : 1;
        _onClipboardIcon.Visibility = isCurrent && ownsClipboard ? Visibility.Visible : Visibility.Collapsed;
        _currentIcon.Visibility = isCurrent && !ownsClipboard ? Visibility.Visible : Visibility.Collapsed;

        _delete.IsEnabled = canDelete;
        ToolTipService.SetToolTip(_delete, $"Delete snippet {label}");
        AutomationProperties.SetName(_delete, $"Delete snippet {label}");

        var summary = snippet.Title.Length > 0 ? snippet.Title : _text.Text;
        var state = !isCurrent ? string.Empty : ownsClipboard ? ", on clipboard" : ", current";
        AutomationProperties.SetName(Item, $"Snippet {label}{state}: {summary}");
    }

    private static void AddToColumn(Grid grid, FrameworkElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }
}
