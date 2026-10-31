namespace ProcessLens.Core;

public static class ServiceExplanation
{
    public static string WhyRunning(ServiceSnapshot service)
    {
        if (!service.IsRunning)
            return service.StateCode == 1
                ? "Servizio arrestato. La modalità di avvio è una configurazione, non una richiesta in corso."
                : $"Stato: {service.StateLabel}. Motivo del cambio sconosciuto.";

        var start = service.StartupCode switch
        {
            2 => "Avvio automatico configurato.",
            3 => "Avvio su richiesta. Può dipendere da Windows, trigger, servizi o app.",
            4 => "Attivo con avvio ora disabilitato. La configurazione può essere cambiata dopo l’avvio.",
            _ => "Servizio in esecuzione. Autore dell’avvio sconosciuto."
        };
        return start + " Richiedente non disponibile in SCM.";
    }

    public static string Impact(ServiceSnapshot service)
    {
        if (service.StateCode == 1) return "Servizio arrestato. Gli altri servizi nello stesso PID possono restare attivi.";
        return "L’arresto può interrompere funzioni di Windows o delle app. Valuta le dipendenze. Arresto solo su conferma; nessuna terminazione forzata del PID.";
    }

    public static string Association(ServiceSnapshot service, int coHostedServices, string? hostProcessName)
    {
        if (service.ProcessId <= 0) return "PID ospitante: N/D.";
        var name = string.IsNullOrWhiteSpace(hostProcessName) ? "processo non identificato" : hostProcessName;
        var other = coHostedServices > 1 ? $". Il PID ospita {coHostedServices} servizi." : ".";
        return $"Host SCM: {name} · PID {service.ProcessId}{other} Richiedente sconosciuto.";
    }
}
