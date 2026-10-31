# Prima / dopo — R19

| Area | R18 R2 | R19 |
| --- | --- | --- |
| Barra globale | CPU, media, CPU logici | Aggiunta RAM fisica in uso / totale e percentuale |
| Dato RAM mancante | Nessun indicatore globale | N/D; lettura non convertita in zero |
| Finestra ridotta | CPU sospesa | Anche RAM marcata Sospeso |
| Tabella componenti | Griglia verticale e orizzontale; righe compatte | Righe alternate, separatori orizzontali, celle distanziate |
| Dettagli | Testo sotto il selettore | Riquadro con titolo e testo più grande |
| Analisi PID | Pulsante nella barra superiore | Pulsante accanto alla scelta dell'istanza |
| Copertura catalogo | Nota sempre aperta | Nota conservata nell'espansore esistente |
| Acquisizione | Letture già presenti | Stesse API; una lettura globale RAM nel worker CPU, nessuna scansione aggiuntiva |

La semantica dei contatori dei componenti e i rapporti copiati non cambiano. La colonna WS max letto resta il massimo fra le istanze disponibili, non una somma. Codice del catalogo e della vista invariati; cambia il markup della vista.

Nessuna nuova misurazione di consumo reale o verifica visiva Windows è stata eseguita qui.
