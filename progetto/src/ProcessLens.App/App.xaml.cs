using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ProcessLens.App;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private bool _fatalErrorDisplayed;
    private TrayWindowController? _windowController;

    public App()
    {
        SessionEnding += (_, _) => _windowController?.AllowExit();
        DispatcherUnhandledException += HandleDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppDiagnostics.Record("Eccezione non gestita dal processo", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppDiagnostics.Record("Eccezione in operazione asincrona non osservata", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDiagnostics.Record("Richiesto avvio della GUI");
        try
        {
            const string mutexName = @"Local\ProcessLens.SingleInstance.v1";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                bool activated = ActivateExistingWindow();
                AppDiagnostics.Record(activated
                    ? "Istanza esistente: richiesta di ripristino accolta"
                    : "Single-instance mutex exists, but no visible ProcessLens window found");
                if (!activated)
                {
                    MessageBox.Show(
                        "Un'altra istanza di ProcessLens potrebbe essere già attiva, ma non è stata trovata una finestra. " +
                        "Controlla Gestione attività e il registro diagnostico:\n" + AppDiagnostics.LogFile,
                        "ProcessLens - Istanza già attiva", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                Shutdown();
                return;
            }

            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            AppDiagnostics.Record("Creazione finestra principale e caricamento XAML");
            var window = new MainWindow();
            MainWindow = window;
            _windowController = new TrayWindowController(window, () => Shutdown(), window.SetDashboardWindowVisibility, window.ReportWindowActionFailure, window.WindowDiagnostics);
            window.MinimizeRequested += _windowController.RequestHideToTray;
            window.ExitRequested += () => { _windowController.AllowExit(); Shutdown(); };
            AppDiagnostics.Record("Finestra principale creata: apertura in corso");
            window.Show();
            AppDiagnostics.Record("Richiesta apertura finestra completata (visibilita da verificare)");
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("Avvio interrotto da un errore", ex);
            ShowFatalError(ex, "Errore durante l'avvio");
            _windowController?.AllowExit();
            Shutdown(1);
        }
    }

    private void HandleDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppDiagnostics.Record("Errore non gestito dal thread della GUI", e.Exception);
        ShowFatalError(e.Exception, "Errore dell'interfaccia");
        e.Handled = true;
        _windowController?.AllowExit();
        Shutdown(1);
    }

    private void ShowFatalError(Exception error, string title)
    {
        if (_fatalErrorDisplayed) return;
        _fatalErrorDisplayed = true;
        try
        {
            MessageBox.Show(
                "ProcessLens ha incontrato un errore e non può continuare.\n\n" +
                error.GetType().Name + ": " + error.Message + "\n\n" +
                "Dettagli e stack trace salvati in:\n" + AppDiagnostics.LogFile,
                "ProcessLens - " + title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // The message box itself can fail in a broken UI session.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _windowController?.Dispose();
        AppDiagnostics.Record("Chiusura GUI; codice di uscita " + e.ApplicationExitCode);
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // May not own the mutex (e.g. the duplicate-instance branch).
        }
        finally
        {
            _singleInstanceMutex?.Dispose();
        }
        base.OnExit(e);
    }

    private static bool ActivateExistingWindow()
    {
        try
        {
            // MainWindowHandle can omit hidden windows. The acknowledged message reaches
            // the existing GUI even when it is hidden or was launched through dotnet.
            if (TrayWindowController.TryRestoreExisting()) return true;
            using var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                using (process)
                {
                    if (process.Id == current.Id) continue;
                    if (!string.Equals(process.MainWindowTitle, "ProcessLens", StringComparison.Ordinal)) continue;
                    IntPtr hwnd = process.MainWindowHandle;
                    if (hwnd == IntPtr.Zero) continue;
                    if (IsIconic(hwnd)) ShowWindowAsync(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("Impossibile riattivare la finestra gia aperta", ex);
        }
        return false;
    }

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
