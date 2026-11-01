> R35 R2: corretti CS0509 e CS0263 nella finestra WPF. Lingue IT/EN conservate. Rapporto: `progetto/audit/R10_R35_R2/REVISIONE_R35_R2.md`.

> R35 R1: ulteriori correzioni dei testi inglesi e localizzazione delle finestre di scelta file. Rapporto in `progetto/audit/R10_R35_R1/REVISIONE_R35_R1.md`.

> R35: scegli una sola lingua per tutta l’analisi. Avvia `AVVIA_PROCESSLENS.cmd`. Dettagli e limiti della candidata in `PROCESS_ANALYSIS_IT_EN.md` e nel rapporto R35.

# SysPulse (ProcessLens R34) — Analizzatore Avanzato di Processi, Memoria e Rete Windows

[![Language](https://img.shields.io/badge/Language-Italiano-green.svg)](#) [![English](https://img.shields.io/badge/Language-English-blue.svg)](README.md)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-blue.svg)](#requisiti-di-sistema)
[![Runtime](https://img.shields.io/badge/.NET-8.0%20WPF%20%26%20Desktop-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Architecture](https://img.shields.io/badge/Architecture-Read--Only%20%7C%20Zero--Intervention-success.svg)](#principi-architetturali-e-sicurezza)
[![Tests](https://img.shields.io/badge/Verification-341%20Tests%20%26%2053%20Static%20Gates%20Passed-brightgreen.svg)](#fasi-della-pipeline-di-compilazione)

*Disponibile anche in: [English](README.md) | [Italiano](README.it.md) | [Enciclopedia Analisi dei Processi Bilingue (IT + EN)](PROCESS_ANALYSIS_IT_EN.md)*

**SysPulse** (sviluppato sulla base tecnologica ProcessLens) è una dashboard di analisi avanzata per sistemi operativi Windows (Task Manager & Network Inspector ad alta precisione), progettata specificamente per garantire **trasparenza assoluta**, **misurazioni in tempo reale a livello di microsecondo** e **totale non-intrusività** (zero-intervention architecture).

A differenza dei tradizionali task manager commerciali o dei monitor generici, SysPulse opera rigorosamente in **sola lettura**: non inietta DLL nei processi utente, non installa driver filtro di terze parti, non intercetta le chiamate al kernel e distingue sistematicamente tra **dati osservati direttamente da Windows**, **inferenze probabilistiche** e **stime euristiche**.

---

## Indice

1. [Panoramica e Caratteristiche Principali](#panoramica-e-caratteristiche-principali)
2. [Principi Architetturali e Sicurezza](#principi-architetturali-e-sicurezza)
3. [Struttura della Soluzione e Componenti](#struttura-della-soluzione-e-componenti)
4. [Come si Avvia: Procedura ed Esecuzione](#come-si-avvia-procedura-ed-esecuzione)
5. [Cosa Succede Durante l'Avvio (Processi e Fasi)](#cosa-succede-durante-lavvio-processi-e-fasi)
6. [Pipeline di Compilazione Dettagliata (Fasi 1-6)](#pipeline-di-compilazione-dettagliata-fasi-1-6)
7. [Tempi di Compilazione e Avvio](#tempi-di-compilazione-e-avvio)
8. [Architettura a Runtime: Come Lavora l'Applicazione](#architettura-a-runtime-come-lavora-lapplicazione)
   - [Gestione della Rete in Tempo Reale](#gestione-della-rete-in-tempo-reale)
   - [Pipeline di Risoluzione Icone e Glifi Vettoriali](#pipeline-di-risoluzione-icone-e-glifi-vettoriali)
   - [Memoria: Working Set Privato vs Totale](#memoria-working-set-privato-vs-totale)
   - [Ciclo di Vita della Finestra e Area di Notifica](#ciclo-di-vita-della-finestra-e-area-di-notifica)
9. [Requisiti di Sistema e Prerequisiti](#requisiti-di-sistema-e-prerequisiti)
10. [Risoluzione dei Problemi (Troubleshooting)](#risoluzione-dei-problemi-troubleshooting)

---

## Panoramica e Caratteristiche Principali

SysPulse fornisce una visibilità granulare sui processi in esecuzione in ambiente Windows, aggregando telemetria avanzata in un'interfaccia moderna in WPF:

- **Monitoraggio Rete Live per PID e Gruppo**:
  Misurazione istantanea del throughput di invio e ricezione (`KB/s`, `MB/s`), byte cumulativi trasferiti e conteggio dei socket attivi (TCP in ascolto, connessioni TCP stabilite, porte UDP aperte). Ordinamento nativo delle righe per consumo di banda (`Rete`).
- **Sistema Icone Intelligente (Zero Icone Generiche Sgradevoli)**:
  Risoluzione a più livelli che elimina i placeholder generici o a blocchi pixelati:
  1. Estrazione icone ad alta risoluzione dagli eseguibili nativi (PE `.ico`).
  2. Parsing automatico di `AppxManifest.xml` e recupero dei loghi PNG ufficiali per le app moderne di Windows Store / UWP.
  3. Ereditarietà semantica dell'icona per processi secondari, helper e worker (che ereditano l'icona dell'eseguibile primario nella medesima directory o dal genitore).
  4. Mappatura contestuale per i servizi ospitati da `svchost.exe`.
  5. Generatore di simboli vettoriali nitidi e moderni, basati su palette semantiche legate al ruolo applicativo e pattern hash stabili.
- **Ispezione Approfondita On-Demand (Deep Inspection)**:
  Nessuna scansione pesante di massa: i moduli DLL, i token di sicurezza, l'AUMID, i listener di rete e i thread vengono ispezionati dettagliatamente solo al momento della selezione del processo da parte dell'utente.
- **Tracciamento Distinto della Memoria**:
  Separazione categorica tra memoria fisica residente ad uso esclusivo del processo (*Private Working Set*) e memoria condivisa (*Working Set* totale), integrata con la rilevazione della pressione globale di memoria tramite `GlobalMemoryStatusEx`.
- **Integrazione con l'Area di Notifica (Tray Icon)**:
  Minimizzazione a zero consumo nella barra di sistema accanto all'orologio, con sospensione automatica dei timer di polling ad alta frequenza e decodifica completa degli eventi di shell Win32 v4.

---

## Principi Architetturali e Sicurezza

L'architettura di SysPulse segue criteri rigidi di integrità:

```
┌────────────────────────────────────────────────────────────────────────┐
│                          PRINCIPI GUIDA                              │
├──────────────────────────┬─────────────────────────────────────────────┤
│ 1. Zero-Intervention     │ Nessun driver di terze parti installato.    │
│                          │ Nessun aggancio (hook) a processi terzi.    │
│                          │ Chiamate documentate Win32 / IP Helper.     │
├──────────────────────────┼─────────────────────────────────────────────┤
│ 2. Rigore delle Evidenze │ Separazione rigida fra:                     │
│                          │ • Dato grezzo (es. TCP socket aperto)       │
│                          │ • Inferenza (es. ruolo di Server Web)       │
│                          │ • Stima (es. percentuale di carico)         │
├──────────────────────────┼─────────────────────────────────────────────┤
│ 3. Disciplina di Carico  │ Snapshot globale ultraleggero.              │
│                          │ Ispezione approfondita solo on-demand.      │
│                          │ Sospensione totale del polling se ridotta.  │
└──────────────────────────┴─────────────────────────────────────────────┘
```

1. **Accesso in Sola Lettura**:
   Il monitoraggio utilizza le API native di sistema (`OpenProcess` con diritti minimi `PROCESS_QUERY_LIMITED_INFORMATION`, `GetExtendedTcpTable`, `GetExtendedUdpTable`, `GetProcessMemoryInfo`). Nessun parametro di sistema o criterio di audit viene alterato.
2. **Elevazione UAC Esplicita**:
   L'eseguibile include nel manifest nativo `requireAdministrator` per permettere la lettura dei contatori prestazionali e delle porte di rete di tutti i processi utente e di sistema senza errori di accesso negato.
3. **Isolamento del Profiler**:
   Gli strumenti di dump della memoria e profiling .NET risiedono in binari completamente separati, non partono mai automaticamente e richiedono l'esplicito consenso e la conferma dell'utente, con una soglia di sicurezza che blocca le operazioni se la RAM supera l'85%.

---

## Struttura della Soluzione e Componenti

Il codice sorgente è organizzato in una soluzione modulare in .NET 8 / C# 12:

```
ProcessLens-Leggero/
├── AVVIA_SYSPULSE_IT.cmd              # Script batch di avvio rapido in lingua Italiana
├── START_SYSPULSE_EN.cmd              # Script batch di avvio rapido in lingua Inglese
├── AVVIA_PROCESSLENS.cmd              # Script batch predefinito di avvio rapido per l'utente
├── LEGGIMI.txt                        # Note sintetiche di avvio e revisione
├── README.md                          # Documentazione principale in lingua inglese (GitHub standard)
├── README.it.md                       # Documentazione completa in lingua italiana
├── supporto/
│   ├── avvia.ps1                      # Orchestratore di pre-flight, build e avvio con parametro -Language (it/en)
│   └── build-r34.ok                   # Ricevuta di certificazione build completata
└── progetto/
    ├── build-dashboard.ps1            # Script maestro di verifica, test e compilazione
    ├── publish-release.ps1            # Pubblicazione binari Windows x64
    ├── src/
    │   ├── ProcessLens.Core/          # Modelli immutabili, metriche live e astrazioni
    │   ├── ProcessLens.Windows/       # Interoperabilità Win32, IP Helper e raccoglitori
    │   ├── ProcessLens.Semantics/     # Riconoscimento framework, ruoli e cataloghi
    │   ├── ProcessLens.App/           # Interfaccia grafica WPF, viste, tray e icone
    │   ├── ProcessLens.Profiler/      # Tool autonomo di analisi memoria .NET
    │   ├── ProcessLens.MemoryAgent/   # Agente opzionale per soglie e alert RAM
    │   └── ProcessLens.RamMonitor/    # Monitor standalone di memoria
    ├── tests/                         # Suite di test automatizzati per ogni modulo
    ├── audit/                         # Strumenti di verifica statica e conformità (R10-R34)
    └── publish/win-x64/               # Output finale compilato ed eseguibile (ProcessLens.exe)
```

---

## Come si Avvia: Procedura ed Esecuzione

```mermaid
flowchart TD
    A["Doppio clic su AVVIA_PROCESSLENS.cmd"] --> B["supporto/avvia.ps1"]
    B --> C{"Esiste build certificata in<br/>publish/win-x64/ProcessLens.exe<br/>e build-r34.ok valido?"}
    C -- "SÌ (Avvii successivi)" --> D{"Processo già con privilegi Amministratore?"}
    C -- "NO (Primo avvio)" --> E["Invoke-ProcessLensBuild<br/>(build-dashboard.ps1)"]
    E --> F["Esecuzione Controlli Statici, Test Unitari e Compilazione"]
    F --> G["Generazione ProcessLens.exe e scrittura build-r34.ok"]
    G --> D
    D -- "NO" --> H["Richiesta Elevazione UAC (RunAs Administrator)"]
    H --> I["Nuova istanza PowerShell elevata"]
    I --> J["Avvio processo nativo ProcessLens.exe"]
    D -- "SÌ" --> J
    J --> K["Apertura Finestra GUI WPF SysPulse"]
```

### Metodi di Avvio e Supporto Bilingue (Italiano / Inglese)

1. **Avvio Diretto in Lingua Italiana**:
   Doppio clic su `AVVIA_SYSPULSE_IT.cmd` (oppure `AVVIA_PROCESSLENS.cmd`) nella root del progetto.
2. **Avvio Diretto in Lingua Inglese**:
   Doppio clic su `START_SYSPULSE_EN.cmd` nella root del progetto.
3. **Da PowerShell con selezione lingua**:
   ```powershell
   # Per avviare in Italiano:
   .\supporto\avvia.ps1 -Language it

   # Per avviare in Inglese:
   .\supporto\avvia.ps1 -Language en
   ```
4. **Cambio Lingua Dinamico dall'Interfaccia Grafica**:
   Nella barra superiore dell'applicazione è presente il pulsante interattivo **Lingua: IT / Language: EN** che consente di commutare istantaneamente tutte le etichette, colonne, dettagli e descrizioni dei processi tra Italiano e Inglese senza riavviare. La scelta viene salvata e ricordata automaticamente.
5. **Compilazione Manuale e Test Completi**:
   ```powershell
   cd .\progetto
   .\build-dashboard.ps1
   ```
6. **Esecuzione Diretta del Binario Compilato con flag CLI**:
   ```powershell
   .\progetto\publish\win-x64\ProcessLens.exe --lang it
   # oppure
   .\progetto\publish\win-x64\ProcessLens.exe --lang en
   ```

---

## Cosa Succede Durante l'Avvio (Processi e Fasi)

1. **Inizializzazione e Pre-Flight (`supporto\avvia.ps1`)**:
   Lo script verifica la piattaforma Windows e controlla la presenza dell'ambiente .NET 8 (Desktop Runtime / SDK). Se presente una cartella runtime offline locale, la riutilizza in totale isolamento.
2. **Risoluzione della Ricevuta di Build (`build-r34.ok`)**:
   Se il binario e la firma `ProcessLens-R34-network-clarity` sono già presenti, la compilazione viene saltata.
3. **Elevazione dei Privilegi UAC**:
   Se il processo non dispone dei diritti di amministratore, richiede automaticamente l'elevazione tramite `RunAs`.
4. **Avvio dell'Applicazione GUI**:
   Avvio del processo, registrazione hook messaggi di finestra, inizializzazione tray icon (`Shell_NotifyIconW`), aggancio timer di campionamento e caricamento snapshot iniziale dei processi.

---

## Pipeline di Compilazione Dettagliata (Fasi 1-6)

Lo script `progetto/build-dashboard.ps1` include:
- **FASE 1**: 53 controlli statici PowerShell e audit di sicurezza (XAML context menu, leggibilità UI, isolamento profiler, regole R8-R34).
- **FASE 2**: suite di test automatizzati (diagnostica JSONL, SQLite sola lettura, soglie memoria 85/80, intervalli CPU, metriche live RAM e socket TCP/UDP, filtri identità, 61 icone catalogo, test headless WPF e shell smoke test reale `MainWindowSmoke`).
- **FASI 3 & 4**: compilazione fixture sintetica e profiler autonomo isolato.
- **FASE 5**: compilazione Release WPF con convalida XAML.
- **FASE 6**: pubblicazione eseguibile nativo in `publish/win-x64/ProcessLens.exe` e contrassegno `build-r34.ok`.

---

## Tempi di Compilazione e Avvio

| Scenario di Esecuzione | Tempo Stimato | Dettaglio Operazioni |
| :--- | :--- | :--- |
| **Avvio Diretto (già compilato)** | **< 1 secondo** | Esecuzione immediata dell'eseguibile nativo `ProcessLens.exe` tramite `supporto/avvia.ps1`. |
| **Compilazione Incrementale (`dotnet build`)** | **~3 - 5 secondi** | Ricompilazione rapida dei soli assembly modificati (Core, Windows, App). |
| **Pipeline di Build e Validazione Completa** | **~60 - 90 secondi** | Esecuzione completa di tutti i 53 audit statici, 341 asserzioni di test unitari, test WPF headless, fixture di finestra reale `MainWindowSmoke`, compilazione di tutti i 7 progetti e pubblicazione di 3 binari separati. |

---

## Architettura a Runtime: Come Lavora l'Applicazione

### Gestione della Rete in Tempo Reale
- Query a basso overhead alle tabelle di connessione TCP/UDP (`GetExtendedTcpTable`, `GetExtendedUdpTable`).
- Calcolo in tempo reale del throughput di invio/ricezione e byte trasferiti tramite `IO_COUNTERS`.
- Ordinamento dinamico per consumo di rete (`Rete`) e tracciamento socket aperti.

### Pipeline di Risoluzione Icone e Glifi Vettoriali
1. **Icona Nativa PE**: estrazione della risorsa `.ico` incorporata nell'eseguibile.
2. **Appx / Windows Store (UWP)**: lettura di `AppxManifest.xml` e recupero del logo PNG ufficiale ad alta definizione.
3. **Ereditarietà per Sottoprocessi Helper**: i processi secondari privi di icona ereditano l'icona dell'eseguibile primario nella medesima cartella o dal genitore.
4. **Glifi Vettoriali Semantici**: generazione procedurale di simboli geometrici nitidi da catalogo semantico (61 categorie, oltre 480 binari mappati), congelati in memoria senza alcuna icona sgradevole o a blocchi.

### Memoria: Working Set Privato vs Totale
- Distinzione tra Private Working Set (RAM fisica esclusiva) e Working Set totale (inclusa memoria condivisa).
- Adattamento automatico della frequenza di polling tramite `GlobalMemoryStatusEx`.

### Ciclo di Vita della Finestra e Area di Notifica
- Riduzione nell'area di notifica con sospensione immediata dei timer ad alta frequenza per azzerare il consumo della CPU.
- Ripristino con singolo clic e supporto per riavvio di Windows Explorer (`TaskbarCreated`).

---

## Requisiti di Sistema e Prerequisiti

- **Sistema Operativo**: Windows 10 (versione 1809 o successiva) o Windows 11 a 64 bit (x64).
- **Ambiente .NET**: .NET 8.0 Desktop Runtime (x64) per l'esecuzione; .NET 8.0 SDK (x64) per la compilazione.
- **Privilegi**: Amministratore (UAC) per l'accesso alle metriche di rete e contatori globali.
- **Dipendenze Esterne**: Nessuna (zero driver, zero pacchetti di terze parti).

---

## Licenza e Note Legali

SysPulse è rilasciato come software di utilità open source. Tutti i marchi, loghi ed elementi grafici di Windows appartengono a Microsoft Corporation.
