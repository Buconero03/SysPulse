using System.Text.RegularExpressions;

namespace ProcessLens.App;

// View projection only. The SQLite reader, stored record and raw text stay unchanged.
internal sealed record GuardianDiagnosticPresentation(
    string LocalTime, string Source, string Message, string DetailText,
    string Limitations, string OriginalSource, string OriginalMessage)
{
    private static readonly Regex Health = new(
        @"\Aaccepted=(?<accepted>[0-9]+) saved=(?<saved>[0-9]+) working_set_mib=(?<ram>[0-9]+(?:[.,][0-9]+)?)\z",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static GuardianDiagnosticPresentation From(GuardianDiagnosticView row)
    {
        Match? values = row.Source == "health" && row.Message.Length <= 256 ? Health.Match(row.Message) : null;
        if (values is not { Success: true })
            return new(row.LocalTime, row.Source, row.Message,
                $"Ora locale: {row.LocalTime}\nOrigine: {row.Source}\n\n{row.Message}",
                "Voce conservata senza interpretare campi assenti, sconosciuti o con formato inatteso.",
                row.Source, row.Message);

        // Keep decimal text from the producer's culture. No numeric reparse, inferred
        // queue depth, loss count, percentage, or success judgement is introduced.
        string accepted = values.Groups["accepted"].Value;
        string saved = values.Groups["saved"].Value;
        string ram = values.Groups["ram"].Value;
        return new(row.LocalTime, "Contatori Guardian",
            $"Accodati: {accepted} · scritti: {saved} · Working Set totale: {ram} MiB",
            $"Ora locale registrata: {row.LocalTime}\n\n" +
            $"EVENTI ACCETTATI DALLA CODA\n{accepted}\n\n" +
            $"EVENTI SCRITTI SU SQLITE\n{saved}\n\n" +
            $"WORKING SET TOTALE DEL SERVIZIO\n{ram} MiB",
            "Contatori cumulativi del servizio, non limitati al periodo selezionato. " +
            "Letture separate: la differenza non misura coda o perdite. " +
            "Conteggi uguali: non prova la completezza ETW. " +
            "Working Set totale: include pagine condivise; non è RAM privata o commit. " +
            "Totale suite: non misurato.",
            row.Source, row.Message);
    }
}
