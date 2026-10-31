using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;
using ProcessLens.Core;

namespace ProcessLens.Windows;

public sealed record CaptureCommandResult(int ExitCode, string Output, string Error, bool Interrupted)
{
    public bool Success => ExitCode == 0 && !Interrupted;
}
public interface ICaptureCommandRunner
{
    Task<CaptureCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, int seconds, CancellationToken token);
}

public sealed class CaptureCommandRunner : ICaptureCommandRunner
{
    public async Task<CaptureCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, int seconds, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var child = new Process { StartInfo = new ProcessStartInfo(executable) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        }};
        foreach (string argument in arguments) child.StartInfo.ArgumentList.Add(argument);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(seconds));
        var output = new StringBuilder(); var error = new StringBuilder();
        bool started = false, interrupted = false; int overflow = 0;
        async Task ReadAsync(StreamReader reader, StringBuilder destination)
        {
            char[] buffer = new char[4096];
            try
            {
                int count;
                while ((count = await reader.ReadAsync(buffer.AsMemory(), budget.Token)) > 0)
                {
                    int remaining = PacketCapturePolicy.MaximumTextChars - destination.Length;
                    destination.Append(buffer, 0, Math.Min(remaining, count));
                    if (count > remaining) { Interlocked.Exchange(ref overflow, 1); budget.Cancel(); break; }
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) when (budget.IsCancellationRequested) { }
        }
        try
        {
            if (!child.Start()) return new(-1, "", "Avvio dello strumento non riuscito.", false);
            started = true;
            Task stdout = ReadAsync(child.StandardOutput, output), stderr = ReadAsync(child.StandardError, error);
            try { await child.WaitForExitAsync(budget.Token); }
            catch (OperationCanceledException)
            {
                interrupted = true;
                if (!child.HasExited) child.Kill(); // Only the exact child started above, never a process-name lookup.
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await child.WaitForExitAsync(stop.Token);
            }
            await Task.WhenAll(stdout, stderr);
            string reason = token.IsCancellationRequested ? "Operazione interrotta." : overflow != 0 ? "Output oltre il limite." : interrupted ? "Tempo massimo superato." : "";
            return new(child.ExitCode, output.ToString(), reason + (reason.Length > 0 ? "\n" : "") + error, interrupted || overflow != 0);
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException or OperationCanceledException)
        { return new(-1, "", "Strumento non disponibile: " + ex.Message, true); }
        finally
        {
            budget.Cancel();
            if (started)
                try { if (!child.HasExited) child.Kill(); }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
        }
    }
}

