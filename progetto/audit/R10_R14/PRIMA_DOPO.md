# R13 → R14 — revisione mirata

| Prima | Dopo |
|---|---|
| Il filtro Windows usa la categoria del classificatore | Sezione Sistema Windows con criteri di origine espliciti e opzione Tutti i processi rilevati |
| Nessun inventario nativo separato nella GUI | NtQuerySystemInformation su richiesta; fallback Toolhelp con errore originale |
| Nessuna sezione globale del kernel in questa vista | GetPerformanceInfo: commit e pool separati dal Working Set totale |
| Nessuna navigazione da questo inventario | Apertura analisi solo con PID, nome e avvio corrispondenti |
| Nessuna tabella dedicata | Testo selezionabile, tabella copiabile/virtualizzata, ricerca PID esatta |

Sono cambiati quattro file di produzione esistenti; quattro file di produzione sono nuovi. Tutti i 122 altri file di produzione esistenti sono identici alla R13. Le suite C# pregresse sono conservate byte per byte; le due nuove suite sono aggiunte al build standard. Gli audit R11/R12/R13 sono storici; i controlli dipendenti dai vecchi ZIP non sono stati nuovamente eseguiti.

I nuovi controlli Python sono controlli sui sorgenti/XML e mutazioni di quei controlli. Non sono compilazione C#, test numerici eseguiti in .NET, lettura nativa Windows o prova di rendering. Il consumo RAM non è stato misurato.
