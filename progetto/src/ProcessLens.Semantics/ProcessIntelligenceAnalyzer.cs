using ProcessLens.Core;

namespace ProcessLens.Semantics;

/// <summary>
/// Generic evidence-first analyzer. Product-specific knowledge is optional enrichment;
/// identity, ownership and role are resolved independently so ProcessLens can explain
/// applications it has never seen before.
/// </summary>
public sealed class ProcessIntelligenceAnalyzer : IProcessAnalyzer
{
    public ProcessInsight Analyze(ProcessSnapshot process)
    {
        var identity = IdentityResolver.Resolve(process);
        var framework = FrameworkDetector.Detect(process);
        var ownership = OwnershipResolver.Resolve(process, identity, framework);
        var role = RoleClassifier.Classify(process, identity, framework, ownership);

        var evidence = new List<string>
        {
            $"Identità: {identity.Evidence}",
            $"Ambiente di esecuzione: {framework.Evidence}",
            $"Applicazione associata: {ownership.Evidence}",
            $"Funzione del processo: {role.Evidence}"
        };

        if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
            evidence.Add($"Eseguibile: {process.ExecutablePath}");
        if (!string.IsNullOrWhiteSpace(process.CommandLine))
            evidence.Add("Command line disponibile per l'analisi del ruolo");
        if (process.ChildCount > 0)
            evidence.Add($"Relazioni: {process.ChildCount} figli totali, {process.SameNameChildCount} omonimi");
        if (process.AncestorNames.Count > 0)
            evidence.Add($"Catena padre: {string.Join(" <- ", process.AncestorNames.Take(6))}");
        if (process.ServiceDisplayNames.Count > 0)
            evidence.Add($"Servizi: {string.Join(", ", process.ServiceDisplayNames)}");
        if (!string.IsNullOrWhiteSpace(process.PackageFullName))
            evidence.Add($"Package Windows: {process.PackageFullName}");
        if (!string.IsNullOrWhiteSpace(process.ApplicationUserModelId))
            evidence.Add($"AUMID Windows: {process.ApplicationUserModelId}");
        if (process.IsDeepInspected)
        {
            evidence.Add($"Deep inspection Windows: finestra visibile={process.HasVisibleWindow}, TCP listen={process.TcpListeningCount}, TCP established={process.TcpEstablishedCount}, UDP={process.UdpEndpointCount}");
            if (process.ModuleNames.Count > 0)
            {
                evidence.Add($"Moduli caricati disponibili: {process.ModuleNames.Count}");
                var interesting = process.ModuleNames
                    .Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    .Take(12)
                    .ToArray();
                if (interesting.Length > 0)
                    evidence.Add($"Campione DLL osservate: {string.Join(", ", interesting)}");
            }
        }

        int overall = Weighted(identity.Confidence, ownership.Confidence, role.Confidence);
        // I punteggi sono euristici, non probabilita' statistiche.
        // Il dettaglio completo, con ID della regola, resta in Evidence.
        string evidenceKind = role.Evidence.Contains("--type=", StringComparison.OrdinalIgnoreCase)
            ? "Parametri di avvio del processo"
            : role.Evidence.Contains("PID -> servizio", StringComparison.OrdinalIgnoreCase)
                ? "Associazione con un servizio Windows"
                : role.Evidence.Contains("flag di processo critico", StringComparison.OrdinalIgnoreCase)
                    ? "Informazione fornita da Windows"
                    : "Dati del rapporto tecnico";
        string detectionBasis =
            "Metodo: regole locali.\n" +
            $"Fonte: {evidenceKind}.\n" +
            "Attività in corso: non rilevata dalla classificazione.";

        return new ProcessInsight
        {
            FriendlyName = identity.FriendlyName,
            Category = role.Category,
            OwnerApp = ownership.Owner,
            Summary = role.Summary,
            Role = role.Role,
            WhatIsIt = role.Meaning,
            WhyRunning = role.WhyRunning,
            CloseImpact = role.CloseImpact,
            CloseRisk = role.CloseRisk,
            DetectionLabel = ProcessTerminology.FunctionLabel(role.Confidence),
            DetectionBasis = detectionBasis,
            ConfidencePercent = overall,
            IdentityConfidencePercent = identity.Confidence,
            OwnershipConfidencePercent = ownership.Confidence,
            RoleConfidencePercent = role.Confidence,
            Framework = framework.Name,
            ProjectRoot = null,
            Evidence = evidence.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static int Weighted(int identity, int ownership, int role) =>
        Math.Clamp((int)Math.Round(identity * 0.25 + ownership * 0.25 + role * 0.50), 0, 100);

}
