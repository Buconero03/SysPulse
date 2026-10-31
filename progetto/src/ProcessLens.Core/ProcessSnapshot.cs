namespace ProcessLens.Core;

public sealed record NetworkEndpointSnapshot(
    string Protocol,
    string State,
    string LocalAddress,
    int LocalPort,
    string? RemoteAddress,
    int? RemotePort,
    string Scope);

public sealed record RelatedNetworkProcessSnapshot(
    int Pid,
    string Name,
    string RoleHint,
    int TcpListeningCount,
    int TcpEstablishedCount,
    int UdpEndpointCount,
    int PublicRemoteConnectionCount,
    IReadOnlyList<NetworkEndpointSnapshot> SampleEndpoints);

// A live ancestor resolved from the same process snapshot. PID reuse is checked
// against creation times; when either timestamp is unavailable the edge is
// reported as unverified instead of being presented as certain provenance.
public sealed record ProcessAncestorSnapshot(
    int Pid,
    int ParentPid,
    string Name,
    DateTime? StartTime,
    string? CommandLine,
    string? ExecutablePath,
    bool VerifiedByStartTime);

public sealed record ProcessSnapshot
{
    public required int Pid { get; init; }
    public required int ParentPid { get; init; }
    public required string Name { get; init; }
    public string? ParentName { get; init; }
    public IReadOnlyList<string> AncestorNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ProcessAncestorSnapshot> AncestorDetails { get; init; } = Array.Empty<ProcessAncestorSnapshot>();
    public string? ExecutablePath { get; init; }
    public string? CommandLine { get; init; }
    public string? FileDescription { get; init; }
    public string? ProductName { get; init; }
    public string? CompanyName { get; init; }
    public string? PackageFullName { get; init; }
    public int ChildCount { get; init; }
    public int SameNameChildCount { get; init; }
    public IReadOnlyList<string> ServiceNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ServiceDisplayNames { get; init; } = Array.Empty<string>();
    /// <summary>Instantaneous percentage of entire logical CPU capacity, not of one core.</summary>
    public double CpuPercent { get; init; }
    public bool CpuSampleAvailable { get; init; }
    public double CpuAverage30Seconds { get; init; }
    public double CpuPeak30Seconds { get; init; }
    public double CpuLogicalCoreEquivalent { get; init; }
    public double CpuCumulativeSeconds { get; init; }
    public bool CpuCumulativeAvailable { get; init; }
    public double CpuSampleIntervalSeconds { get; init; }
    public long WorkingSetBytes { get; init; }
    public bool WorkingSetAvailable { get; init; }
    // Real resident/private pages (PSAPI PROCESS_MEMORY_COUNTERS_EX2); distinct from commit.
    public long PrivateWorkingSetBytes { get; init; }
    public bool PrivateWorkingSetAvailable { get; init; }
    // PrivateBytes is private COMMITTED memory, not resident RAM.
    public long PrivateBytes { get; init; }
    public bool PrivateCommitAvailable { get; init; }
    public double MemorySampledAtMonotonicSeconds { get; init; }
    public DateTime? MemorySampledUtc { get; init; }
    public int ThreadCount { get; init; }
    public int HandleCount { get; init; }
    public DateTime? StartTime { get; init; }
    public bool IsCritical { get; init; }

