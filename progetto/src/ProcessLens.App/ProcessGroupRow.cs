using System.ComponentModel;
using System.Windows.Media;
using ProcessLens.Core;

namespace ProcessLens.App;

public sealed class ProcessGroupRow(string key, string name, IReadOnlyList<ProcessRow> members, int totalMembers) : INotifyPropertyChanged
{
    public string Key { get; } = key;
    public string Title => _totalMembers == Members.Count ? $"{_name} ({Members.Count})" : $"{_name} ({Members.Count}/{_totalMembers})";
    public IReadOnlyList<ProcessRow> Members { get; private set; } = members;
    private string _name = name;
    private int _totalMembers = totalMembers;
    internal void UpdateMembers(string title, IReadOnlyList<ProcessRow> rows, int total)
    {
        _name = title; _totalMembers = total; Members = rows;
        Refresh();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconSource)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconDescription)));
    }
    public ImageSource? IconSource => ProcessIconResolver.GetGroupIcon(_name, Members.FirstOrDefault());
    public string IconDescription => ProcessIconResolver.GetGroupIconDescription(_name, Members.FirstOrDefault());
    private bool _expanded;
    public bool IsExpanded
    {
        get => _expanded;
        set { if (_expanded == value) return; _expanded = value; PropertyChanged?.Invoke(this, new(nameof(IsExpanded))); }
    }
    private ProcessGroupUsage _usage = ProcessGroupUsage.From(members.Select(x => x.Process));
    public double CpuPercent => _usage.CpuPercent;
    public string CpuDisplay => _usage.CpuKnown == 0 ? "N/D" : ProcessDisplayFormatter.Percent(CpuPercent) +
        (_usage.CpuKnown == Members.Count ? "" : $" ({_usage.CpuKnown}/{Members.Count})");
    public double MemoryDisplayMb => _usage.MemoryMb;
    public string MemoryDisplayText => _usage.MemoryKnown == 0 ? "N/D" : $"{MemoryDisplayMb:F1} MiB";
    public string MemoryLabel => (_usage.PrivateResident ? "WS PRIVATO" : "WS TOTALE") +
        (_usage.MemoryKnown == Members.Count ? "" : $" ({_usage.MemoryKnown}/{Members.Count})");
    public double NetworkBytesPerSecond => _usage.NetworkBytesPerSecond;
    public double NetworkSortScore => _usage.NetworkBytesPerSecond +
        (_usage.NetworkPublicCount * 100d) +
        (_usage.NetworkEstablishedCount * 10d) +
        (_usage.NetworkListeningCount * 2d) +
        _usage.NetworkUdpCount;
    public string NetworkDisplayText => !_usage.NetworkAvailable
        ? "N/D"
        : _usage.NetworkBytesPerSecond >= 1
            ? LiveNetworkSample.FormatRate(_usage.NetworkBytesPerSecond)
            : _usage.NetworkEstablishedCount > 0
                ? $"{_usage.NetworkEstablishedCount} TCP"
                : _usage.NetworkListeningCount > 0
                    ? $"{_usage.NetworkListeningCount} LISTEN"
                    : _usage.NetworkUdpCount > 0
                        ? $"{_usage.NetworkUdpCount} UDP"
                        : "0 KB/s";
    public string NetworkLabel => !_usage.NetworkAvailable
        ? "RETE N/D"
        : _usage.NetworkBytesPerSecond >= 1 && _usage.NetworkEstablishedCount > 0
            ? $"{_usage.NetworkEstablishedCount} CONN."
            : _usage.NetworkPublicCount > 0
                ? $"{_usage.NetworkPublicCount} PUBBL."
                : (_usage.NetworkEstablishedCount + _usage.NetworkListeningCount + _usage.NetworkUdpCount) > 0
                    ? $"{_usage.NetworkEstablishedCount + _usage.NetworkListeningCount + _usage.NetworkUdpCount} SOCKET"
                    : "RETE";
    public string NetworkDescription => !_usage.NetworkAvailable
        ? "Rete di gruppo non disponibile."
        : $"Rete totale del gruppo ({Members.Count} processi): {LiveNetworkSample.FormatRate(_usage.NetworkBytesPerSecond)} · " +
          $"TCP stabilite: {_usage.NetworkEstablishedCount} (pubbliche: {_usage.NetworkPublicCount}) · " +
          $"TCP in ascolto: {_usage.NetworkListeningCount} · Endpoint UDP: {_usage.NetworkUdpCount}.";
    public string Description => "Gruppo per percorso eseguibile e catena di avvio con orari compatibili. " +
        "Somme dei soli PID mostrati; contatori mancanti esclusi e copertura indicata. CPU: intervalli propri di ciascun PID. " +
        "Working Set totale: le pagine condivise possono essere contate più volte. " +
        "Non include automaticamente altri eseguibili o processi di cui manca l'identità.";
    public event PropertyChangedEventHandler? PropertyChanged;
    private static readonly PropertyChangedEventArgs[] CounterChanges = new[] { nameof(CpuPercent), nameof(CpuDisplay),
        nameof(MemoryDisplayMb), nameof(MemoryDisplayText), nameof(MemoryLabel),
        nameof(NetworkBytesPerSecond), nameof(NetworkDisplayText), nameof(NetworkLabel), nameof(NetworkDescription), nameof(NetworkSortScore) }
        .Select(x => new PropertyChangedEventArgs(x)).ToArray();
    public void Refresh()
    {
        _usage = ProcessGroupUsage.From(Members.Select(x => x.Process));
        foreach (var change in CounterChanges) PropertyChanged?.Invoke(this, change);
    }
}
