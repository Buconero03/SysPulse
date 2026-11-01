using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using ProcessLens.Core;
namespace ProcessLens.App;
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension {
    public string Key{get;}
    public TrExtension(string key)=>Key=key;
    public override object ProvideValue(IServiceProvider provider)=>SysPulseLocalization.Translate(SysPulseLocalization.ByKey(Key));
}
internal static class LocalizationView {
    private static bool _initialized;
    internal static object Display(DependencyObject owner,object value) {
        if(value is not string text)return value;
        if(owner is TextBox box&&!box.IsReadOnly)return value;
        if(owner is FrameworkElement element && element.Name is "DetailProjectShortText")return value;
        if(owner is FrameworkElement bound && (owner is TextBlock or TextBox)) {
            var path=BindingOperations.GetBindingExpression(bound,owner is TextBlock?TextBlock.TextProperty:TextBox.TextProperty)?.ParentBinding.Path?.Path;
            if(path is "Name" or "Process.Name" or "Process.CommandLine" or "Process.ExecutablePath")return value;
        }
        return SysPulseLocalization.Render(text);
    }
    internal static void Register(Type type,params DependencyProperty[] properties) {
        foreach(var property in properties)property.OverrideMetadata(type,new FrameworkPropertyMetadata(property.GetMetadata(type).DefaultValue,null,Display));
    }
    internal static void Initialize() {
        if(_initialized)return;_initialized=true;
        SysPulseLocalization.LanguageChanged+=language=>{
            ApplyCulture(language);
            if(Application.Current is {} app)app.Dispatcher.Invoke(()=>{foreach(Window w in app.Windows)Refresh(w);});
        };
        ApplyCulture(SysPulseLocalization.CurrentLanguage);
    }
    private static void ApplyCulture(AppLanguage language) {
        var culture=CultureInfo.GetCultureInfo(language==AppLanguage.English?"en-US":"it-IT");
        CultureInfo.CurrentCulture=culture;CultureInfo.CurrentUICulture=culture;CultureInfo.DefaultThreadCurrentCulture=culture;CultureInfo.DefaultThreadCurrentUICulture=culture;
    }
    internal static void Refresh(DependencyObject? root)=>RefreshTree(root,new System.Collections.Generic.HashSet<DependencyObject>());
    private static void RefreshTree(DependencyObject? root,System.Collections.Generic.HashSet<DependencyObject> seen) {
        if(root is null||!seen.Add(root))return;
        if(root is TextBlock t){t.CoerceValue(TextBlock.TextProperty);foreach(Inline inline in t.Inlines)if(inline is Run run)run.CoerceValue(Run.TextProperty);}
        if(root is SelectableText or LocalizedTextBox)root.CoerceValue(TextBox.TextProperty);
        if(root is Window window)window.Title=SysPulseLocalization.Translate(window.Title);
        if(root is ContentControl)root.CoerceValue(ContentControl.ContentProperty);
        if(root is HeaderedContentControl)root.CoerceValue(HeaderedContentControl.HeaderProperty);
        if(root is HeaderedItemsControl)root.CoerceValue(HeaderedItemsControl.HeaderProperty);
        if(root is Control or TextBlock)root.CoerceValue(FrameworkElement.ToolTipProperty);
        if(root is DataGrid grid)foreach(var c in grid.Columns)c.CoerceValue(DataGridColumn.HeaderProperty);
        if(root is Visual || root is System.Windows.Media.Media3D.Visual3D)for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)RefreshTree(VisualTreeHelper.GetChild(root,i),seen);
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())RefreshTree(child,seen);
        if(root is FrameworkElement element) {
            if(element.ContextMenu is {} menu)RefreshTree(menu,seen);
            if(element.ToolTip is string tip && BindingOperations.GetBindingBase(element,FrameworkElement.ToolTipProperty) is null)element.ToolTip=SysPulseLocalization.Translate(tip);
        }
    }
    internal static bool SelectAtStartup(string[] args) {
        bool explicitLanguage=args.Any(a=>a.ToLowerInvariant() is "--lang=en" or "--english" or "-en" or "/en" or "en" or "--lang=it" or "--italian" or "-it" or "/it" or "it");
        if(explicitLanguage){ApplyCulture(SysPulseLocalization.CurrentLanguage);return true;}
        var chooser=new Window{Title="Lingua / Language",Width=430,Height=210,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen};
        var panel=new StackPanel{Margin=new Thickness(22)};
        panel.Children.Add(new TextBlock{Text="Scegli la lingua delle analisi\nChoose the analysis language",FontSize=18,Margin=new Thickness(0,0,0,18)});
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var item in new[]{(AppLanguage.Italian,"Italiano"),(AppLanguage.English,"English")}) {
            var button=new Button{Content=item.Item2,MinWidth=160,Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,12,0)};
            button.Click+=(_,_)=>{SysPulseLocalization.SetLanguage(item.Item1,persist:true);ApplyCulture(item.Item1);chooser.DialogResult=true;};row.Children.Add(button);
        }
        panel.Children.Add(row);chooser.Content=panel;return chooser.ShowDialog()==true;
    }
}
