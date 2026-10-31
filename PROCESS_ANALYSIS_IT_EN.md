# SysPulse (ProcessLens) — Analisi Approfondita dei Processi (IT) & Process Analysis Encyclopedia (EN)

Questo documento è diviso in **due parti complete**:
1. **PARTE 1 (Italiano)**: Analisi architetturale del motore di Process Intelligence di SysPulse, miglioramenti introdotti per il riconoscimento profondo dei processi e catalogo descrittivo in italiano.
2. **PART 2 (English)**: Complete English guide to all Windows, Developer, Runtime, Container, Database, Hardware Driver, and Desktop Application processes analyzed by SysPulse.

---

# PARTE 1 — Analisi del Motore di Process Intelligence e Catalogo Processi (Italiano)

## 1. Architettura del Motore di Analisi dei Processi (`ProcessLens.Semantics`)

SysPulse non si limita a leggere il nome dell'eseguibile come un normale Task Manager, ma esegue una pipeline deterministica a **6 stadi** in sola lettura:

1. **`IdentityResolver`**: Ricava il nome amichevole verificando `FileDescription`, `ProductName`, `CompanyName`, manifesto UWP/MSIX (`PackageFullName`, `ApplicationUserModelId`) e servizi ospitati dal Service Control Manager (`ServiceDisplayNames`), filtrando le diciture generiche di sistema per non nascondere il vero eseguibile.
2. **`FrameworkDetector`**: Riconosce lo stack tecnologico sottostante combinando metadati, switch della riga di comando e **moduli DLL caricati in memoria** (`coreclr.dll`/`clr.dll` per .NET, `python3x.dll` per Python, `jvm.dll` per Java, `chrome_elf.dll`/`libcef.dll` per Chromium/Electron, `Qt5Core.dll`/`Qt6Core.dll` per Qt, oltre a WebView2 e WSL).
3. **`OwnershipResolver`**: Attribuisce il processo all'applicazione proprietaria o alla suite software reale (es. Visual Studio Code, Google Chrome, Docker Desktop, sottosistema Windows).
4. **`KnowledgeSignatureEngine`**: Motore euristico pesato basato su regole JSON (`knowledge/*.json`) che combina segnali multipli (prefissi DLL, classi di finestre Win32, socket TCP LISTEN/ESTABLISHED, omonimia padre/figlio).
5. **`RoleClassifier` (Potenziato a 130+ famiglie di processi)**: Classifica il ruolo esatto, la sintesi operativa, il motivo dell'avvio, l'impatto della chiusura e il livello di rischio (`CRITICO`, `ALTO`, `MEDIO`, `BASSO`), estraendo i parametri specifici da:
   - `svchost.exe` (`-k <gruppo>` e `-s <servizio>`)
   - `dllhost.exe` (`/Processid:{CLSID}` per identificare il componente COM Surrogate isolato)
   - `rundll32.exe` (libreria `.dll` target invocata)
   - Sottoprocessi Chromium/Electron (`--type=renderer`, `--type=gpu-process`, `--type=utility`, `--utility-sub-type=network/audio/storage/node`)
   - Runtimes di sviluppo (`python -m`, `dotnet <assembly>.dll`, `node <script>.js`, `java -jar`, `powershell -File`, `msbuild <progetto>.sln`)
6. **`ProcessBehaviorExplainer` & `ProcessOriginAnalyzer`**: Ricostruiscono la catena di avvio verificando i timestamp di creazione (`CreationTime`) padre-figlio contro il riutilizzo dei PID, analizzano i sottosistemi DLL caricati (DirectX/OpenGL/Vulkan, WinSock/WinHTTP, Crypt32/BCrypt, CLR, SQLite) e generano il report bilingue Italiano + Inglese.

---

## 2. Catalogo dei Processi Analizzati (Italiano)

