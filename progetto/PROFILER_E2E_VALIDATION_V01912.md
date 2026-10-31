# ProcessLens v0.19.12 - Prova isolata del profiler .NET

## Scopo e stato

Questo pacchetto include un programma .NET 8 di test con 4096 oggetti `KnownPayload`,
contenuti sintetici, nessuna rete e nessun file dell'utente. Serve esclusivamente
per valutare la compatibilita fra `ProcessLensProfiler.exe`, `dotnet-gcdump` e un
processo CLR non critico. Non dimostra che il profiler funzioni sui processi reali.

**Stato:** sola verifica statica del sorgente qui; occorrono build Windows e prova
esplicita prima di parlare di compatibilita end-to-end.

## Avvio tradizionale invariato

`AVVIA-TUTTO.cmd` compila la GUI, il profiler e il target sintetico di prova,
ma **non avvia** quest'ultimo e **non induce** alcuna garbage collection nel sistema.
Non viene modificato Guardian, il suo database o MemoryAgent.

## Primo passo: sola verifica, nessun profiler

Dopo la build, aprire una PowerShell nella cartella estratta ed eseguire:

```powershell
.\verify-dotnet-profiler.ps1
```

Questo controlla disponibilita dei componenti e non avvia processi bersaglio,
GC, dump, installazioni o servizi.

## Secondo passo: prova volontaria con target sintetico

Eseguire **solo quando CPU e RAM sono a livelli normali**, mai sotto pressione.

```powershell
.\verify-dotnet-profiler.ps1 -RunCapture
```

Viene richiesta la parola esatta `PROFILA`. Senza di essa nulla viene raccolto.
Lo script rifiuta la prova se la RAM e gia al 80% o oltre. Il profiler esistente
applica inoltre il proprio blocco RAM al 85%, controlla identita PID + ora avvio,
limiti temporali/di disco e annullamento.

Se autorizzato, lo script compila e avvia **solo** il proprio eseguibile di prova,
misura brevemente CPU e working set del target, invoca il profiler esterno,
controlla che il report JSON corrisponda al PID e che ci siano statistiche
positive. Se `KnownPayload` compare nei primi 20 tipi, lo segnala come ulteriore
prova; se manca, non finge di averlo rilevato. Infine tenta di terminare il
target e rimuove il report JSON temporaneo.

La raccolta forza una Gen2 GC nel target sintetico: puo aumentare temporaneamente
RAM e latenza. La differenza di working set osservata NON e la pausa GC, e una
sola acquisizione non dimostra memory leak o motivi di retention. Per misurare
la pausa serve una fase ulteriore con apposita strumentazione.

## Esiti da riportare

Inviare l'output della build e della verifica. In caso di errore indicare
l'intero messaggio, lo stato `ok/unavailable`, il codice del profiler e il tempo
di esecuzione; non pubblicare command line personali o altri dump.

## Qualita e limiti

- Nessuna installazione o rete automatica.
- Nessuna modifica a Guardian/SQLite, MemoryAgent o altri processi Windows.
- Programma target con durata massima di 150 secondi e memoria sintetica modesta.
- Protezione RAM di 80% per la prova, piu gate 85% nel profiler.
- I limiti helper/dump sono di tipo best effort; non sono quote OS rigide.
- Stop del target in `finally` e cleanup dei file di sessione.
- Prima di dichiarare pronta la funzionalita servono test reali di errori,
  timeout, cancel, PID reuse, output malformato e misurazione overhead.
