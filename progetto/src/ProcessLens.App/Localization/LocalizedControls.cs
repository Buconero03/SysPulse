using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
namespace ProcessLens.App;
// These types inherit native templates and behavior. Metadata is registered only for
// each new derived type before its first instance; original WPF owner metadata is untouched.
public sealed class LocalizedTextBlock : TextBlock {
    static LocalizedTextBlock(){ LocalizationView.Register(typeof(LocalizedTextBlock),TextBlock.TextProperty,FrameworkElement.ToolTipProperty); }
}
public sealed class LocalizedRun : Run {
    static LocalizedRun(){ LocalizationView.Register(typeof(LocalizedRun),Run.TextProperty); }
    public LocalizedRun(){} public LocalizedRun(string text):base(text){}
}
public sealed class LocalizedButton : Button {
    static LocalizedButton(){ LocalizationView.Register(typeof(LocalizedButton),ContentControl.ContentProperty,FrameworkElement.ToolTipProperty); }
}
public sealed class LocalizedCheckBox : CheckBox {
    static LocalizedCheckBox(){ LocalizationView.Register(typeof(LocalizedCheckBox),ContentControl.ContentProperty,FrameworkElement.ToolTipProperty); }
}
public sealed class LocalizedComboBoxItem : ComboBoxItem {
    static LocalizedComboBoxItem(){ LocalizationView.Register(typeof(LocalizedComboBoxItem),ContentControl.ContentProperty,FrameworkElement.ToolTipProperty); }
}
public sealed class LocalizedLabel : Label {
    static LocalizedLabel(){ LocalizationView.Register(typeof(LocalizedLabel),ContentControl.ContentProperty,FrameworkElement.ToolTipProperty); }
}
public sealed class LocalizedMenuItem : MenuItem {
    static LocalizedMenuItem(){ LocalizationView.Register(typeof(LocalizedMenuItem),HeaderedItemsControl.HeaderProperty,FrameworkElement.ToolTipProperty); }
}
public sealed class LocalizedExpander : Expander {
    static LocalizedExpander(){ LocalizationView.Register(typeof(LocalizedExpander),HeaderedContentControl.HeaderProperty,ContentControl.ContentProperty,FrameworkElement.ToolTipProperty); }
}
public sealed class LocalizedTabItem : TabItem {
    static LocalizedTabItem(){ LocalizationView.Register(typeof(LocalizedTabItem),HeaderedContentControl.HeaderProperty,ContentControl.ContentProperty,FrameworkElement.ToolTipProperty); }
}
public sealed class LocalizedGroupBox : GroupBox {
    static LocalizedGroupBox(){ LocalizationView.Register(typeof(LocalizedGroupBox),HeaderedContentControl.HeaderProperty,ContentControl.ContentProperty,FrameworkElement.ToolTipProperty); }
}
public sealed class LocalizedTextBox : TextBox {
    static LocalizedTextBox(){ LocalizationView.Register(typeof(LocalizedTextBox),TextBox.TextProperty,FrameworkElement.ToolTipProperty); IsReadOnlyProperty.OverrideMetadata(typeof(LocalizedTextBox),new FrameworkPropertyMetadata(false,(_,__)=>((TextBox)_).CoerceValue(TextProperty))); }
}
public sealed class LocalizedDataGridTextColumn : DataGridTextColumn {
    static LocalizedDataGridTextColumn(){ LocalizationView.Register(typeof(LocalizedDataGridTextColumn),DataGridColumn.HeaderProperty); }
    protected override FrameworkElement GenerateElement(DataGridCell cell,object item) {
        var original=base.GenerateElement(cell,item);
        if(original is not TextBlock text || original is LocalizedTextBlock)return original;
        var localized=new LocalizedTextBlock();
        var values=text.GetLocalValueEnumerator();
        while(values.MoveNext()) {
            var value=values.Current;if(value.Property.ReadOnly)continue;
            var binding=System.Windows.Data.BindingOperations.GetBindingBase(text,value.Property);
            if(binding is not null)System.Windows.Data.BindingOperations.SetBinding(localized,value.Property,binding);
            else localized.SetValue(value.Property,value.Value);
        }
        if(text.Style is not null)localized.Style=text.Style;
        return localized;
    }
    public override object OnCopyingCellClipboardContent(object item) {
        object value=base.OnCopyingCellClipboardContent(item);
        return value is string text?ProcessLens.Core.SysPulseLocalization.Render(text):value;
    }
}
public sealed class LocalizedDataGridTemplateColumn : DataGridTemplateColumn {
    static LocalizedDataGridTemplateColumn(){ LocalizationView.Register(typeof(LocalizedDataGridTemplateColumn),DataGridColumn.HeaderProperty); }
}
public sealed class LocalizedDataGridCheckBoxColumn : DataGridCheckBoxColumn {
    static LocalizedDataGridCheckBoxColumn(){ LocalizationView.Register(typeof(LocalizedDataGridCheckBoxColumn),DataGridColumn.HeaderProperty); }
}
// WPF x:Class windows derive from the XAML root type during markup compilation.
// Keep this base inheritable; MainWindow is a LocalizedWindow in generated XAML.
public class LocalizedWindow : Window {
    static LocalizedWindow(){ LocalizationView.Register(typeof(LocalizedWindow),Window.TitleProperty,ContentControl.ContentProperty,FrameworkElement.ToolTipProperty); }
}
