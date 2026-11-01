using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ProcessLens.Core;

namespace ProcessLens.Semantics;

/// <summary>
/// Natural-language query layer over process evidence. It never invents progress or ETA:
/// estimates are emitted only when an explicit progress signal can be observed and at
/// least two comparable samples show forward movement.
/// </summary>
public sealed class ProcessQueryEngine
{
    public ProcessQueryResult Ask(
        ProcessSnapshot process,
        ProcessInsight insight,
        string question,
        ProcessProgressSample? previousProgress,
        IReadOnlyList<double>? recentCpuSamples = null,
        MemoryTrendReport? memoryTrend = null, MemoryMapSnapshot? memoryMap = null, SystemMemoryEvidence? systemMemory = null)
    {
        question = (question ?? string.Empty).Trim();
        if (question.Length == 0)
            return Help();

        var intents = DetectIntents(question);
        var progress = ProgressDetector.TryDetect(process);
        ProcessProgressSample? currentSample = progress is null
            ? null
            : new ProcessProgressSample(process.Pid, process.StartTime, DateTime.UtcNow, progress.Percent, progress.SourceKey, progress.Description);

        if (intents.Count == 0)
            return Help();

        var answers = intents
            .Select(intent => AnswerIntent(intent, process, insight, progress, currentSample, previousProgress, recentCpuSamples, memoryTrend, memoryMap, systemMemory))
            .ToList();

        if (answers.Count == 1)
            return answers[0];

        string combined = string.Join("\n\n", answers.Select(a => $"{a.Title.ToUpperInvariant()}\n{a.Answer}"));
        var combinedEvidence = answers.SelectMany(a => a.Evidence).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var sample = answers.Select(a => a.ProgressSample).FirstOrDefault(x => x is not null);
        return new ProcessQueryResult("Risposta composta", combined, "PER SEZIONE", combinedEvidence, sample);
    }

    private static ProcessQueryResult AnswerIntent(
        ProcessQueryIntent intent,
        ProcessSnapshot process,
        ProcessInsight insight,
        ProgressEvidence? progress,
        ProcessProgressSample? currentSample,
        ProcessProgressSample? previousProgress,
        IReadOnlyList<double>? recentCpuSamples,
        MemoryTrendReport? memoryTrend, MemoryMapSnapshot? memoryMap, SystemMemoryEvidence? systemMemory)
    {
        var evidence = new List<string>();
        return intent switch
        {
            ProcessQueryIntent.Activity => ProcessBehaviorExplainer.Explain(process, insight),
            ProcessQueryIntent.Progress => Progress(process, insight, progress, currentSample, evidence),
            ProcessQueryIntent.Eta => Eta(process, insight, progress, currentSample, previousProgress, evidence),
            ProcessQueryIntent.Health => Health(process, insight, recentCpuSamples, evidence),
            ProcessQueryIntent.Cpu => Cpu(process, insight, evidence),
            ProcessQueryIntent.Memory => Memory(process, insight, memoryTrend, memoryMap, systemMemory, evidence),
            ProcessQueryIntent.Network => Network(process, evidence),
            ProcessQueryIntent.Origin => Origin(process, insight, evidence),
            ProcessQueryIntent.WhyRunning => WhyRunning(process, insight, evidence),
            ProcessQueryIntent.Termination => Termination(process, insight, evidence),
            _ => Help()
        };
    }