### A. Kernel e Processi Critici di Sistema Windows
| Eseguibile | Ruolo Identificato | Cosa fa e perché è in esecuzione | Impatto se terminato |
| :--- | :--- | :--- | :--- |
| `System` (PID 4) | Kernel NT e sottosistema di sistema | Ospita i thread del kernel NT, i driver di dispositivo e la gestione della memoria fisica. | **CRITICO**: Non terminabile; nucleo del sistema operativo. |
| `smss.exe` | Sottosistema gestione sessioni | Primo processo in modalità utente avviato dal kernel; inizializza la memoria virtuale e avvia `csrss` e `wininit`. | **CRITICO**: Provoca schermata blu (BSOD) immediata. |
| `csrss.exe` | Sottosistema runtime Client/Server | Gestisce i thread Win32, le tabelle dei processi e le finestre di console della sessione attiva. | **CRITICO**: Provoca l'arresto immediato del sistema. |
| `wininit.exe` | Inizializzazione di sistema | Avvia i servizi fondamentali della Sessione 0 (`services.exe`, `lsass.exe`, `lsaiso.exe`). | **CRITICO**: Arresto anomalo immediato del sistema. |
| `services.exe` | Gestore controllo servizi (SCM) | Avvia, arresta e coordina tutti i servizi di sistema e i driver registrati in Windows. | **CRITICO**: Blocca l'intera gestione dei servizi di Windows. |
| `lsass.exe` | Sottosistema di sicurezza locale | Verifica le credenziali di login, genera i token di accesso e gestisce i criteri di sicurezza. | **CRITICO**: Disconnessione immediata o riavvio del PC. |
| `lsaiso.exe` | Isolamento credenziali VBS | Protegge i segreti di autenticazione tramite isolamento hardware Hyper-V (Credential Guard). | **CRITICO**: Protetto dall'hypervisor. |
| `winlogon.exe` | Applicazione di accesso Windows | Gestisce l'autenticazione interattiva, la schermata di blocco e la combinazione `Ctrl+Alt+Canc`. | **CRITICO**: Chiude forzatamente la sessione utente. |
| `svchost.exe` | Host condiviso servizi Windows | Esegue servizi di sistema implementati in librerie DLL raggruppati per privilegi e funzione (`-k netsvcs`, `-k localService`, `-k DcomLaunch`, `-s <servizio>`). | **ALTO**: Interrompe i servizi di rete, audio o sistema ospitati nel PID. |
| `dwm.exe` | Gestione finestre desktop (DWM) | Compone e disegna tutte le finestre del desktop sfruttando l'accelerazione GPU DirectX. | **ALTO**: Riavvio dello schermo e possibili blocchi grafici. |
| `explorer.exe` | Esplora risorse e shell Windows | Gestisce il desktop, la barra delle applicazioni, il menu Start e le finestre delle cartelle. | **MEDIO**: Scomparsa temporanea della taskbar e delle cartelle aperte. |
| `sihost.exe` | Host infrastruttura shell | Supporta il menu Start moderno, il centro notifiche e le scorciatoie di sistema. | **ALTO**: Blocca il menu Start e la barra delle applicazioni. |
| `dllhost.exe` | Host componenti COM (COM Surrogate) | Isola componenti COM esterni o generatori di anteprime (`/Processid:{CLSID}`) fuori da Esplora file. | **BASSO / MEDIO**: Interrompe l'operazione COM o la miniatura in corso. |
| `taskhostw.exe` | Host attività pianificate Windows | Esegue in background le attività di manutenzione registrate nell'Utilità di pianificazione. | **MEDIO**: Interrompe i task pianificati ospitati. |
| `RuntimeBroker.exe` | Runtime Broker di Windows | Media i permessi di accesso a file, rete e hardware per le applicazioni moderne UWP/MSIX. | **BASSO / MEDIO**: Le app Store collegate possono chiudersi. |
| `WUDFHost.exe` | Host driver in modalità utente | Esegue driver USB, biometrici e sensori in spazio utente per evitare crash del kernel. | **ALTO**: Disconnette le periferiche hardware gestite. |

