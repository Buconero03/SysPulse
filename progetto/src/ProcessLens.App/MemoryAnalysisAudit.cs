using ProcessLens.Core;
using System.IO;
using System.Text;

namespace ProcessLens.App;

/// <summary>Transparent audit of actual measurement stages. No paths, arguments or memory content.</summary>
internal sealed class MemoryAnalysisAudit
{
    private readonly int _pid;
    private readonly string _operationId = Guid.NewGuid().ToString("N");
    private readonly List<string> _entries = new();
    private static readonly object FileGate = new();
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProcessLens", "logs");
    public static string LogPath => Path.Combine(Root, "memory-analysis.log");
    public string Display => string.Join(Environment.NewLine, _entries) +
        Environment.NewLine + "Registro locale: " + LogPath;

    public MemoryAnalysisAudit(int pid)
    {
        _pid = pid;
        Add("INIZIO", "Richiesta utente; contenuto memoria non letto.");
    }

    public void Add(string stage, string status)
    {
        // stage/status are generated exclusively by the program, never raw command lines.
        var timestamp = DateTime.Now;
        string line = AuditLogPresentation.FormatRamEntry(timestamp, stage, status);
        _entries.Add(line);
        string sanitizedStage = System.Text.RegularExpressions.Regex.Replace(stage.ToUpperInvariant(), @"[^A-Z0-9 ]", "");
        if (sanitizedStage.Length > 32) sanitizedStage = sanitizedStage[..32];
        StructuredDiagnostics.Write("memory-analysis", new DiagnosticEvent
        {
            EventId = DiagnosticEventIds.RamMeasurement,
            Component = "ProcessLens.App", Operation = "Memory.AuditStep",
            Severity = "Information",
            Status = "Observed",
            Stage = sanitizedStage,
            ReasonCode = "RAM_AUDIT_STEP", Api = "WindowsMemoryAPIs",
            OperationId = _operationId, TargetPid = _pid
        });
        try
        {
            lock (FileGate)
            {
                Directory.CreateDirectory(Root);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1024 * 1024)
                    File.Move(LogPath, LogPath + ".old", overwrite: true);
                File.AppendAllText(LogPath,
                    $"[{timestamp:O}] [RAM] {stage}: PID={_pid}; {status}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch (IOException) { } // Never break the user-facing diagnosis to write a log.
        catch (UnauthorizedAccessException) { }
    }
}