    private static ProcessQueryResult Memory(ProcessSnapshot p, ProcessInsight i,
        MemoryTrendReport? trend, MemoryMapSnapshot? map, SystemMemoryEvidence? systemMemory, List<string> evidence)
    {
        if (!p.PrivateWorkingSetAvailable && !p.WorkingSetAvailable && !p.PrivateCommitAvailable)
        {
            return Result("Perché occupa RAM?",
                "■ DIAGNOSI IN BREVE\n" +
                $"Il processo {p.Name} (PID {p.Pid}) è protetto da Windows o non espone contatori di memoria leggibili. " +
                "L'assenza del dato indica un limite di accesso del sistema operativo, non un consumo pari a zero.\n\n" +
                "■ COME APPROFONDIRE\n" +
                "Verifica che il PID sia ancora attivo e che ProcessLens disponga dei permessi necessari.",
                "DATI INSUFFICIENTI", evidence);
        }

        var blocks = new List<string>();
        string processLabel = string.IsNullOrWhiteSpace(p.Name) ? $"PID {p.Pid}" : $"{p.Name} (PID {p.Pid})";
        string roleLabel = string.IsNullOrWhiteSpace(i.Role) ? "ruolo non identificato" : i.Role;
        string roleText = $"{p.Name} {i.Role} {i.Summary} {i.Framework}".ToLowerInvariant();

        // Calcolo indicatori tecnici reali (quota su sistema, quota condivisa, rapporto RAM/Commit)
        string systemShareInfo = "";
        if (systemMemory is { TotalBytes: > 0 } && (p.PrivateWorkingSetAvailable || p.WorkingSetAvailable))
        {
            double activeBytes = p.PrivateWorkingSetAvailable ? p.PrivateWorkingSetBytes : p.WorkingSetBytes;
            double pctOfRam = 100.0 * activeBytes / systemMemory.TotalBytes;
            systemShareInfo = $" ({pctOfRam:F2}% della RAM fisica del PC)";
        }

        string residentHeadline = p.PrivateWorkingSetAvailable
            ? $"{p.PrivateWorkingSetMb:F1} MiB di Working Set privato{systemShareInfo}"
            : p.WorkingSetAvailable
                ? $"{p.WorkingSetMb:F1} MiB di Working Set totale{systemShareInfo} (include pagine condivisibili)"
                : $"solo contatore di commit privato ({p.PrivateMb:F1} MiB)";

        string trendSummary = trend?.HasTrend == true
            ? trend.DeltaMb >= 5
                ? $"{trend.Metric}: +{trend.DeltaMb:F1} MiB negli ultimi {trend.Seconds / 60d:F1} min · {trend.FirstMb:F1} -> {trend.CurrentMb:F1} MiB."
                : trend.DeltaMb <= -5
                    ? $"{trend.Metric}: -{Math.Abs(trend.DeltaMb):F1} MiB negli ultimi {trend.Seconds / 60d:F1} min · {trend.FirstMb:F1} -> {trend.CurrentMb:F1} MiB."
                    : $"{trend.Metric}: {trend.DeltaMb:+0.0;-0.0;0.0} MiB negli ultimi {trend.Seconds / 60d:F1} min. Escursione: {trend.ExcursionMb:F1} MiB; possibili oscillazioni intermedie."
            : "Tendenza: in attesa di 3 campioni su almeno 20 s.";

        string primaryDriver = "";
        if (map?.Available == true && map.TotalCommittedMb > 0)
        {
            if (map.PrivateMb >= map.ImageMb && map.PrivateMb >= map.MappedMb)
            {
                string detail = map.ReadWritePrivateMb > 0
                    ? $"\n• Pagine private in lettura/scrittura (RW): {map.ReadWritePrivateMb:F1} MiB." +
                      (map.LargeBlocksCount > 0 ? $"\n• Blocchi ≥4 MiB: {map.LargeBlocksCount} · totale {map.LargeBlocksMb:F1} MiB." : "")
                    : "";
                primaryDriver = $"\nLa memoria impegnata è composta soprattutto da allocazioni private: {map.PrivateMb:F1} MiB." + detail;
            }
            else if (map.ImageMb >= map.PrivateMb && map.ImageMb >= map.MappedMb)
            {
                primaryDriver = $"\nLa memoria impegnata è composta soprattutto da eseguibili e DLL: {map.ImageMb:F1} MiB.";
            }
            else
            {
                primaryDriver = $"\nFile mappati e sezioni condivise: {map.MappedMb:F1} MiB.";
            }
        }

        blocks.Add("■ DIAGNOSI IN BREVE\n" +
                   $"Processo: {processLabel}\nRuolo: {roleLabel} · {i.Framework}\n• Uso attuale: {residentHeadline}.\n" +
                   (p.WorkingSetAvailable && p.PrivateWorkingSetAvailable
                       ? $"• Working Set totale: {p.WorkingSetMb:F1} MiB\n• Commit privato: {(p.PrivateCommitAvailable ? $"{p.PrivateMb:F1} MiB" : "N/D")}\n"
                       : "") +
                   $"{trendSummary}{primaryDriver}");

        // Sezione Valutazione
        string pressureDetail;
        if (systemMemory is { TotalBytes: > 0 })
        {
            double used = systemMemory.UsedPercent;
            double availableGiB = systemMemory.AvailableMb / 1024d;
            double totalGiB = systemMemory.TotalMb / 1024d;
            pressureDetail = $"Stato RAM del sistema: {used:F1}% in uso ({availableGiB:F2} GiB liberi su {totalGiB:F2} GiB totali). ";
            pressureDetail += used >= 85
                ? "Uso RAM elevato: almeno 85%. Valuta tutti i processi; il dato non identifica un responsabile."
                : used >= 75
                    ? "Uso RAM tra 75% e 85%: controlla la memoria disponibile."
                    : "Il carico sulla RAM del sistema è normale.";
            evidence.Add($"GlobalMemoryStatusEx: RAM totale {systemMemory.TotalBytes} byte, disponibile {systemMemory.AvailableBytes} byte");
        }
        else
            pressureDetail = "RAM del sistema: N/D.";

        bool growing = trend?.HasTrend == true && trend.Seconds >= 60 && trend.DeltaMb >= 20 &&
                       trend.FirstMb > 0 && trend.DeltaMb / trend.FirstMb >= 0.20;
        string judgment = growing
            ? $"CONSUMO IN AUMENTO: +{trend!.DeltaMb:F1} MiB negli ultimi {trend.Seconds / 60d:F1} min. Verifica se si stabilizza."
            : trend?.HasTrend == true && trend.DeltaMb <= -5
                ? $"CONSUMO IN CALO: {trend.DeltaMb:F1} MiB negli ultimi {trend.Seconds / 60d:F1} min."
                : trend?.HasTrend == true && Math.Abs(trend.DeltaMb) < 5
                    ? $"VARIAZIONE NETTA LIMITATA: {trend.DeltaMb:+0.0;-0.0;0.0} MiB negli ultimi {trend.Seconds / 60d:F1} min.\nMinimo: {trend.MinimumMb:F1} MiB · picco: {trend.PeakMb:F1} MiB."
                    : "TENDENZA DA VERIFICARE: servono altri campioni confrontabili.";

        string commitVsRamRatio = "";
        if (p.PrivateWorkingSetAvailable && p.PrivateCommitAvailable && p.PrivateBytes > 0)
        {
            double residencyPct = Math.Clamp(100.0 * p.PrivateWorkingSetBytes / p.PrivateBytes, 0, 100);
            double nonResidentCommitMb = Math.Max(0, p.PrivateMb - p.PrivateWorkingSetMb);
            commitVsRamRatio = $"\nWorking Set privato / commit: {residencyPct:F0}%.\n• Working Set privato: {p.PrivateWorkingSetMb:F1} MiB.\n• Commit privato: {p.PrivateMb:F1} MiB.\n• Differenza tra contatori: {nonResidentCommitMb:F1} MiB; non è una misura delle pagine fuori RAM.\nLe pagine non residenti sono allocate ma al momento non occupano RAM fisica.";
        }

        blocks.Add("■ VALUTAZIONE: È UN CONSUMO ECCESSIVO?\n" +
                   judgment + "\n" + pressureDetail + commitVsRamRatio);

        // Sezione Come si distribuisce la memoria
        var metrics = new List<string>();
        if (p.PrivateWorkingSetAvailable)
        {
            metrics.Add($"• Working Set privato (Private Working Set): {p.PrivateWorkingSetMb:F1} MiB — Pagine private presenti in RAM.");
            evidence.Add($"Private Working Set: {p.PrivateWorkingSetBytes} byte");
        }
        else metrics.Add("• Working Set privato: contatore non disponibile.");

        if (p.WorkingSetAvailable)
        {
            metrics.Add($"• Working Set totale (Working Set): {p.WorkingSetMb:F1} MiB — Include pagine private e condivisibili.");
            evidence.Add($"Working Set: {p.WorkingSetBytes} byte");
            if (p.PrivateWorkingSetAvailable && p.WorkingSetBytes >= p.PrivateWorkingSetBytes)
            {
                double sharedMb = p.WorkingSetMb - p.PrivateWorkingSetMb;
                double sharedPct = p.WorkingSetBytes > 0 ? 100.0 * sharedMb / p.WorkingSetMb : 0;
                metrics.Add($"• Quota residente condivisibile: {sharedMb:F1} MiB ({sharedPct:F0}% del Working Set totale).");
            }
        }
        if (p.PrivateCommitAvailable)
        {
            metrics.Add($"• Memoria privata impegnata (Private Commit): {p.PrivateMb:F1} MiB — Memoria con copertura garantita da Windows; può non essere residente in RAM.");
            evidence.Add($"Private Commit: {p.PrivateBytes} byte");
        }
        blocks.Add("■ COME SI DISTRIBUISCE LA MEMORIA\n" + string.Join("\n", metrics) +
                   "\nIl Working Set totale include il privato. Non sommare Working Set e commit.");

        // Sezione Andamento nel tempo
        if (trend?.HasTrend == true)
        {
            double minutes = trend.Seconds / 60d;
            double rate = minutes > 0 ? trend.DeltaMb / minutes : 0;
            double pctChange = trend.FirstMb > 0 ? 100.0 * trend.DeltaMb / trend.FirstMb : 0;
            string assessment = trend.DeltaMb >= 15
                ? "Consumo in aumento. Verifica se cresce anche a riposo."
                : trend.DeltaMb <= -15
                    ? "Consumo in calo. La causa del calo non è misurata."
                    : "Variazione netta contenuta. Controlla anche minimo e picco.";
            blocks.Add("■ ANDAMENTO MISURATO NEL TEMPO\n" +
                       $"• Contatore tracciato: {trend.Metric} ({trend.Samples} letture in {minutes:F1} minuti).\n" +
                       $"• Valore iniziale: {trend.FirstMb:F1} MiB → Valore attuale: {trend.CurrentMb:F1} MiB " +
                       $"(Δ {trend.DeltaMb:+0.0;-0.0;0.0} MiB, {pctChange:+0.0;-0.0;0.0}%, velocità media {rate:+0.0;-0.0;0.0} MiB/min).\n" +
                       $"• Range osservato: minimo {trend.MinimumMb:F1} MiB · picco {trend.PeakMb:F1} MiB (escursione {trend.ExcursionMb:F1} MiB).\n" +
                       assessment + " Un solo intervallo non basta per diagnosticare un memory leak.");
            evidence.Add($"Trend: {trend.Samples} campioni in {trend.Seconds:F0} secondi ({trend.Metric})");
        }
        else
            blocks.Add("■ ANDAMENTO MISURATO NEL TEMPO\n" +
                       "Servono almeno tre letture distribuite su almeno 20 secondi per calcolare velocità di crescita e picchi. " +
                       "Attendi qualche secondo e riesegui l'analisi.");

        // Sezione Mappa della memoria virtuale (arricchita con scomposizione per permessi e grandi blocchi!)
        if (map?.Available == true)
        {
            double totalVm = Math.Max(0.1, map.TotalCommittedMb);
            var vmLines = new List<string>
            {
                $"• Dati e Allocazioni Private (MEM_PRIVATE): {map.PrivateMb:F1} MiB ({100.0 * map.PrivateMb / totalVm:F0}% del totale impegnato).",
                $"• Moduli Eseguibili e Librerie DLL (MEM_IMAGE): {map.ImageMb:F1} MiB ({100.0 * map.ImageMb / totalVm:F0}%).",
                $"• File Mappati e Sezioni Condivise (MEM_MAPPED): {map.MappedMb:F1} MiB ({100.0 * map.MappedMb / totalVm:F0}%)."
            };
            if (map.OtherMb > 0.05)
                vmLines.Add($"• Altre regioni impegnate: {map.OtherMb:F1} MiB.");

            if (map.HasDetailedBreakdown)
            {
                vmLines.Add("Memoria privata per permessi di pagina:");
                if (map.ReadWritePrivateMb > 0)
                    vmLines.Add($"  ◦ Pagine private in lettura/scrittura (READWRITE): {map.ReadWritePrivateMb:F1} MiB");
                if (map.ExecutablePrivateMb > 0)
                    vmLines.Add($"  ◦ Pagine private eseguibili (EXECUTE): {map.ExecutablePrivateMb:F1} MiB");
                if (map.ReadOnlyPrivateMb > 0)
                    vmLines.Add($"  ◦ Pagine private in sola lettura (READONLY): {map.ReadOnlyPrivateMb:F1} MiB");
                if (map.GuardStackMb > 0)
                    vmLines.Add($"  ◦ Pagine di guardia (PAGE_GUARD): {map.GuardStackMb:F1} MiB");
                if (map.LargeBlocksCount > 0)
                    vmLines.Add($"  ◦ Grandi allocazioni contigue (≥ 4 MiB): {map.LargeBlocksCount} blocchi per un totale di {map.LargeBlocksMb:F1} MiB (blocco singolo max: {map.LargestSingleBlockMb:F1} MiB).");
                if (map.ReservedMb > 0)
                    vmLines.Add($"  ◦ Spazio di indirizzamento prenotato ma non impegnato (MEM_RESERVE): {map.ReservedMb:F1} MiB (non occupa RAM né file di paging).");
            }
            vmLines.Add($"• Totale regioni ispezionate: {map.Regions:N0} ({(map.Truncated ? "scansione parziale per limite di tempo" : "scansione completa")}).");
            vmLines.Add("Nota: questi valori descrivono lo spazio virtuale impegnato (MEM_COMMIT). Non devono essere sommati ai MiB residenti.");

            blocks.Add("■ MAPPA DELLA MEMORIA VIRTUALE\n" + string.Join("\n", vmLines));
            evidence.Add($"VirtualQueryEx: {map.Regions} regioni; parziale={map.Truncated}");
        }
        else
            blocks.Add("■ MAPPA DELLA MEMORIA VIRTUALE\n" +
                       (map is null
                           ? "Mappa virtuale non richiesta in questa lettura."
                           : "Mappa virtuale non accessibile: " + map.Note));

        // Sezione Componenti tipici per architettura/runtime
        string[] plausibleReasons;
        string profiler;
        if (ContainsAny(roleText, "renderer", "chromium", "electron", "webview", "render", "browser", "interfaccia"))
        {
            plausibleReasons = new[]
            {
                "Heap JavaScript (V8), albero DOM/CSS e stato dell'interfaccia web ospitata nel processo.",
                "Superfici grafiche decodificate, cache delle risorse web e librerie condivise Chromium."
            };
            profiler = "Apri i DevTools del programma (scheda Memory → Heap Snapshot) per vedere i singoli oggetti JavaScript/DOM.";
        }
        else if (ContainsAny(roleText, "python", "interprete python"))
        {
            plausibleReasons = new[]
            {
                "Oggetti nell'heap dell'interprete Python, dizionari, liste e moduli caricati in memoria.",
                "Array numerici/tensori allocati da librerie C/C++ native (NumPy, Pandas, PyTorch) nei blocchi MEM_PRIVATE RW."
            };
            profiler = "Usa tracemalloc o memory_profiler dentro lo script Python per vedere la riga di codice che alloca più memoria.";
        }
        else if (ContainsAny(roleText, "node", "javascript", "typescript", "v8"))
        {
            plausibleReasons = new[]
            {
                "Heap gestito dal motore V8 (oggetti JS/TS, chiusure, AST del codice se è un language server o bundler).",
                "Buffer nativi (ArrayBuffer / Buffer Node.js) e cache dei moduli importati."
            };
            profiler = "Avvia Node.js con --heapsnapshot-signal o usa Chrome DevTools collegato all'inspector V8.";
        }
        else if (ContainsAny(roleText, ".net", "dotnet", "clr", "c#"))
        {
            plausibleReasons = new[]
            {
                "Heap gestito dal Garbage Collector .NET (Generazioni 0/1/2 e Large Object Heap per array > 85 KB).",
                "Codice nativo compilato dal JIT, metadati degli assembly caricati e buffer non gestiti."
            };
            profiler = "Usa direttamente la voce 'Profilazione heap .NET · oggetti reali' qui nel menu Analisi approfondita di ProcessLens.";
        }
        else if (ContainsAny(roleText, "java", "jvm"))
        {
            plausibleReasons = new[]
            {
                "Heap della Java Virtual Machine (Young/Old Generation) e cache applicative.",
                "Metaspace delle classi, stack dei thread e memoria Direct ByteBuffer fuori dall'heap."
            };
            profiler = "Usa Java Flight Recorder (JFR) o jcmd GC.class_histogram sul PID selezionato.";
        }
        else
        {
            plausibleReasons = new[]
            {
                "Strutture dati nell'heap nativo del processo, buffer di lavoro e stato interno dell'applicazione.",
                "Cache mantenute in RAM per velocizzare le operazioni successive e librerie DLL mappate."
            };
            profiler = "Se è un processo .NET usa il profiler integrato di ProcessLens; per processi nativi usa Windows Performance Recorder (ETW VirtualAlloc/Heap).";
        }

        blocks.Add("■ COMPONENTI TIPICI · NON MISURATI\n" +
                   $"Componenti tipici di {roleLabel} · {i.Framework}; non misurati da questa analisi:\n" +
                   string.Join("\n", plausibleReasons.Select(x => "• " + x)) +
                   (p.CpuSampleAvailable
                       ? $"\nNota attività: la CPU attuale è {p.CpuPercent:F1}%. " +
                         (p.CpuPercent < 0.3
                             ? "Anche con CPU a riposo è normale che un programma mantenga allocata la propria RAM per riutilizzarla rapidamente."
                             : "Il processo sta eseguendo lavoro attivo sulla CPU, il che può comportare allocazioni temporanee.")
                       : ""));

        blocks.Add("■ QUANDO APPROFONDIRE E STRUMENTI CONSIGLIATI\n" +
                   "• Quando approfondire: Working Set privato in crescita continua a parità di lavoro; attenzione anche all’uso RAM del sistema.\n" +
                   "• EmptyWorkingSet rimuove pagine dal Working Set. Gli accessi successivi possono causare page fault e rallentamenti.\n" +
                   "• Strumento per vedere i singoli oggetti interni: " + profiler);

        int evaluationIndex = blocks.FindIndex(x => x.StartsWith("■ VALUTAZIONE:", StringComparison.Ordinal));
        if (evaluationIndex > 1)
        {
            var evaluation = blocks[evaluationIndex];
            blocks.RemoveAt(evaluationIndex);
            blocks.Insert(1, evaluation);
        }
        return Result("Perché occupa RAM?", string.Join("\n\n", blocks),
            "CONTATORI · REGIONI VIRTUALI · CAMPIONE PAGINE", evidence);
    }

