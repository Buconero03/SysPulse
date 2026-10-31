using System.Globalization;

namespace ProcessLens.Core;

public sealed record SystemProcessEntry(int Pid, int ParentPid, string Name, uint? Threads,
    uint? Handles, uint? Session, long? WorkingSetBytes, string? Path = null,
    DateTime? StartUtc = null, string Access = "Dettagli non letti");
public sealed record SystemMemorySnapshot(long? PhysicalTotal, long? PhysicalAvailable,
    long? Commit, long? CommitLimit, long? KernelPaged, long? KernelNonpaged, string Note);
public sealed record SystemProcessCatalog(DateTime CapturedUtc, bool Available, bool Partial,
    string Source, string Note, IReadOnlyList<SystemProcessEntry> Processes, SystemMemorySnapshot Memory)
{
    // Counts from the original enumeration, before the Windows-only navigation filter.
    // No duplicate inventory or history is retained.
    public WindowsComponentCoverage? ComponentCoverage { get; init; }
}
public interface ISystemProcessSource
{
    SystemProcessCatalog Capture(CancellationToken cancellationToken);
}

// A navigation group is not an authenticity verdict. Name hints never certify Windows ownership.
public static class SystemProcessPresentation
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    { "System", "System Idle Process", "Idle", "Memory Compression", "Registry", "Secure System",
      "smss.exe", "csrss.exe", "wininit.exe", "services.exe", "lsass.exe", "winlogon.exe", "svchost.exe",
      "dwm.exe", "fontdrvhost.exe", "sihost.exe", "explorer.exe", "MsMpEng.exe", "NisSrv.exe" };
    public static bool InternalName(string name) => name.ToLowerInvariant() is "system" or "system idle process" or "idle" or
        "memory compression" or "registry" or "secure system";
    public static bool SameInstance(SystemProcessEntry entry, ProcessSnapshot process) =>
        entry.Pid == process.Pid && entry.StartUtc.HasValue && process.StartTime.HasValue &&
        entry.StartUtc.Value == process.StartTime.Value.ToUniversalTime() &&
        entry.Name.Equals(process.Name, StringComparison.OrdinalIgnoreCase);
    public static string Origin(SystemProcessEntry p, string windowsDirectory)
    {
        if (p.Pid is 0 or 4) return "Voce di sistema · PID riservato";
        if (UnderWindows(p.Path, windowsDirectory)) return "Percorso nella cartella Windows";
        if (Names.Contains(p.Name)) return "Nome tipico di Windows · origine non verificata";
        return p.Path is null ? "Origine N/D" : "Percorso esterno a Windows";
    }
    public static bool WindowsGroup(SystemProcessEntry p, string windowsDirectory) =>
        p.Pid is 0 or 4 || UnderWindows(p.Path, windowsDirectory) || Names.Contains(p.Name);
    public static bool UnderWindows(string? path, string directory)
    {
        // Lexical grouping only: boundary + rooted paths + no traversal, junction or signature claims.
        if (string.IsNullOrWhiteSpace(path) || directory.Length < 3 ||
            !Rooted(path) || !Rooted(directory)) return false;
        var parts = path.Replace('/', '\\').Split('\\');
        if (parts.Any(x => x is "." or "..")) return false;
        string prefix = directory.Replace('/', '\\').TrimEnd('\\') + "\\";
        return path.Replace('/', '\\').StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
    private static bool Rooted(string value) => value.Length > 2 && char.IsAsciiLetter(value[0]) &&
        value[1] == ':' && value[2] is '\\' or '/';
    public static string MiB(long? bytes) => bytes is >= 0
        ? (bytes.Value / 1048576d).ToString("N1", CultureInfo.CurrentCulture) + " MiB" : "N/D";
    public static string Role(SystemProcessEntry p) => p.Pid == 0
        ? "Inattività CPU: voce contabile, non un'app in esecuzione."
        : p.Pid == 4 ? "System: contiene attività in modalità kernel. I suoi contatori non rappresentano tutta la memoria del sistema."
        : p.Name.Equals("Memory Compression", StringComparison.OrdinalIgnoreCase)
            ? "Nome associato alla compressione delle pagine di memoria. Il Working Set della voce non misura la dimensione logica dei dati prima della compressione."
        : p.Name.Equals("Registry", StringComparison.OrdinalIgnoreCase)
            ? "Nome associato alla gestione del registro. Identità e operazione corrente non verificate dal solo nome."
        : p.Name.Equals("Secure System", StringComparison.OrdinalIgnoreCase)
            ? "Nome associato all'ambiente di sicurezza isolato di Windows. Non identifica tutti i servizi di sicurezza."
        : "Per ruolo, CPU e servizi usa l'analisi della stessa istanza. Il nome e il percorso non verificano la firma digitale.";
    public static string MemoryReport(SystemMemorySnapshot m) =>
        $"RAM fisica totale: {MiB(m.PhysicalTotal)}\nRAM disponibile: {MiB(m.PhysicalAvailable)}\n" +
        $"Memoria impegnata: {MiB(m.Commit)} / limite {MiB(m.CommitLimit)}\n" +
        $"Pool kernel paginabile: {MiB(m.KernelPaged)}\nPool kernel non paginabile: {MiB(m.KernelNonpaged)}\n" +
        "Il pool paginabile può trovarsi anche fuori dalla RAM. Questi contatori si sovrappongono ad altre viste: non sommarli ai Working Set.\n" + m.Note;
    public static string Details(SystemProcessEntry p, string windowsDirectory) =>
        $"{p.Name} · PID {p.Pid}\n{Role(p)}\n\nOrigine: {Origin(p, windowsDirectory)}\n" +
        $"Percorso: {p.Path ?? "N/D"}\nAvvio UTC: {(p.StartUtc.HasValue ? p.StartUtc.Value.ToString("O") : "N/D")}\n" +
        $"Accesso ai dettagli: {p.Access}\nWorking Set totale: {MiB(p.WorkingSetBytes)}\n" +
        "Working Set totale: pagine residenti private e condivise; non equivale al Working Set privato.\n" +
        "CPU istantanea e firma digitale non misurate in questa vista. La presenza nell'elenco non prova che il processo stia lavorando.";
}