### B. Strumenti di Sviluppo, Compilatori, Database e AI Locale
| Eseguibile | Ruolo Identificato | Cosa fa e perché è in esecuzione | Impatto se terminato |
| :--- | :--- | :--- | :--- |
| `dotnet.exe` | Runtime / SDK .NET | Esegue applicazioni `.dll` compilate o coordina la build e l'hot-reload (`dotnet watch`). | **BASSO / MEDIO**: Interrompe l'app o il comando .NET in corso. |
| `MSBuild.exe` / `VBCSCompiler.exe` | Compilatore Roslyn e motore MSBuild | Compila soluzioni C#/VB.NET e mantiene in memoria la cache sintattica del compilatore Roslyn. | **BASSO / MEDIO**: Fa fallire la compilazione .NET corrente. |
| `python.exe` | Interprete Python | Esegue script `.py`, moduli `-m` (es. `http.server`, `pip`, `pytest`) o worker `multiprocessing`. | **BASSO / MEDIO**: Termina lo script o il server Python. |
| `node.exe` / `bun.exe` / `deno.exe` | Runtime JavaScript / TypeScript | Esegue server web, tool di build (Vite, Webpack, Next.js), language server o script JS/TS. | **BASSO / MEDIO**: Arresta il processo o dev-server JS/TS. |
| `cargo.exe` / `rustc.exe` | Toolchain e compilatore Rust | Analizza i crate, verifica il borrow-checker e compila binari nativi Rust. | **BASSO / MEDIO**: Interrompe la compilazione Rust. |
| `cl.exe` / `link.exe` / `gcc.exe` / `clang.exe` | Compilatori C/C++ nativi | Traduce codice sorgente C/C++ in file oggetto e collega gli eseguibili nativi. | **BASSO / MEDIO**: Interrompe il job di compilazione C/C++. |
| `ollama.exe` / `llama-server.exe` / `LM Studio.exe` | Motore di inferenza AI / LLM locale | Esegue modelli linguistici generativi (LLM) direttamente su GPU/CPU locale. | **BASSO / MEDIO**: Interrompe la generazione AI e libera la VRAM/RAM. |
| `sqlservr.exe` / `postgres.exe` / `mysqld.exe` / `mongod.exe` / `redis-server.exe` | Motori Database e Cache | Gestisce transazioni SQL/NoSQL, indici, log transazionali (WAL) e connessioni client su socket TCP. | **MEDIO / ALTO**: Interrompe le connessioni database e annulla le transazioni aperte. |
| `docker.exe` / `com.docker.backend.exe` / `vmmemWSL` | Container Docker e Sottosistema WSL2 | Esegue il kernel Linux virtualizzato Hyper-V e i container applicativi isolati. | **MEDIO / ALTO**: Spegne i container attivi e le distribuzioni WSL2. |

---

# PART 2 — Complete Process Analysis & Encyclopedia (English)

This section provides the complete **English technical reference** for all processes analyzed by SysPulse. In addition to this guide, SysPulse displays a live **`ENGLISH PROCESS ANALYSIS`** card inside the Process Details panel and appends a full English report to every on-demand process explanation.

## 1. How SysPulse Analyzes Processes in Real Time

SysPulse evaluates every running process (`PID` + `CreationTime` identity) through an **evidence-based, zero-fabrication pipeline**:

1. **Identity Verification (`IdentityResolver`)**: Extracts verified Windows PE version blocks (`ProductName`, `FileDescription`, `CompanyName`), UWP/MSIX package identity (`PackageFullName`, `ApplicationUserModelId`), and live Windows Service Control Manager associations (`ServiceNames` and `ServiceDisplayNames`).
2. **Runtime & Technology Fingerprinting (`FrameworkDetector`)**: Inspects loaded module tables (`EnumProcessModulesEx`), command-line flags, and PE metadata to detect `.NET CLR`, `Python`, `Node.js`, `Java/JVM`, `Chromium/Electron`, `Microsoft Edge WebView2`, `Qt`, `PowerShell`, and `WSL2`.
3. **Multi-Signal Rule Matching (`KnowledgeSignatureEngine` & `RoleClassifier`)**: Evaluates 130+ process families and command-line parameters without ever exposing sensitive tokens, passwords, or private file contents.
4. **Parent Lineage Verification (`ProcessOriginAnalyzer`)**: Walks the parent process chain (`PPID`) and validates whether `Parent.CreationTime <= Child.CreationTime` to distinguish true parent instances from recycled Windows PIDs.
5. **Live Resource & Socket Attribution (`ProcessBehaviorExplainer`)**: Combines CPU time counters, physical RAM (`PrivateWorkingSetSize`, `WorkingSetSize`, `PrivateUsage`), Win32 visible window enumeration (`EnumWindows`), and IP Helper socket tables (`GetExtendedTcpTable` / `GetExtendedUdpTable`) to report what the process is actually doing right now.

