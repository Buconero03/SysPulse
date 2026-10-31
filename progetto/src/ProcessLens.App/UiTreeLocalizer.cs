using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using ProcessLens.Core;

namespace ProcessLens.App;

/// <summary>
/// Recursively localizes static XAML labels, headers, buttons, menus, combo items,
/// data grid columns, and tooltips between Italian and English without breaking WPF bindings.
/// </summary>
internal static class UiTreeLocalizer
{
    private static readonly DependencyProperty OriginalTextProperty =
        DependencyProperty.RegisterAttached("OriginalText", typeof(string), typeof(UiTreeLocalizer), new PropertyMetadata(null));

    private static readonly DependencyProperty OriginalContentProperty =
        DependencyProperty.RegisterAttached("OriginalContent", typeof(string), typeof(UiTreeLocalizer), new PropertyMetadata(null));

    private static readonly DependencyProperty OriginalHeaderProperty =
        DependencyProperty.RegisterAttached("OriginalHeader", typeof(string), typeof(UiTreeLocalizer), new PropertyMetadata(null));

    private static readonly DependencyProperty OriginalToolTipProperty =
        DependencyProperty.RegisterAttached("OriginalToolTip", typeof(string), typeof(UiTreeLocalizer), new PropertyMetadata(null));

    private static readonly ConditionalWeakTable<DataGridColumn, string> ColumnOriginalHeaders = new();

    public static void Apply(DependencyObject? root, AppLanguage language)
    {
        if (root is null) return;
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Traverse(root, language, visited);
    }

    private static void Traverse(object? node, AppLanguage language, HashSet<object> visited)
    {
        if (node is null || !visited.Add(node))
            return;

        if (node is DependencyObject dep)
        {
            LocalizeElement(dep, language, visited);

            foreach (object? child in LogicalTreeHelper.GetChildren(dep))
            {
                Traverse(child, language, visited);
            }
        }
    }

