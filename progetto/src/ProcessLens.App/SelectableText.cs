using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProcessLens.App;

// Native TextBox selection and keyboard commands, with the appearance of output text.
// Used in detail/report panels, not in the virtualized process/service list templates.
public sealed class SelectableText : TextBox
{
    public static readonly RoutedUICommand CopyAllCommand = new("Copia tutto", "CopyAll", typeof(SelectableText));
    private readonly Action<string> _copy;

    static SelectableText()
    {
        IsReadOnlyProperty.OverrideMetadata(typeof(SelectableText),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.None, null, (_, _) => true));
    }

    public SelectableText() : this(null) { }

    // The UI regression suite injects a sink; it never changes the user's clipboard.
    internal SelectableText(Action<string>? copy)
    {
        _copy = copy ?? (text => ClipboardText.Copy(this, text));
        IsReadOnly = true;
        IsUndoEnabled = false;
        AcceptsReturn = true;
        AcceptsTab = false;
        AllowDrop = false;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        Background = Brushes.Transparent;
        TextWrapping = TextWrapping.Wrap;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,
            (_, e) => { if (SelectionLength > 0) _copy(SelectedText); e.Handled = true; },
            (_, e) => { e.CanExecute = SelectionLength > 0; e.Handled = true; }));
        CommandBindings.Add(new CommandBinding(CopyAllCommand,
            (_, e) => { if (Text.Length > 0) _copy(Text); e.Handled = true; },
            (_, e) => { e.CanExecute = Text.Length > 0; e.Handled = true; }));

        // Build menus in code: no event-wired ContextMenu in a XAML style setter.
        ContextMenu = new ContextMenu();
        ContextMenu.Items.Add(new MenuItem { Header = "Copia selezione", Command = ApplicationCommands.Copy,
            CommandTarget = this, InputGestureText = "Ctrl+C" });
        ContextMenu.Items.Add(new MenuItem { Header = "Copia tutto", Command = CopyAllCommand, CommandTarget = this });
        ContextMenu.Items.Add(new Separator());
        ContextMenu.Items.Add(new MenuItem { Header = "Seleziona tutto", Command = ApplicationCommands.SelectAll,
            CommandTarget = this, InputGestureText = "Ctrl+A" });
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        // Output grows with its content. Keep the surrounding details panel scrollable.
        if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled &&
            VisualTreeHelper.GetParent(this) is UIElement parent)
        {
            e.Handled = true;
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                { RoutedEvent = Mouse.MouseWheelEvent, Source = this });
            return;
        }
        base.OnPreviewMouseWheel(e);
    }
}
