# R8 candidata — riduzione degli oggetti trattenuti

Base: ZIP ProcessLens-v0.19.16-R7.zip fornito dall’utente; modifiche personalizzate mantenute.

- Dashboard create al primo accesso e riutilizzate dopo la prima apertura.
- Dettagli classificazione e tecnici creati solo su richiesta, rilasciati alla chiusura del pannello.
- Cache limitate: 128 icone, 8 deep inspection, 16 cronologie servizi.
- Storico RAM con campioni inline e materializzazione dei record solo sul trend richiesto.
- Riciclo esplicito dei contenitori di processi e servizi.
- Nome ProcessLens esplicito nell’apphost pubblicato.
- Nuova suite MemoryEfficiencyTests, verificatore R8 e campionamento passivo prima/dopo.

Core/Semantics, acquisizione Windows, Guardian, MemoryAgent e profiler invariati. Nessuna profilazione o riduzione forzata del working set. Build/test .NET/PowerShell/WPF e RAM R8 su Windows NON ESEGUITI qui. Nessun risparmio in MB dichiarato.
