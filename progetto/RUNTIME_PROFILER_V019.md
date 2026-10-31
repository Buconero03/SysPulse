# ProcessLens v0.19.0 — Runtime Memory Profiler (fase .NET)

## Architettura

- `ProcessLens.exe` mantiene GUI e raccolta normale; **non carica alcuna libreria di profilazione**.
- `ProcessLensGuardian` v0.14.1 resta indipendente e non viene aggiornato.
- `publish/win-x64/Profiler/ProcessLensProfiler.exe` è un processo temporaneo, avviato esclusivamente su consenso esplicito nella voce `Analisi approfondita → Profilazione heap .NET`.
- `ProcessLensProfiler.exe` usa il tool Microsoft `dotnet-gcdump` **se già installato**; non scarica né installa niente. Gli altri runtime restano non supportati dalla v0.19.0.
- IPC: un file JSON limitato e temporaneo nella directory di sessione; nessun dump letto dalla GUI.

## Prerequisiti e uso

1. `AVVIA-TUTTO.cmd` per la build; `AVVIA-VELOCE.cmd` negli avvii seguenti.
2. `profiler-status.ps1`: verifica in sola lettura binario e tool.
3. Solo per la profilazione .NET: installare volontariamente il tool Microsoft ufficiale con `dotnet tool install --global dotnet-gcdump` da un account attendibile; potrebbe essere già installato.
4. In alternativa, usare `PROFILA-DOTNET.cmd` (standalone, richiede PID e conferma `PROFILA`; non apre ProcessLens.exe).
5. In ProcessLens selezionare un processo **.NET CLR**, poi il menu **Profilazione heap .NET**. Leggere la finestra di avvertenza e dare consenso specifico.
6. Premere **Annulla profilazione** per interrompere la sessione.

## Evidenze e limiti

Il report `dotnet-gcdump report` (heapstat) contiene quantità di **oggetti GC gestiti per tipo**, *non* tutta la RAM residente, né gli stack di allocazione, né automaticamente le ragioni del trattenimento. Identificare un `CachedResponse` non dimostra che sia una cache necessaria o una perdita; è soltanto il nome del tipo.

`dotnet-gcdump collect` provoca una **GC Gen2 completa nel target**. Il runtime può fermarsi temporaneamente e usare memoria aggiuntiva. Il target può allocare fino a **256 MB di buffer di raccolta** oltre al profiler stesso; il budget della sessione NON garantisce un limite all'utilizzo complessivo del sistema. Non avviare su processi importanti, protetti o in forte pressione RAM.

Protezioni best-effort: una sessione per GUI, identità PID+orario, 45 s, RAM fisica sotto 85%, helper limitato a circa 256 MiB di working set, file gcdump max 128 MiB, output JSON max 256 KiB, cancellazione user/timeout, eliminazione temporanei. In caso di crash/terminazione forzata possono rimanere file temporanei nella cartella `%TEMP%/ProcessLensProfiler-*`; non vengono creati dump permanenti per impostazione predefinita.

Nota: il supporto .NET di questa versione richiede Windows x64, runtime CoreCLR accessibile e interfaccia diagnostica abilitata. Per catturare riferimenti/radici nel dettaglio usare strumenti dedicati su snapshot acquisiti con permessi e consenso, non in background.

## Estensioni future

L'architettura separata consente in futuro adapter Python, JVM, V8/Node, ETW heap nativo C/C++, ciascuno con protocollo e misure delle proprie risorse, senza caricarli dentro ProcessLens.exe. **Non sono ancora implementati.**
