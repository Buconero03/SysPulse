namespace ProcessLens.Core;

/// <summary>One Service Control Manager event, without guessed attribution.</summary>
public sealed record ServiceHistoryEvent(
    long? RecordId,
    int EventId,
    DateTime TimestampLocal,
    string ServiceName,
    string Title,
    string Detail,
    string Evidence)
{
    public string WhenLabel => TimestampLocal.ToString("dd/MM/yyyy HH:mm:ss");
    public string EventIdLabel => $"SCM {EventId}";
}

public sealed record ServiceHistoryResult(
    IReadOnlyList<ServiceHistoryEvent> Events,
    string SourceStatus,
    bool Available);
