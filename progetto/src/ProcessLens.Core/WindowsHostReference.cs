namespace ProcessLens.Core;

public sealed record WindowsHostEntry(string Executable, string Title, string Summary, string Source);
// Architectural reference, matched by exact executable name, not an authenticity check.
public static class WindowsHostReference
{
    public static IReadOnlyList<WindowsHostEntry> Entries { get; } = Array.AsReadOnly(new[] {
        new WindowsHostEntry("svchost.exe", "Host di servizi Windows",
            "Carica servizi implementati in DLL. I servizi ospitati variano tra PID: usa l’associazione SCM del campione, non il solo nome.",
            "https://learn.microsoft.com/en-us/windows/application-management/svchost-service-refactoring"),
        new("prevhost.exe", "Host delle anteprime",
            "Ospita gestori che mostrano anteprime di file fuori dal processo chiamante. Il nome non rivela il file visualizzato o il gestore caricato.",
            "https://learn.microsoft.com/en-us/windows/win32/shell/preview-handlers"),
        new("RuntimeBroker.exe", "Runtime Broker · permessi delle app",
            "Supporta la gestione dei permessi delle app Microsoft Store. Il campione non identifica quale app o autorizzazione sia coinvolta.",
            "https://support.microsoft.com/en-us/windows/runtime-broker-is-using-too-much-memory-ca6ed4e3-2a36-964c-4d2e-8c93980d8a98"),
        new("smartscreen.exe", "Microsoft Defender SmartScreen",
            "Componente dei controlli di reputazione contro file e applicazioni potenzialmente dannosi. La presenza del processo non prova una scansione o una minaccia in corso.",
            "https://learn.microsoft.com/en-us/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/")
    });
    public static WindowsHostEntry? Find(string name) => Entries.FirstOrDefault(x =>
        x.Executable.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        x.Executable[..^4].Equals(name, StringComparison.OrdinalIgnoreCase));
    public static bool Matches(ProcessSnapshot process, string query)
    {
        string text = query.Trim();
        if (text.Length == 0 || text.All(char.IsAsciiDigit)) return false;
        var entry = Find(process.Name);
        if (entry is null) return false;
        if (entry.Title.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
        return entry.Executable == "svchost.exe" && process.ServiceDisplayNames.Concat(process.ServiceNames).Take(16)
            .Any(name => name.Contains(text, StringComparison.OrdinalIgnoreCase));
    }
    public static string ListTitle(ProcessSnapshot p, string fallback)
    {
        var entry = Find(p.Name);
        if (entry is null) return fallback;
        var names = p.ServiceDisplayNames.Count > 0 ? p.ServiceDisplayNames : p.ServiceNames;
        if (entry.Executable == "svchost.exe" && names.Count > 0)
            return "Host servizi · " + string.Join(", ", names.Take(2)) + (names.Count > 2 ? "…" : "");
        return entry.Title;
    }
    public static string Services(ProcessSnapshot p) => p.ServiceDisplayNames.Count > 0
        ? "Servizi: " + string.Join(", ", p.ServiceDisplayNames.Take(2)) + (p.ServiceDisplayNames.Count > 2 ? "…" : "")
        : p.ServiceNames.Count > 0 ? "Servizi: " + string.Join(", ", p.ServiceNames.Take(2)) + (p.ServiceNames.Count > 2 ? "…" : "")
        : "Servizi ospitati: elenco non disponibile";
}
