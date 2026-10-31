# Process Query Engine

ProcessLens v0.7.2 mantiene un livello interrogabile sopra il motore evidence-first.
L'utente seleziona un PID e può chiedere in linguaggio naturale:

- Cosa sta facendo?
- A che punto è?
- Quando finirà?
- È bloccato?
- Ha porte aperte? / Ha connessioni di rete?
- Chi lo ha avviato?
- Perché è aperto?
- Posso chiuderlo?

## Regola fondamentale

`fatto osservato != inferenza != stima`.

ProcessLens non ricava una percentuale da CPU/RAM e non inventa un'ETA. Un progresso
viene accettato solo quando il processo espone un segnale esplicito, per esempio:

- `Epoch 34/100`
- `step 420 / 1000`
- `317 of 1200 tests`
- `72%`

Le sorgenti iniziali sono command line e titoli finestra del PID selezionato. Il sistema è
estensibile con futuri provider per log, ETW, I/O, protocolli applicativi e workload noti.

## ETA

Un solo punto di progresso non basta. ProcessLens memorizza il primo campione. Una query
successiva sullo stesso PID e sulla stessa serie di progresso consente di calcolare la
velocità osservata e quindi un'ETA con intervallo prudente. Se il contatore non avanza,
l'ETA viene dichiarata non stimabile.

Per renderer, server, service host, language server e altri componenti long-running, l'ETA
è normalmente `NON APPLICABILE`: sono processi progettati per restare attivi.

## Leggerezza

Il Process Query Engine lavora solo sul PID selezionato. La lista globale continua a usare
il collector leggero e le cache già introdotte. La cronologia CPU conserva al massimo circa
90 secondi / 40 campioni per PID.


## Rete e porte

La query di rete è evidence-first:

- `TCP LISTEN` = porta/socket in ascolto attribuita al PID;
- `TCP ESTABLISHED` = connessione TCP attualmente stabilita;
- `UDP endpoint` = endpoint UDP associato al PID;
- `famiglia stesso eseguibile` = processo selezionato più processi correlati same-executable nella stessa gerarchia multiprocesso.

ProcessLens non traduce automaticamente questi dati in “usa Internet sì/no”, perché una connessione può essere locale/LAN, una porta LISTEN può essere solo locale e molte applicazioni delegano la rete a un helper separato.


## Network ownership (v0.7.2)

La query `Porte / connessioni?` separa sempre:

- socket possedute dal PID selezionato;
- porte TCP `LISTEN` e indirizzo di bind;
- TCP `ESTABLISHED` con endpoint locale/remoto;
- connessioni remote classificate come loopback, LAN/private o indirizzo pubblico;
- socket possedute da processi correlati della stessa famiglia;
- ruolo generico del processo che possiede le socket (quando dichiarato dalla command line, ad esempio Chromium `NetworkService`).

Una connessione a un IP pubblico viene descritta come evidenza di rete esterna, non come prova assoluta del servizio remoto usato. Una porta `LISTEN` su `127.0.0.1`/`::1` è locale; `0.0.0.0`/`::` significa bind su tutte le interfacce locali ma non implica automaticamente esposizione Internet.
