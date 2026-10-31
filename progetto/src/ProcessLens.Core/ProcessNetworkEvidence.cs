namespace ProcessLens.Core;

public static class ProcessNetworkEvidence
{
    public static ProcessSnapshot Unavailable(ProcessSnapshot p, string reason) => p with
    {
        NetworkCapturedUtc = DateTime.UtcNow, NetworkAcquisitionComplete = false,
        DirectNetworkIdentityVerified = false, NetworkVerifiedProcessCount = 0, NetworkAcquisitionNote = reason,
        DeepInspectionNote = (p.DeepInspectionNote?.Split(" | Rete: ")[0] ?? "") + " | Rete: " + reason,
        TcpListeningCount = 0, TcpEstablishedCount = 0, UdpEndpointCount = 0,
        ListeningTcpPorts = Array.Empty<int>(), UdpPorts = Array.Empty<int>(),
        PublicRemoteConnectionCount = 0, PrivateRemoteConnectionCount = 0, LoopbackRemoteConnectionCount = 0,
        NetworkEndpoints = Array.Empty<NetworkEndpointSnapshot>(), RelatedNetworkProcesses = Array.Empty<RelatedNetworkProcessSnapshot>(),
        NetworkFamilyRootPid = p.Pid, NetworkFamilyProcessCount = 1, NetworkFamilyProcessesWithSockets = 0,
        NetworkFamilyTcpListeningCount = 0, NetworkFamilyTcpEstablishedCount = 0, NetworkFamilyUdpEndpointCount = 0,
        NetworkFamilyPublicRemoteConnectionCount = 0, NetworkFamilyPrivateRemoteConnectionCount = 0,
        NetworkFamilyLoopbackRemoteConnectionCount = 0, NetworkFamilyListeningTcpPorts = Array.Empty<int>()
    };
}
