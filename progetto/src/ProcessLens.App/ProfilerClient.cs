using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using ProcessLens.Core;

namespace ProcessLens.App;

/// <summary>Launches the separate profiler executable only on explicit user action.
/// GUI never loads heap dumps and never imports profiling libraries.</summary>
internal static class ProfilerClient
{
    public static async Task<ProfilerResult> CaptureAsync(int pid, DateTime startTime,
        CancellationToken cancel)
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "Profiler", "ProcessLensProfiler.exe");
        if (!File.Exists(executable))
            return new ProfilerResult { Pid = pid, Status = "unavailable", Adapter = "dotnet-gcdump",
                Detail = "Eseguibile separato non trovato nella distribuzione. Ripetere build-dashboard.ps1." };

        string session = Guid.NewGuid().ToString("N");
        StructuredDiagnostics.Write("startup", new DiagnosticEvent
        {
            EventId = DiagnosticEventIds.ProfilerRequested,
            Component = "ProcessLens.App", Operation = "ManagedHeap.Request",
            Severity = "Information", Status = "Started", Api = "ProcessLensProfiler",
            OperationId = session, TargetPid = pid, ReasonCode = "EXPLICIT_USER_REQUEST"
        });
        string sessionDir = Path.Combine(Path.GetTempPath(), "ProcessLensProfiler-" + session);
        Directory.CreateDirectory(sessionDir);
        string path = Path.Combine(sessionDir, "result.json");
        using var child = new Process();
        child.StartInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        foreach (string arg in new[] {
            "--mode", "managed-heap", "--pid", pid.ToString(), "--start-utc-ticks",
            startTime.ToUniversalTime().Ticks.ToString(), "--output", path, "--session-guid", session
        }) child.StartInfo.ArgumentList.Add(arg);
        try
        {
            child.Start();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(TimeSpan.FromSeconds(ProfilerPolicy.MaxSeconds + 12));
            try { await child.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
                return new ProfilerResult { Pid = pid, Status = cancel.IsCancellationRequested ? "cancelled" : "timeout",
                    Detail = cancel.IsCancellationRequested ? "Sessione annullata dall'utente." : "Profilazione scaduta, processo separato terminato." };
            }
            if (!File.Exists(path))
                return new ProfilerResult { Pid = pid, Status = "unavailable",
                    Detail = "Il profiler separato non ha prodotto un report verificabile." };
            // Only small JSON summaries cross into the GUI; all dumps are deleted by the worker.
            if (new FileInfo(path).Length > 256 * 1024)
                return new ProfilerResult { Pid = pid, Status = "unavailable", Detail = "Report oltre budget IPC." };
            var result = JsonSerializer.Deserialize<ProfilerResult>(await File.ReadAllTextAsync(path, cancel));
            return result ?? new ProfilerResult { Pid = pid, Status = "unavailable", Detail = "Report non interpretabile." };
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            return new ProfilerResult { Pid = pid, Status = "unavailable", Detail = "Avvio non disponibile: " + ex.GetType().Name };
        }
        finally
        {
            try { if (child.Id != 0 && !child.HasExited) child.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* no child started */ }
            catch (System.ComponentModel.Win32Exception) { /* child exited during cleanup */ }
            try { Directory.Delete(sessionDir, recursive: true); } catch { /* may remain after crash */ }
        }
    }

    public static string ToItalianText(ProfilerResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PROFILAZIONE .NET ISOLATA — RISULTATI E LIMITI");
        sb.AppendLine($"PID {result.Pid} · adapter {result.Adapter} · runtime {result.Runtime}");
        if (result.Status != "ok")
        {
            sb.AppendLine("ESITO: " + (result.Status switch
            {
                "cancelled" => "ANNULLATA DALL'UTENTE",
                "timeout" => "TEMPO MASSIMO SUPERATO",
                _ => "DATI NON DISPONIBILI O INCOMPLETI"
            }));
            if (!string.IsNullOrWhiteSpace(result.ReasonCode))
                sb.AppendLine("Codice diagnostico: " + result.ReasonCode);
            if (!string.IsNullOrWhiteSpace(result.OperationId))
                sb.AppendLine("Identificativo acquisizione: " + result.OperationId);
            if (result.ToolExitCode.HasValue)
                sb.AppendLine("Codice uscita dotnet-gcdump: " + result.ToolExitCode.Value);
            sb.AppendLine("Motivo: " + result.Detail);
            sb.AppendLine("Azione suggerita: verificare il processo, i permessi e il tool con profiler-status.ps1; riprovare solo su un PID .NET di test.");
            sb.AppendLine("Nessuna memoria interna attribuita senza misurazione.");
            return sb.ToString();
        }
        sb.AppendLine("ESITO: STATISTICHE HEAP DISPONIBILI");
        if (!string.IsNullOrWhiteSpace(result.OperationId)) sb.AppendLine("Identificativo acquisizione: " + result.OperationId);
        if (result.CaptureDurationMs.HasValue) sb.AppendLine("Durata raccolta: " + result.CaptureDurationMs.Value + " ms");
        if (result.ReportDurationMs.HasValue) sb.AppendLine("Durata analisi: " + result.ReportDurationMs.Value + " ms");
        sb.AppendLine($"Heap GC gestito: {result.ManagedHeapBytes / 1048576d:F1} MiB | Oggetti rilevati: {result.ManagedObjects:N0}");
        sb.AppendLine("TIPI CON MAGGIORE MEMORIA NELL'HEAP GC:");
        foreach (var t in result.TopTypes.Take(15))
            sb.AppendLine($"• {t.TypeName}: {t.Bytes / 1048576d:F2} MiB, {t.Count:N0} oggetti");
        sb.AppendLine();
        sb.AppendLine("INTERPRETAZIONE: i tipi elencati sono realmente presenti nell'heap GC catturato; " +
            "non rappresentano tutta la RAM fisica. Non dimostrano che si tratti di cache, leak o allocazioni inutili.");
        sb.AppendLine("Per capire perché rimangono in vita occorre analizzare radici e riferimenti " +
            "o confrontare acquisizioni distinte, con il consenso dell'utente.");
        return sb.ToString();
    }
}
