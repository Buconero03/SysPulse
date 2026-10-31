# ProcessLens v0.16.0 — Memory Intelligence & User-Session Alerts

## Scope
- `Memoria RAM` WPF: `GlobalMemoryStatusEx` physical memory (used=total-available) every 10 seconds **only while tab is visible**; top 20 resident memory users queried on request/visible tab, read-only.
- WinForms tray MemoryAgent: reads only `GlobalMemoryStatusEx` every 10 seconds; alerts on first reading `>= 85%`, **no repeated alerts** until a later reading `< 80%`. Startup when usage is already 85%+ alerts once. Tooltips and balloon warnings can be suppressed by Windows Focus Assist/notification preferences; no guaranteed notification delivery; the tray icon remains for user checks.
- HKCU Run optional explicit opt-in script, installed in LocalAppData. User session only: Windows Service **cannot** directly show interactive toasts. Guardian v0.14.1 is neither stopped nor modified.
- `memory-agent.log` stored in LocalAppData (bounded ~64 KiB per log file; rotated on overflow). Contains timestamps and RAM threshold crossings, no process commands or names.
- `MemoryAlertPolicy` test project validates 85/80 hysteresis, initial >85, invalid readings.

## Install (after build)
```powershell
.\build-dashboard.ps1
.\run-release.ps1
# In a separate NON-admin PowerShell under the account that should receive the alerts:
.\install-memory-alerts.ps1
# Type ATTIVA to confirm. To inspect:
.\memory-alerts-status.ps1
# To opt out:
.\uninstall-memory-alerts.ps1
```

## Safe process memory management
The dashboard only shows top processes by working set and private commit. Working set includes shared memory; these rows must not be summed. No force working-set trimming, process killing, pagefile modifications or memory optimization claims about third-party programs. Closing applications or adjusting browser tabs remains the user's decision. OS memory compression and standby cache are not automatically problems.

## Performance optimization
The WPF process list no longer performs full 2-second process captures when the `Servizi`, `Guardian` or `Memoria RAM` view is displayed; the memory tab uses a small targeted memory reader and a bounded top-N enumeration every 10 seconds. The Windows Collector and existing Guardian service code/data stay unchanged. MemoryAgent consumes additional RAM itself, so its actual cost must be measured on the target Windows machine before making claims of net RAM savings.

## Validation
Preflight checks, pure .NET policy tests and WPF+agent compilation on Windows via `build-dashboard.ps1`. No unattended background installation. Test runtime notifications with temporary CPU/RAM loads **only with the user's deliberate safe actions**; do not artificially exhaust RAM to force 85%. The tray menu can display current physical RAM usage at any time and includes **Prova notifica** to verify Windows notification settings without deliberately exhausting memory. Use `.\memory-footprint.ps1` to measure per-process Working Set/Private before and after.
