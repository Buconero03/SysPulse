using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ProcessLens.RamMonitor;

public sealed class LogSession : IDisposable
{
    private readonly StreamWriter _text, _totals, _processes;
    private readonly long _limit;
    private int _samples, _completeSamples;
    private long? _privatePeak;
    private decimal _privateSum;
    public string DirectoryPath { get; }
    public LogSession(string parent, MonitorScope scope, int interval, int maxMiB)
    {
        DirectoryPath = Path.Combine(parent, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Environment.ProcessId);
        Directory.CreateDirectory(DirectoryPath);
        _limit = (long)maxMiB * 1048576;
        _text = Writer("monitor.log"); _totals = Writer("totals.csv"); _processes = Writer("processes.csv");
        _totals.WriteLine("BeginUtc,EndUtc,EnumerationComplete,IncludedPids,UnresolvedCandidates,OutsideScope,PrivateCompleteBytes,PrivateObservedBytes,PrivateAvailable,WorkingSetCompleteBytes,WorkingSetObservedBytes,WorkingSetAvailable,CommitCompleteBytes,CommitObservedBytes,CommitAvailable,MonitorPrivateBytes,MonitorWorkingSetBytes");
        _processes.WriteLine("SampleUtc,ReadUtc,PID,ParentPID,HandleParentPID,StartUtcTicks,Name,Path,Association,Included,PrivateResidentBytes,WorkingSetBytes,PrivateCommitBytes,Errors");
        File.WriteAllText(Path.Combine(DirectoryPath, "scope.json"), JsonSerializer.Serialize(new
        {
            Schema = "ProcessLens.RamMonitor.v1", StartedUtc = DateTime.UtcNow, MeterPid = Environment.ProcessId,
            Paths = scope.Paths.Order().ToArray(), IntervalSeconds = interval, MaxLogMiB = maxMiB,
            Rules = "Percorsi esatti e discendenti identificati tramite PID e orario di avvio; inclusi i discendenti già osservati ancora attivi.",
            Limit = "Campioni periodici non simultanei: possibili esclusioni di processi brevi, orfani precedenti, altre installazioni e componenti ospitati da dotnet.",
            MainMetric = "Somma dei Working Set privati dei PID identificati. Totale della memoria fisica condivisa non misurato.",
            WorkingSet = "La somma può duplicare pagine condivise. Commit distinto dalla RAM fisica. Monitor escluso."
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        Flush(); SaveSummary("Running");
    }
    private StreamWriter Writer(string name) => new(Path.Combine(DirectoryPath, name), false, new UTF8Encoding(false)) { AutoFlush = false };
    private static string Csv(object? value) => "\"" + (value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
    public static string FormatLine(DateTime timestamp, string name, string value) =>
        $"[{timestamp.ToUniversalTime():O}] [RAM Monitor] {name}: {value}";
    public static string MiB(long? value) => value.HasValue ? (value.Value / 1048576d).ToString("F1", CultureInfo.CurrentCulture) + " MiB" : "N/D";
    private static string Describe(MetricTotal metric)
    {
        if (metric.Complete) return MiB(metric.CompleteBytes) + $" · contatori {metric.Available}/{metric.Included}";
        return $"N/D · parziale · somma disponibile {MiB(metric.ObservedBytes)} · contatori {metric.Available}/{metric.Included}";
    }
    public void Append(CaptureResult capture)
    {
        var total = SampleTotals.Aggregate(capture.Processes, capture.EnumerationComplete);
        _samples++;
        if (total.PrivateResident.CompleteBytes is long bytes)
        {
            _completeSamples++; _privateSum += bytes;
            _privatePeak = Math.Max(_privatePeak ?? 0, bytes);
        }
        var block = new StringBuilder();
        void Line(string name, string value) => block.AppendLine(FormatLine(capture.EndUtc, name, value));
        Line("Campione", _samples.ToString(CultureInfo.InvariantCulture));
        Line("Working Set privato totale", Describe(total.PrivateResident));
        Line("Ambito", $"PID inclusi={total.IncludedProcesses}; non identificati={total.UnresolvedCandidates}; esclusi={total.OutsideScope}");
        foreach (var row in capture.Processes)
        {
            block.AppendLine(FormatLine(row.ReadUtc, $"PID {row.Pid} · {row.Name}",
                $"stato={(row.Included ? "INCLUSO" : row.Association)}; WS privato={MiB(row.PrivateResidentBytes)}; WS totale={MiB(row.WorkingSetBytes)}; commit={MiB(row.PrivateCommitBytes)}"));
            Line($"PID {row.Pid} · identità", $"origine={row.Association}; avvio UTC ticks={row.StartUtcTicks?.ToString(CultureInfo.InvariantCulture) ?? "N/D"}; percorso={row.Path ?? "N/D"}");
            if (row.Errors.Length > 0) Line($"PID {row.Pid} · errori", row.Errors);
        }
        Line("Working Set totale sommato", Describe(total.WorkingSet) + "; può contare più volte le pagine condivise");
        Line("Commit privato", Describe(total.PrivateCommit) + "; non RAM fisica");
        Line("Monitor escluso", $"PID={capture.Monitor.Pid}; WS privato={MiB(capture.Monitor.PrivateResidentBytes)}; WS totale={MiB(capture.Monitor.WorkingSetBytes)}");
        if (!capture.EnumerationComplete) Line("Enumerazione parziale/fallita", capture.EnumerationError);
        Line("Intervallo UTC", $"{capture.BeginUtc:O} -> {capture.EndUtc:O}; letture non simultanee");
        Line("Copertura", "PID identificati; memoria fisica condivisa complessiva non misurata");
        _text.WriteLine(block);
        Console.WriteLine(block);
        object?[] line = { capture.BeginUtc.ToString("O"), capture.EndUtc.ToString("O"), capture.EnumerationComplete,
            total.IncludedProcesses, total.UnresolvedCandidates, total.OutsideScope,
            total.PrivateResident.CompleteBytes, total.PrivateResident.ObservedBytes, total.PrivateResident.Available,
            total.WorkingSet.CompleteBytes, total.WorkingSet.ObservedBytes, total.WorkingSet.Available,
            total.PrivateCommit.CompleteBytes, total.PrivateCommit.ObservedBytes, total.PrivateCommit.Available,
            capture.Monitor.PrivateResidentBytes, capture.Monitor.WorkingSetBytes };
        _totals.WriteLine(string.Join(",", line.Select(Csv)));
        foreach (var row in capture.Processes.Append(capture.Monitor))
        {
            object?[] values = { capture.EndUtc.ToString("O"), row.ReadUtc.ToString("O"), row.Pid, row.ParentPid, row.HandleParentPid,
                row.StartUtcTicks, row.Name, row.Path, row.Association, row.Included,
                row.PrivateResidentBytes, row.WorkingSetBytes, row.PrivateCommitBytes, row.Errors };
            _processes.WriteLine(string.Join(",", values.Select(Csv)));
        }
        Flush(); SaveSummary("Running");
    }
    public bool LimitReached => _text.BaseStream.Length + _totals.BaseStream.Length + _processes.BaseStream.Length >= _limit;
    private void Flush() { _text.Flush(); _totals.Flush(); _processes.Flush(); }
    public void SaveSummary(string status)
    {
        string path = Path.Combine(DirectoryPath, "summary.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new
        {
            Status = status, UpdatedUtc = DateTime.UtcNow, Samples = _samples, PrivateCompleteSamples = _completeSamples,
            MeanCompletePrivateBytes = _completeSamples > 0 ? (decimal?)(_privateSum / _completeSamples) : null,
            SampledPeakCompletePrivateBytes = _privatePeak,
            Note = "Media e picco sui soli campioni completi di Working Set privato. N/D non equivale a zero. Possibili picchi tra campioni. Monitor escluso."
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(path + ".tmp", path, true);
    }
    public void Dispose() { _text.Dispose(); _totals.Dispose(); _processes.Dispose(); }
}
