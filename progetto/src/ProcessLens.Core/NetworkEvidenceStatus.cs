namespace ProcessLens.Core;

public static class NetworkEvidenceStatus
{
    public const string WfpGuid = "{0CCE9226-69AE-11D9-BED3-505054503030}";
    public const string PacketDropGuid = "{0CCE9225-69AE-11D9-BED3-505054503030}";
    public const string Setup = "Per registrare i futuri blocchi WFP, apri un terminale come amministratore ed esegui:\n" +
        "auditpol /get /subcategory:\"" + WfpGuid + "\"\n" +
        "auditpol /set /subcategory:\"" + WfpGuid + "\" /failure:enable\n" +
        "Poi premi Leggi eventi Windows. Gli eventi passati mancanti non vengono ricreati; un elenco vuoto resta possibile se non avvengono blocchi. " +
        "Il registro Security può crescere più rapidamente. Se prima la raccolta era disabilitata e vuoi ripristinarla:\n" +
        "auditpol /set /subcategory:\"" + WfpGuid + "\" /failure:disable\n" +
        "\nLETTURA ESTESA · 5152\nL'opzione legge gli scarti già registrati, senza abilitare la registrazione. Verifica il criterio con:\n" +
        "auditpol /get /subcategory:\"" + PacketDropGuid + "\"\n" +
        "Se vuoi registrare i futuri scarti, puoi abilitarlo manualmente con /set e /failure:enable sulla stessa sottocategoria. " +
        "Gli eventi per pacchetto possono essere numerosi. Conserva il criterio precedente per ripristinarlo.\n" +
        "ProcessLens mostra i comandi, senza eseguirli o cambiare audit e firewall. Su PC gestiti, i criteri aziendali possono prevalere.";
    public static string Summary(NativeNetworkSnapshot s)
    {
        if (!s.Available) return "Registro Security non disponibile · conteggi N/D";
        string counts = $"{s.Events.Count} eventi trovati negli ultimi {s.Options.Minutes} min" + (s.Partial ? " · lettura parziale" : "");
        if (s.Audit.Any(a => a.FailuresEnabled is false)) return "Copertura incompleta · audit disabilitato per alcune voci · " + counts;
        if (s.Audit.Count < (s.Options.IncludePacketDrops ? 3 : 2) || s.Audit.Any(a => a.FailuresEnabled is null)) return "Copertura non verificata · " + counts;
        return s.Events.Count == 0 ? $"Nessun evento trovato negli ultimi {s.Options.Minutes} min" + (s.Partial ? " · lettura parziale" : "") : counts;
    }
    public static string Action(NativeNetworkSnapshot s)
    {
        if (!s.Available) return "La lettura del registro non è riuscita. Leggi il motivo qui sotto; non è un conteggio pari a zero.";
        var disabled = s.Audit.Where(a => a.FailuresEnabled is false).Select(a => a.Category).ToArray();
        if (disabled.Length > 0) return "Audit disabilitato: " + string.Join(", ", disabled) + ". Windows potrebbe non aver registrato questi eventi. " +
            "Per i blocchi WFP apri «Come attivare la raccolta». La configurazione attuale non ricostruisce quella passata.";
        if (s.Audit.Count < (s.Options.IncludePacketDrops ? 3 : 2) || s.Audit.Any(a => a.FailuresEnabled is null)) return "Non è stato possibile verificare tutti i criteri di audit. I dettagli indicano quali letture mancano.";
        return "Lettura eseguita. Le righe compaiono solo se Windows ha registrato eventi compatibili nell’intervallo; l’audit attivo adesso non dimostra la copertura di tutto il passato.";
    }
}
