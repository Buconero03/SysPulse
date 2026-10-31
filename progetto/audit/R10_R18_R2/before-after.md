# Prima / dopo — R18 R2

| Contesto | R18 R1 | R18 R2 |
| --- | --- | --- |
| `template=` nel messaggio C# | Blocco erroneo con `-match` | Stringa esclusa dal controllo del codice |
| Commenti e stringhe con `Template=` o `.Show()` | Possono causare falsi positivi | Esclusi dal controllo lessicale |
| Assegnazione `Template = ...` o chiamata `.Show()` | Vietata | Ancora vietata, verificata con mutazioni |
| Uguaglianza `Template == ...` | Scambiata per assegnazione | Accettata |
| Asserzioni originali WPF | Richieste | Invariate |
| UI, rete, Guardian, MemoryAgent e profiler | Codice della R18 R1 | Identico byte per byte |

Il controllo è lessicale e limitato a questo fixture C#: non sostituisce la compilazione C# o una verifica WPF su Windows. Le forme di stringa usate dal fixture sono coperte dai casi di regressione; non è un analizzatore generale del linguaggio C#.
