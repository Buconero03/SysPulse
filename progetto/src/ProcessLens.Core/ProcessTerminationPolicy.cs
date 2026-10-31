namespace ProcessLens.Core;

/// <summary>
/// Conservative preflight check. This is only the first gate: the Windows executor
/// must re-validate the live process using its handle before sending TerminateProcess.
/// </summary>
public static class ProcessTerminationPolicy
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "system idle process", "registry", "memory compression", "secure system",
        "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe", "lsass.exe",
        "services.exe", "svchost.exe", "fontdrvhost.exe"
    };

    public static ProcessTerminationDecision Evaluate(ProcessSnapshot process, int applicationPid)
    {
        if (process.Pid <= 4 || process.Pid == applicationPid)
            return new(false, "Il processo di sistema o ProcessLens stesso non può essere terminato da questa funzione.");
        if (process.IsCritical || IsProtectedName(process.Name))
            return new(false, "Processo di Windows protetto o critico: operazione bloccata.");
        if (process.ServiceNames.Count > 0)
            return new(false, "Il processo ospita un servizio Windows: per arrestarlo usa Gestione servizi.");
        if (process.StartTime is null)
            return new(false, "Orario di creazione non disponibile: non posso verificare in sicurezza l'identità del PID.");
        if (string.IsNullOrWhiteSpace(process.ExecutablePath))
            return new(false, "Percorso dell'eseguibile non accessibile: verifica dell'identità non disponibile.");
        return new(true, "Processo verificabile: sarà richiesta una conferma prima della terminazione.");
    }

    public static bool IsProtectedName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && ProtectedNames.Contains(name.Trim());

    /// <summary>Creation timestamps must match, not merely PID/name, to reject PID reuse.</summary>
    public static bool IsSameCreationTime(DateTime observed, DateTime current)
    {
        if (observed == DateTime.MinValue || current == DateTime.MinValue)
            return false;
        return Math.Abs((observed.ToUniversalTime() - current.ToUniversalTime()).TotalMilliseconds) <= 100;
    }
}

public sealed record ProcessTerminationDecision(bool Allowed, string Reason);
