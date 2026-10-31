# ProcessLens v0.8 — Origine dei processi (prima fase)

## Obiettivo

Quando si seleziona un processo, mostrare **da dove nasce** (catena padre/figlio)
separatamente da **quale comando sta eseguendo**. Il motore non deve confondere
il nome `git.exe` con l'affermazione `git push`.

## Fonti

- `CreateToolhelp32Snapshot` restituisce PID e Parent PID.
- `System.Diagnostics.Process.StartTime` permette di escludere un presunto padre
  creato *dopo* il figlio (tipico segnale di PID riutilizzato).
- `NtQueryInformationProcess(ProcessCommandLineInformation)` fornisce, quando
  accessibile, la command line dell'eseguibile/degli antenati ancora attivi.
- Nessuna connessione di rete, scrittura nel repository o esecuzione di Git viene
  effettuata da questa funzione: è solo lettura.

## Cosa può dire

Se trova `git.exe push origin main`, ProcessLens può dire che il comando `git push`
è stato richiesto alla istanza di Git. Questo NON prova il trasferimento dei
commit né il suo buon esito. I comandi dei processi padre vengono usati soltanto
se gli orari di creazione ne verificano la relazione temporale.

Se osserva la catena `ChatGPT.exe -> git.exe`, può attribuire a Windows la
relazione parent/child. NON sa se l'utente, un agente o un workflow automatico
abbia ordinato quella operazione senza informazioni applicative aggiuntive.

## Limiti

1. I processi padre già terminati non possono essere ricostruiti retroattivamente
   da uno snapshot (servirebbe un monitor eventi ETW avviato prima).
2. Il Parent PID può essere impostato esplicitamente alla creazione di un processo;
   quindi neppure una catena verificata temporalmente prova l'intenzione dell'utente.
3. `git -C <path>` è un indizio di directory, non prova della root Git; per i
   comandi Git senza `-C`, la directory di lavoro non è ricavata in questa fase.
4. ProcessLens non cerca di leggere token/password eventualmente presenti nelle
   command line dei parent; la vista predefinita mostra solo sintesi e PID.
5. Le operazioni Git diverse dai sottocomandi riconosciuti restano non determinate.

## Test da eseguire su Windows

1. Aprire ProcessLens, selezionare `git.exe` durante `git status` e chiedere
   `Da dove è partito?`: attendersi la catena osservabile e `status`.
2. Se il processo è scomparso, non aspettarsi informazioni retroattive.
3. Verificare che un processo `git.exe` privo di command line NON venga
   automaticamente classificato come `git push`.
4. Verificare catene con `powershell.exe -> git.exe`, `Code.exe -> git.exe`
   e comandi Windows generici (non Git).
5. Eseguire `dotnet build .\ProcessLens.sln -c Release` e segnalare eventuali errori.
