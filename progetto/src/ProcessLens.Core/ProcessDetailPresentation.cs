using System.Text;

namespace ProcessLens.Core;

/// <summary>Presentation only. Does not change command lines or evidence semantics.</summary>
public static class ProcessDetailPresentation
{
    public sealed record Fact(string Group, string Label, string Value);

    public static string FormatCommandLine(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return "Accesso non disponibile";

        // Only a visual line break before standalone --options outside quoted strings.
        // This is not a Windows command-line parser; the exact original remains available.
        var result = new StringBuilder(commandLine.Length + 40);
        bool quoted = false;
        for (int index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (character == '"' && (index == 0 || commandLine[index - 1] != '\\'))
                quoted = !quoted;

            if (!quoted && char.IsWhiteSpace(character))
            {
                int next = index;
                while (next < commandLine.Length && char.IsWhiteSpace(commandLine[next])) next++;
                if (next + 2 < commandLine.Length && commandLine[next] == '-' && commandLine[next + 1] == '-'
                    && char.IsLetter(commandLine[next + 2]))
                {
                    result.Append('\n');
                    index = next - 1;
                    continue;
                }
            }
            result.Append(character);
        }
        return result.ToString();
    }

    /// <summary>
    /// Factual, plain-language explanation for the process detail view. The
    /// identity is taken from the Windows snapshot; owner and role are local
    /// interpretations. Do not render scores as probabilities or expose raw
    /// command-line arguments in this summary.
    /// </summary>
    public static IReadOnlyList<Fact> IdentificationExplanation(ProcessSnapshot process, ProcessInsight insight)
    {
        string identity = $"{process.Name} · PID {process.Pid}.";

        bool parentMatchesOwner = !string.IsNullOrWhiteSpace(process.ParentName)
            && !string.IsNullOrWhiteSpace(insight.OwnerApp)
            && string.Equals(Path.GetFileNameWithoutExtension(process.ParentName), insight.OwnerApp,
                StringComparison.OrdinalIgnoreCase);
        string association = parentMatchesOwner
            ? $"App possibile: {insight.OwnerApp}. Padre: {process.ParentName}. " +
              "Il nome del padre non conferma l’appartenenza."
            : $"App possibile: {insight.OwnerApp}. Criteri nei dettagli tecnici.";

        string role;
        if (insight.RoleConfidencePercent < 60)
            role = "Funzione sconosciuta.";
        else if (insight.Role.Equals("Script Python", StringComparison.OrdinalIgnoreCase)
                 && insight.Summary.StartsWith("Script indicato: ", StringComparison.OrdinalIgnoreCase))
            role = $"Script nei parametri: {insight.Summary[17..]}. " +
                   "Istruzione in esecuzione non rilevata.";
        else
            role = $"Ruolo stimato: {insight.Role}. " +
                   "Non indica il lavoro in corso.";

        if (SysPulseLocalization.CurrentLanguage == AppLanguage.English)
        {
            string enAssociation = parentMatchesOwner
                ? $"Possible app: {insight.OwnerApp}. Parent: {process.ParentName}. Parent name does not confirm ownership."
                : $"Possible app: {insight.OwnerApp}. Criteria in technical details.";
            string enRole;
            if (insight.RoleConfidencePercent < 60)
                enRole = "Unknown function.";
            else if (insight.Role.Equals("Script Python", StringComparison.OrdinalIgnoreCase)
                     && insight.Summary.StartsWith("Script indicato: ", StringComparison.OrdinalIgnoreCase))
                enRole = $"Script in parameters: {insight.Summary[17..]}. Running instruction not measured.";
            else
                enRole = $"Estimated role: {SysPulseLocalization.Translate(insight.Role)}. Does not indicate ongoing work.";

            return new[]
            {
                new Fact("Summary", "Windows data", identity),
                new Fact("Summary", "Suggested association", enAssociation),
                new Fact("Summary", "Suggested type", enRole)
            };
        }

        return new[]
        {
            new Fact("Riepilogo", "Dato Windows", identity),
            new Fact("Riepilogo", "Associazione proposta", association),
            new Fact("Riepilogo", "Tipo suggerito", role)
        };
    }

    public static IReadOnlyList<Fact> ParseTechnicalFacts(string? technicalContext)
    {
        if (string.IsNullOrWhiteSpace(technicalContext)) return Array.Empty<Fact>();
        var rows = new List<Fact>();
        foreach (var raw in technicalContext.Split('\n'))
        {
            var line = raw.Trim();
            var divider = line.IndexOf(':');
            if (divider <= 0) continue;
            var label = line[..divider].Trim();
            var value = line[(divider + 1)..].Trim();
            var group = TechnicalGroup(label);
            rows.Add(new Fact(group, label, value));
        }
        return rows;
    }