    // Deep inspection fields. They are populated only for a selected process so the
    // global refresh stays cheap. They are evidence, not guesses.
    public bool IsDeepInspected { get; init; }
    public string? ApplicationUserModelId { get; init; }
    public bool HasVisibleWindow { get; init; }
    public IReadOnlyList<string> WindowTitles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> WindowClasses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ModuleNames { get; init; } = Array.Empty<string>();
    public int TcpListeningCount { get; init; }
    public int TcpEstablishedCount { get; init; }
    public int UdpEndpointCount { get; init; }
    public IReadOnlyList<int> ListeningTcpPorts { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> UdpPorts { get; init; } = Array.Empty<int>();
    public int PublicRemoteConnectionCount { get; init; }
    public int PrivateRemoteConnectionCount { get; init; }
    public int LoopbackRemoteConnectionCount { get; init; }
    public IReadOnlyList<NetworkEndpointSnapshot> NetworkEndpoints { get; init; } = Array.Empty<NetworkEndpointSnapshot>();

    // Generic process-family network evidence. "Family" means the selected executable
    // plus related same-executable descendants under its top-most same-name ancestor.
    // The inspector also exposes which concrete family PIDs own sockets and, where the
    // command line says so, their generic role (e.g. Chromium Network Service).
    public int NetworkFamilyRootPid { get; init; }
    public int NetworkFamilyProcessCount { get; init; }
    public int NetworkFamilyProcessesWithSockets { get; init; }
    public int NetworkFamilyTcpListeningCount { get; init; }
    public int NetworkFamilyTcpEstablishedCount { get; init; }
    public int NetworkFamilyUdpEndpointCount { get; init; }
    public int NetworkFamilyPublicRemoteConnectionCount { get; init; }
    public int NetworkFamilyPrivateRemoteConnectionCount { get; init; }
    public int NetworkFamilyLoopbackRemoteConnectionCount { get; init; }
    public IReadOnlyList<int> NetworkFamilyListeningTcpPorts { get; init; } = Array.Empty<int>();
    public IReadOnlyList<RelatedNetworkProcessSnapshot> RelatedNetworkProcesses { get; init; } = Array.Empty<RelatedNetworkProcessSnapshot>();

    public string? DeepInspectionNote { get; init; }
    public bool? DirectNetworkIdentityVerified { get; init; }
    public int? NetworkVerifiedProcessCount { get; init; }
    public DateTime? NetworkCapturedUtc { get; init; }
    public bool NetworkAcquisitionComplete { get; init; }
    public string? NetworkAcquisitionNote { get; init; }
    public double NetworkBytesPerSecond { get; init; }
    public double NetworkSendBytesPerSecond { get; init; }
    public double NetworkReceiveBytesPerSecond { get; init; }
    public bool NetworkBandwidthAvailable { get; init; }
    public bool NetworkLiveAvailable { get; init; }
    public double NetworkSampledAtMonotonicSeconds { get; init; }

    public int ActiveNetworkSocketCount => TcpEstablishedCount + TcpListeningCount + UdpEndpointCount;
    public string NetworkShortLabel =>
        SysPulseLocalization.IsEnglish
            ? (TcpEstablishedCount > 0 && NetworkBandwidthAvailable && NetworkBytesPerSecond >= 64
                ? $"NET · {TcpEstablishedCount} CONN."
                : PublicRemoteConnectionCount > 0 ? "NET · EXTERNAL" : "NET")
            : (TcpEstablishedCount > 0 && NetworkBandwidthAvailable && NetworkBytesPerSecond >= 64
                ? $"RETE · {TcpEstablishedCount} CONN."
                : PublicRemoteConnectionCount > 0 ? "RETE · ESTERNA" : "RETE");
    public string NetworkDisplayText =>
        NetworkBandwidthAvailable && NetworkBytesPerSecond >= 64
            ? LiveNetworkSample.FormatRate(NetworkBytesPerSecond)
            : TcpEstablishedCount > 0
                ? $"{TcpEstablishedCount} conn."
                : TcpListeningCount > 0
                    ? (SysPulseLocalization.IsEnglish ? $"{TcpListeningCount} listen" : $"{TcpListeningCount} ascolto")
                    : UdpEndpointCount > 0
                        ? $"{UdpEndpointCount} UDP"
                        : NetworkLiveAvailable || IsDeepInspected
                            ? LiveNetworkSample.FormatRate(0)
                            : "0,0\u00a0KB/s";
    public string NetworkDescription =>
        SysPulseLocalization.IsEnglish
            ? $"Detected traffic: {(NetworkBandwidthAvailable ? $"{LiveNetworkSample.FormatRate(NetworkBytesPerSecond)} (send {LiveNetworkSample.FormatRate(NetworkSendBytesPerSecond)}, recv {LiveNetworkSample.FormatRate(NetworkReceiveBytesPerSecond)})" : "0.0 KB/s")} · " +
              $"Established TCP connections: {TcpEstablishedCount} (to public IPs: {PublicRemoteConnectionCount}) · " +
              $"Listening TCP ports: {TcpListeningCount} · UDP endpoints: {UdpEndpointCount}."
            : $"Traffico rilevato: {(NetworkBandwidthAvailable ? $"{LiveNetworkSample.FormatRate(NetworkBytesPerSecond)} (invio {LiveNetworkSample.FormatRate(NetworkSendBytesPerSecond)}, ricezione {LiveNetworkSample.FormatRate(NetworkReceiveBytesPerSecond)})" : "0,0 KB/s")} · " +
              $"Connessioni TCP stabilite: {TcpEstablishedCount} (verso IP pubblici: {PublicRemoteConnectionCount}) · " +
              $"Porte TCP in ascolto: {TcpListeningCount} · Endpoint UDP: {UdpEndpointCount}.";

    public double WorkingSetMb => WorkingSetBytes / 1024d / 1024d;
    public double PrivateMb => PrivateBytes / 1024d / 1024d;
    public double PrivateWorkingSetMb => PrivateWorkingSetBytes / 1024d / 1024d;
    public double MemoryDisplayMb => PrivateWorkingSetAvailable ? PrivateWorkingSetMb : WorkingSetMb;
    public string MemoryDisplayLabel =>
        SysPulseLocalization.IsEnglish
            ? (PrivateWorkingSetAvailable ? "PRIVATE WORKING SET" :
               WorkingSetAvailable ? "TOTAL WORKING SET" : "RAM N/A")
            : (PrivateWorkingSetAvailable ? "WORKING SET PRIVATO" :
               WorkingSetAvailable ? "WORKING SET TOTALE" : "RAM N/D");
    public string MemoryDisplayText => PrivateWorkingSetAvailable || WorkingSetAvailable
        ? $"{MemoryDisplayMb:F1} MiB" : (SysPulseLocalization.IsEnglish ? "N/A" : "N/D");
    public string MemoryDescription =>
        SysPulseLocalization.IsEnglish
            ? (PrivateWorkingSetAvailable
                ? "Private pages resident in RAM. Compare the same instance, counter, and interval; Task Manager may use different aggregations."
                : WorkingSetAvailable
                    ? "Private Working Set unavailable: showing TOTAL resident memory including shared pages; do NOT compare directly with Task Manager / Processes."
                    : "Memory counters inaccessible for this PID.")
            : (PrivateWorkingSetAvailable
                ? "Pagine private presenti in RAM. Confronta la stessa istanza, contatore e intervallo; Gestione attività può usare aggregazioni diverse."
                : WorkingSetAvailable
                    ? "Private Working Set non disponibile: viene mostrata la memoria residente TOTALE, inclusa quella condivisa; NON confrontare direttamente con Gestione attività / Processi."
                    : "Contatori di memoria non accessibili per questo PID.");

    // The semantic meaning of a process depends on identity, ancestry, command line
    // and (when available) deep Windows evidence, not on CPU/RAM values.
    public string SemanticIdentityKey => string.Join("|",
        Name,
        ParentPid,
        ParentName ?? string.Empty,
        ExecutablePath ?? string.Empty,
        CommandLine ?? string.Empty,
        FileDescription ?? string.Empty,
        ProductName ?? string.Empty,
        CompanyName ?? string.Empty,
        PackageFullName ?? string.Empty,
        ChildCount,
        SameNameChildCount,
        IsCritical ? "1" : "0",
        IsDeepInspected ? "deep" : "shallow",
        ApplicationUserModelId ?? string.Empty,
        HasVisibleWindow ? "window" : "nowindow",
        TcpListeningCount,
        TcpEstablishedCount,
        UdpEndpointCount,
        PublicRemoteConnectionCount,
        PrivateRemoteConnectionCount,
        LoopbackRemoteConnectionCount,
        NetworkFamilyRootPid,
        NetworkFamilyProcessCount,
        NetworkFamilyProcessesWithSockets,
        NetworkFamilyTcpListeningCount,
        NetworkFamilyTcpEstablishedCount,
        NetworkFamilyUdpEndpointCount,
        NetworkFamilyPublicRemoteConnectionCount,
        NetworkFamilyPrivateRemoteConnectionCount,
        NetworkFamilyLoopbackRemoteConnectionCount,
        string.Join(",", NetworkFamilyListeningTcpPorts),
        string.Join(",", AncestorNames),
        string.Join(",", ServiceNames),
        string.Join(",", ServiceDisplayNames),
        string.Join(",", WindowClasses),
        string.Join(",", ModuleNames.Take(80)),
        string.Join(",", ListeningTcpPorts),
        string.Join(",", UdpPorts),
        string.Join(";", RelatedNetworkProcesses.Select(x => $"{x.Pid}:{x.RoleHint}:{x.TcpListeningCount}:{x.TcpEstablishedCount}:{x.UdpEndpointCount}:{x.PublicRemoteConnectionCount}")));
}
