using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ProcessLens.Core;

// Isolated on-demand .NET heap statistics collector. No resident service, injection,
// debugger attachment, persistent raw dumps or auto-downloads. Output is a bounded JSON report.
return await ProfilerHost.RunAsync(args);

internal sealed class ProfilerDiagnosticException(string reasonCode, string message) : Exception(message)
{
    public string ReasonCode { get; } = reasonCode;
}

internal static class ProfilerHost
{
    private const long MiB = 1024L * 1024;
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical,
        TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, ExtendedVirtual; }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    public static async Task<int> RunAsync(string[] args)
    {
        var options = ReadArguments(args);
        if (!options.TryGetValue("--output", out string? output) ||
            !options.TryGetValue("--pid", out string? rawPid) ||
            !int.TryParse(rawPid, out int pid) || pid <= 4 ||
            !options.TryGetValue("--start-utc-ticks", out string? rawTicks) ||
            !long.TryParse(rawTicks, out long expectedTicks) || expectedTicks <= 0 ||
            !options.TryGetValue("--mode", out string? mode) || mode != "managed-heap")
        {
            Console.Error.WriteLine("Uso: ProcessLensProfiler --mode managed-heap --pid N --start-utc-ticks N --output FILE.json");
            return 2;
        }
        // IPC destination must be an absolute .json path. The GUI creates its own unpredictable path.
        if (!Path.IsPathFullyQualified(output) ||
            !string.Equals(Path.GetExtension(output), ".json", StringComparison.OrdinalIgnoreCase))
            return 2;

        var result = new ProfilerResult { Pid = pid, Adapter = "dotnet-gcdump", CapturedUtc = DateTimeOffset.UtcNow };
        string? scratch = null;
        bool holdsSession = false;
        string reasonCode = "HEAP_STATISTICS_AVAILABLE";
        string operationId = options.TryGetValue("--session-guid", out string? sessionInput) &&
            Guid.TryParseExact(sessionInput, "N", out Guid checkedId)
            ? checkedId.ToString("N") : Guid.NewGuid().ToString("N");
        long? collectMs = null, reportMs = null, dumpBytes = null;
        int? toolExitCode = null;
        bool? cleanupSucceeded = null;
        Exception? failure = null;
        using var mutex = new Mutex(false, @"Local\ProcessLensProfiler.ManagedHeap.SingleSession.v1");
        try
        {
            try { holdsSession = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { holdsSession = true; }
            if (!holdsSession) throw new ProfilerDiagnosticException("PROFILER_SESSION_BUSY", "Una profilazione e gia in corso: una sessione per volta.");
            using var target = Process.GetProcessById(pid);
            if (target.Id == Environment.ProcessId)
                throw new ProfilerDiagnosticException("SELF_PROFILING_BLOCKED", "Non eseguo la raccolta sul profiler stesso.");
            DateTime start = target.StartTime.ToUniversalTime();
            if (start.Ticks != expectedTicks)
                throw new ProfilerDiagnosticException("TARGET_IDENTITY_MISMATCH", "PID riutilizzato o identita del processo cambiata: annullato.");

            string runtime = DetectRuntime(target);
            result = result with { Runtime = runtime };
            if (runtime != ".NET CLR")
                throw new NotSupportedException(runtime == "sconosciuto"
                    ? "Impossibile verificare il runtime: moduli non accessibili o assenti. Non effettuo una profilazione invasiva."
                    : "Il runtime rilevato non e .NET CLR; il relativo adapter non e ancora disponibile.");

            var ram = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            if (!GlobalMemoryStatusEx(ref ram) || ram.TotalPhysical == 0)
                throw new ProfilerDiagnosticException("MEMORY_READ_UNAVAILABLE", "Pressione RAM non misurabile; acquisizione annullata.");
            double used = 100d * (ram.TotalPhysical - Math.Min(ram.TotalPhysical, ram.AvailablePhysical)) / ram.TotalPhysical;
            if (!ProfilerPolicy.SafeToCollect(used))
                throw new ProfilerDiagnosticException("MEMORY_SAFETY_GATE", $"RAM di sistema al {used:F1}%: acquisizione rifiutata oltre la soglia 85%.");

            string? tool = FindGcDumpTool();
            if (tool is null)
                throw new ProfilerDiagnosticException("TOOL_NOT_FOUND", "dotnet-gcdump non installato. Eseguire volontariamente: dotnet tool install --global dotnet-gcdump");

            string? sessionId = options.GetValueOrDefault("--session-guid");
            if (sessionId is not null && Guid.TryParseExact(sessionId, "N", out var validSession))
                scratch = Path.Combine(Path.GetTempPath(), "ProcessLensProfiler-" + validSession.ToString("N"));
            else
                scratch = Path.Combine(Path.GetTempPath(), "ProcessLensProfiler-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            string dump = Path.Combine(scratch, "capture.gcdump");
            // Full Gen2 GC in target: user has explicitly approved this from ProcessLens.
            var collectWatch = Stopwatch.StartNew();
            var collect = await RunToolAsync(tool, ["collect", "--process-id", pid.ToString(), "--output", dump,
                "--timeout", ProfilerPolicy.MaxSeconds.ToString()], dump);
            collectMs = collectWatch.ElapsedMilliseconds;
            toolExitCode = collect.ExitCode;
            if (collect.ExitCode != 0 || !File.Exists(dump))
                throw new ProfilerDiagnosticException(
                    collect.Reason == "timeout di acquisizione" ? "CAPTURE_TIMEOUT" :
                    collect.Reason == "helper oltre il budget RAM configurato" ? "HELPER_MEMORY_LIMIT" :
                    collect.Reason == "snapshot oltre budget disco" ? "SNAPSHOT_SIZE_LIMIT" : "GC_CAPTURE_FAILED",
                    "Raccolta GC non riuscita o limitata dalle protezioni: " + collect.Reason);
            dumpBytes = new FileInfo(dump).Length;
            if (dumpBytes > ProfilerPolicy.MaxGcDumpFileMiB * MiB)
                throw new ProfilerDiagnosticException("SNAPSHOT_SIZE_LIMIT", "Snapshot oltre budget su disco; acquisizione non pubblicata.");
            if (target.HasExited || target.StartTime.ToUniversalTime().Ticks != expectedTicks)
                throw new ProfilerDiagnosticException("TARGET_EXITED_OR_CHANGED", "Il target e terminato o cambiato durante l'acquisizione.");

            var reportWatch = Stopwatch.StartNew();
            var report = await RunToolAsync(tool, ["report", dump], null);
            reportMs = reportWatch.ElapsedMilliseconds;
            toolExitCode = report.ExitCode;
            if (report.ExitCode != 0)
                throw new ProfilerDiagnosticException(
                    report.Reason == "timeout di acquisizione" ? "REPORT_TIMEOUT" :
                    report.Reason == "helper oltre il budget RAM configurato" ? "HELPER_MEMORY_LIMIT" : "HEAP_REPORT_FAILED",
                    "Report heap non disponibile: " + report.Reason);
            var heap = GcDumpHeapStatParser.Parse(report.Output, 20);
            if (heap.HeapBytes <= 0 || heap.TopTypes.Count == 0)
                throw new ProfilerDiagnosticException("HEAP_REPORT_PARSE_FAILED", "Output heapstat senza righe riconoscibili: nessuna stima inventata.");

            result = result with { Status = "ok", Complete = true, ManagedHeapBytes = heap.HeapBytes,
                ManagedObjects = heap.HeapObjects, TopTypes = heap.TopTypes,
                Evidence = "dotnet-gcdump: GC heap graph e heapstat, non RAM fisica/working set.",
                Detail = "Statistiche effettive sui tipi gestiti dopo una raccolta GC Gen2. " +
                    "Oggetti viventi osservati; non identificano da soli cache, leak, stack di allocazione o tutta la RAM del processo." };
        }
        catch (Exception ex)
        {
            failure = ex;
            reasonCode = ex is ProfilerDiagnosticException identified ? identified.ReasonCode
                : ex is NotSupportedException ? "RUNTIME_NOT_SUPPORTED"
                : ex is UnauthorizedAccessException ? "TARGET_ACCESS_DENIED" : "PROFILER_UNEXPECTED_EXCEPTION";
            result = result with { Status = "unavailable", Complete = false,
                Detail = ex is ProfilerDiagnosticException or FileNotFoundException or NotSupportedException or InvalidOperationException
                    ? ex.Message : "Analisi non completata: " + ex.GetType().Name,
                Evidence = "Nessun risultato di heap inventato; consultare disponibilita dell'adapter." };
        }
        finally
        {
            if (scratch is not null)
            {
                try { Directory.Delete(scratch, recursive: true); cleanupSucceeded = true; }
                catch { cleanupSucceeded = false; } // best effort
            }
            if (holdsSession)
                try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        result = result with
        {
            ReasonCode = reasonCode, OperationId = operationId, ToolExitCode = toolExitCode,
            CaptureDurationMs = collectMs, ReportDurationMs = reportMs,
            TemporaryFilesRemoved = cleanupSucceeded
        };
        StructuredDiagnostics.Write("profiler", new DiagnosticEvent
        {
            EventId = result.Status == "ok" ? DiagnosticEventIds.ProfilerCompleted : DiagnosticEventIds.ProfilerUnavailable,
            Component = "ProcessLens.Profiler", Operation = "ManagedHeap.CaptureAndReport",
            Api = "dotnet-gcdump/EventPipe", TargetPid = pid, OperationId = operationId,
            Severity = result.Status == "ok" ? "Information" : reasonCode is "MEMORY_SAFETY_GATE" or "MEMORY_READ_UNAVAILABLE" ? "Warning" : "Error",
            Status = result.Status == "ok" ? "Completed"
                : reasonCode is "MEMORY_SAFETY_GATE" or "TARGET_IDENTITY_MISMATCH" or "PROFILER_SESSION_BUSY" ? "Blocked"
                : reasonCode is "RUNTIME_NOT_SUPPORTED" or "TOOL_NOT_FOUND" ? "Unavailable" : "Failed",
            ReasonCode = reasonCode, Complete = result.Complete, NativeExitCode = toolExitCode,
            DurationMs = collectMs.HasValue || reportMs.HasValue ? (collectMs ?? 0) + (reportMs ?? 0) : null,
            ExceptionType = failure?.GetType().Name,
            HResultHex = failure is null ? null : "0x" + failure.HResult.ToString("X8"),
            Measurements = BuildMeasurements(collectMs, reportMs, dumpBytes, result, cleanupSucceeded)
        });
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result,SysPulseLocalization.ReportJsonOptions()));
            return result.Status == "ok" ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Impossibile produrre il report: " + ex.GetType().Name);
            return 3;
        }
    }

    private static IReadOnlyDictionary<string, double> BuildMeasurements(
        long? collectMs, long? reportMs, long? dumpBytes, ProfilerResult result, bool? cleanupSucceeded)
    {
        var values = new Dictionary<string, double>();
        if (collectMs.HasValue) values["collect_duration_ms"] = collectMs.Value;
        if (reportMs.HasValue) values["report_duration_ms"] = reportMs.Value;
        if (dumpBytes.HasValue) values["capture_file_bytes"] = dumpBytes.Value;
        if (result.Complete) values["managed_heap_bytes"] = result.ManagedHeapBytes;
        if (cleanupSucceeded.HasValue) values["cleanup_succeeded"] = cleanupSucceeded.Value ? 1 : 0;
        return values;
    }

    private static Dictionary<string,string> ReadArguments(string[] args)
    {
        var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i + 1 < args.Length; i += 2)
            if (args[i].StartsWith("--", StringComparison.Ordinal)) result[args[i]] = args[i + 1];
        return result;
    }

    private static string DetectRuntime(Process target)
    {
        try
        {
            var names = target.Modules.Cast<ProcessModule>().Select(m => m.ModuleName.ToLowerInvariant()).ToHashSet();
            if (names.Contains("coreclr.dll") || names.Contains("clr.dll")) return ".NET CLR";
            if (names.Any(s => s.StartsWith("python") && s.EndsWith(".dll"))) return "Python";
            if (names.Contains("jvm.dll")) return "Java";
            if (names.Contains("node.dll") || names.Contains("v8.dll")) return "V8/Node";
            return "nativo/non classificato";
        }
        catch { return "sconosciuto"; }
    }

    private static string? FindGcDumpTool()
    {
        string name = "dotnet-gcdump.exe";
        var found = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => { try { return Path.Combine(s.Trim('"'), name); } catch { return ""; } })
            .FirstOrDefault(File.Exists);
        if (found is not null) return found;
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string global = Path.Combine(profile, ".dotnet", "tools", name);
        return File.Exists(global) ? global : null;
    }

    private sealed record ToolResult(int ExitCode, string Output, string Reason);
    private sealed record BoundedText(string Text, bool Truncated);

    private static async Task<ToolResult> RunToolAsync(string executable, IReadOnlyList<string> parameters, string? dumpFile)
    {
        using var child = new Process();
        child.StartInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string parameter in parameters) child.StartInfo.ArgumentList.Add(parameter);
        child.Start();
        // Drain both streams concurrently or the tool might block on full stdout pipes.
        Task<BoundedText> stdout = ReadBoundedAsync(child.StandardOutput, 2 * 1024 * 1024);
        Task<BoundedText> stderr = ReadBoundedAsync(child.StandardError, 128 * 1024);
        string? reason = null;
        var watch = Stopwatch.StartNew();
        while (!child.HasExited)
        {
            await Task.Delay(250);
            try
            {
                child.Refresh();
                if (child.WorkingSet64 > ProfilerPolicy.MaxHelperWorkingSetMiB * MiB)
                    reason = "helper oltre il budget RAM configurato";
                if (dumpFile is not null && File.Exists(dumpFile) &&
                    new FileInfo(dumpFile).Length > ProfilerPolicy.MaxGcDumpFileMiB * MiB)
                    reason = "snapshot oltre budget disco";
                if (watch.Elapsed > TimeSpan.FromSeconds(ProfilerPolicy.MaxSeconds))
                    reason = "timeout di acquisizione";
                if (reason is not null) { child.Kill(entireProcessTree: true); break; }
            }
            catch (InvalidOperationException) { break; } // Process exited while checked.
        }
        await child.WaitForExitAsync();
        var outText = await stdout;
        var errText = await stderr;
        // Silent truncation would make totals inferred from v10 rows materially wrong.
        if (outText.Truncated || errText.Truncated)
            return new ToolResult(-1, "", "output del tool oltre il budget diagnostico");
        // Do not write child output/errors verbatim to report (may include sensitive paths).
        return new ToolResult(reason is null ? child.ExitCode : -1, outText.Text,
            reason ?? (child.ExitCode == 0 ? "completato" : "errore tool, exit code " + child.ExitCode));
    }

    private static async Task<BoundedText> ReadBoundedAsync(StreamReader reader, int maximum)
    {
        char[] buffer = new char[4096];
        var result = new System.Text.StringBuilder();
        bool truncated = false;
        while (true)
        {
            int count = await reader.ReadAsync(buffer);
            if (count == 0) break;
            int available = Math.Max(0, maximum - result.Length);
            int kept = Math.Min(count, available);
            if (kept > 0) result.Append(buffer, 0, kept);
            if (kept < count) truncated = true;
        }
        return new BoundedText(result.ToString(), truncated);
    }
}
