# R33 R1 — correzione del pacchetto ZIP

Il pacchetto R33 ricevuto contiene 1.523 record locali completi, ma termina prima degli ultimi file e dell'indice centrale ZIP. Dimensione: 4.383.571 byte; SHA-256: 0bb884e5cb7554c5a7ed9caa859a93bfeb0a75f4001896f8db8ac719773a4eb2. Python ZipFile lo rifiuta e Windows lo mostra vuoto. La causa della troncatura non è dimostrata.

Ricostruzione: base R27 verificata, applicazione delle patch R28, R29, R30, R31, R31 R1, R32, R32 R1, R32 R2 e R33; sovrapposizione dei record recuperati dopo verifica CRC. Tutti i 1.790 file del manifesto R33 originale sono presenti e coincidono esattamente con le rispettive impronte SHA-256. Nessun file applicativo ricostruito per supposizione.

Questa revisione corregge soltanto il confezionamento. Sorgenti, interfaccia, icone, launcher e test sono identici alla R33. Aggiunti questo resoconto e il controllo riproducibile del pacchetto; rigenerati i due manifesti. I 308 controlli applicativi della R33 sono storici e non sono stati rieseguiti per questa correzione del contenitore.

Il confezionamento scrive prima un file temporaneo, chiude il writer, sincronizza i byte, verifica indice, numero di file, CRC e manifesti, poi pubblica il nome definitivo con sostituzione atomica. Compressione ZIP DEFLATE, nessuna cifratura o ZIP64. Controllo indipendente con Info-ZIP unzip e confronto byte per byte dell'estrazione. Dopo il salvataggio viene scaricato il pacchetto persistito e confrontato con il file verificato, per controllare anche il trasferimento.

Avvio su Windows: estrarre tutto in una cartella nuova, aprire ProcessLens-Leggero e avviare AVVIA_PROCESSLENS.cmd. Non eseguire il launcher dentro lo ZIP. Il comportamento WPF resta da verificare su Windows.