    private static ProcessQueryResult Cpu(ProcessSnapshot p, ProcessInsight insight, List<string> evidence)
    {
        if (!p.CpuSampleAvailable)
            return Result("Consumo CPU", "Windows non ha ancora fornito due letture comparabili per questo PID. " +
                "Attendi il prossimo aggiornamento oppure verifica i permessi; N/D non significa 0%.", "DATI INSUFFICIENTI", evidence);

        evidence.Add($"CPU del PID su tutta la macchina: {p.CpuPercent:F2}%");
        evidence.Add($"Media/picco fino a 30 s: {p.CpuAverage30Seconds:F2}% / {p.CpuPeak30Seconds:F2}%");
        evidence.Add($"Intervallo campione: {p.CpuSampleIntervalSeconds:F1}s");
        string level = p.CpuAverage30Seconds switch
        {
            >= 20 => "media CPU elevata nei campioni disponibili",
            >= 5 => "media CPU moderata nei campioni disponibili",
            >= 1 => "attività CPU misurabile",
            _ => "media CPU bassa nei campioni disponibili"
        };
        string explanation =
            $"CPU nell’intervallo campionato: {p.CpuPercent:F1}% della capacità del PC. " +
            $"Media nel periodo disponibile (fino a 30 s): {p.CpuAverage30Seconds:F1}%, picco: {p.CpuPeak30Seconds:F1}%. " +
            $"Equivalente approssimativo: {p.CpuLogicalCoreEquivalent:F2} core logici a pieno utilizzo (equivalente, non conteggio dei core effettivi).\n\n" +
            $"Interpretazione: {level}. Le regole suggeriscono il tipo {insight.Role}. " +
            "Questo aiuta a comprendere a cosa serve, ma non dimostra quale funzione interna stia impegnando la CPU. " +
            "Per attribuire il consumo a una specifica funzione servirebbe un campionamento degli stack dei thread/profilazione (ETW), non solo Task Manager.";
        return Result("Consumo CPU e significato", explanation, "CPU OSSERVATA; CAUSA NON PROVATA", evidence);
    }

