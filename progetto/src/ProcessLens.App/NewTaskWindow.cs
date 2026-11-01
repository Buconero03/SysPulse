using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ProcessLens.Core;
using ProcessLens.Windows;

namespace ProcessLens.App;

// Created on demand; constructing the dialog never starts a process.
public sealed class NewTaskWindow : Window
{
    private readonly INewTaskLauncher _launcher;
    private bool _busy, _closed;
    internal TextBox ProgramBox { get; } = Input();
    internal TextBox ArgumentsBox { get; } = Input();
    internal TextBox DirectoryBox { get; } = Input();
    internal SelectableText StatusText { get; } = new() { Text = "", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    internal Button RunButton { get; } = Action("Esegui");
    private readonly Button _cancel = Action("Annulla"), _browse = Action("Sfoglia…");
    public event Action<NewTaskResult>? TaskStarted;

    public NewTaskWindow() : this(new WindowsNewTaskLauncher()) { }
    internal NewTaskWindow(INewTaskLauncher launcher)
    {
        _launcher = launcher;
        Title = "Esegui nuova attività";
        Width = 650; Height = 510; MinWidth = 480; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Segoe UI"); Background = Brushes.White;
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = Title, FontSize = 21, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(Note("Scegli il programma da avviare. Inserisci le opzioni nel campo Argomenti.", 6));
        body.Children.Add(Label("Programma"));
        var program = new Grid(); program.ColumnDefinitions.Add(new ColumnDefinition());
        program.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        program.Children.Add(ProgramBox); Grid.SetColumn(_browse, 1); program.Children.Add(_browse);
        _browse.Margin = new Thickness(8, 0, 0, 0); body.Children.Add(program);
        ProgramBox.ToolTip = "Nome, per esempio notepad.exe, oppure percorso completo del programma.";
        body.Children.Add(Label("Argomenti (facoltativi)")); body.Children.Add(ArgumentsBox);
        ArgumentsBox.ToolTip = "Argomenti esatti, comprese le virgolette dei percorsi con spazi. Non aggiungere qui il nome del programma.";
        body.Children.Add(Label("Cartella iniziale (facoltativa)")); body.Children.Add(DirectoryBox);
        DirectoryBox.ToolTip = "Cartella di lavoro del programma. Se vuota, usa la cartella corrente di ProcessLens.";
        body.Children.Add(Note("Privilegi: quelli di ProcessLens, quindi amministratore nell’avvio normale.", 12));
        body.Children.Add(Note("Per comandi di shell o script, scegli cmd.exe, powershell.exe o l’interprete necessario. Nessuna cronologia dei comandi salvata da questa finestra.", 6));
        body.Children.Add(StatusText);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        RunButton.IsDefault = true; _cancel.IsCancel = true;
        actions.Children.Add(RunButton); actions.Children.Add(_cancel); body.Children.Add(actions);
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        UiTreeLocalizer.Apply(this, SysPulseLocalization.CurrentLanguage);
        RunButton.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => Close();
        _browse.Click += (_, _) => Browse();
        Loaded += (_, _) => ProgramBox.Focus();
        Closing += (_, e) => { if (_busy) e.Cancel = true; };
        Closed += (_, _) => { _closed = true; ProgramBox.Clear(); ArgumentsBox.Clear(); DirectoryBox.Clear(); };
    }

    private static TextBox Input() => new() { MinHeight = 36, Padding = new Thickness(8, 6, 8, 6), TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, MaxLength = 32760 };
    private static TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 13, 0, 5), FontWeight = FontWeights.SemiBold };
    private static TextBlock Note(string text, int margin) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, margin, 0, 0) };
    private static Button Action(string text) => new() {
        Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
        MinHeight = 36, MinWidth = 88, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(8, 0, 0, 0)
    };
    internal static OpenFileDialog CreateProgramPicker() => new OpenFileDialog {
        Title = "Scegli il programma", Filter = "Programmi (*.exe;*.com)|*.exe;*.com", CheckFileExists = true, Multiselect = false
    };

    private void Browse()
    {
        var picker = CreateProgramPicker();
        if (picker.ShowDialog(this) == true) ProgramBox.Text = picker.FileName;
    }
    internal async Task RunAsync()
    {
        if (_busy || _closed) return;
        if (!NewTaskRequest.TryCreate(ProgramBox.Text, ArgumentsBox.Text, DirectoryBox.Text, out var request, out var error))
        { StatusText.Text = error; return; }
        _busy = true; Toggle(false); StatusText.Text = "Avvio in corso…";
        NewTaskResult result;
        try { result = await Task.Run(() => _launcher.Start(request!)); }
        catch (Exception ex) { result = WindowsNewTaskLauncher.Failure(ex); }
        finally { _busy = false; if (!_closed) Toggle(true); }
        if (_closed) return;
        StatusText.Text = result.Message;
        if (result.Started && result.Pid is > 0) { TaskStarted?.Invoke(result); Close(); }
    }
    private void Toggle(bool enabled)
    { ProgramBox.IsEnabled = ArgumentsBox.IsEnabled = DirectoryBox.IsEnabled = _browse.IsEnabled = RunButton.IsEnabled = _cancel.IsEnabled = enabled; }
}
