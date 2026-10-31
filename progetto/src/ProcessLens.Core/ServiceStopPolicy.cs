namespace ProcessLens.Core;

/// <summary>
/// Eligible service STOP requests. SCM is authoritative: this policy only filters
/// critical services and unsupported states, never makes permission claims.
/// A host image inside Windows (e.g. svchost.exe) is NOT in itself a reason to
/// deny STOP for an individual, noncritical service hosted by that process.
/// </summary>
public static class ServiceStopPolicy
{
    // Additional conservative guard for services essential to the OS, its security
    // boundary or core networking. Other services still require SCM approval.
    private static readonly HashSet<string> CriticalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "RpcEptMapper", "DcomLaunch", "EventLog", "PlugPlay", "Power",
        "ProfSvc", "WinDefend", "SamSs", "LanmanWorkstation", "Winmgmt",
        "BFE", "mpssvc", "CryptSvc", "Schedule", "LSM", "BrokerInfrastructure",
        "Wcmsvc", "Dhcp", "Dnscache", "nsi", "CoreMessagingRegistrar",
        "SecurityHealthService", "SgrmBroker", "WdNisSvc", "gpsvc",
        "TrustedInstaller", "Appinfo", "UserManager", "StateRepository"
    };

    public static ServiceStopDecision Evaluate(ServiceSnapshot service, string windowsDirectory)
    {
        // Preserve signature for callers/tests. The image location is not an SCM
        // eligibility criterion; Windows services legitimately use shared svchost.
        _ = windowsDirectory;
        if (!service.IsRunning || service.ProcessId <= 0)
            return new(false, "Il servizio non è in esecuzione o non ha un PID valido.");
        if ((service.ServiceType & 0x30) == 0)
            return new(false, "Questo comando gestisce solo servizi Win32, non driver kernel.");
        if (string.IsNullOrWhiteSpace(service.Name))
            return new(false, "Nome del servizio non disponibile.");
        if (CriticalNames.Contains(service.Name))
            return new(false, "Servizio essenziale/protetto da ProcessLens: arresto non disponibile per sicurezza.");
        return new(true, "Puoi richiedere un arresto ordinato: Windows verificherà i diritti STOP e se il servizio accetta STOP.");
    }
}

public sealed record ServiceStopDecision(bool Allowed, string Reason);