---

## 2. Comprehensive English Process Reference

### A. Windows Kernel & Core Operating System Processes

* **`System` (PID 4) — NT Kernel and System Subsystem**
  * **What it is**: The core Windows NT kernel process hosting kernel-mode threads, hardware device drivers, and physical memory management.
  * **Why it runs**: Started by the Windows boot loader at power-on.
  * **If terminated**: **Critical Risk** — Cannot and must not be terminated; it is the operating system kernel itself.

* **`smss.exe` — Session Manager Subsystem**
  * **What it is**: The first user-mode process created by the NT kernel during boot. It initializes virtual memory paging files, environment variables, and launches `csrss.exe` and `wininit.exe`.
  * **Why it runs**: Essential operating system bootstrap coordinator.
  * **If terminated**: **Critical Risk** — Terminating it triggers an immediate system bugcheck (BSOD).

* **`csrss.exe` — Client/Server Runtime Subsystem**
  * **What it is**: The user-mode portion of the Win32 subsystem responsible for managing process/thread tables, console windows, and shutdown orchestration.
  * **Why it runs**: Required by every active Windows session (Session 0 for services, Session 1+ for interactive user desktops).
  * **If terminated**: **Critical Risk** — Causes an immediate operating system crash.

* **`wininit.exe` — Windows Initialization Process**
  * **What it is**: Session 0 initialization host that launches the Service Control Manager (`services.exe`), the Local Security Authority (`lsass.exe`), and Credential Guard (`lsaiso.exe`).
  * **Why it runs**: Started during system boot to establish background service infrastructure.
  * **If terminated**: **Critical Risk** — Causes an immediate system crash.

* **`services.exe` — Service Control Manager (SCM)**
  * **What it is**: The central coordinator that starts, stops, and monitors all registered Windows services and device drivers, maintaining the internal service database mapped to shared `svchost.exe` PIDs.
  * **Why it runs**: Core Windows service orchestration daemon.
  * **If terminated**: **Critical Risk** — Halts all system and network service management.

* **`lsass.exe` & `lsaiso.exe` — Local Security Authority & VBS Credential Guard**
  * **What it is**: Verifies user logon credentials, issues security access tokens, enforces local security policies, and isolates authentication secrets inside Virtualization-Based Security (`lsaiso.exe`).
  * **Why it runs**: Core Windows authentication and security enforcement engine.
  * **If terminated**: **Critical Risk** — Forces an immediate system reboot or session termination.

* **`winlogon.exe` & `LogonUI.exe` / `LockApp.exe` — Windows Logon & Lock Screen**
  * **What it is**: Handles interactive user sign-in, profile loading, Secure Attention Sequence (`Ctrl+Alt+Del`), and the graphical lock screen interface.
  * **Why it runs**: Active throughout every interactive user session.
  * **If terminated**: **Critical Risk** — Immediately terminates the user session.

* **`svchost.exe` — Shared Windows Services Host**
  * **What it is**: Generic host process that executes Windows services compiled as dynamic-link libraries (`.dll`). SysPulse inspects both the SCM service table and the `-k <group>` / `-s <service>` command-line switches (e.g., `netsvcs`, `localService`, `networkService`, `DcomLaunch`, `AppModel`, `UtcSvc`) to show the exact services hosted inside each specific `svchost.exe` PID.
  * **Why it runs**: Spawned by `services.exe` to isolate service groups by security privilege and resource policy.
  * **If terminated**: **High Risk** — Disrupts all Windows services hosted by that PID (such as DNS, DHCP, Windows Audio, DCOM, or Windows Update). Use the **Services** tab in SysPulse to stop a single non-essential service safely.

* **`dwm.exe` — Desktop Window Manager**
  * **What it is**: Hardware-accelerated compositor that renders all application windows, transparency effects, high-DPI scaling, and multi-monitor frames via DirectX/GPU.
  * **Why it runs**: Core graphical session manager for Windows.
  * **If terminated**: **High Risk** — Causes display blackouts, screen reloads, and potential graphical freezes.

