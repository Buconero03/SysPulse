using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProcessLens.App;

internal static class ReportView
{
    public static StackPanel Body(string text)
    {
        var panel = new StackPanel();
        foreach (var block in ReportLayout.Blocks(text))
        {
            var content = new StackPanel();
            if (block.Label.Length > 0)
                content.Children.Add(new SelectableText {
                    Text = block.Label, FontSize = 11.5, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), Margin = new Thickness(0, 0, 0, 5)
                });
            content.Children.Add(new SelectableText {
                Text = block.Text, FontSize = 12.5, FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59))
            });
            panel.Children.Add(new Border {
                Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(10, 9, 10, 11),
                Margin = new Thickness(0, 0, 0, 6), Child = content
            });
        }
        return panel;
    }

    public static Expander Section(string title, Func<UIElement> content, bool expanded = false)
    {
        // Native header strings can be measured on one long line. Constrain this TextBlock
        // to the section width so long API/chapter titles wrap even in a narrow detail pane.
        var header = new TextBlock {
            Text = title, TextWrapping = TextWrapping.Wrap, FontSize = 13,
            FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(35, 76, 120))
        };
        var item = new Expander {
            Header = header, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 253)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(220, 229, 241)), BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 9, 10, 9), Margin = new Thickness(0, 0, 0, 10)
        };
        item.SizeChanged += (_, _) => header.MaxWidth = Math.Max(1, item.ActualWidth - 64);
        item.Expanded += (_, e) => {
            if (ReferenceEquals(item, e.OriginalSource) && item.Content is null) item.Content = content();
        };
        item.Collapsed += (_, e) => {
            if (ReferenceEquals(item, e.OriginalSource)) item.Content = null;
        };
        item.IsExpanded = expanded;
        return item;
    }
}