    private static void LocalizeElement(DependencyObject dep, AppLanguage language, HashSet<object> visited)
    {
        // ToolTip on FrameworkElement
        if (dep is FrameworkElement fe)
        {
            if (fe.ToolTip is string tipStr && !string.IsNullOrWhiteSpace(tipStr))
            {
                string origTip = (string?)fe.GetValue(OriginalToolTipProperty) ?? tipStr;
                if (fe.GetValue(OriginalToolTipProperty) is null)
                    fe.SetValue(OriginalToolTipProperty, origTip);

                fe.ToolTip = language == AppLanguage.English
                    ? SysPulseLocalization.GetLabel(origTip, AppLanguage.English)
                    : origTip;
            }
            else if (fe.ToolTip is DependencyObject tipDep)
            {
                Traverse(tipDep, language, visited);
            }

            if (fe.ContextMenu is not null)
            {
                Traverse(fe.ContextMenu, language, visited);
            }
        }

        // Window Title
        if (dep is Window win && !string.IsNullOrWhiteSpace(win.Title) &&
            !win.Title.StartsWith("SysPulse", StringComparison.OrdinalIgnoreCase))
        {
            string origTitle = (string?)win.GetValue(OriginalHeaderProperty) ?? win.Title;
            if (win.GetValue(OriginalHeaderProperty) is null)
                win.SetValue(OriginalHeaderProperty, origTitle);

            win.Title = language == AppLanguage.English
                ? SysPulseLocalization.Translate(origTitle, AppLanguage.English)
                : origTitle;
        }

        // TextBlock (skip if Text is data-bound)
        if (dep is TextBlock tb && BindingOperations.GetBindingBase(tb, TextBlock.TextProperty) is null)
        {
            if (!string.IsNullOrWhiteSpace(tb.Text))
            {
                string orig = (string?)tb.GetValue(OriginalTextProperty) ?? tb.Text;
                string translatedEn = SysPulseLocalization.GetLabel(orig, AppLanguage.English);
                if (!string.Equals(orig, translatedEn, StringComparison.Ordinal) || tb.GetValue(OriginalTextProperty) is not null)
                {
                    if (tb.GetValue(OriginalTextProperty) is null)
                        tb.SetValue(OriginalTextProperty, orig);

                    tb.Text = language == AppLanguage.English ? translatedEn : orig;
                }
            }
        }

        // SelectableText (skip if Text is data-bound)
        if (dep is SelectableText st && BindingOperations.GetBindingBase(st, TextBox.TextProperty) is null)
        {
            if (!string.IsNullOrWhiteSpace(st.Text))
            {
                string orig = (string?)st.GetValue(OriginalTextProperty) ?? st.Text;
                string translatedEn = SysPulseLocalization.GetLabel(orig, AppLanguage.English);
                if (!string.Equals(orig, translatedEn, StringComparison.Ordinal) || st.GetValue(OriginalTextProperty) is not null)
                {
                    if (st.GetValue(OriginalTextProperty) is null)
                        st.SetValue(OriginalTextProperty, orig);

                    st.Text = language == AppLanguage.English ? translatedEn : orig;
                }
            }
        }

        //HeaderedContentControl (Expander, TabItem, GroupBox)
        if (dep is HeaderedContentControl hcc)
        {
            if (hcc.Header is string headerStr && !string.IsNullOrWhiteSpace(headerStr) &&
                BindingOperations.GetBindingBase(hcc, HeaderedContentControl.HeaderProperty) is null)
            {
                string origHeader = (string?)hcc.GetValue(OriginalHeaderProperty) ?? headerStr;
                if (hcc.GetValue(OriginalHeaderProperty) is null)
                    hcc.SetValue(OriginalHeaderProperty, origHeader);

                hcc.Header = language == AppLanguage.English
                    ? SysPulseLocalization.GetLabel(origHeader, AppLanguage.English)
                    : origHeader;
            }
            else if (hcc.Header is DependencyObject headerDep)
            {
                Traverse(headerDep, language, visited);
            }

            if (hcc.Content is DependencyObject contentDep)
            {
                Traverse(contentDep, language, visited);
            }
        }

        // HeaderedItemsControl (MenuItem)
        if (dep is HeaderedItemsControl hic)
        {
            if (hic.Header is string menuHeader && !string.IsNullOrWhiteSpace(menuHeader) &&
                BindingOperations.GetBindingBase(hic, HeaderedItemsControl.HeaderProperty) is null)
            {
                string origHeader = (string?)hic.GetValue(OriginalHeaderProperty) ?? menuHeader;
                if (hic.GetValue(OriginalHeaderProperty) is null)
                    hic.SetValue(OriginalHeaderProperty, origHeader);

                hic.Header = language == AppLanguage.English
                    ? SysPulseLocalization.GetLabel(origHeader, AppLanguage.English)
                    : origHeader;
            }
            else if (hic.Header is DependencyObject headerDep)
            {
                Traverse(headerDep, language, visited);
            }
        }

        // ContentControl (Button, CheckBox, ComboBoxItem, Label, etc.)
        if (dep is ContentControl cc && dep is not HeaderedContentControl && dep is not Window)
        {
            if (cc.Name != "LanguageToggleButton" &&
                cc.Content is string contentStr &&
                !string.IsNullOrWhiteSpace(contentStr) &&
                BindingOperations.GetBindingBase(cc, ContentControl.ContentProperty) is null)
            {
                string origContent = (string?)cc.GetValue(OriginalContentProperty) ?? contentStr;
                if (cc.GetValue(OriginalContentProperty) is null)
                    cc.SetValue(OriginalContentProperty, origContent);

                cc.Content = language == AppLanguage.English
                    ? SysPulseLocalization.GetLabel(origContent, AppLanguage.English)
                    : origContent;
            }
            else if (cc.Content is DependencyObject contentDep)
            {
                Traverse(contentDep, language, visited);
            }
        }

        // DataGrid Columns
        if (dep is DataGrid dg)
        {
            foreach (DataGridColumn col in dg.Columns)
            {
                if (col.Header is string colHeader && !string.IsNullOrWhiteSpace(colHeader))
                {
                    if (!ColumnOriginalHeaders.TryGetValue(col, out string? origColHeader))
                    {
                        origColHeader = colHeader;
                        ColumnOriginalHeaders.Add(col, origColHeader);
                    }

                    col.Header = language == AppLanguage.English
                        ? SysPulseLocalization.GetLabel(origColHeader, AppLanguage.English)
                        : origColHeader;
                }
            }
        }

        // ItemsControl (Menu, ContextMenu, ComboBox, TabControl) - only traverse non-databound static items
        if (dep is ItemsControl ic && ic.ItemsSource is null)
        {
            foreach (object? item in (IEnumerable)ic.Items)
            {
                Traverse(item, language, visited);
            }
        }

        // Popup child
        if (dep is Popup popup && popup.Child is not null)
        {
            Traverse(popup.Child, language, visited);
        }
    }
}
