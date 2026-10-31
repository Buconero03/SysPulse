using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProcessLens.App;

internal sealed class AuditLogView
{
    private readonly StackPanel _host;
    private IReadOnlyList<AuditLogPresentation.Entry> _rendered = Array.Empty<AuditLogPresentation.Entry>();
    public AuditLogView(StackPanel host) { _host = host; }

    public void Clear() { _host.Children.Clear(); _rendered = Array.Empty<AuditLogPresentation.Entry>(); }

    public void Render(AuditLogPresentation.Document document)
    {
        // Append new events without replacing unchanged controls/selection during acquisition.
        bool prefix = _rendered.Count <= document.Entries.Count &&
            _rendered.SequenceEqual(document.Entries.Take(_rendered.Count));
        if (!prefix) Clear();
        foreach (var entry in document.Entries.Skip(_rendered.Count)) _host.Children.Add(Card(entry));
        _rendered = document.Entries;
    }

    private static Border Card(AuditLogPresentation.Entry entry)
    {
        var content = new StackPanel();
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (entry.Time.Length > 0)
            header.Children.Add(new SelectableText {
                Text = entry.Time, FontFamily = new FontFamily("Consolas"), FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 2, 12, 0), VerticalAlignment = VerticalAlignment.Top
            });
        var stage = new SelectableText {
            Text = $"[{entry.Component}] {entry.Stage}", FontFamily = new FontFamily("Segoe UI"), FontSize = 12.5,
            FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(39, 64, 94))
        };
        Grid.SetColumn(stage, 1);
        header.Children.Add(stage);
        content.Children.Add(header);
        content.Children.Add(new SelectableText {
            Text = entry.Message, FontFamily = new FontFamily("Segoe UI"), FontSize = 12.5,
            FontWeight = FontWeights.Normal, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Margin = new Thickness(0, 4, 0, 0)
        });
        return new Border {
            Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(216, 226, 239)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7),
            Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 6), Child = content
        };
    }
}
