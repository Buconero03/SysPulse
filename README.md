# SysPulse (R35 R2) — Advanced Windows Process, Memory & Network Inspector

[![Language](https://img.shields.io/badge/Language-English-blue.svg)](#) [![Italiano](https://img.shields.io/badge/Language-Italiano-green.svg)](README.it.md)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-blue.svg)](#system-requirements)
[![Runtime](https://img.shields.io/badge/.NET-8.0%20WPF%20%26%20Desktop-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Architecture](https://img.shields.io/badge/Architecture-Non--Invasive%20Telemetry%20%7C%20Explicit%20Admin%20Actions-blue.svg)](#architectural-principles--safety)
[![Verification](https://img.shields.io/badge/Verification-55%20Static%20Gates%20%26%20All%20Test%20Suites%20Passed-brightgreen.svg)](#compilation-pipeline-phases-1-6)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

*Read this in other languages: [English](README.md) | [Italiano](README.it.md) | [Bilingual Process Analysis Reference (IT + EN)](PROCESS_ANALYSIS_IT_EN.md)*

---

## Live Demonstration

![SysPulse Dashboard](docs/images/Screenshot%202026-11-09%20023852.png)*Figure 1: SysPulse R35 R2 running on Windows 11. Live telemetry displays per-process CPU %, Private Working Set RAM, and Network I/O throughput. The inspection panel presents deep behavioral insights and diagnostic options.*

---

**SysPulse** is an advanced system telemetry dashboard, process monitor, and network inspector for 64-bit Windows environments. Engineered for clarity, structural transparency, and reliability, SysPulse delivers **high-resolution live telemetry**, **in-depth behavioral process analysis**, and a **clean, non-invasive observation architecture**.

SysPulse observes running workloads using standard, documented Windows operating system APIs. It never injects DLLs into user processes, never installs third-party filter drivers (such as WinPcap or Npcap), and never hooks kernel system calls. Furthermore, SysPulse strictly differentiates between **directly observed system telemetry**, **structural facts**, and **heuristic inferences**.

> [!NOTE]
> **Observation vs. Administrative Actions**: While all telemetry monitoring and deep process queries are strictly non-invasive and read-only, certain explicit user actions—specifically **Terminating a Process**, **Stopping a Service**, **Launching a New Task**, or executing an on-demand **Memory Heap Dump**—are designed to directly modify system state upon explicit confirmation by an elevated administrator.

---

## Table of Contents

1. [Overview and Key Features](#overview-and-key-features)
2. [Architectural Principles & Safety](#architectural-principles--safety)
3. [Solution Architecture & Components](#solution-architecture--components)
4. [How to Run: Launching the Application](#how-to-run-launching-the-application)
5. [What Happens During Startup (Processes & Lifecycle)](#what-happens-during-startup-processes--lifecycle)
6. [Compilation Pipeline (Phases 1-6)](#compilation-pipeline-phases-1-6)
7. [Compilation & Startup Durations](#compilation--startup-durations)
8. [Runtime Architecture: How SysPulse Operates](#runtime-architecture-how-syspulse-operates)
   - [Real-Time Network Monitoring & Sockets](#real-time-network-monitoring--sockets)
   - [4-Tier Process Icon Resolution (Zero Generic Placeholders)](#4-tier-process-icon-resolution-zero-generic-placeholders)
   - [Memory Analytics: Private Working Set vs Total](#memory-analytics-private-working-set-vs-total)
   - [Window Lifecycle & System Notification Tray](#window-lifecycle--system-notification-tray)
9. [System Requirements](#system-requirements)
10. [Troubleshooting](#troubleshooting)
11. [License & Disclaimers](#license--disclaimers)

---

## Overview and Key Features

SysPulse provides granular visibility into all running processes on Windows, consolidating live telemetry into a modern, responsive WPF desktop interface:

- **Unified Bilingual Telemetry (English / Italian)**:
  Full native bilingual engine. Launch directly into English (`START_SYSPULSE_EN.cmd`), Italian (`AVVIA_SYSPULSE_IT.cmd`), or choose at launch (`AVVIA_PROCESSLENS.cmd`). The language can also be toggled interactively in real time from the top bar (**Language: EN / Lingua: IT**).
- **Live Network Monitoring per Process and Application Group**:
  Per-process inbound and outbound throughput measurement (`KB/s`, `MB/s`), cumulative transferred bytes, and active socket enumeration (listening TCP ports, established TCP connections, open UDP endpoints). Built-in column sorting by bandwidth consumption (`Network` / `Rete`).
- **Smart 4-Tier Icon Resolver (Zero Generic Default Placeholders)**:
  Eliminates default square or blocky pixel grids through an intelligent multi-stage resolver:
  1. High-resolution extraction from native Portable Executable (PE `.ico`) resources.
  2. Automatic `AppxManifest.xml` parsing and official PNG asset extraction for modern Microsoft Store / UWP packaged applications.
  3. Parent/sibling inheritance for background helpers, compile tools, and Chromium/Electron worker subprocesses.
  4. Contextual service mapping for multi-instance `svchost.exe` hosts.
  5. Clean, crisp semantic vector glyphs generated dynamically with hash-stable geometric accents based on role classification.
- **On-Demand Deep Inspection**:
  Protects system responsiveness by strictly avoiding global heavy scans. Modules, DLLs, security tokens, package identities (AUMID), network sockets, and execution threads are inspected deeply only when a process is selected by the user.
- **Accurate Physical Memory Metrics**:
  Rigorous distinction between process-exclusive physical memory (**Private Working Set**) and shared memory pages (**Working Set Total**), integrated with physical system memory pressure detection via `GlobalMemoryStatusEx`.
- **Low-Overhead Notification Tray Minimization**:
  Minimizes into the Windows Notification Area (system tray next to the clock), immediately suspending high-frequency polling timers to conserve background CPU and memory resources.

---

## Architectural Principles & Safety

```
┌────────────────────────────────────────────────────────────────────────┐
│                          GUIDING PRINCIPLES                            │
├──────────────────────────┬─────────────────────────────────────────────┤
│ 1. Non-Invasive Observe  │ No third-party drivers or kernel hooks.     │
│                          │ No DLL injection into external processes.   │
│                          │ Strictly documented Win32 & IP Helper APIs. │
├──────────────────────────┼─────────────────────────────────────────────┤
│ 2. Evidence Integrity    │ Uncompromising separation of:               │
│                          │ • Raw facts (e.g., open TCP listener)       │
│                          │ • Inferences (e.g., Web Server role)        │
│                          │ • Estimates (e.g., CPU load %)              │
├──────────────────────────┼─────────────────────────────────────────────┤
│ 3. Explicit Admin Action │ Modifications (kill process, stop service,  │
│                          │ new task, gcdump) require explicit user     │
│                          │ intent and administrator elevation.         │
├──────────────────────────┼─────────────────────────────────────────────┤
│ 4. Load Discipline       │ Global scan stays lightweight.              │
│                          │ Heavy deep inspection is strictly on-demand.│
│                          │ Polling suspended when minimized to tray.   │
└──────────────────────────┴─────────────────────────────────────────────┘
```

1. **Non-Invasive Observation Architecture**:
   Process telemetry queries use minimal access rights (`PROCESS_QUERY_LIMITED_INFORMATION`, `GetExtendedTcpTable`, `GetExtendedUdpTable`, `GetProcessMemoryInfo`). No system parameters, firewall rules, or audit settings are modified during monitoring.
2. **Explicit Administrative Elevation**:
   The native assembly manifest declares `requireAdministrator`, ensuring Windows presents an explicit UAC prompt at launch so all network sockets and cross-session process metrics can be queried without access denials.
3. **Controlled System Modifications**:
   Administrative actions such as process termination (`ProcessTerminator`), service stopping (`ServiceStopController`), and launching new tasks (`NewTaskLauncher`) are isolated behind explicit user confirmation dialogs.
4. **Isolated Memory Profiler**:
   Heavy memory profiling and .NET GCDump inspection reside in completely decoupled standalone executables (`ProcessLens.Profiler.exe`). Profiling never starts automatically and incorporates safety gates that prevent execution if physical RAM usage exceeds 85%.

---

## Solution Architecture & Components

The solution is built on modern .NET 8 / C# 12 and WPF:

```
SysPulse / ProcessLens-Leggero/
├── START_SYSPULSE_EN.cmd              # Instant-launch batch script in English
├── AVVIA_SYSPULSE_IT.cmd              # Instant-launch batch script in Italian
├── AVVIA_PROCESSLENS.cmd              # Default instant-launch batch script with language prompt
├── LEGGIMI.txt                        # Concise launch and release notes
├── README.md                          # Full English documentation (GitHub root)
├── README.it.md                       # Full Italian documentation
├── LICENSE                            # MIT Open Source License
├── docs/
│   └── images/
│       └── syspulse-dashboard.png     # Demonstration screenshot of live UI
├── supporto/
│   ├── avvia.ps1                      # Launch orchestrator with -Language support (en/it)
│   └── build-r35r2.ok                 # Verified build receipt for instant startup
└── progetto/
    ├── build-dashboard.ps1            # Master build, test, and audit script
    ├── publish-release.ps1            # Release compilation and artifact packager
    ├── localization-manifest.json     # SHA-256 hashes of all canonical and generated sources
    ├── tools/
    │   ├── refresh-localization.ps1   # Automatic Roslyn localization generator runner
    │   └── LocalizationGenerator/     # AST generator for bilingual models and XAML
    ├── src/
    │   ├── ProcessLens.Core/          # Immutable domain models, metrics, and contracts
    │   ├── ProcessLens.Windows/       # Win32 P/Invoke, IP Helper, and OS samplers
    │   ├── ProcessLens.Semantics/     # Process categorization, role inference & catalog
    │   ├── ProcessLens.App/           # WPF GUI, MVVM bindings, tray shell & icon resolver
    │   ├── ProcessLens.Profiler/      # Isolated on-demand .NET memory profiler
    │   ├── ProcessLens.MemoryAgent/   # Standalone memory alert service
    │   └── ProcessLens.RamMonitor/    # Dedicated lightweight RAM monitor
    ├── tests/                         # Full automated test suites across all layers
    ├── audit/                         # Static compliance, security & architectural gates
    └── publish/win-x64/               # Published production release binaries (SysPulse.exe)
```

### Module Responsibilities

- **`ProcessLens.Core`**:
  Defines immutable state snapshots (`ProcessSnapshot`, `ProcessGroupUsage`), live telemetry samples (`LiveNetworkSample`, `LiveCpuSample`, `LiveMemorySample`), and bilingual formatters.
- **`ProcessLens.Windows`**:
  Interfaces directly with the Windows kernel:
  - `WindowsLiveNetworkSampler.cs`: Queries TCP/UDP extended connection tables and process I/O counters.
  - `WindowsLiveCpuSampler.cs`: Derives high-precision CPU percentages from monotonic performance counters and `GetProcessTimes`.
  - `WindowsProcessCollector.cs`: Performs rapid, low-overhead sweeps of running processes.
  - `WindowsDeepProcessInspector.cs`: Inspects selected PID modules, tokens, threads, and network bindings.
- **`ProcessLens.Semantics`**:
  Analyzes command lines, loaded modules, Windows SCM services, and process tree topology to categorize processes (e.g., database, compiler, web browser renderer, IDE, system service).
- **`ProcessLens.App`**:
  The WPF presentation layer, featuring virtualized list views, split-pane inspection panels, system notification integration (`TrayWindowController.cs`), vector glyph rendering (`ProcessSymbolDrawing.cs`), and diagnostic logging.

---

## How to Run: Launching the Application

```mermaid
flowchart TD
    A["Double-click AVVIA_PROCESSLENS.cmd<br/>or START_SYSPULSE_EN.cmd"] --> B["supporto/avvia.ps1"]
    B --> C{"Is verified build present in<br/>publish/win-x64/ProcessLens.exe<br/>and build-r35r2.ok valid?"}
    C -- "YES (Subsequent launches)" --> D{"Is current process elevated as Administrator?"}
    C -- "NO (First-time launch)" --> E["Invoke-ProcessLensBuild<br/>(build-dashboard.ps1)"]
    E --> F["Execute 55 Static Gates, Unit Tests, and Build"]
    F --> G["Generate SysPulse.exe / ProcessLens.exe<br/>and write build-r35r2.ok receipt"]
    G --> D
    D -- "NO" --> H["Request UAC Elevation (RunAs Administrator)"]
    H --> I["Relaunch elevated PowerShell session"]
    I --> J["Start native executable"]
    D -- "YES" --> J
    J --> K["SysPulse WPF UI opens on desktop"]
```

### Quick Launch Options & Bilingual Mode

1. **English Mode Launch**:
   Double-click `START_SYSPULSE_EN.cmd` from the root folder, or run:
   ```powershell
   .\supporto\avvia.ps1 -Language en
   ```
2. **Italian Mode Launch**:
   Double-click `AVVIA_SYSPULSE_IT.cmd` from the root folder, or run:
   ```powershell
   .\supporto\avvia.ps1 -Language it
   ```
3. **Interactive Launch with Language Selection Prompt**:
   Double-click `AVVIA_PROCESSLENS.cmd` from the root folder to choose your preferred language before starting.
4. **Dynamic In-App Switching**:
   Click the **Language: EN / Lingua: IT** button situated directly in the top header bar of the application at any time to toggle between English and Italian on the fly. The selected language is saved and remembered across sessions.
5. **Direct Binary Execution (when pre-built)**:
   ```cmd
   .\progetto\publish\win-x64\SysPulse.exe --lang en
   ```

---

## What Happens During Startup (Processes & Lifecycle)

When the launch script is executed, the following coordinated steps take place:

1. **Pre-Flight Environment Detection (`supporto\avvia.ps1`)**:
   - Executes Windows PowerShell with `-NoLogo -NoProfile -ExecutionPolicy Bypass`.
   - Verifies the host OS is `Windows_NT`.
   - Scans for an available .NET 8 Desktop Runtime and SDK:
     - Checks system-wide SDK installations.
     - Checks local private runtimes (e.g. `Programs/ProcessLens/R20/dotnet`) to enable 100% offline standalone operation without external downloads.
2. **Build Receipt Verification (`supporto/build-r35r2.ok`)**:
   - Checks if `progetto/publish/win-x64/ProcessLens.exe` exists and matches the receipt signature `ProcessLens-R35-R2-wpf-inheritance`.
   - **If valid**: Compilation is skipped entirely. Launch proceeds in **< 1 second**.
   - **If missing or outdated**: Runs `build-dashboard.ps1` to re-verify and compile.
3. **UAC Security Elevation**:
   - Checks user token role (`WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)`).
   - If unprivileged, requests elevation via Windows `RunAs`, spawning an elevated instance.
4. **WPF Application Bootstrap**:
   - Initializes internal diagnostics journal (`AppDiagnostics`).
   - Obtains native window handle (`HWND`) and registers message hooks (`HwndSource.AddHook`).
   - Configures the notification area icon via `Shell_NotifyIconW`.
   - Launches background polling timers for CPU, RAM, and Network metrics (1-2s intervals).
   - Collects initial snapshot and renders virtualized process cards.

---

## Compilation Pipeline (Phases 1-6)

Running `build-dashboard.ps1` executes an exhaustive automated verification gate consisting of 55 static checks, Roslyn localization audits, and extensive unit/integration test suites:

```mermaid
flowchart LR
    F1["PHASE 1<br/>55 Static Checks & Audits<br/>(PowerShell, R35 Localization, Safety)"] --> F2["PHASE 2<br/>Automated Test Suites<br/>(RAM, Sockets, R35 Localization, UI Smoke)"]
    F2 --> F3["PHASES 3 & 4<br/>Profiler & Fixture Build"]
    F3 --> F5["PHASE 5<br/>WPF App Compilation<br/>(SysPulse.App)"]
    F5 --> F6["PHASE 6<br/>Windows x64 Publication<br/>(publish/win-x64)"]
```

### Phase Breakdown

- **PHASE 1: Static Code Audits & Safety Rules**:
  - Validates AST syntax across all 113 PowerShell scripts in the repository.
  - Verifies R35 localization manifest hashes across 143 generated files and canonical sources (`check-localization-r35.ps1`).
  - Verifies inheritable XAML window roots and code-behind alignment (`check-localized-window-r35r2.ps1`).
  - Verifies XAML safety (no dynamic event handlers embedded inside Style setters).
  - Verifies profiler isolation (no unprompted memory dumps; 85% RAM guard rule).
  - Validates historical revision conformance gates (R8 through R35 R2).
- **PHASE 2: Automated Unit, Integration & UI Tests**:
  - `LocalizationTests`: 372 comprehensive checks verifying dictionary coverage, technical identity preservation, and report formatting in English and Italian.
  - `LocalizationUiTests`: Headless verification of localized WPF window types and titles.
  - `DiagnosticsTests`: JSONL event structure and privacy redactions.
  - `GuardianDashboardTests`: Read-only SQLite query verification.
  - `MemoryAlertTests`: Memory threshold hysteresis policy (85% alert, 80% clear).
  - `CpuTests`: Monotonic time intervals and dual-sample delta math.
  - `LiveMetricsTests`: Working set calculations and socket counting.
  - `ProcessGroupsTests`: Group aggregation and identity filter matching.
  - `ProcessGroupsUiTests` & `MainWindowSmoke`: Real WPF `MainWindow` lifecycle, caption minimize, taskbar restoration, and Shell v4 notification packet routing.
  - `IconResourceTests`: Multi-resolution PNG icon resource extraction and symbol catalog validation.
  - `NetworkUiTests` & `NativeNetworkTests`: Simulated socket state verification.
- **PHASES 3 & 4: Profiler & Fixture Build**:
  - Builds synthetic test fixture and independent on-demand profiler (`ProcessLens.Profiler.exe`).
- **PHASE 5: Main Application Build**:
  - Compiles `ProcessLens.App.csproj` in `Release` configuration and compiles XAML to BAML.
- **PHASE 6: Windows x64 Publication**:
  - Publishes ready-to-run binaries to `publish/win-x64/` (generating both `SysPulse.exe` and `ProcessLens.exe`) and stamps the `supporto/build-r35r2.ok` receipt.

---

## Compilation & Startup Durations

| Stage | Typical Duration | Description |
| :--- | :--- | :--- |
| **Instant Launch (Pre-Built)** | **< 1.0 second** | Immediate native launch of `SysPulse.exe` via `AVVIA_PROCESSLENS.cmd`. |
| **Incremental Build (`dotnet build`)** | **~3 - 5 seconds** | Fast rebuild of modified assemblies during development. |
| **Full Build & Verification Pipeline** | **~60 - 90 seconds** | Complete execution of 55 static audits, R35 localization checks, full test suites, WPF shell fixture, and multi-project release publication. |

---

## Runtime Architecture: How SysPulse Operates

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant GUI as MainWindow (WPF Dispatcher)
    participant Timers as Background Polling Timers
    participant Sampler as Native Windows Samplers (IP Helper)
    participant Inspector as Deep Process Inspector
    participant Tray as TrayWindowController (Shell32)

    User->>GUI: Launch application
    GUI->>Tray: Register icon (Shell_NotifyIconW)
    GUI->>Timers: Start CPU/RAM/Net timer (1-2s) & Process scan (2-5s)
    loop Periodic Telemetry Sampling
        Timers->>Sampler: Poll CPU times, Working Set, TCP/UDP tables
        Sampler-->>GUI: Immutable samples (LiveNetworkSample, LiveCpuSample)
        GUI->>GUI: Update visible rows & recompute group aggregates
    end
    User->>GUI: Select process row (PID)
    GUI->>Inspector: On-demand deep inspection (Modules, Sockets, UWP)
    Inspector-->>GUI: Enriched facts & semantic classification
    User->>GUI: Click "Hide to tray" / "Nascondi accanto all'orologio"
    GUI->>Tray: HideToTray() -> _window.Hide()
    GUI->>Timers: Suspend polling timers (conserves background CPU)
    User->>Tray: Click notification tray icon (Message 0x8045)
    Tray->>GUI: Restore() -> _window.Show()
    GUI->>Timers: Resume background polling timers
```

---

### Real-Time Network Monitoring & Sockets

Implemented in `WindowsLiveNetworkSampler.cs`, the network pipeline is non-invasive:

1. **Extended Connection Tables**:
   Safe Win32 P/Invoke of `GetExtendedTcpTable` and `GetExtendedUdpTable` maps open endpoints to owning PIDs.
2. **Transfer Throughput Calculation**:
   Samples `IO_COUNTERS` on process handles with `PROCESS_QUERY_LIMITED_INFORMATION`:
   $$\text{Throughput} = \frac{\Delta \text{Bytes}}{\Delta t}$$
3. **Live GUI Binding & Sorting**:
   Formatted as readable units (`KB/s`, `MB/s`) with live connection counts. Clicking the **Network** sort header instantly surfaces top network consumers.

---

### 4-Tier Process Icon Resolution (Zero Generic Placeholders)

Implemented in `ProcessIconResolver.cs`, ensuring every single process has a clear visual identity:

```text
[ Process Instance: ExecutablePath, ProcessName, CommandLine ]
                              │
             ┌────────────────▼───────────────┐
             │ 1. Native PE Executable Icon   │ ──(Found)───> [ BitmapSource ]
             └────────────────┬───────────────┘
                              │ Missing or generic
             ┌────────────────▼───────────────┐
             │ 2. AppxManifest.xml (UWP/Store)│ ──(Found)───> [ BitmapSource ]
             └────────────────┬───────────────┘
                              │ Missing
             ┌────────────────▼───────────────┐
             │ 3. Sibling / Parent App Icon   │ ──(Found)───> [ BitmapSource ]
             └────────────────┬───────────────┘
                              │ Missing
             ┌────────────────▼───────────────┐
             │ 4. Semantic Catalog & Vector   │ ──(Vector)──> [ Frozen DrawingImage ]
             │    Glyph (ProcessSymbols.json) │
             └────────────────────────────────┘
```

1. **Native PE Icon**: Extracts primary icon using `ExtractIconExW`.
2. **Appx / Windows Store (UWP)**: Parses `AppxManifest.xml` in package directory and loads official high-res PNG logos (e.g. `Square44x44Logo.png`).
3. **Helper / Sibling Inheritance**: Unadorned helper executables (e.g. Electron or browser renderers) inherit the branding of the parent application or main directory executable.
4. **Semantic Vector Glyphs**: For utility binaries without icons, `ProcessSymbolDrawing.cs` leverages `ProcessSymbols.json` (61 categories, 480+ mapped binaries) to render crisp, modern vector icons tinted according to their functional role and accented with hash-stable geometric markers.

---

### Memory Analytics: Private Working Set vs Total

- **Private Working Set**:
  Physical RAM held exclusively by the process. Cannot be shared with other processes; represents the true memory cost of running the executable.
- **Total Working Set**:
  Physical RAM including shared library pages mapped by multiple processes (e.g., system DLLs).
- **System-Wide Pressure Protection**:
  Monitors `GlobalMemoryStatusEx` and dynamically eases refresh intervals during severe memory pressure to prevent system stutter.

---

### Window Lifecycle & System Notification Tray

Managed by `TrayWindowController.cs`:

- **Standard Minimize**: Minimizes to the taskbar.
- **Hide to Tray**:
  Hides the window, clears taskbar presence, and suspends polling loops to conserve CPU while running in background.
- **Restore**:
  Single-click or context menu activation restores the window to its exact previous state (Normal/Maximized).
- **Shell Recovery**:
  Listens for the `TaskbarCreated` message to cleanly re-register the tray icon if Windows Explorer crashes or restarts.

---

## System Requirements

- **Operating System**: Windows 10 (version 1809 or later) or Windows 11 (64-bit x64).
- **.NET Environment**:
  - **Running**: .NET 8.0 Desktop Runtime (x64) installed or local offline runtime bundle.
  - **Building**: .NET 8.0 SDK (x64).
- **Privileges**: Administrator privileges (UAC prompt) to query global process I/O and network endpoints.
- **Dependencies**: **Zero**. No external driver installations, WinPcap, or Npcap required.

---

## Troubleshooting

### Application does not open when clicking `AVVIA_PROCESSLENS.cmd`
- Ensure the archive is extracted into a standard user directory with write access (not inside `C:\Windows\System32`).
- Accept the Windows UAC confirmation dialog ("Do you want to allow this app to make changes?").
- Verify that .NET 8 Desktop Runtime is installed:
  ```cmd
  dotnet --list-runtimes
  ```
  Look for `Microsoft.WindowsDesktop.App 8.0.x`.

### Compilation fails running `build-dashboard.ps1`
- Confirm .NET 8 SDK is present:
  ```cmd
  dotnet --list-sdks
  ```
- Ensure PowerShell script execution is enabled:
  ```powershell
  Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
  ```
- Review the automated build journal at `%LOCALAPPDATA%\ProcessLens\logs\build.jsonl`.

---

## License & Disclaimers

SysPulse is open source under the terms of the [MIT License](LICENSE). 

Windows, Win32, WPF, and UWP are trademarks of Microsoft Corporation. SysPulse interacts exclusively through documented Windows operating system APIs in strict adherence to platform security guidelines.