* **`explorer.exe`, `sihost.exe`, `ShellExperienceHost.exe`, `StartMenuExperienceHost.exe` — Windows Desktop Shell**
  * **What it is**: Provides the Windows desktop, taskbar, File Explorer folders (`explorer.exe`), modern shell infrastructure (`sihost.exe`), system tray flyouts/clock/notifications (`ShellExperienceHost.exe`), and the Start Menu (`StartMenuExperienceHost.exe`).
  * **Why it runs**: Interactive desktop environment for the signed-in user.
  * **If terminated**: **Low to High Risk** — Closing `explorer.exe` hides the taskbar and open folders until restarted; closing `sihost.exe` can freeze modern shell shortcuts and the Start Menu.

* **`dllhost.exe` — COM Surrogate Component Host**
  * **What it is**: Hosts Component Object Model (COM) DLLs in an isolated process outside the calling application (for example, when File Explorer generates image/video thumbnails or invokes system shell extensions). SysPulse extracts the `/Processid:{CLSID}` parameter when present.
  * **Why it runs**: Launched on demand by DCOM or shell components to prevent faulty third-party DLLs from crashing the main application.
  * **If terminated**: **Low / Medium Risk** — Aborts the current COM operation or thumbnail generation.

* **`RuntimeBroker.exe`, `ApplicationFrameHost.exe`, `backgroundTaskHost.exe` — Modern UWP/MSIX App Infrastructure**
  * **What it is**: `RuntimeBroker.exe` mediates capability permissions (camera, microphone, file system) for packaged Windows apps; `ApplicationFrameHost.exe` provides desktop window frames for UWP apps; `backgroundTaskHost.exe` runs periodic background tasks.
  * **Why it runs**: Automatically managed by Windows when modern Store/MSIX applications are used.
  * **If terminated**: **Low / Medium Risk** — Connected UWP apps may close or lose access to system resources.

* **`SearchHost.exe`, `SearchIndexer.exe`, `SearchProtocolHost.exe` — Windows Search & Indexing**
  * **What it is**: Renders the taskbar search panel (`SearchHost.exe`) and maintains the full-text and metadata file search catalog in the background (`SearchIndexer.exe`).
  * **Why it runs**: Enables instant file and app search across Windows.
  * **If terminated**: **Low Risk** — Temporarily closes the search UI or pauses file indexing until Windows restarts it.

* **`MsMpEng.exe`, `MpDefenderCoreService.exe`, `NisSrv.exe`, `SecurityHealthService.exe` — Windows Security & Microsoft Defender**
  * **What it is**: Real-time antimalware scanning engine (`MsMpEng.exe`), network traffic inspection service (`NisSrv.exe`), and Windows Security health monitor.
  * **Why it runs**: Protects the system against malware, ransomware, and network exploits.
  * **If terminated**: **High Risk** — Protected by Windows kernel tamper protection; terminating weakens system security.

* **`audiodg.exe`, `spoolsv.exe`, `fontdrvhost.exe`, `WmiPrvSE.exe`, `WUDFHost.exe` — Audio, Print, Fonts, WMI & User-Mode Drivers**
  * **What it is**:
    * `audiodg.exe`: Windows Audio Device Graph Isolation (processes audio streams and DSP effects in an isolated process).
    * `spoolsv.exe`: Windows Print Spooler (manages local and network print queues).
    * `fontdrvhost.exe`: Usermode Font Driver Host (renders TrueType/OpenType fonts safely outside the kernel).
    * `WmiPrvSE.exe`: WMI Provider Host (executes Windows Management Instrumentation diagnostic queries).
    * `WUDFHost.exe`: Windows User-Mode Driver Framework Host (runs USB, sensor, and portable device drivers in user space).

---

### B. Web Browsers & Chromium / Electron / WebView2 Multi-Process Architecture

Modern browsers (`chrome.exe`, `msedge.exe`, `firefox.exe`, `brave.exe`, `opera.exe`, `vivaldi.exe`) and desktop apps built on **Electron** or **Microsoft Edge WebView2** (`msedgewebview2.exe`, `Code.exe`, `Cursor.exe`, `Discord.exe`, `Slack.exe`, `Teams.exe`, `Spotify.exe`) split their work across multiple specialized processes. SysPulse inspects their command-line switches to classify each subprocess accurately:

* **Main Browser / Coordinator Process (no `--type=` switch)**
  * **What it does**: Coordinates top-level windows, tabs, user profile storage, bookmarks, and child process sandboxes.
  * **If terminated**: **Medium Risk** — Closes the entire browser or desktop application and all its child processes.
* **`--type=renderer` (or `-contentproc` in Firefox) — Web Content Renderer**
  * **What it does**: Executes HTML, CSS, DOM layout, and V8/SpiderMonkey JavaScript inside an isolated security sandbox for a specific tab, extension, or UI frame.
  * **If terminated**: **Low / Medium Risk** — Only the associated tab or UI panel crashes ("Aw, Snap!") without closing the whole browser.
* **`--type=gpu-process` — GPU Compositor & Hardware Acceleration**
  * **What it does**: Handles WebGL, Canvas, video decoding, and hardware-accelerated window compositing via Direct3D/ANGLE.
  * **If terminated**: **Low / Medium Risk** — The screen may flicker briefly while the application automatically respawns the GPU process.
* **`--type=utility` (`--utility-sub-type=...`) — Specialized Utility Services**
  * **What it does**: Isolates specific subsystems in dedicated processes:
    * `network.mojom.NetworkService`: Handles HTTP/HTTPS/QUIC network connections, DNS, and TLS sockets.
    * `audio.mojom.AudioService`: Plays and captures audio streams for web pages or voice calls.
    * `storage.mojom.StorageService`: Manages IndexedDB, Cache API, and local web storage.
    * `node.mojom.NodeService`: Executes background Node.js plugins in Electron applications.
* **`--type=crashpad-handler` — Crash Reporter**
  * **What it does**: Monitors the application for unexpected exceptions and writes minidump crash reports.

---

### C. Developer Runtimes, Compilers, IDEs & Local AI Engines

* **`.NET Runtime & SDK` (`dotnet.exe`, `MSBuild.exe`, `VBCSCompiler.exe`, `csc.exe`)**
  * **What it does**: Executes managed `.NET` assemblies (`dotnet <app>.dll`), runs hot-reload watchers (`dotnet watch run`), orchestrates project builds (`MSBuild.exe`), and keeps the Roslyn C#/VB compiler server warm in memory (`VBCSCompiler.exe`) for fast incremental compilation.
  * **Note**: For any `.NET` process, SysPulse can launch its isolated on-demand **`ProcessLensProfiler.exe`** (`EventPipe` GC heap snapshot) to inspect actual allocated .NET object types safely.
* **`Python` (`python.exe`, `pythonw.exe`)**
  * **What it does**: Runs Python scripts (`<script>.py`), library modules (`-m http.server`, `-m pytest`, `-m pip`, `-m uvicorn`), inline commands (`-c`), or parallel worker pools (`--multiprocessing-fork`).
* **`Node.js, Bun & Deno` (`node.exe`, `bun.exe`, `deno.exe`, `npm`, `pnpm`, `yarn`)**
  * **What it does**: Runs JavaScript/TypeScript backend services, frontend bundlers (Vite, Next.js, Astro, Webpack), TypeScript language servers (`tsserver.js`), and package installation tasks.
* **`Java Virtual Machine & Build Tools` (`java.exe`, `javaw.exe`, `javac.exe`, `gradle`, `mvn`)**
  * **What it does**: Executes compiled JVM bytecode, standalone `.jar` archives, Spring/Quarkus network services, JetBrains IDEs, and Gradle/Maven build daemons.
* **`Native C/C++, Rust & Go Toolchains` (`cargo.exe`, `rustc.exe`, `go.exe`, `cl.exe`, `link.exe`, `gcc.exe`, `clang.exe`, `cmake.exe`, `ninja.exe`)**
  * **What it does**: Compiles C, C++, Rust, and Go source code into native machine binaries, resolves dependency graphs (`cmake`/`ninja`/`cargo`), and generates debug symbol databases (`mspdbsrv.exe`).
* **`Code Editors & IDEs` (`Code.exe`, `Cursor.exe`, `Windsurf.exe`, `devenv.exe`, `rider64.exe`, `idea64.exe`, `pycharm64.exe`)**
  * **What it does**: Provides the primary software development environment, syntax analysis, debugging, extension hosts (`--type=extensionHost`), and workspace file watchers (`--type=watcherService`).
