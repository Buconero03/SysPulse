using ProcessLens.Core;

namespace ProcessLens.App;

internal sealed record WindowsComponentReport(bool IsApplicable, string Report);

// Static architectural reference + existing snapshot. No probes, signatures,
// health verdicts, or new classifier rules are introduced by this view.
internal static class WindowsComponentContext
{
    private const string Authentication = "https://learn.microsoft.com/en-us/windows-server/security/windows-authentication/credentials-processes-in-windows-authentication";

    public static WindowsComponentReport Describe(ProcessSnapshot p)
    {
        // Match an executable NAME exactly. A path, suffix or lookalike must not
        // receive a Windows role merely because it contains a familiar substring.
        string name = p.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p.Name[..^4] : p.Name;
        (string Role, string Source) reference = name.ToLowerInvariant() switch
        {
            "svchost" => ("Host generico di servizi Windows, anche di servizi implementati in DLL. I servizi ospitati cambiano tra istanze; il nome da solo non identifica il servizio responsabile di un consumo.",
                "https://learn.microsoft.com/en-us/windows/application-management/svchost-service-refactoring"),
            "services" => ("Service Control Manager: mantiene il database dei servizi e gestisce avvio e controllo dei servizi. I contatori di services.exe non sono la somma dei contatori dei servizi ospitati in altri PID.",
                "https://learn.microsoft.com/en-us/windows/win32/services/service-control-manager"),
            "lsass" => ("Processo della Local Security Authority: applica funzioni di autenticazione e criteri di sicurezza locali. Questo prospetto non legge credenziali né verifica quali operazioni di autenticazione stiano avvenendo.", Authentication),
            "winlogon" => ("Gestisce interazioni sicure dell'accesso utente e coordina il percorso di logon. Il ruolo non dimostra che un accesso sia attualmente in corso.", Authentication),
            "dwm" => ("Desktop Window Manager: compone le finestre per la presentazione del desktop. CPU, RAM o moduli caricati non identificano da soli una finestra o un'app responsabile del carico.",
                "https://learn.microsoft.com/en-us/windows/win32/dwm/dwm-overview"),
            _ => ("Funzione sconosciuta. I servizi associati non identificano l’autore dell’eseguibile.", "")
        };
        if (WindowsHostReference.Find(p.Name) is { } host) reference = (host.Summary, host.Source);
        bool applicable = reference.Source.Length > 0 || p.ServiceNames.Count > 0 || p.IsCritical;
        string services = p.ServiceNames.Count > 0
            ? string.Join(", ", p.ServiceNames.Take(16)) + (p.ServiceNames.Count > 16 ? " (elenco parziale)" : "")
            : "Servizi: N/D; elenco non disponibile.";
        string metadata(string? value) => string.IsNullOrWhiteSpace(value) ? "N/D" : value;
        string status = p.IsCritical ? "Flag critico rilevato nello snapshot." :
            "Flag critico: N/D; assenza non confermata.";
        string report = $"■ RUOLO TIPICO DEL COMPONENTE OMONIMO\n{reference.Role}\n" +
            "Ruolo dal nome. Identità e attività attuale non verificate.\n\n" +
            $"■ DATI DEL PROCESSO\nEseguibile: {p.Name}\nPID: {p.Pid}\n" +
            $"Descrizione dichiarata dal file: {metadata(p.FileDescription)}\n" +
            $"Prodotto dichiarato dal file: {metadata(p.ProductName)}\n" +
            $"Azienda dichiarata dal file: {metadata(p.CompanyName)}\n" +
            $"Servizi associati nello snapshot: {services}\n{status}\n\n" +
            "■ COSA APPROFONDIRE\nApri «Mostra servizi Windows ospitati dal PID» per stato, configurazione e dipendenze SCM. " +
            "L'associazione viene verificata per istanza del processo. Per il carico usa i rapporti CPU/RAM e confronta campioni della stessa istanza e dello stesso contatore.\n\n" +
            "■ LIMITI\nFirma digitale e autenticità: non verificate. " +
            "Avvio, operazione e allocazioni interne: sconosciuti. " +
            "Effetti della chiusura: da verificare.\n\n" +
            (reference.Source.Length > 0 ? $"■ RIFERIMENTO TECNICO\nMicrosoft Learn: {reference.Source}" :
                "■ RIFERIMENTO TECNICO\nScheda tecnica: non disponibile.");
        return new(applicable, report);
    }
}
