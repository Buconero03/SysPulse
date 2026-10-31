using System.Globalization;
using ProcessLens.App;

static void Check(bool ok, string label)
{
    if (!ok) throw new Exception("FAIL copy: " + label);
    Console.WriteLine("PASS copy: " + label);
}
string body = "■ DIAGNOSI IN BREVE\nAPI VirtualQueryEx: 26,0 MiB\n\n■ SEZIONE CHIUSA\nC:\\Cartella con spazi\\file.dll\n\nRiga senza titolo, da conservare.\nEmoji 🧪 e accenti: perché, già.";
string report = CopyTextFormat.Report("Analisi RAM", body, "MISURE PARZIALI", "Evidenze: Windows", "[ESITO] Dato non disponibile ≠ zero.");
Check(report.Contains(body, StringComparison.Ordinal), "full body, closed sections and unheaded paragraphs preserved exactly");
Check(report.EndsWith("REGISTRO VERIFICHE\n[ESITO] Dato non disponibile ≠ zero.", StringComparison.Ordinal), "visible measurement log included");
Check(CopyTextFormat.Report("", body, "", "") == body, "empty optional fields do not rewrite body");
Check(!report.Contains("CommandLine:", StringComparison.Ordinal), "no extra process metadata appended");
string table = CopyTextFormat.Table(new[] { "API", "Messaggio" }, new IReadOnlyList<string>[]
{
    new[] { "VirtualQueryEx", "prima\r\nseconda\tcon \"virgolette\"" },
    new[] { "QueryWorkingSetEx", "N/D; lettura parziale" }
});
Check(table == "API\tMessaggio\r\nVirtualQueryEx\t\"prima\r\nseconda\tcon \"\"virgolette\"\"\"\r\nQueryWorkingSetEx\tN/D; lettura parziale",
    "TSV retains tabs, newlines, quotes and missing-value labels");
var saved = CultureInfo.CurrentCulture;
try
{
    foreach (string culture in new[] { "it-IT", "en-US" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        string value = 26d.ToString("F1", CultureInfo.CurrentCulture) + " MiB";
        Check(CopyTextFormat.Table(new[] { "RAM" }, new[] { (IReadOnlyList<string>)new[] { value } }) == "RAM\r\n" + value,
            "copy preserves rendered decimal culture " + culture);
    }
}
finally { CultureInfo.CurrentCulture = saved; }

string structured = "■ DIAGNOSI IN BREVE\r\nPrima osservazione.\r\n\r\nParagrafo senza titolo: perché 🧪.\r\n\r\n■ ALLOCAZIONI WINDOWS — PROVE NATIVE E PICCHI STORICI\r\nVirtualQueryEx: 824,7 MiB virtuali impegnati.\r\nQueryWorkingSetEx: 751 pagine campionate.\r\n\r\nLimite finale da non perdere.";
var chapters = ReportLayout.Sections(structured);
Check(chapters.Count == 2 && chapters[0].Body.Contains("Paragrafo senza titolo: perché 🧪.", StringComparison.Ordinal),
    "report layout retains unheaded paragraphs within the correct chapter");
Check(string.Join("\n", chapters.Select(c => "■ " + c.Heading + "\n" + c.Body)) == structured.Replace("\r\n", "\n", StringComparison.Ordinal),
    "report chapter split preserves every normalized source line");
var preamble = ReportLayout.Sections("Introduzione senza titolo.\n\n■ API\nRisultato.\n\n■ SOLO TITOLO");
Check(preamble.Count == 3 && preamble[0].Body.Contains("Introduzione senza titolo.", StringComparison.Ordinal) &&
    preamble[2].Heading == "SOLO TITOLO" && preamble[2].Body == "",
    "preamble and heading-only final chapter are retained");
Check(ReportLayout.Sections("L'icona ■ nel testo non è un titolo.").Count == 1,
    "embedded square marker does not create a false chapter");
Check(ReportLayout.Sections("Senza marcatori\n\nAncora testo")[0].Body == "Senza marcatori\n\nAncora testo",
    "plain reports preserve all paragraphs");
var blocks = ReportLayout.Blocks(chapters[1].Body);
Check(blocks.Count == 3 && blocks[0].Label == "VirtualQueryEx" && blocks[1].Label == "QueryWorkingSetEx" &&
    blocks[2].Text == "Limite finale da non perdere.", "API sources and unheaded limits get distinct readable blocks");
var psapi = ReportLayout.Blocks("Contatori storici e Kernel Pool (PSAPI EX2): Picco 280,4 MiB · Fault 281.714 · Pool 1,48 MiB");
Check(psapi.Single().Text == "Picco 280,4 MiB\nFault 281.714\nPool 1,48 MiB",
    "PSAPI visual lines retain exact numbers and units");
Check(ReportLayout.EvidenceGroup("Moduli caricati (campione): a.dll, b.dll", "Altre osservazioni") == "Moduli e librerie" &&
    ReportLayout.EvidenceGroup("Deep inspection: Accesso parziale", "Altre osservazioni") == "Esito delle letture" &&
    ReportLayout.EvidenceGroup("Ruolo non verificato", "Funzione") == "Funzione", "evidence chapters distinguish modules and acquisition status without altering other groups");
Check(CopyTextFormat.Report("RAM", structured, "", "").Contains(structured, StringComparison.Ordinal),
    "complete copied report remains original CRLF text independent of visual hierarchy");

var notices = new ProcessExitNotice();
var instance = ProcessExitIdentity.From(1234, new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc), "esempio.exe")!;
Check(!notices.WasShown(instance), "accepted request alone has no confirmed exit notice");
Check(notices.Confirm(instance) == "Il processo esempio.exe (PID 1234) si è chiuso.",
    "confirmed exit notice identifies executable and PID without inventing its cause");
