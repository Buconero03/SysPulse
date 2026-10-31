using System.ComponentModel;
using System.Diagnostics;
using ProcessLens.Core;

namespace ProcessLens.Windows;

// Invoked only by the explicit Run action. No command history, shell wrapper, monitoring or child termination.
public sealed class WindowsNewTaskLauncher : INewTaskLauncher
{
    internal static ProcessStartInfo CreateStartInfo(NewTaskRequest request) => new()
    {
        FileName = request.Program, Arguments = request.Arguments, WorkingDirectory = request.WorkingDirectory,
        UseShellExecute = false, ErrorDialog = false, CreateNoWindow = false
    };
    public NewTaskResult Start(NewTaskRequest request)
    {
        // Revalidate public inputs; do not rely on the UI being the sole caller.
        if (!NewTaskRequest.TryCreate(request.Program, request.Arguments, request.WorkingDirectory, out var validated, out string error, expandEnvironment: false))
            return new(false, null, error);
        if (validated!.WorkingDirectory.Length > 0 && !Directory.Exists(validated.WorkingDirectory))
            return new(false, null, "Cartella iniziale non trovata o non accessibile.");
        try
        {
            using var process = Process.Start(CreateStartInfo(validated));
            return process is null ? new(false, null, "Avvio non confermato da Windows.")
                : new(true, process.Id, $"Attività avviata · PID {process.Id}.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
        { return Failure(ex); }
    }
    public static NewTaskResult Failure(Exception error)
    {
        // Raw exception messages can contain the command line: retain the error code, not those arguments.
        int? code = (error as Win32Exception)?.NativeErrorCode;
        string message = code switch {
            2 or 3 => "Programma o percorso non trovato.",
            5 => "Accesso negato al programma.",
            193 or 216 => "Formato del programma non compatibile con Windows o con questa architettura.",
            740 => "Il programma richiede privilegi di amministratore.",
            1223 => "Avvio annullato.",
            _ => "Impossibile avviare il programma. Controlla programma, argomenti e cartella iniziale."
        };
        if (code.HasValue) message += $" Codice Windows: {code.Value}.";
        return new(false, null, message);
    }
}