    private static ProcessQueryResult Activity(ProcessSnapshot p, ProcessInsight i, List<string> evidence)
    {
        evidence.Add($"Tipo proposto dalle regole: {i.Role}. Il punteggio del classificatore è disponibile nel rapporto tecnico.");
        evidence.Add(p.CpuSampleAvailable ? $"CPU osservata: {p.CpuPercent:F1}%" : "CPU: campione non disponibile");

        var observed = new List<string>();
        if (!p.CpuSampleAvailable) observed.Add("CPU non disponibile");
        else if (p.CpuPercent >= 10) observed.Add("CPU molto attiva");
        else if (p.CpuPercent >= 2) observed.Add("CPU attiva");
        else if (p.CpuPercent >= 0.2) observed.Add("attività CPU leggera");
        else observed.Add("utilizzo CPU minimo in questo intervallo");

        if (p.IsDeepInspected)
        {
            if (p.TcpListeningCount > 0)
            {
                observed.Add($"{p.TcpListeningCount} socket TCP in ascolto");
                evidence.Add($"Porte TCP LISTEN: {(p.ListeningTcpPorts.Count == 0 ? "non esposte" : string.Join(", ", p.ListeningTcpPorts))}");
            }
            if (p.TcpEstablishedCount > 0)
            {
                observed.Add($"{p.TcpEstablishedCount} connessioni TCP stabilite");
                evidence.Add($"TCP ESTABLISHED attribuite al PID: {p.TcpEstablishedCount}");
            }
            if (p.UdpEndpointCount > 0)
                observed.Add($"{p.UdpEndpointCount} endpoint UDP");

            bool directNetwork = p.TcpListeningCount > 0 || p.TcpEstablishedCount > 0 || p.UdpEndpointCount > 0;
            bool familyNetwork = p.NetworkFamilyTcpListeningCount > 0 ||
                                 p.NetworkFamilyTcpEstablishedCount > 0 ||
                                 p.NetworkFamilyUdpEndpointCount > 0;
            if (!directNetwork && familyNetwork && p.NetworkFamilyProcessCount > 1)
            {
                observed.Add("nessuna socket diretta; rete osservata in processi correlati dello stesso eseguibile");
                evidence.Add($"Famiglia stesso eseguibile con socket: {p.NetworkFamilyProcessesWithSockets}/{p.NetworkFamilyProcessCount}");
            }

            if (p.HasVisibleWindow)
                observed.Add("possiede una finestra visibile");
        }

        string body =
            $"{i.Summary}. {i.WhatIsIt}\n\n" +
            $"Adesso: {string.Join("; ", observed)}.";

        return Result("Cosa sta facendo", body, i.RoleConfidenceLabel, evidence);
    }