public sealed class WiresharkTools : IPacketCaptureClient
{
    private readonly ICaptureCommandRunner _runner;
    private readonly HashSet<string> _verifiedMonitor = new(StringComparer.Ordinal);
    public WiresharkTools(ICaptureCommandRunner? runner = null) => _runner = runner ?? new CaptureCommandRunner();
    private static string Stamp(CaptureTools tools, CaptureInterface adapter)
    {
        var file = new FileInfo(tools.Dumpcap);
        return tools.Dumpcap + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks + "|" + adapter.Name;
    }
    private static void ValidateTools(CaptureTools tools)
    {
        foreach (var (path, name) in new[] { (tools.Dumpcap, "dumpcap.exe"), (tools.Tshark, "tshark.exe") })
            if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || !File.Exists(path) ||
                !Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetDirectoryName(path), Path.GetDirectoryName(tools.Dumpcap), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Seleziona dumpcap.exe nella cartella di installazione di Wireshark, con tshark.exe accanto.");
    }
    public async Task<CaptureDiscovery> DiscoverAsync(string? dumpcapPath, CancellationToken token)
    {
        _verifiedMonitor.Clear();
        if (dumpcapPath is null)
        {
            dumpcapPath = new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
                .Select(f => Path.Combine(Environment.GetFolderPath(f), "Wireshark", "dumpcap.exe"))
                .FirstOrDefault(File.Exists);
        }
        if (dumpcapPath is null) return new(null, Array.Empty<CaptureInterface>(), "Wireshark non rilevato. Installa Wireshark con TShark e Npcap, poi scegli Verifica strumenti.");
        string directory = Path.GetDirectoryName(Path.GetFullPath(dumpcapPath))!;
        string npcap = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Npcap", "wpcap.dll");
        var tools = new CaptureTools(Path.GetFullPath(dumpcapPath), Path.Combine(directory, "tshark.exe"), Path.Combine(directory, "Wireshark.exe"),
            File.Exists(npcap) ? "File Npcap rilevati; il supporto monitor va verificato per scheda e driver." : "File Npcap non rilevati nel percorso standard: verifica l’installazione.");
        ValidateTools(tools);
        var result = await _runner.RunAsync(tools.Dumpcap, new[] { "-D" }, 10, token);
        return new(tools, result.Success ? PacketCapturePolicy.ParseInterfaces(result.Output) : Array.Empty<CaptureInterface>(),
            tools.DriverNote + "\n" + (result.Success ? "Elenco interfacce locali aggiornato." : "Elenco interfacce non disponibile: " + result.Error));
    }
    public async Task<CaptureCheck> CheckMonitorAsync(CaptureTools tools, CaptureInterface adapter, CancellationToken token)
    {
        ValidateTools(tools);
        if (!PacketCapturePolicy.LocalInterface(adapter.Name)) throw new ArgumentException("Interfaccia locale non valida.");
        string stamp = Stamp(tools, adapter); _verifiedMonitor.Remove(stamp);
        var result = await _runner.RunAsync(tools.Dumpcap, new[] { "-i", adapter.Name, "-I", "-L" }, 10, token);
        bool supported = result.Success && PacketCapturePolicy.HasMonitorLinkType(result.Output);
        if (supported) _verifiedMonitor.Add(stamp);
        return new(supported, supported ? "Lo strumento dichiara intestazioni 802.11 in modalità monitor. La ricezione va verificata con una cattura." :
            "Modalità monitor non verificata. Controlla driver, Npcap e permessi.\n" + result.Error + "\n" + result.Output);
    }
    public async Task<CaptureFileResult> CaptureAsync(CaptureTools tools, CaptureInterface adapter, bool monitor, string output, CancellationToken token)
    {
        ValidateTools(tools); token.ThrowIfCancellationRequested();
        if (monitor && !_verifiedMonitor.Contains(Stamp(tools, adapter)))
            throw new InvalidOperationException("Verifica la modalità monitor per questa interfaccia prima di acquisire.");
        string[] args = PacketCapturePolicy.CaptureArguments(adapter, monitor, output);
        // Reserve a NEW user-selected file; never overwrite an existing recording.
        using (File.Open(output, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        var result = await _runner.RunAsync(tools.Dumpcap, args, PacketCapturePolicy.Seconds + 10, token);
        long bytes = new FileInfo(output).Length;
        bool complete = result.Success && bytes > 0 && bytes <= PacketCapturePolicy.MaximumFileBytes;
        return new(output, complete, complete ? "Cattura terminata entro i limiti impostati.\n" + result.Error :
            "Cattura interrotta, non valida o parziale. Il file può essere incompleto.\n" + result.Error);
    }
    public async Task<PacketEvidence> AnalyzeAsync(CaptureTools tools, string path, CancellationToken token)
    {
        ValidateTools(tools);
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)) throw new IOException("Scegli un file locale.");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        if (file.Length == 0 || file.Length > PacketCapturePolicy.MaximumFileBytes)
            throw new IOException("File mancante, vuoto o oltre il limite di 12 MiB.");
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
        var result = await _runner.RunAsync(tools.Tshark, PacketCapturePolicy.AnalysisArguments(path), 15, token);
        if (!result.Success) throw new IOException("Analisi non disponibile o interrotta: " + result.Error);
        var evidence = PacketEvidenceParser.Parse(result.Output);
        return evidence with { Note = evidence.Note + "\nSHA-256 del file letto: " + hash };
    }
}
