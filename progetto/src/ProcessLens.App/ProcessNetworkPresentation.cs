using ProcessLens.Core;

namespace ProcessLens.App;

internal static class ProcessNetworkPresentation
{
    public static string Describe(ProcessSnapshot p)
    {
        if (!p.IsDeepInspected && !p.NetworkCapturedUtc.HasValue)
            return "Porte e connessioni: in attesa dell’analisi approfondita del processo selezionato.";

        var direct = new List<string>
        {
            $"Porte e connessioni del PID {p.Pid}",
            $"TCP in ascolto (LISTEN): {p.TcpListeningCount}",
            $"Connessioni TCP stabilite (ESTABLISHED): {p.TcpEstablishedCount}",
            $"Endpoint UDP: {p.UdpEndpointCount}",
            $"Connessioni TCP verso IP pubblici: {p.PublicRemoteConnectionCount}"
        };
        if (p.ListeningTcpPorts.Count > 0)
            direct.Add($"Porte TCP in ascolto: {string.Join(", ", p.ListeningTcpPorts)}");
        if (p.NetworkBandwidthAvailable)
            direct.Add($"Traffico attuale del PID: {LiveNetworkSample.FormatRate(p.NetworkBytesPerSecond)} " +
                $"(↑ {LiveNetworkSample.FormatRate(p.NetworkSendBytesPerSecond)} · ↓ {LiveNetworkSample.FormatRate(p.NetworkReceiveBytesPerSecond)})");

        if (p.DirectNetworkIdentityVerified == false)
            direct = new List<string> { $"PID {p.Pid}: socket N/D — identità non verificata durante il campione." };
        var sections = new List<string> { string.Join("\n", direct) };
        if (p.NetworkCapturedUtc.HasValue)
            sections.Insert(0, $"Campione rete: {p.NetworkCapturedUtc.Value.ToLocalTime():HH:mm:ss} · " +
                (p.NetworkAcquisitionComplete ? "quattro tabelle lette" : "LETTURA PARZIALE") +
                $"\nPID con identità confrontata prima/dopo: {p.NetworkVerifiedProcessCount}/{p.NetworkFamilyProcessCount}\n{p.NetworkAcquisitionNote}");
        if (p.NetworkFamilyProcessCount > 1)
        {
            sections.Add("Processi correlati per nome e albero\n" +
                $"Processi con socket: {p.NetworkFamilyProcessesWithSockets} su {p.NetworkFamilyProcessCount}\n" +
                $"Connessioni TCP verso IP pubblici, totale del gruppo: {p.NetworkFamilyPublicRemoteConnectionCount}");

            var networkOwner = p.RelatedNetworkProcesses
                .FirstOrDefault(x => x.RoleHint.Contains("Servizio di rete", StringComparison.OrdinalIgnoreCase));
            if (networkOwner is not null)
            {
                sections.Add($"Ruolo di rete suggerito dai parametri: PID {networkOwner.Pid}\n" +
                    $"{networkOwner.RoleHint}\n" +
                    $"Connessioni TCP stabilite: {networkOwner.TcpEstablishedCount}\n" +
                    $"Endpoint UDP: {networkOwner.UdpEndpointCount}");
            }
            else
            {
                var busiest = p.RelatedNetworkProcesses
                    .Where(x => x.Pid != p.Pid)
                    .OrderByDescending(x => x.PublicRemoteConnectionCount)
                    .ThenByDescending(x => x.TcpEstablishedCount)
                    .FirstOrDefault();
                if (busiest is not null)
                    sections.Add("Processo correlato con più connessioni TCP pubbliche/stabilite rilevate\n" +
                        $"PID {busiest.Pid} ({busiest.RoleHint})");
            }
        }

        sections.Add("Conteggi nelle letture disponibili: zero non garantisce l’assenza di socket. " +
            "Richieste accettate e byte trasferiti non sono misurati.");
        if (!string.IsNullOrWhiteSpace(p.DeepInspectionNote))
            sections.Add("Esito e limiti di acquisizione\n" + p.DeepInspectionNote);
        return string.Join("\n\n", sections);
    }
    public static string DescribeEndpointOwners(ProcessSnapshot p)
    {
        var lines = new List<string> { Describe(p), "SOCKET OSSERVATI — proprietario PID, indirizzo locale e destinazione" };
        static string Endpoint(int pid, NetworkEndpointSnapshot e) =>
            $"PID {pid} · {e.Protocol} {e.State} · locale [{e.LocalAddress}]:{e.LocalPort}" +
            (e.RemoteAddress is null ? "" : $" → remoto [{e.RemoteAddress}]:{e.RemotePort}");
        if (p.DirectNetworkIdentityVerified != false)
            lines.AddRange(p.NetworkEndpoints.Select(e => Endpoint(p.Pid, e)));
        foreach (var owner in p.RelatedNetworkProcesses.Where(x => x.Pid != p.Pid))
            lines.AddRange(owner.SampleEndpoints.Select(e => Endpoint(owner.Pid, e)));
        lines.Add("Elenco campionato: fino a 40 endpoint del PID selezionato e 12 per ciascuno dei primi 32 PID correlati con socket. " +
            "I conteggi includono le righe lette oltre questi limiti; gli altri stati TCP compaiono soltanto nell'elenco. " +
            "Gruppo per stesso eseguibile e catena di avvio con orari compatibili; altri eseguibili o identità non leggibili esclusi. " +
            "Una porta remota 443 non è una porta locale in ascolto. Nessuna misura dei byte trasferiti.");
        return string.Join("\n\n", lines);
    }

}
