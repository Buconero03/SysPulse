using ProcessLens.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ProcessLens.App;

/// <summary>
/// Local, bounded startup diagnostics. Does not transmit information.
/// Best-effort only: a failure to write the log must never prevent startup.
/// </summary>
internal static class AppDiagnostics
{
    private static readonly object Gate = new();
    private static readonly DeferredDiagnosticWriter WindowActions = new(
        action => ThreadPool.QueueUserWorkItem(_ => action()), text => Record(text));
    internal static void RecordWindowAction(string step) => WindowActions.Enqueue(step);
    internal static string WindowLogFile => Path.Combine(LogDirectory, "window-actions.log");
    private static int _traceDrops, _traceTotalDrops, _traceWrites;
    private static string? _traceFailure;
    internal static string WindowTraceStatus => $"Righe scritte: {Volatile.Read(ref _traceWrites)}; scartate dalla coda: {Volatile.Read(ref _traceTotalDrops)}; ultimo errore: {Volatile.Read(ref _traceFailure) ?? "nessuno rilevato"}. Scrittura in background; il rapporto in memoria resta copiabile.";
    private static readonly DeferredDiagnosticWriter WindowTrace = new(
        action => ThreadPool.QueueUserWorkItem(_ => action()), WriteWindowTrace, capacity: 256);
    internal static void RecordWindowTrace(string line)
    {
        if (!WindowTrace.Enqueue(line)) { Interlocked.Increment(ref _traceDrops); Interlocked.Increment(ref _traceTotalDrops); }
    }
    private static void WriteWindowTrace(string line)
    {
        try
        {
        lock (Gate)
        {
            Directory.CreateDirectory(LogDirectory);
            if (File.Exists(WindowLogFile) && new FileInfo(WindowLogFile).Length > 1024 * 1024)
                File.Move(WindowLogFile, Path.Combine(LogDirectory, "window-actions.previous.log"), overwrite: true);
            int dropped = Interlocked.Exchange(ref _traceDrops, 0);
            File.AppendAllText(WindowLogFile, (dropped > 0 ? $"[Finestra] Coda piena: {dropped} eventi non scritti su disco.\n" : "") + line + Environment.NewLine, Encoding.UTF8);
            Interlocked.Increment(ref _traceWrites);
        }
        }
        catch (Exception ex) { Volatile.Write(ref _traceFailure, $"{ex.GetType().Name} HRESULT=0x{ex.HResult:X8}"); }
    }
    private static readonly string GuiSession = Guid.NewGuid().ToString("N");

    internal static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProcessLens", "logs");

    internal static string LogFile => Path.Combine(LogDirectory, "startup.log");

    internal static void Record(string step, Exception? exception = null)
    {
        var error = exception is null ? default : StructuredDiagnostics.ErrorDetails(exception);
        StructuredDiagnostics.Write("startup", new DiagnosticEvent
        {
            EventId = exception is null ? DiagnosticEventIds.GuiLifecycle : DiagnosticEventIds.GuiFailure,
            Component = "ProcessLens.App", Operation = "Gui.Lifecycle",
            Severity = exception is null ? "Information" : "Error",
            Status = exception is null ? "Completed" : "Failed",
            OperationId = GuiSession,
            ReasonCode = exception is null ? "GUI_STAGE" : "GUI_EXCEPTION",
            ExceptionType = exception is null ? null : error.ExceptionType,
            HResultHex = exception is null ? null : error.HResultHex,
            Win32ErrorCode = exception is null ? null : error.Win32ErrorCode,
            SqliteErrorCode = exception is null ? null : error.SqliteErrorCode,
            SqliteExtendedErrorCode = exception is null ? null : error.SqliteExtendedErrorCode
        });
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                var path = LogFile;
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                {
                    string old = Path.Combine(LogDirectory, "startup.previous.log");
                    File.Move(path, old, overwrite: true);
                }

                var entry = new StringBuilder();
                entry.Append('[').Append(DateTimeOffset.Now.ToString("O"))
                    .Append("] | ").Append(exception is null ? "INFO" : "ERRORE")
                    .Append(" | PID=").Append(Environment.ProcessId)
                    .Append(" | FASE=").AppendLine(step);
                if (exception is not null)
                {
                    entry.Append("  Tipo errore: ").AppendLine(exception.GetType().Name);
                    entry.Append("  Descrizione: ").AppendLine(exception.Message);
                    entry.AppendLine("  Dettagli tecnici (uso locale, possono contenere percorsi):");
                    entry.AppendLine(exception.ToString());
                }
                File.AppendAllText(path, entry.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging is diagnostic, never essential for app availability.
        }
    }
}