    /// <summary>
    /// Explains classifications without displaying rule identifiers or raw process
    /// arguments in the standard UI. Technical values remain in the audit view.
    /// A rule score is not a statistically calibrated probability.
    /// </summary>
    public static IReadOnlyList<Fact> ClassificationFacts(ProcessSnapshot process, ProcessInsight insight)
    {
        var facts = new List<Fact>
        {
            new("Identificazione", "File eseguibile", process.Name),
            new("Identificazione", "Nome proposto", insight.FriendlyName),
            new("Identificazione", "Associazione proposta", insight.OwnerApp),
            new("Identificazione", "Tipo suggerito", insight.RoleConfidencePercent >= 60
                ? insight.Role
                : "Funzione sconosciuta"),
            new("Identificazione", "Corrispondenza alle regole",
                ProcessTerminology.EvidenceLevel(insight.RoleConfidencePercent) + " (regole interne)")
        };
        string source = "Elementi registrati nel rapporto tecnico";
        foreach (var line in insight.DetectionBasis.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("Fonte:", StringComparison.OrdinalIgnoreCase)) continue;
            var parsed = trimmed["Fonte:".Length..].Trim().TrimEnd('.');
            if (!string.IsNullOrWhiteSpace(parsed)) source = parsed;
            break;
        }
        facts.Add(new("Identificazione", "Dati utilizzati", source));
        // Cite the single recognized option, never the full command line:
        // arguments may contain credentials, file paths or user data.
        var cmd = process.CommandLine;
        if (!string.IsNullOrWhiteSpace(cmd) && cmd.Contains("--type=renderer", StringComparison.OrdinalIgnoreCase)
            && insight.RoleConfidencePercent >= 60)
            facts.Add(new("Identificazione", "Parametro riconosciuto", "--type=renderer"));
        else if (!string.IsNullOrWhiteSpace(cmd) && cmd.Contains("--type=gpu-process", StringComparison.OrdinalIgnoreCase)
                 && insight.RoleConfidencePercent >= 60)
            facts.Add(new("Identificazione", "Parametro riconosciuto", "--type=gpu-process"));
        facts.Add(new("Identificazione", "Limite dell'analisi",
            "Attività attuale non verificata dalla classificazione."));
        if (SysPulseLocalization.CurrentLanguage == AppLanguage.English)
        {
            return facts.Select(f => new Fact(
                SysPulseLocalization.Translate(f.Group),
                SysPulseLocalization.Translate(f.Label),
                SysPulseLocalization.Translate(f.Value))).ToList();
        }
        return facts;
    }

    /// <summary>
    /// Creates readable, grouped rows for the opt-in technical classification panel.
    /// The classifier's original evidence is never rewritten or discarded: it remains
    /// available verbatim in the nested raw report. No text is interpreted as proof
    /// of live activity and a rule score is not a statistical probability.
    /// </summary>
    public static IReadOnlyList<Fact> ClassificationAuditFacts(ProcessInsight insight, int maxEvidence = 32)
    {
        var facts = new List<Fact>
        {
            new("Valutazione interna", "Punteggio assegnato dalle regole",
                $"{insight.RoleConfidencePercent}/100 (non è una probabilità statistica)"),
            new("Valutazione interna", "Significato del risultato",
                "Corrispondenza alle regole. Attività attuale non misurata.")
        };

        // Limita il numero di righe renderizzate, non il contenuto del rapporto:
        // le evidenze originali restano sempre consultabili integralmente.
        int limit = Math.Clamp(maxEvidence, 0, 50);
        var evidenceFacts = insight.Evidence
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .SelectMany(ExpandAuditEvidence)
            .ToList();
        facts.AddRange(evidenceFacts.Take(limit));
        if (evidenceFacts.Count > limit)
            facts.Add(new("Altre osservazioni", "Altri dati disponibili",
                $"Altre {evidenceFacts.Count - limit} voci nel rapporto originale."));
        return facts;
    }

    private static IEnumerable<Fact> ExpandAuditEvidence(string raw)
    {
        var cleaned = raw.Trim();
        int separator = cleaned.IndexOf("):", StringComparison.Ordinal);
        separator = separator >= 0 ? separator + 1 : cleaned.IndexOf(':');
        string prefix = separator > 0 ? cleaned[..separator].Trim() : cleaned;
        string detail = separator > 0 ? cleaned[(separator + 1)..].Trim() : cleaned;
        string group = EvidenceGroup(cleaned);
        string label = EvidenceDisplayLabel(prefix);

        // Soltanto le evidenze di classificazione prodotte dal programma
        // vengono divise visivamente nei sotto-elementi delimitati da "; ".
        // Non si analizzano né si modificano righe di comando arbitrarie.
        bool structured = prefix.StartsWith("Identità", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("Applicazione associata", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("Funzione del processo", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("Ambiente di esecuzione", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("Appartenenza", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("Ruolo", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("Framework", StringComparison.OrdinalIgnoreCase);
        var parts = structured
            ? detail.Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { detail };
        foreach (var part in parts)
        {
            string itemLabel = label;
            string itemValue = part;
            int equals = part.IndexOf(" = ", StringComparison.Ordinal);
            if (structured && equals > 0 && equals < 80)
            {
                itemLabel = MetadataDisplayLabel(part[..equals].Trim(), label);
                itemValue = part[(equals + 3)..].Trim();
            }
            else if (part.StartsWith("knowledge:", StringComparison.OrdinalIgnoreCase))
                itemLabel = "Identificativo della regola interna";
            yield return new Fact(group, itemLabel,
                string.IsNullOrWhiteSpace(itemValue) ? "Non specificato" : itemValue);
        }
    }

    private static string MetadataDisplayLabel(string key, string fallback)
    {
        if (key.Equals("descrizione file", StringComparison.OrdinalIgnoreCase)) return "Descrizione del file eseguibile";
        if (key.Equals("prodotto", StringComparison.OrdinalIgnoreCase)) return "Nome prodotto";
        if (key.Equals("publisher metadata", StringComparison.OrdinalIgnoreCase)) return "Editore dichiarato";
        if (key.Equals("identità package Windows", StringComparison.OrdinalIgnoreCase)) return "Identificativo pacchetto Windows";
        if (key.Equals("AUMID Windows", StringComparison.OrdinalIgnoreCase)) return "Identificativo applicazione Windows (AUMID)";
        return key.Length > 64 ? fallback : key;
    }

    private static string EvidenceDisplayLabel(string prefix)
    {
        if (prefix.StartsWith("Identità", StringComparison.OrdinalIgnoreCase)) return "Identificazione dell'eseguibile";
        if (prefix.StartsWith("Appartenenza", StringComparison.OrdinalIgnoreCase) ||
            prefix.StartsWith("Applicazione associata", StringComparison.OrdinalIgnoreCase)) return "Applicazione associata";
        if (prefix.StartsWith("Ruolo", StringComparison.OrdinalIgnoreCase) ||
            prefix.StartsWith("Funzione del processo", StringComparison.OrdinalIgnoreCase)) return "Funzione del processo";
        if (prefix.StartsWith("Framework", StringComparison.OrdinalIgnoreCase) ||
            prefix.StartsWith("Ambiente di esecuzione", StringComparison.OrdinalIgnoreCase)) return "Ambiente di esecuzione";
        if (prefix.StartsWith("Origine", StringComparison.OrdinalIgnoreCase)) return "Origine del processo";
        if (prefix.StartsWith("knowledge:", StringComparison.OrdinalIgnoreCase)) return "Identificativo della regola interna";
        return prefix.Length > 65 ? prefix[..65] + "…" : prefix;
    }

    public static string ClosingRiskLabel(string? risk) =>
        SysPulseLocalization.ClosingRiskLabel(risk);

    public static string TechnicalGroup(string label)
    {
        if (label.StartsWith("CPU", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("Tempo CPU", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("RAM", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("Working Set", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("Memoria", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("Thread", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("Handle", StringComparison.OrdinalIgnoreCase))
            return "Risorse";

        if (label.StartsWith("PID -", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("Famiglia", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("Figli", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("Servizi", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("Catena padre", StringComparison.OrdinalIgnoreCase))
            return "Relazioni";
        return "Identità";
    }

    public static string EvidenceGroup(string? evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence)) return "Altre osservazioni";
        var e = evidence.Trim();
        if (StartsAny(e, "Origine:", "PPID", "Catena padre:", "Comando osservato"))
            return "Avvio e relazioni";
        if (StartsAny(e, "Identità", "Appartenenza", "Applicazione associata", "Eseguibile:", "Package Windows:", "AUMID Windows:"))
            return "Identità e applicazione";
        if (StartsAny(e, "Ruolo", "Framework", "Funzione del processo", "Ambiente di esecuzione", "knowledge:", "Command line", "Classificatore"))
            return "Funzione e ambiente di esecuzione";
        return "Altre osservazioni";
    }

    private static bool StartsAny(string text, params string[] candidates) =>
        candidates.Any(value => text.StartsWith(value, StringComparison.OrdinalIgnoreCase));
}
