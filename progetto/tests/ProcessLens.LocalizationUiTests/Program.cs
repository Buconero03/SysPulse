using System;
using System.Windows;
using System.Windows.Controls;
using ProcessLens.App;
using ProcessLens.Core;
using Microsoft.Win32;
// Compile-time regression: the localized XAML root must remain inheritable.
internal sealed class DerivedWindowFixture : LocalizedWindow { }
internal static class Program {
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL bilingual UI: "+name);Console.WriteLine("PASS bilingual UI: "+name);}
 [STAThread]public static void Main(){
  _ = new TextBlock{Text="Native control constructed before localization"};
  Check(!typeof(LocalizedWindow).IsSealed && typeof(MainWindow).BaseType==typeof(LocalizedWindow),"R35 R2 localized XAML root and code-behind have the same inheritable base");
  LocalizationView.Initialize();var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  SysPulseLocalization.SetLanguage(AppLanguage.Italian,persist:false);
  string name=new("Funzione sconosciuta".AsSpan());
  var report=SysPulseLocalization.Capture($"Processo: {SysPulseLocalization.Opaque(name)}");
  var output=new SelectableText{Text=report};var label=new LocalizedTextBlock{Text=SysPulseLocalization.Literal("Funzione sconosciuta")};
  var input=new LocalizedTextBox{Text=name};var button=new LocalizedButton{Content=SysPulseLocalization.Literal("Copia tutto")};
  var window=new Window{Content=new StackPanel()};var panel=(StackPanel)window.Content;panel.Children.Add(output);panel.Children.Add(label);panel.Children.Add(input);panel.Children.Add(button);
  Check(output.Text=="Processo: Funzione sconosciuta","Italian original and data retained");
  SysPulseLocalization.SetLanguage(AppLanguage.English,persist:false);LocalizationView.Refresh(window);
  Check(output.Text=="Process: Funzione sconosciuta","English full diagnostic text with original process name");
  Check(label.Text=="Unknown function","native WPF text coercion");Check(input.Text==name,"editable data never translated");Check((string)button.Content=="Copy all","native WPF button content");
  output.Text=SysPulseLocalization.Capture($"Processo: {SysPulseLocalization.Opaque("changed.exe")}");Check(output.Text=="Process: changed.exe","current report replaces previous report without cached stale content");
  SysPulseLocalization.SetLanguage(AppLanguage.Italian,persist:false);LocalizationView.Refresh(window);Check(output.Text=="Processo: changed.exe","Italian restored from current canonical value");
  SysPulseLocalization.SetLanguage(AppLanguage.English,persist:false);
  var picker=NewTaskWindow.CreateProgramPicker();
  Check(picker.Title=="Choose a program" || picker.Title=="Choose the program","native file dialog title follows English without showing the dialog");
  Check(picker.Filter=="Programs (*.exe;*.com)|*.exe;*.com","native file dialog filter translated while file masks remain intact");
  Check(picker.CheckFileExists && !picker.Multiselect,"file dialog validation options preserved");
  SysPulseLocalization.SetLanguage(AppLanguage.Italian,persist:false);
  var italianPicker=NewTaskWindow.CreateProgramPicker();Check(italianPicker.Title=="Scegli il programma" && italianPicker.Filter=="Programmi (*.exe;*.com)|*.exe;*.com","Italian file dialog remains exact");
  var derived=new DerivedWindowFixture{Title=SysPulseLocalization.Literal("Funzione sconosciuta")};
  SysPulseLocalization.SetLanguage(AppLanguage.English,persist:false);LocalizationView.Refresh(derived);
  Check(derived.Title=="Unknown function","R35 R2 derived window inherits localized title metadata");
  SysPulseLocalization.SetLanguage(AppLanguage.Italian,persist:false);LocalizationView.Refresh(derived);
  Check(derived.Title=="Funzione sconosciuta","R35 R2 derived window restores original Italian title");
  derived.Close();window.Close();app.Shutdown();
 }
}
