namespace ProcessLens.Core;

/// <summary>SCM enumeration + configuration facts. Never invent an initiating application.</summary>
public sealed record ServiceSnapshot
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required uint StateCode { get; init; }
    public required int ProcessId { get; init; }
    public required uint ServiceType { get; init; }
    public int? StartupCode { get; init; }
    public bool DelayedAutoStart { get; init; }
    public string? BinaryPath { get; init; }
    public string? AccountName { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GroupDependencies { get; init; } = Array.Empty<string>();

    public bool IsRunning => StateCode == 4;
    public string StateLabel => StateCode switch
    {
        1 => "Arrestato", 2 => "Avvio in corso", 3 => "Arresto in corso",
        4 => "In esecuzione", 5 => "Continuazione in corso",
        6 => "Pausa in corso", 7 => "In pausa", _ => "Stato non disponibile"
    };
    public string StartupLabel => StartupCode switch
    {
        0 => "Boot", 1 => "Sistema", 2 when DelayedAutoStart => "Automatico (ritardato)",
        2 => "Automatico", 3 => "Manuale / su richiesta", 4 => "Disabilitato",
        _ => "Non disponibile"
    };
    public string ProcessIdLabel => ProcessId > 0 ? ProcessId.ToString() : "—";
    public string HostType => (ServiceType & 0x20) != 0 ? "Processo condiviso" :
        (ServiceType & 0x10) != 0 ? "Processo dedicato" : "Driver o altro tipo di servizio";
}
