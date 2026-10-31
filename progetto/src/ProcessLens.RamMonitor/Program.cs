using System.Diagnostics;
using ProcessLens.RamMonitor;

if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine(LogSession.FormatLine(DateTime.UtcNow, "Errore", "Windows richiesto; raccolta non avviata")); return 2; }
string root = "", gui = "", output = "";
int interval = 5, duration = 0, maxMiB = 50;
var additional = new List<string>();
try
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--help")
        {
            Console.WriteLine("ProcessLens.RamMonitor --project-root PATH [--gui-exe PATH] [--include-exe PATH] [--output-dir PATH] [--interval-seconds 5] [--duration-seconds 0] [--max-log-mib 50]");
            return 0;
        }
        string option = args[i];
        if (++i >= args.Length) throw new ArgumentException("Valore mancante: " + option);
        string value = args[i];
        switch (option)
        {
            case "--project-root": root = Path.GetFullPath(value); break;
            case "--gui-exe": gui = Path.GetFullPath(value); break;
            case "--include-exe": additional.Add(Path.GetFullPath(value)); break;
            case "--output-dir": output = Path.GetFullPath(value); break;
            case "--interval-seconds": interval = int.Parse(value); break;
            case "--duration-seconds": duration = int.Parse(value); break;
            case "--max-log-mib": maxMiB = int.Parse(value); break;
            default: throw new ArgumentException("Opzione sconosciuta: " + option);
        }
    }
    if (root.Length == 0 || !Directory.Exists(root)) throw new ArgumentException("--project-root deve indicare la cartella del progetto.");
    if (interval < 1 || interval > 60 || duration < 0 || maxMiB < 1 || maxMiB > 1024) throw new ArgumentException("Intervallo 1..60 s; durata >=0; limite log 1..1024 MiB.");
    var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
    if (gui.Length > 0)
    {
        if (!File.Exists(gui) || !Path.GetFileName(gui).Equals("ProcessLens.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("GUI attesa: percorso esistente di ProcessLens.exe.");
        string folder = Path.GetDirectoryName(gui)!;
        additional.Add(gui); additional.Add(Path.Combine(folder, "Profiler", "ProcessLensProfiler.exe"));
        var info = new DirectoryInfo(folder);
        // Standard published GUI layout: <project>/publish/win-x64/ProcessLens.exe.
        if (info.Name.Equals("win-x64", StringComparison.OrdinalIgnoreCase) && info.Parent?.Name.Equals("publish", StringComparison.OrdinalIgnoreCase) == true)
            roots.Add(info.Parent.Parent!.FullName);
    }
    foreach (string baseRoot in roots)
    {
        foreach (string config in new[] { "Release", "Debug" })
        {
            additional.Add(Path.Combine(baseRoot, "src", "ProcessLens.App", "bin", config, "net8.0-windows", "ProcessLens.exe"));
            additional.Add(Path.Combine(baseRoot, "src", "ProcessLens.Guardian.Service", "bin", config, "net8.0-windows", "ProcessLens.Guardian.Service.exe"));
            additional.Add(Path.Combine(baseRoot, "src", "ProcessLens.MemoryAgent", "bin", config, "net8.0-windows", "ProcessLens.MemoryAgent.exe"));
            additional.Add(Path.Combine(baseRoot, "src", "ProcessLens.Profiler", "bin", config, "net8.0-windows", "ProcessLensProfiler.exe"));
        }
        additional.Add(Path.Combine(baseRoot, "publish", "win-x64", "ProcessLens.exe"));
        additional.Add(Path.Combine(baseRoot, "publish", "win-x64", "Profiler", "ProcessLensProfiler.exe"));
        additional.Add(Path.Combine(baseRoot, "publish", "MemoryAgent-win-x64", "ProcessLens.MemoryAgent.exe"));
        additional.Add(Path.Combine(baseRoot, "dist", "Guardian-win-x64", "ProcessLens.Guardian.Service.exe"));
    }
    additional.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ProcessLens", "Guardian", "ProcessLens.Guardian.Service.exe"));
    additional.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProcessLens", "MemoryAgent", "ProcessLens.MemoryAgent.exe"));
    var scope = new MonitorScope(additional);
    if (output.Length == 0) output = Path.Combine(root, "verification-r10r8", "ram-monitor");
    var sampler = new NativeSampler(scope);
    using var log = new LogSession(output, scope, interval, maxMiB);
    using var cancel = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
    Console.WriteLine(LogSession.FormatLine(DateTime.UtcNow, "Avvio", "Ctrl+C arresta solo il monitor"));
    Console.WriteLine(LogSession.FormatLine(DateTime.UtcNow, "Log", log.DirectoryPath));
    Console.WriteLine(LogSession.FormatLine(DateTime.UtcNow, "Processi", "GUI, Guardian, MemoryAgent, profiler già attivi e discendenti identificati; nessun avvio automatico"));
    Console.WriteLine(LogSession.FormatLine(DateTime.UtcNow, "Ambito", "scope.json; consumo del monitor separato"));
    string status = "StoppedByUser";
    var elapsed = Stopwatch.StartNew();
    try
    {
        while (!cancel.IsCancellationRequested)
        {
            log.Append(sampler.Capture());
            if (log.LimitReached) { status = "LogSizeLimitReached"; Console.WriteLine(LogSession.FormatLine(DateTime.UtcNow, "Arresto", "limite log raggiunto; file conservati")); break; }
            if (duration > 0 && elapsed.Elapsed.TotalSeconds >= duration) { status = "DurationReached"; break; }
            int wait = duration > 0 ? (int)Math.Min(interval * 1000, Math.Max(0, (duration - elapsed.Elapsed.TotalSeconds) * 1000)) : interval * 1000;
            if (cancel.Token.WaitHandle.WaitOne(wait)) break;
        }
        log.SaveSummary(status);
        return 0;
    }
    catch (Exception ex)
    {
        try { log.SaveSummary("Failed:" + ex.GetType().Name); } catch { }
        throw;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(LogSession.FormatLine(DateTime.UtcNow, "Errore", ex.Message));
    return 1;
}