Check(notices.Confirm(instance with { Name = "nome alternativo" }) is null,
    "selection and termination signals for the same instance produce one notice");
Check(notices.Confirm(instance with { StartUtcTicks = instance.StartUtcTicks + 1 }) is not null,
    "reused PID with a different creation timestamp is a separate process");
Check(ProcessExitIdentity.From(1234, null, "N/D") is null && ProcessExitIdentity.From(0, DateTime.UtcNow, "N/D") is null &&
    notices.Confirm(new ProcessExitIdentity(2, 0, "N/D")) is null, "missing identity cannot produce a confirmed exit notice");
for (int n = 0; n < 32; n++) notices.Confirm(instance with { Pid = 2000 + n });
Check(!notices.WasShown(instance), "exit-notice deduplication history stays bounded");

Check(WindowLifetimePolicy.Close(false, true) == WindowCloseAction.MinimizeToTaskbar,
    "X preserves a taskbar restore route even when tray registration succeeded");
Check(WindowLifetimePolicy.Close(false, false) == WindowCloseAction.MinimizeToTaskbar,
    "unavailable tray preserves a taskbar restore route");
Check(WindowLifetimePolicy.Close(true, true) == WindowCloseAction.Exit &&
    WindowLifetimePolicy.Close(true, false) == WindowCloseAction.Exit,
    "explicit Exit and session ending can close with or without a tray icon");
Check(WindowLifetimePolicy.PollingActive(true, false) && !WindowLifetimePolicy.PollingActive(true, true) &&
    !WindowLifetimePolicy.PollingActive(false, false) && !WindowLifetimePolicy.PollingActive(false, true),
    "UI polling resumes only for a visible non-minimized window");

string auditSource = "[23:31:34.149] [VERIFICA RAM] INIZIO — Richiesta esplicita.\r\n" +
    "[23:31:34.506] [VERIFICA RAM] PSAPI — Working Set privato 44,0 MiB.\r\n" +
    "[23:31:34.611] [VERIFICA RAM] ESITO — CAUSE INTERNE NON IDENTIFICATE.\r\n" +
    "Limite aggiuntivo: perché 🧪, nessuna causa inventata.\r\nRegistro locale: C:\\Users\\esempio\\memory-analysis.log";
var audit = AuditLogPresentation.Parse(auditSource);
Check(audit.Entries.Count == 3 && audit.Entries[1].Time == "23:31:34.506" && audit.Entries[1].Stage == "PSAPI" &&
    audit.Entries[1].Message == "Working Set privato 44,0 MiB.", "audit layout separates real timestamp, stage and message");
Check(audit.Entries[2].Message.Contains("Limite aggiuntivo: perché 🧪", StringComparison.Ordinal) &&
    audit.Location == "Registro locale: C:\\Users\\esempio\\memory-analysis.log", "audit continuation and actual local-log location retained");
Check(AuditLogPresentation.Overview(audit) == "3 voci · ultima: ESITO · 23:31:34.611",
    "audit overview counts source entries without declaring them successful");
Check(AuditLogPresentation.Overview(AuditLogPresentation.Parse("")) == "Registro vuoto.",
    "audit overview explicitly labels an empty register");
Check(CopyTextFormat.Report("RAM", "Rapporto", "", "", auditSource).EndsWith(auditSource, StringComparison.Ordinal),
    "audit complete copy retains original CRLF and repeated raw prefixes exactly");
var profilerAudit = AuditLogPresentation.Parse("[AVVIO] Richiesto.\n[SICUREZZA] Limiti attivi.\n[ATTESA] Nessun risultato.");
Check(profilerAudit.Entries.Count == 3 && profilerAudit.Entries[1].Stage == "SICUREZZA" && profilerAudit.Entries[0].Time == "",
    "profiler stages remain distinct without invented timestamps");
