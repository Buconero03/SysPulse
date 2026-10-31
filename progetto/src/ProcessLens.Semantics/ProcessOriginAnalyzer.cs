using System.Text.RegularExpressions;
using ProcessLens.Core;

namespace ProcessLens.Semantics;

/// <summary>
/// Generic process provenance, with an optional Git command adapter.
/// The parent-chain comes from Windows; it is not a proof of user intent.
/// No background instrumentation or Git-specific process-name overrides.
/// </summary>
public static class ProcessOriginAnalyzer
{
    private static readonly Dictionary<string, string> GitOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["push"] = "Invio delle modifiche al repository remoto (push)",
        ["pull"] = "Recupero e integrazione delle modifiche remote (pull)",
        ["fetch"] = "Recupero degli aggiornamenti remoti (fetch)",
        ["clone"] = "Clonazione di un repository",
        ["commit"] = "Creazione di un commit locale",
        ["add"] = "Aggiunta di modifiche all'indice Git",
        ["status"] = "Lettura dello stato del repository",
        ["diff"] = "Confronto delle modifiche",
        ["merge"] = "Integrazione di rami (merge)",
        ["rebase"] = "Riorganizzazione dei commit (rebase)",
        ["checkout"] = "Cambio di riferimento o ripristino di file",
        ["switch"] = "Cambio di ramo",
        ["restore"] = "Ripristino di file",
        ["branch"] = "Gestione dei rami",
        ["log"] = "Consultazione della cronologia dei commit",
        ["ls-remote"] = "Interrogazione dei riferimenti remoti"
    };

    public static ProcessOriginReport Analyze(ProcessSnapshot process)
    {
        var ancestors = process.AncestorDetails;
        var nearest = ancestors.FirstOrDefault();
        string launch = nearest is null
            ? $"PPID: {process.ParentPid}. Processo padre non disponibile."
            : nearest.VerifiedByStartTime
                ? $"Padre: {nearest.Name} · PID {nearest.Pid}. Orari di avvio compatibili."
                : $"Possibile padre: {nearest.Name} · PID {nearest.Pid}. Orari di avvio insufficienti per la verifica.";

        var steps = ancestors.Reverse().Select(a =>
            $"{a.Name} (PID {a.Pid}) · Avvio: {ProcessStartFormatter.FormatStart(a.StartTime)}" +
            (a.VerifiedByStartTime ? "" : " [non verificato]"))
            .Append($"{process.Name} (PID {process.Pid}) · Avvio: {ProcessStartFormatter.FormatStart(process.StartTime)}");
        string chain = string.Join("\n  ↓\n", steps);
        if (ancestors.Count == 0) chain = $"{process.Name} (PID {process.Pid}) · Avvio: {ProcessStartFormatter.FormatStart(process.StartTime)}\nOrigine precedente non ricostruibile nello snapshot.";

        // The command of the selected process is evidence about the requested
        // operation. An ancestor's command is used only when its relationship
        // to the selected process was verified from creation timestamps.
        GitCommand? git = TryGitCommand(process.Name, process.CommandLine);
        string cmdSource = $"PID {process.Pid}";
        if (git is null)
        {
            foreach (var ancestor in ancestors)
            {
                if (!ancestor.VerifiedByStartTime) break;
                git = TryGitCommand(ancestor.Name, ancestor.CommandLine);
                if (git is not null)
                {
                    cmdSource = $"PID antenato {ancestor.Pid}";
                    break;
                }
            }
        }

        string operation;
        string status;
        var evidence = new List<string>();
        if (nearest is not null)
            evidence.Add($"PPID {process.ParentPid} → PID {nearest.Pid}; orari di avvio {(nearest.VerifiedByStartTime ? "coerenti" : "non verificabili")}");
        else
            evidence.Add($"PPID {process.ParentPid} senza istanza padre osservabile");

        if (git is not null)
        {
            operation = $"{git.Description}.";
            if (git.WorkingDirectory is not null)
                operation += $" Directory indicata dall'opzione -C: {git.WorkingDirectory}.";
            status = "Comando Git identificato. Esito e avanzamento sconosciuti.";
            evidence.Add($"Comando osservato ({cmdSource}): git {git.Verb}");
            if (git.WorkingDirectory is not null)
                evidence.Add("Directory rilevata da argomento -C; non è necessariamente la root del repository.");
        }
        else if (IsGitProcess(process.Name))
        {
            operation = "Componente Git in esecuzione; sottocomando non identificabile dai dati disponibili.";
            status = "Operazione Git sconosciuta; il nome del processo non identifica un push.";
        }
        else
        {
            operation = "Operazione sconosciuta.";
            status = "Motivo dell’avvio sconosciuto; il padre non basta a ricostruirlo.";
        }

        return new ProcessOriginReport(launch, chain, operation, status, evidence);
    }

    private static bool IsGitProcess(string name) =>
        name.Equals("git.exe", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("git", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("git-remote-", StringComparison.OrdinalIgnoreCase);

    private static GitCommand? TryGitCommand(string processName, string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine) || !IsGitProcess(processName))
            return null;

        // Tokenization is used only to recognize the verb, never to execute a
        // command. Quoted Windows paths stay in one token.
        var tokens = Regex.Matches(commandLine, @"(?:""[^""]*""|\S+)")
            .Select(m => m.Value.Trim('"')).ToArray();
        int start = Array.FindIndex(tokens, t =>
            t.Equals("git", StringComparison.OrdinalIgnoreCase) ||
            t.EndsWith("\\git.exe", StringComparison.OrdinalIgnoreCase) ||
            t.EndsWith("/git.exe", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("git.exe", StringComparison.OrdinalIgnoreCase));
        if (start < 0) return null;

        string? directory = null;
        for (int i = start + 1; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (token is "-C" or "-c" or "--git-dir" or "--work-tree" or "--config-env")
            {
                if (i + 1 < tokens.Length)
                {
                    string value = tokens[++i];
                    if (token == "-C") directory = value;
                }
                continue;
            }
            if (token.StartsWith("-C", StringComparison.Ordinal) && token.Length > 2)
            {
                directory = token[2..];
                continue;
            }
            if (token.StartsWith("-", StringComparison.Ordinal)) continue;
            if (GitOperations.TryGetValue(token, out var description))
                return new GitCommand(token.ToLowerInvariant(), description, directory);
            // First non-option is the subcommand. Unknown subcommands must
            // not be skipped: doing so could misidentify a later argument.
            return null;
        }
        return null;
    }

    private sealed record GitCommand(string Verb, string Description, string? WorkingDirectory);
}

public sealed record ProcessOriginReport(
    string ParentSummary,
    string Chain,
    string Operation,
    string Limitations,
    IReadOnlyList<string> Evidence);