    private static ProcessQueryResult Progress(
        ProcessSnapshot p,
        ProcessInsight i,
        ProgressEvidence? progress,
        ProcessProgressSample? sample,
        List<string> evidence)
    {
        if (progress is not null)
        {
            evidence.Add(progress.Description);
            return Result(
                "Progresso",
                $"Ho riconosciuto un possibile indicatore testuale di avanzamento: {progress.Percent:F1}%. " +
                $"Segnale osservato: {progress.Description}.\n\n" +
                "È un valore letto nel titolo della finestra o nei parametri: può riferirsi a una fase, a un valore iniziale o a un obiettivo. Non verifica il completamento del lavoro.",
                progress.ConfidenceLabel,
                evidence,
                sample);
        }

        if (LooksLongLived(i))
        {
            evidence.Add($"Ruolo long-running: {i.Role}");
            return Result(
                "Progresso",
                "Questo processo sembra essere un componente a lunga durata (servizio, renderer, host o processo principale). " +
                "Per questo non ha necessariamente una 'percentuale completata' o una fine prevista.",
                "NON DETERMINATO",
                evidence);
        }

        evidence.Add("Nessun contatore di progresso esplicito trovato in command line o titoli finestra.");
        return Result(
            "Progresso",
            "ProcessLens vede che il processo è attivo, ma non dispone di un totale di lavoro affidabile. " +
            "CPU, RAM o tempo trascorso da soli non permettono di dire correttamente 'a che punto è'.",
            "NON DETERMINATO",
            evidence);
    }

    private static ProcessQueryResult Eta(
        ProcessSnapshot p,
        ProcessInsight i,
        ProgressEvidence? progress,
        ProcessProgressSample? current,
        ProcessProgressSample? previous,
        List<string> evidence)
    {
        if (LooksLongLived(i) && progress is null)
        {
            evidence.Add($"Ruolo long-running: {i.Role}");
            return Result(
                "Tempo alla fine",
                "Il tipo suggerito è compatibile con un componente a lunga durata. Non è disponibile un indicatore di progresso: la fine del processo e quella dei suoi singoli lavori non sono stimabili.",
                "NON DETERMINATO",
                evidence);
        }

        if (progress is null || current is null)
        {
            evidence.Add("Nessun indicatore esplicito current/total o percentuale osservato.");
            return Result(
                "Tempo alla fine",
                "Non posso stimare in modo affidabile quando finirà. Manca un indicatore di progresso osservabile: " +
                "senza sapere quanto lavoro totale resta, una previsione basata solo su CPU/RAM sarebbe inventata.",
                "NON STIMABILE",
                evidence);
        }

        evidence.Add(progress.Description);

        if (previous is null || !SameProgressSeries(previous, current))
        {
            return Result(
                "Tempo alla fine",
                $"Il testo disponibile contiene un possibile progresso del {progress.Percent:F1}%, ma questa è la prima osservazione utile. " +
                "Per calcolare una velocità devo confrontarla con una seconda osservazione dello stesso indicatore. " +
                "Ripeti la domanda dopo un po': ProcessLens conserverà questo punto come riferimento.",
                "IN ATTESA DI UN SECONDO CAMPIONE",
                evidence,
                current);
        }

        double elapsedSeconds = (current.TimestampUtc - previous.TimestampUtc).TotalSeconds;
        double delta = current.Percent - previous.Percent;
        if (elapsedSeconds <= 0 || delta <= 0.0001)
        {
            evidence.Add($"Campione precedente: {previous.Percent:F1}%");
            evidence.Add($"Campione attuale: {current.Percent:F1}%");
            return Result(
                "Tempo alla fine",
                $"Il progresso osservato è {current.Percent:F1}%, ma tra i due campioni non è avanzato abbastanza per ricavare una velocità positiva. " +
                "Può essere in una fase lenta, in attesa o il contatore può aggiornarsi a intervalli più lunghi.",
                "NON ANCORA STIMABILE",
                evidence,
                current);
        }

        double ratePerSecond = delta / elapsedSeconds;
        double secondsRemaining = Math.Max(0, (100.0 - current.Percent) / ratePerSecond);
        if (double.IsInfinity(secondsRemaining) || double.IsNaN(secondsRemaining) || secondsRemaining > TimeSpan.FromDays(30).TotalSeconds)
        {
            return Result(
                "Tempo alla fine",
                "Il valore testuale aumenta, ma la sua variazione produce una stima troppo instabile. Servono altri campioni e una verifica del significato dell’indicatore.",
                "BASSA",
                evidence,
                current);
        }

        string eta = FormatDuration(TimeSpan.FromSeconds(secondsRemaining));
        string low = FormatDuration(TimeSpan.FromSeconds(secondsRemaining * 0.75));
        string high = FormatDuration(TimeSpan.FromSeconds(secondsRemaining * 1.35));
        string confidence = "STIMA CONDIZIONATA DA DUE CAMPIONI TESTUALI";

        evidence.Add($"Avanzamento: {previous.Percent:F1}% -> {current.Percent:F1}% in {FormatDuration(TimeSpan.FromSeconds(elapsedSeconds))}");
        evidence.Add($"Velocità osservata: {ratePerSecond * 60:F2} punti percentuali/minuto");

        return Result(
            "Tempo alla fine",
            $"Possibile progresso testuale: {current.Percent:F1}%. Alla velocità osservata tra gli ultimi due campioni, l'ETA è circa {eta}. " +
            $"Scenario aritmetico (-25% / +35% della stima): {low} - {high}; non è un intervallo di confidenza.\n\n" +
            "La stima assume che il testo rappresenti il lavoro completato e che la velocità resti costante; fasi e totali possono cambiare. Non è una previsione calibrata.",
            confidence,
            evidence,
            current);
    }

    private static ProcessQueryResult Health(ProcessSnapshot p, ProcessInsight i, IReadOnlyList<double>? recentCpu, List<string> evidence)
    {
        if (!p.CpuSampleAvailable && recentCpu is not { Count: > 0 })
            return Result("È bloccato?", "Campioni CPU non disponibili. Lo stato TCP, se presente, non permette di diagnosticare o escludere un blocco.",
                "DATI INSUFFICIENTI", evidence);
        double avg = recentCpu is { Count: > 0 } ? recentCpu.Average() : p.CpuPercent;
        double max = recentCpu is { Count: > 0 } ? recentCpu.Max() : p.CpuPercent;
        evidence.Add($"CPU media campioni recenti: {avg:F2}%");
        evidence.Add($"CPU massima campioni recenti: {max:F2}%");

        if (max >= 0.5 || p.TcpEstablishedCount > 0)
        {
            if (p.TcpEstablishedCount > 0)
                evidence.Add($"Connessioni TCP stabilite: {p.TcpEstablishedCount}");
            return Result(
                "È bloccato?",
                "CPU e stato TCP forniscono solo segnali parziali: non bastano per stabilire se il processo sia bloccato. " +
                "Queste misure non permettono di escludere un blocco parziale: per verificarlo servono segnali di progresso o informazioni sui thread.",
                "NON DETERMINATO",
                evidence);
        }

        return Result(
            "È bloccato?",
            "Nei campioni disponibili la CPU misurata è molto bassa, ma questo NON dimostra che il processo sia bloccato: " +
            "potrebbe semplicemente essere in attesa di input, rete, timer o eventi. Per diagnosticare uno stallo servirebbero anche I/O e stato dei thread.",
            "NON DETERMINATO",
            evidence);
    }

