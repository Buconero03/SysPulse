# ProcessLens v0.19.16 R10 R1 — stile del pulsante Analizza RAM ora

Il log Windows dell’utente si interrompe nel controllo `check-ui-format-r9.ps1` con `FAIL R9: unstyled or fixed-height button`. Le fasi successive non sono state eseguite.

La causa è il pulsante `AnalyzeRamDirectButton`, introdotto nel nuovo ramo utente e integrato nella R10 senza il riferimento a `ActionButtonStyle`. Il controllo R9 richiede per ogni pulsante uno dei due stili comuni e vieta un’altezza fissa. Il pulsante era l’unico che violava questa regola. È un errore nell’integrazione della candidata R10.

Correzioni:

- Aggiunto `Style="{StaticResource ActionButtonStyle}"` al pulsante. L’azione di analisi RAM e i suoi parametri rimangono gli stessi. Lo stile fornisce wrapping e altezza minima.
- Il controllo PowerShell conserva la stessa condizione di errore; ora stampa nome, testo, stile e altezza del controllo coinvolto.
- Aggiunte al controllo PowerShell tre mutazioni negative sul vero markup: rimozione dello stile, stile sconosciuto e altezza fissa. Devono essere tutte respinte.
- Aggiunta la regola mancante anche al verificatore portabile R10, che prima controllava altri aspetti dell’interfaccia e non questo requisito.
- Regressione portabile sullo ZIP R10 realmente consegnato: riproduce il difetto del solo pulsante Analizza RAM ora; la copia corretta passa. Altre tre mutazioni vengono respinte e il collegamento dell’evento rimane presente.

Eseguiti in questo ambiente: 6 regressioni portabili, 41 controlli strutturali, applicazione reale della patch e confronto SHA-256, integrità del pacchetto. Non sono esecuzioni PowerShell o WPF.

**NON ESEGUITO:** PowerShell corretto, build/test .NET, caricamento WPF e verifica visuale/DPI. Restano le criticità di contabilità RAM documentate nel rapporto R10. Non vengono modificate acquisizioni Windows, contratti, servizi, profiler o funzionalità di copia.

Il log Windows fornito viene registrato come osservazione dell’utente: controlli iniziali superati fino a R8, controllo R9 fallito, fasi successive non eseguite. Non si attribuisce il risultato positivo delle regressioni portabili alla suite Windows.

Estrarre la candidata corretta in una cartella nuova; nella radice con `src` e `tests` eseguire:

```powershell
.\build-dashboard.ps1
```

Se termina con successo:

```powershell
.\run-release.ps1
```

Materiali: `audit/R10_R1/`, patch esatta R10 → R10 R1, matrice aggiornata e manifest `SHA256SUMS_R10.txt`. Le registrazioni precedenti rimangono storiche.