* **`Local AI & LLM Inference Engines` (`ollama.exe`, `ollama_llama_server.exe`, `llama-server.exe`, `LM Studio.exe`, `claude.exe`)**
  * **What it does**: Loads and executes Large Language Models (LLMs) locally on GPU VRAM and system RAM, serving local HTTP inference APIs or AI coding assistants.
* **`Git Version Control` (`git.exe`, `git-lfs.exe`, `git-remote-https.exe`)**
  * **What it does**: Executes repository operations (`status`, `fetch`, `pull`, `push`, `commit`, `clone`, `gc`). SysPulse automatically identifies the exact Git subcommand while redacting repository file paths and credentials for privacy.

---

### D. Databases, Web Servers, Containers & Virtualization

* **`Relational & NoSQL Databases` (`sqlservr.exe`, `postgres.exe`, `mysqld.exe`, `mariadbd.exe`, `mongod.exe`, `redis-server.exe`)**
  * **What it does**: Manages persistent database tables, B-tree/LSM indexes, write-ahead logs (WAL), in-memory caches (`Redis`), and client TCP socket connections (e.g., ports `1433`, `5432`, `3306`, `27017`, `6379`).
  * **If terminated**: **Medium to High Risk** — Abruptly disconnects active database clients and rolls back uncommitted transactions.
* **`HTTP Web Servers & Reverse Proxies` (`nginx.exe`, `httpd.exe`, `caddy.exe`, `traefik.exe`, `w3wp.exe`, `iisexpress.exe`)**
  * **What it does**: Listens on HTTP/HTTPS ports (`80`, `443`, `5000`, `8080`), serves static/dynamic web applications, and proxies traffic to backend microservices.
* **`Containers, WSL2 & Virtual Machines` (`wsl.exe`, `wslhost.exe`, `vmmemWSL`, `docker.exe`, `com.docker.backend.exe`, `vmwp.exe`, `VirtualBoxVM.exe`, `vmware-vmx.exe`)**
  * **What it does**: Runs virtualized Linux kernels (`WSL2`), Docker containers, Hyper-V virtual machine partitions (`vmwp.exe`), or full guest operating systems (`VirtualBox`/`VMware`).
  * **If terminated**: **Medium to High Risk** — Stops all running Linux sessions, Docker containers, or guest virtual machines.

---

### E. GPU Drivers, Audio Services, Remote Desktop & Media Applications

* **`GPU Driver Containers & Overlays` (`nvcontainer.exe`, `NVDisplay.Container.exe`, `NVIDIA Share.exe`, `amdrsserv.exe`, `RadeonSoftware.exe`, `igfxEM.exe`)**
  * **What it does**: Manages display driver profiles, G-Sync/FreeSync settings, hardware telemetry, and in-game recording overlays for NVIDIA GeForce, AMD Radeon, and Intel Graphics hardware.
* **`Audio & Multimedia Engines` (`RtkAudUService64.exe`, `obs64.exe`, `ffmpeg.exe`, `vlc.exe`, `Spotify.exe`)**
  * **What it does**: Handles Realtek HD audio jack sensing (`RtkAudUService64.exe`), live screen recording/streaming (`obs64.exe`), command-line media transcoding (`ffmpeg.exe`), and audio/video playback (`vlc.exe`, `Spotify.exe`).
* **`Remote Desktop & Network Diagnostics` (`AnyDesk.exe`, `TeamViewer.exe`, `RustDesk.exe`, `mstsc.exe`, `Wireshark.exe`, `dumpcap.exe`)**
  * **What it does**: Provides remote desktop control and screen sharing (`AnyDesk`, `TeamViewer`, `RustDesk`, Windows RDP `mstsc.exe`) or captures and decodes live network packets (`Wireshark`, `dumpcap`).
* **`Cloud Sync & Productivity Utilities` (`OneDrive.exe`, `Dropbox.exe`, `PowerToys.Runner.exe`, `Everything.exe`, `7z.exe`, `WinRAR.exe`)**
  * **What it does**: Synchronizes local folders with cloud storage (`OneDrive`, `Dropbox`), provides advanced window management and keyboard shortcuts (`PowerToys`), maintains an instant NTFS file search index (`Everything.exe`), or compresses/extracts file archives (`7-Zip`, `WinRAR`, `tar`).