    private static ProcessQueryResult Network(ProcessSnapshot p, List<string> evidence)
    {
        if (!p.IsDeepInspected)
            return Result(
                "Porte e connessioni",
                "La rete di questo PID non è ancora stata ispezionata. Seleziona il processo e riprova.",
                "NON DISPONIBILE",
                evidence);

        evidence.Add($"PID diretto - TCP LISTEN: {p.TcpListeningCount}");
        evidence.Add($"PID diretto - TCP ESTABLISHED: {p.TcpEstablishedCount}");
        evidence.Add($"PID diretto - UDP endpoint: {p.UdpEndpointCount}");
        evidence.Add($"PID diretto - connessioni TCP verso IP pubblici: {p.PublicRemoteConnectionCount}");

        var sb = new StringBuilder();

        sb.AppendLine("DETTAGLIO RETE DEL PROCESSO SELEZIONATO");
        sb.AppendLine("Conteggi di socket/endpoint, non traffico o richieste. Zero indica nessuna voce rilevata nelle letture disponibili, non un’assenza garantita.");
        if (!string.IsNullOrWhiteSpace(p.DeepInspectionNote))
            sb.AppendLine("Esito/limiti acquisizione: " + p.DeepInspectionNote);
        if (p.TcpListeningCount > 0)
        {
            sb.Append($"- Socket TCP LISTEN rilevate: {p.TcpListeningCount}");
            if (p.ListeningTcpPorts.Count > 0)
                sb.Append($" ({string.Join(", ", p.ListeningTcpPorts)})");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("- Nessuna socket TCP LISTEN rilevata nelle letture disponibili; non garantisce assenza di socket.");
        }

        sb.AppendLine($"- Connessioni TCP stabilite: {p.TcpEstablishedCount}");
        sb.AppendLine($"- Endpoint UDP: {p.UdpEndpointCount}");
        sb.AppendLine($"- Connessioni TCP verso indirizzi pubblici: {p.PublicRemoteConnectionCount}");

        var directEstablished = p.NetworkEndpoints
            .Where(x => x.Protocol == "TCP" && x.State == "ESTABLISHED")
            .Take(6)
            .ToArray();
        if (directEstablished.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Connessioni osservate del PID:");
            foreach (var ep in directEstablished)
                sb.AppendLine($"- {ep.LocalAddress}:{ep.LocalPort} -> {ep.RemoteAddress}:{ep.RemotePort} [{ep.Scope}]");
        }

        var listeners = p.NetworkEndpoints
            .Where(x => x.Protocol == "TCP" && x.State == "LISTEN")
            .Take(6)
            .ToArray();
        if (listeners.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Porte in ascolto del PID:");
            foreach (var ep in listeners)
                sb.AppendLine($"- {ep.LocalAddress}:{ep.LocalPort} [{ep.Scope}]");
        }

        if (p.NetworkFamilyProcessCount > 1)
        {
            evidence.Add($"Famiglia per nome/albero: {p.NetworkFamilyProcessCount} processi");
            evidence.Add($"Famiglia - processi con socket: {p.NetworkFamilyProcessesWithSockets}");
            evidence.Add($"Famiglia - TCP pubbliche: {p.NetworkFamilyPublicRemoteConnectionCount}");

            sb.AppendLine();
            sb.AppendLine("PROCESSI CORRELATI PER NOME E ALBERO");
            sb.AppendLine($"- Processi correlati analizzati: {p.NetworkFamilyProcessCount}");
            sb.AppendLine($"- Processi che possiedono socket: {p.NetworkFamilyProcessesWithSockets}");
            sb.AppendLine($"- TCP stabilite totali: {p.NetworkFamilyTcpEstablishedCount}");
            sb.AppendLine($"- TCP in ascolto totali: {p.NetworkFamilyTcpListeningCount}");
            sb.AppendLine($"- UDP totali: {p.NetworkFamilyUdpEndpointCount}");
            sb.AppendLine($"- TCP verso indirizzi pubblici: {p.NetworkFamilyPublicRemoteConnectionCount}");

            var networkOwners = p.RelatedNetworkProcesses
                .Where(x => x.Pid != p.Pid)
                .Take(8)
                .ToArray();

            if (networkOwners.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("PID con socket rilevate (ruolo suggerito):");
                foreach (var owner in networkOwners)
                {
                    sb.Append($"- PID {owner.Pid}: {owner.RoleHint}; TCP {owner.TcpEstablishedCount} stabilite, ");
                    sb.Append($"LISTEN {owner.TcpListeningCount}, UDP {owner.UdpEndpointCount}");
                    if (owner.PublicRemoteConnectionCount > 0)
                        sb.Append($", pubbliche {owner.PublicRemoteConnectionCount}");
                    sb.AppendLine(".");
                }
            }

            var declaredNetworkService = p.RelatedNetworkProcesses
                .FirstOrDefault(x => x.RoleHint.Contains("Servizio di rete", StringComparison.OrdinalIgnoreCase));

            if (p.TcpEstablishedCount == 0 && p.UdpEndpointCount == 0 && declaredNetworkService is not null)
            {
                sb.AppendLine();
                sb.AppendLine(
                    $"Interpretazione: nelle letture disponibili non risultano connessioni TCP ESTABLISHED o endpoint UDP per il PID selezionato. " +
                    $"La famiglia contiene invece il PID {declaredNetworkService.Pid}, identificato dalla command line come servizio di rete, " +
                    "con socket rilevate. La correlazione fra processi non dimostra che questo PID gli abbia delegato le operazioni di rete.");
            }
            else if (p.TcpEstablishedCount == 0 && p.UdpEndpointCount == 0 && p.NetworkFamilyProcessesWithSockets > 0)
            {
                sb.AppendLine();
                sb.AppendLine(
                    "Interpretazione: non risultano connessioni TCP ESTABLISHED o endpoint UDP per questo PID; altri processi correlati hanno socket rilevate. Eventuali socket LISTEN sono riportate separatamente. " +
                    "ProcessLens può quindi dire dove sono le socket, senza attribuirle erroneamente al PID selezionato.");
            }
        }

        sb.AppendLine();
        if (p.PublicRemoteConnectionCount > 0)
        {
            sb.Append("Conclusione: Windows riporta per questo PID connessioni TCP verso indirizzi classificati pubblici; i byte trasferiti non sono misurati. ");
        }
        else if (p.NetworkFamilyPublicRemoteConnectionCount > 0)
        {
            sb.Append("Conclusione: non sono rilevate connessioni TCP pubbliche sul PID selezionato; sono rilevate su processi correlati. ");
        }
        else
        {
            sb.Append("Conclusione: nello snapshot attuale non sono state osservate connessioni TCP verso indirizzi pubblici. ");
        }

        sb.Append("La classificazione dell’indirizzo come pubblico non prova il percorso di rete, la raggiungibilità, un trasferimento o il servizio remoto utilizzato. ");
        sb.Append("Una porta LISTEN su 127.0.0.1/::1 è solo locale; 0.0.0.0/:: indica tutte le interfacce locali, ma firewall/NAT determinano l'effettiva raggiungibilità dall'esterno.");

        return Result("Porte e connessioni", sb.ToString().Trim(), "OSSERVATO", evidence);
    }

