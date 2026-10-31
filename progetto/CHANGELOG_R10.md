# R10 R6 — registro verifiche RAM leggibile e priorità del progetto

Leggere `REGISTRO_LEGGIBILE_R10_R6.md` e `VALUTAZIONE_PROGETTO_R10_R6.md`. Il registro visto nella schermata ha ora voci separate: orario, fase, messaggio, carattere più grande, margini e spazi fra eventi. Copia registro completo usa il testo originale; file e codici diagnostici invariati. Aggiornamento incrementale e rilascio delle schede a registro chiuso. R5 e funzionalità precedenti incluse. Build/test .NET/PowerShell e WPF/DPI **NON ESEGUITI** qui. Manifest corrente `SHA256SUMS_R10.txt`.

---

# R10 R5 — riapertura della finestra e icona vicino all’orologio

Leggere `RIPRISTINO_FINESTRA_R10_R5.md`. – minimizza nella barra; X nasconde nella tray; clic sull’icona o Apri ProcessLens ripristina; Esci da ProcessLens chiude davvero. Un secondo avvio richiede il ripristino dell’istanza esistente. Dashboard GUI sospese da nascosto/minimizzato; componenti esterni invariati. Build/test .NET/PowerShell e WPF/tray reali **NON ESEGUITI** qui. Manifest corrente `SHA256SUMS_R10.txt`. Le revisioni precedenti restano incluse.

---

# R10 R4 — notifica di chiusura del processo

Leggere `NOTIFICA_CHIUSURA_R10_R4.md`. Piccolo avviso nella finestra quando Windows segnala la fine del processo selezionato o di quello oggetto della richiesta esplicita. Nome e PID, durata 8 secondi, nessuno spostamento del focus. Richiesta accettata e uscita confermata restano distinte. R3 inclusa; componenti protetti e terminazione nativa invariati. Build/test .NET/PowerShell, WPF e DPI: **NON ESEGUITO** qui. Manifest corrente `SHA256SUMS_R10.txt`.

---

# R10 R3 — quadro generale e gerarchia dei rapporti

Leggere `GERARCHIA_RAPPORTI_R10_R3.md`. Aggiunti quadro generale iniziale, capitoli numerati, titoli a capo e blocchi distinti per API, allocazioni e DLL. Testo originale conservato per la copia completa; paragrafi senza titolo mantenuti anche nella vista strutturata. I controlli si creano aprendo il capitolo. R2 resta inclusa. Acquisizione Windows e componenti protetti invariati. Build/test .NET/PowerShell, WPF e DPI: **NON ESEGUITO** qui. Manifest corrente `SHA256SUMS_R10.txt`, rigenerato per R3.

---

# R10 R2 — inizializzazione del test di layout WPF

Leggere `HOTFIX_R10_R2.md`. Corretto il ciclo di inizializzazione del controllo isolato nella suite CopyUiTests; verifiche esplicite di template nativo, altezza, righe, wrapping e selezione. Nessun controllo disabilitato. Sorgenti applicativi C# e XAML invariati rispetto a R1. La nuova suite .NET/WPF e PowerShell resta **NON ESEGUITA** in questo ambiente. Manifest corrente: `SHA256SUMS_R10.txt`, rigenerato per R2. I risultati Windows del log utente si riferiscono alla R1.

---

# R10 R1 candidata — correzione controllo pulsanti

Stile comune applicato al pulsante Analizza RAM ora. Controllo R9 invariato nei vincoli, con diagnostica puntuale e regressioni negative; regola aggiunta ai controlli portabili. Vedere `HOTFIX_R10_R1.md`. Build/test .NET/PowerShell e verifica WPF: NON ESEGUITO.

---

# ProcessLens v0.19.16 R10 candidata

- Selezione e copia dei testi informativi nei pannelli processo, analisi, RAM, cronologia servizi e stato Guardian.
- Copia del rapporto completo, comprese sezioni chiuse e registro verifiche.
- Menu per copiare cella, righe o tabella filtrata, preservando formati e colonne visibili.
- Snapshot di copia azzerato fra richieste; controlli in sola lettura e binding OneWay.
- Integrazione del nuovo ramo utente con R8/R9: conservate ottimizzazioni RAM, formattazione e nuove funzioni della dashboard.
- Corretto il collegamento dell’evento della dashboard differita; rimosse chiusure forzate automatiche dagli script.
- Nuove suite pure e WPF e controllo PowerShell integrati nella build.
- Revisione documentata dei 15 file modificati dall’utente, con problemi ancora aperti nei totali e nelle attribuzioni RAM.

Leggere `COPIA_TESTI_E_REVISIONE_R10.md`. Build/test .NET, PowerShell e visuale WPF: **NON ESEGUITO**. RAM: **NON MISURATO**. Manifest corrente: `SHA256SUMS_R10.txt`; audit precedenti storici.