Check(AuditLogPresentation.Overview(profilerAudit) == "3 voci · ultima: ATTESA",
    "audit overview without a timestamp does not invent one");
var profilerEnd = AuditLogPresentation.Parse("[23:31:34] ESITO: non disponibile\nAdapter: N/D\nLimite esplicito.");
Check(profilerEnd.Entries.Count == 1 && profilerEnd.Entries[0].Message.Contains("Adapter: N/D\nLimite esplicito.", StringComparison.Ordinal),
    "profiler result keeps all continuation lines");
var unknownAudit = AuditLogPresentation.Parse("Messaggio non strutturato\n[99:99:99.999] [VERIFICA RAM] FASE — invalido");
Check(unknownAudit.Entries.Count == 1 && unknownAudit.Entries[0].Message.Contains("[99:99:99.999]", StringComparison.Ordinal) &&
    AuditLogPresentation.Parse("").Entries.Count == 0, "unknown and malformed audit text is retained without false events");
foreach (string sampleValue in new[] { "44,0", "44.0" })
    Check(AuditLogPresentation.Parse("[23:31:34.506] [VERIFICA RAM] PSAPI — " + sampleValue + " MiB").Entries[0].Message == sampleValue + " MiB",
        "audit layout never reparses or reformats measured decimal text " + sampleValue);

var navigationTarget = new MemoryDetailsTarget(12, "svchost", true);
var navigationTime = DateTime.UnixEpoch;
Check(ServiceNavigation.Matches(navigationTarget, new(12, "svchost.exe", navigationTime, navigationTime)),
    "RAM group lookup accepts normalized executable name only with matching creation");
Check(!ServiceNavigation.Matches(navigationTarget, new(12, "svchost.exe", navigationTime, navigationTime.AddTicks(1))) &&
    !ServiceNavigation.Matches(navigationTarget, new(12, "svchost.exe", null, null)), "RAM navigation rejects reused or unidentified host");
Check(!ServiceNavigation.Matches(navigationTarget with { Grouped = false }, new(112, "svchost", navigationTime, navigationTime)),
    "single PID navigation cannot match a substring PID");
var navigationScope = new Dictionary<int, DateTime> { [12] = navigationTime };
Check(ServiceNavigation.Includes(12, navigationScope) && !ServiceNavigation.Includes(112, navigationScope), "SCM scope uses exact host PID");
Check(ServiceNavigation.Includes(112, null) && !ServiceNavigation.Includes(12, new Dictionary<int, DateTime>()),
    "all-services and empty scoped views remain distinct");

Check(!ServiceNavigation.Matches(navigationTarget with { FrameStartedUtc = navigationTime.AddTicks(-1) },
    new(12, "svchost", navigationTime, navigationTime)), "RAM frame cannot acquire a process born after its acquisition began");
Check(!ServiceNavigation.Matches(navigationTarget with { Grouped = false }, new(12, "other.exe", navigationTime, navigationTime)),
    "same PID with another executable cannot acquire RAM row navigation");

// R11: current and legacy log syntax share a lossless parser; timestamps are real inputs.
foreach (string cultureName in new[] { "it-IT", "en-US" })
{
    var previousCulture = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        string number = 26d.ToString("F1", CultureInfo.CurrentCulture);
        string message = "Working Set privato: " + number + " MiB -> delta -2,0 MiB";
        string line = AuditLogPresentation.FormatRamEntry(new DateTime(2026, 10, 20, 13, 14, 15, 123), "PSAPI", message);
        Check(line == "[13:14:15.123] [RAM] PSAPI: " + message, "R11 compact log grammar and invariant timestamp [" + cultureName + "]");
        var current = AuditLogPresentation.Parse(line + "\r\ncontinuazione 🧪");
        Check(current.Entries.Count == 1 && current.Entries[0].Time == "13:14:15.123" &&
            current.Entries[0].Stage == "PSAPI" && current.Entries[0].Component == "RAM" &&
            current.Entries[0].Message == message + "\ncontinuazione 🧪" &&
            current.Entries[0].Source == line + "\ncontinuazione 🧪",
            "R11 log parser retains numbers units delta continuation and exact source [" + cultureName + "]");
        Check(AuditLogPresentation.Overview(current) == "1 voce · ultima: PSAPI · 13:14:15.123",
            "R11 overview counts one event rather than its continuation lines [" + cultureName + "]");
        var legacy = AuditLogPresentation.Parse("[13:14:15.123] [VERIFICA RAM] PSAPI — " + message);
        Check(legacy.Entries.Single().Message == message && legacy.Entries.Single().Component == "RAM",
            "R11 legacy RAM logs remain readable [" + cultureName + "]");
    }
    finally { CultureInfo.CurrentCulture = previousCulture; }
}