    private static ProcessQueryResult Origin(ProcessSnapshot p, ProcessInsight i, List<string> evidence)
    {
        var origin = ProcessOriginAnalyzer.Analyze(p);
        evidence.AddRange(origin.Evidence);
        return Result("Origine e operazione",
            $"{origin.ParentSummary}\n\n{origin.Operation}\n\nCATENA DI AVVIO\n{origin.Chain}\n\n{origin.Limitations}",
            p.AncestorDetails.Count > 0 && p.AncestorDetails.All(a => a.VerifiedByStartTime)
                ? "CATENA COMPATIBILE PER ORARI" : "CATENA PARZIALE",
            evidence);
    }

    private static ProcessQueryResult WhyRunning(ProcessSnapshot p, ProcessInsight i, List<string> evidence)
    {
        var origin = ProcessOriginAnalyzer.Analyze(p);
        evidence.AddRange(origin.Evidence);
        evidence.Add($"Ruolo: {i.Role}");
        if (p.ParentName is not null) evidence.Add($"Processo padre: {p.ParentName}");
        return Result("Perché è aperto",
            $"{i.WhyRunning}\n\nORIGINE: {origin.ParentSummary}\nOPERAZIONE: {origin.Operation}\n\n{origin.Limitations}",
            "DATI DI AVVIO + TIPO SUGGERITO", evidence);
    }

    private static ProcessQueryResult Termination(ProcessSnapshot p, ProcessInsight i, List<string> evidence)
    {
        if (p.IsCritical) evidence.Add("Windows marca il processo come critico.");
        evidence.Add($"Valutazione rischio: {i.CloseRisk}");
        return Result(
            "Se lo chiudo",
            $"{i.CloseImpact}\n\nPossibili conseguenze secondo le regole (non verificate nel processo in corso). Rischio orientativo: {i.CloseRisk}.",
            p.IsCritical ? "CRITICO" : "EFFETTI STIMATI; STATO NON VERIFICATO",
            evidence);
    }

    private static ProcessQueryResult Help() => new(
        "Process Query",
        SysPulseLocalization.IsEnglish
            ? "You can ask, for example: “What is it doing?”, “What is its progress?”, “When will it finish?”, “Is it stuck?”, “Does it have open ports?”, “Why is it using RAM?”, “Why is it using CPU?”, “Who started it?” or “Can I close it safely?”."
            : "Puoi chiedere, per esempio: “Cosa sta facendo?”, “A che punto è?”, “Quando finirà?”, “È bloccato?”, “Ha porte aperte?”, “Ha connessioni di rete?”, “Chi lo ha avviato?”, “Perché è aperto?” o “Posso chiuderlo?”.",
        "",
        Array.Empty<string>(),
        null);

