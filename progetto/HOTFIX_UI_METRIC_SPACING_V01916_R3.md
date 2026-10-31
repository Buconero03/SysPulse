# ProcessLens v0.19.16 R3 — Etichette CPU/RAM nella GUI

## Segnalazione

Nella lista dei processi, CPU DEL SISTEMA e RAM PRIVATA RESIDENTE si sovrappongono. Anche nel riepilogo del pannello destro le tre etichette CPU, RAM e PROGETTO possono toccarsi o sovrapporsi.

## Modifica

- Lista: colonne riservate; etichette compatte CPU e RAM PRIVATA / RAM RESIDENTE / RAM N/D, con tooltip tecnico completo; valore misurato invariato.
- Scheda dettaglio: etichette CPU, RAM PRIVATA / RAM RESIDENTE e PROGETTO, con TextWrapping, spazio tra colonne, altezza minima e tooltip con definizione della misura.
- La sezione «MEMORIA · MISURE SEPARATE» conserva le denominazioni complete: RAM privata residente, RAM residente totale e memoria privata impegnata.
- Fallback del contatore di memoria rimane esplicitamente distinto: la parola «RAM PRIVATA» compare solo con Private Working Set disponibile.
- Guardiani, collector Windows, MemoryAgent, profiler, logica CPU e contatori di memoria non modificati.

## Verifiche da eseguire su Windows

1. `AVVIA-TUTTO.cmd`: suite e build WPF.
2. Lista a larghezza minima e massima; processi con/ senza RAM privata misurabile.
3. Scheda dettaglio a larghezza minima: nessuna sovrapposizione.
4. Tooltip per dati RAM / fallback / non disponibile.
5. DPI Windows 100%, 125% e 150%, qualora disponibili.

La validazione XML/XAML offline non costituisce una compilazione WPF o uno screenshot di Windows. Questo hotfix rimane Candidate fino ai test locali.
