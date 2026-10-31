using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace ProcessLens.App;

public static class GridCopySupport
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(GridCopySupport), new PropertyMetadata(false, OnEnabledChanged));
    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid || !(bool)e.NewValue || grid.ContextMenu is not null) return;
        grid.SetCurrentValue(DataGrid.ClipboardCopyModeProperty, DataGridClipboardCopyMode.IncludeHeader);
        var menu = new ContextMenu();
        var copyCell = new MenuItem { Header = "Copia cella", IsEnabled = false };
        var copyRows = new MenuItem { Header = "Copia righe selezionate", IsEnabled = false };
        var copyTable = new MenuItem { Header = "Copia tabella visualizzata", IsEnabled = false };
        string? cellText = null, selectedText = null, tableText = null;
        copyCell.Click += (_, _) => { if (cellText is not null) ClipboardText.Copy(grid, cellText); };
        copyRows.Click += (_, _) => { if (selectedText is not null) ClipboardText.Copy(grid, selectedText); };
        copyTable.Click += (_, _) => { if (tableText is not null) ClipboardText.Copy(grid, tableText); };
        menu.Items.Add(copyCell); menu.Items.Add(copyRows); menu.Items.Add(copyTable);
        grid.ContextMenu = menu;
        grid.ContextMenuOpening += (_, args) =>
        {
            // Pin strings when the menu opens: a refresh must not retarget a later click.
            var cell = Ancestor<DataGridCell>(args.OriginalSource as DependencyObject);
            if (cell is null && grid.CurrentCell.IsValid)
                cell = Ancestor<DataGridCell>(grid.CurrentCell.Column.GetCellContent(grid.CurrentCell.Item));
            cellText = cell is not null && Ancestor<DataGrid>(cell) == grid
                ? Value(cell.Column, cell.DataContext) : null;
            selectedText = grid.SelectedItems.Count > 0 ? CaptureTable(grid, true) : null;
            tableText = grid.Items.Count > 0 ? CaptureTable(grid, false) : null;
            copyCell.IsEnabled = !string.IsNullOrEmpty(cellText);
            copyRows.IsEnabled = selectedText is not null;
            copyTable.IsEnabled = tableText is not null;
        };
        menu.Closed += (_, _) => grid.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            // MenuItem can close its popup before dispatching Click; release after that command.
            if (!menu.IsOpen) cellText = selectedText = tableText = null;
        }));
    }

    private static T? Ancestor<T>(DependencyObject? item) where T : DependencyObject
    {
        while (item is not null)
        {
            if (item is T found) return found;
            item = item is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(item) : LogicalTreeHelper.GetParent(item);
        }
        return null;
    }

    private static string Value(DataGridColumn column, object item)
    {
        object? value = column.OnCopyingCellClipboardContent(item);
        // A clipboard binding can return a raw numeric value (object target).
        // Apply its declared format once; already formatted strings stay untouched.
        if (value is not null and not string && column.ClipboardContentBinding is Binding binding &&
            !string.IsNullOrEmpty(binding.StringFormat))
            return string.Format(binding.ConverterCulture ?? CultureInfo.CurrentCulture, binding.StringFormat, value);
        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
    }

    internal static string CaptureTable(DataGrid grid, bool selectedOnly)
    {
        var columns = grid.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToArray();
        var selected = new HashSet<object>(grid.SelectedItems.Cast<object>(), ReferenceEqualityComparer.Instance);
        var items = grid.Items.Cast<object>().Where(item => item != CollectionView.NewItemPlaceholder &&
            (!selectedOnly || selected.Contains(item))).ToArray();
        var headers = columns.Select(c => Convert.ToString(c.Header, CultureInfo.CurrentCulture) ?? "").ToArray();
        var rows = items.Select(item => (IReadOnlyList<string>)columns.Select(c => Value(c, item)).ToArray()).ToArray();
        return CopyTextFormat.Table(headers, rows);
    }
}