    private static ProcessQueryResult Result(string title, string answer, string confidence, List<string> evidence, ProcessProgressSample? sample = null) =>
        SysPulseLocalization.IsEnglish
            ? new(
                SysPulseLocalization.Translate(title),
                SysPulseLocalization.Translate(answer),
                SysPulseLocalization.Translate(confidence),
                evidence.Select(e => SysPulseLocalization.Translate(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                sample)
            : new(title, answer, confidence, evidence.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), sample);

    private static IReadOnlyList<ProcessQueryIntent> DetectIntents(string question)
    {
        string q = question.ToLowerInvariant();
        var intents = new List<ProcessQueryIntent>();

        if (ContainsAny(q, "cosa sta facendo", "cosa fa", "che fa", "attività", "attivita", "sta facendo", "what is it doing", "what does it do")) intents.Add(ProcessQueryIntent.Activity);
        bool cpuQuestion = ContainsAny(q, "cpu", "processore", "core", "carico del computer", "processor");
        if (cpuQuestion) intents.Add(ProcessQueryIntent.Cpu);
        if (Regex.IsMatch(q, @"\bram\b|\b\d+\s*(?:mb|gb)\b") || ContainsAny(q, "memoria", "working set", "commit", "megabyte", "gigabyte", "memory", "cache", "perdita di memoria", "memory leak"))
            intents.Add(ProcessQueryIntent.Memory);
        if (ContainsAny(q, "a che punto", "progresso", "avanzamento", "quanto ha fatto", "progress", "how far") ||
            (!cpuQuestion && ContainsAny(q, "percentuale", "percent"))) intents.Add(ProcessQueryIntent.Progress);
        if (ContainsAny(q, "quanto manca", "quando finir", "quando finisc", "tempo alla fine", "eta", "tra quanto", "when will it finish", "time remaining")) intents.Add(ProcessQueryIntent.Eta);
        if (ContainsAny(q, "bloccato", "bloccata", "fermo", "ferma", "stall", "risponde", "stuck", "frozen", "hanging")) intents.Add(ProcessQueryIntent.Health);
        if (ContainsAny(q, "internet", "rete", "connession", "socket", "network", "connection") || Regex.IsMatch(q, @"\b(?:porta|porte|port|ports)\b")) intents.Add(ProcessQueryIntent.Network);
        if (ContainsAny(q, "chi lo ha avviato", "chi l'ha avviato", "chi ha avviato", "origine", "processo padre", "parent", "da chi", "da dove", "dove è partito", "dove e partito", "chi l'ha lanciato", "chi lo ha lanciato", "perché è partito", "perche e partito", "cosa lo ha avviato", "who started", "origin")) intents.Add(ProcessQueryIntent.Origin);
        if (ContainsAny(q, "perché è aperto", "perche e aperto", "perché è attivo", "perche e attivo", "perché gira", "perche gira", "why is it running", "why is it open")) intents.Add(ProcessQueryIntent.WhyRunning);
        if (ContainsAny(q, "posso chiuder", "se lo chiudo", "termin", "kill", "sicuro chiud", "can i close", "safe to close")) intents.Add(ProcessQueryIntent.Termination);

        return intents.Distinct().ToArray();
    }

    private static bool ContainsAny(string value, params string[] terms) => terms.Any(term => value.Contains(term, StringComparison.Ordinal));

    private static bool SameProgressSeries(ProcessProgressSample a, ProcessProgressSample b)
    {
        if (a.Pid != b.Pid) return false;
        if (a.StartTime.HasValue && b.StartTime.HasValue && a.StartTime.Value != b.StartTime.Value) return false;
        return string.Equals(a.SourceKey, b.SourceKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLongLived(ProcessInsight insight)
    {
        string s = $"{insight.Role} {insight.Summary}".ToLowerInvariant();
        string[] signals =
        {
            "renderer", "gpu", "servizio", "service", "server", "host", "processo principale",
            "coordinatore", "language server", "utility", "shell", "runtime", "crash", "rete", "audio"
        };
        return signals.Any(signal => s.Contains(signal, StringComparison.Ordinal));
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalSeconds < 1) return "meno di 1 secondo";
        if (value.TotalMinutes < 1) return $"{Math.Round(value.TotalSeconds):0} s";
        if (value.TotalHours < 1) return $"{(int)value.TotalMinutes} min {value.Seconds} s";
        if (value.TotalDays < 1) return $"{(int)value.TotalHours} h {value.Minutes} min";
        return $"{(int)value.TotalDays} g {value.Hours} h";
    }

    private enum ProcessQueryIntent
    {
        Unknown,
        Activity,
        Progress,
        Eta,
        Health,
        Cpu,
        Memory,
        Network,
        Origin,
        WhyRunning,
        Termination
    }

    private sealed record ProgressEvidence(double Percent, string SourceKey, string Description, string ConfidenceLabel);

    private static class ProgressDetector
    {
        private static readonly string[] ProgressWords =
        {
            "progress", "progresso", "epoch", "epoca", "step", "passo", "batch", "file", "files",
            "item", "test", "chunk", "fase", "phase", "task", "unit", "unità", "completed", "complet",
            "download", "scaric", "upload", "caric", "copy", "copi", "install", "extract", "estrai",
            "compress", "decompress", "build", "compil", "train", "training", "render", "encode", "transf"
        };

        public static ProgressEvidence? TryDetect(ProcessSnapshot p)
        {
            foreach (string title in p.WindowTitles)
            {
                var hit = TryText(title, "window-title", allowBarePercent: false);
                if (hit is not null) return hit with { Description = "Titolo finestra: " + hit.Description, ConfidenceLabel = "INDICATORE TESTUALE DA VERIFICARE" };
            }

            if (!string.IsNullOrWhiteSpace(p.CommandLine))
            {
                var hit = TryText(p.CommandLine!, "command-line", allowBarePercent: false);
                if (hit is not null) return hit with { Description = "Parametri di avvio: " + hit.Description, ConfidenceLabel = "INDICATORE TESTUALE DA VERIFICARE" };
            }

            return null;
        }

        private static ProgressEvidence? TryText(string text, string sourceKey, bool allowBarePercent)
        {
            string normalized = text.Replace(',', '.');

            var labeled = Regex.Match(
                normalized,
                @"(?i)\b(epoch|epoca|step|passo|batch|file(?:s)?|item|test|chunk|fase|phase|task|unit(?:à)?)\s*[:#-]?\s*(\d+(?:\.\d+)?)\s*(?:/|\bof\b|\bdi\b)\s*(\d+(?:\.\d+)?)");
            if (labeled.Success && TryNumbers(labeled.Groups[2].Value, labeled.Groups[3].Value, out double current, out double total))
            {
                double percent = current / total * 100.0;
                string unit = labeled.Groups[1].Value;
                return new ProgressEvidence(percent, sourceKey + ":" + unit.ToLowerInvariant(), $"{unit}: {current:0.##} / {total:0.##} ({percent:F1}%)", "ALTA");
            }

            var ofPattern = Regex.Match(normalized, @"(?i)\b(\d+(?:\.\d+)?)\s*(?:/|\bof\b|\bdi\b)\s*(\d+(?:\.\d+)?)\s*(file(?:s)?|item|test|chunk|task|unit(?:à)?)\b");
            if (ofPattern.Success && TryNumbers(ofPattern.Groups[1].Value, ofPattern.Groups[2].Value, out current, out total))
            {
                double percent = current / total * 100.0;
                string unit = ofPattern.Groups[3].Value;
                return new ProgressEvidence(percent, sourceKey + ":" + unit.ToLowerInvariant(), $"{current:0.##} / {total:0.##} {unit} ({percent:F1}%)", "ALTA");
            }

            bool hasContext = ProgressWords.Any(w => normalized.Contains(w, StringComparison.OrdinalIgnoreCase));
            if (allowBarePercent || hasContext)
            {
                var pct = Regex.Match(normalized, @"(?<!\d)(100(?:\.0+)?|\d{1,2}(?:\.\d+)?)\s*%");
                if (pct.Success && double.TryParse(pct.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent) && percent is >= 0 and <= 100)
                    return new ProgressEvidence(percent, sourceKey + ":percent", $"Percentuale esposta: {percent:F1}%", allowBarePercent ? "MEDIA" : "ALTA");
            }

            return null;
        }

        private static bool TryNumbers(string a, string b, out double current, out double total)
        {
            current = total = 0;
            if (!double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out current)) return false;
            if (!double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out total)) return false;
            return total > 0 && current >= 0 && current <= total;
        }
    }
}

public sealed record ProcessQueryResult
{
    private string _title, _answer, _confidence;
    private IReadOnlyList<string> _evidence;
    public string Title { get => SysPulseLocalization.Render(_title); init => _title=value; }
    public string Answer { get => SysPulseLocalization.Render(_answer); init => _answer=value; }
    public string Confidence { get => SysPulseLocalization.Render(_confidence); init => _confidence=value; }
    public IReadOnlyList<string> Evidence { get => SysPulseLocalization.IsEnglish ? _evidence.Select(s=>SysPulseLocalization.Render(s)).ToArray() : _evidence; init=>_evidence=value; }
    public ProcessProgressSample? ProgressSample { get; init; }
    public ProcessQueryResult(string Title,string Answer,string Confidence,IReadOnlyList<string> Evidence,ProcessProgressSample? ProgressSample)
    { _title=Title;_answer=Answer;_confidence=Confidence;_evidence=Evidence;this.ProgressSample=ProgressSample; }
    public void Deconstruct(out string Title,out string Answer,out string Confidence,out IReadOnlyList<string> Evidence,out ProcessProgressSample? ProgressSample)
    { Title=this.Title;Answer=this.Answer;Confidence=this.Confidence;Evidence=this.Evidence;ProgressSample=this.ProgressSample; }
}

public sealed record ProcessProgressSample(
    int Pid,
    DateTime? StartTime,
    DateTime TimestampUtc,
    double Percent,
    string SourceKey,
    string Description);
