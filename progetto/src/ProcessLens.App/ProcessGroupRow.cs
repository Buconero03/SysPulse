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
    public string CpuDisplay => _usage.CpuKnown == 0
        ? (SysPulseLocalization.IsEnglish ? "N/A" : "N/D")
        : ProcessDisplayFormatter.Percent(CpuPercent) +
            (_usage.CpuKnown == Members.Count ? "" : $" ({_usage.CpuKnown}/{Members.Count})");
    public double MemoryDisplayMb => _usage.MemoryMb;
    public string MemoryDisplayText => _usage.MemoryKnown == 0
        ? (SysPulseLocalization.IsEnglish ? "N/A" : "N/D")
        : $"{MemoryDisplayMb:F1} MiB";
    public string MemoryLabel =>
        (SysPulseLocalization.IsEnglish
            ? (_usage.PrivateResident ? "PRIVATE WS" : "TOTAL WS")
            : (_usage.PrivateResident ? "WS PRIVATO" : "WS TOTALE")) +
        (_usage.MemoryKnown == Members.Count ? "" : $" ({_usage.MemoryKnown}/{Members.Count})");
    public double NetworkBytesPerSecond => _usage.NetworkBytesPerSecond;
    public double NetworkSortScore => _usage.NetworkBytesPerSecond +
        (_usage.NetworkPublicCount * 100d) +
        (_usage.NetworkEstablishedCount * 10d) +
        (_usage.NetworkListeningCount * 2d) +
        _usage.NetworkUdpCount;
    public string NetworkDisplayText => !_usage.NetworkAvailable
        ? (SysPulseLocalization.IsEnglish ? "N/A" : "N/D")
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
        ? (SysPulseLocalization.IsEnglish ? "NET N/A" : "RETE N/D")
        : _usage.NetworkBytesPerSecond >= 1 && _usage.NetworkEstablishedCount > 0
            ? $"{_usage.NetworkEstablishedCount} CONN."
            : _usage.NetworkPublicCount > 0
                ? (SysPulseLocalization.IsEnglish ? $"{_usage.NetworkPublicCount} PUBLIC" : $"{_usage.NetworkPublicCount} PUBBL.")
                : (_usage.NetworkEstablishedCount + _usage.NetworkListeningCount + _usage.NetworkUdpCount) > 0
                    ? (SysPulseLocalization.IsEnglish
                        ? $"{_usage.NetworkEstablishedCount + _usage.NetworkListeningCount + _usage.NetworkUdpCount} SOCKETS"
                        : $"{_usage.NetworkEstablishedCount + _usage.NetworkListeningCount + _usage.NetworkUdpCount} SOCKET")
                    : (SysPulseLocalization.IsEnglish ? "NET" : "RETE");
    public string NetworkDescription => !_usage.NetworkAvailable
        ? (SysPulseLocalization.IsEnglish ? "Group network unavailable." : "Rete di gruppo non disponibile.")
        : (SysPulseLocalization.IsEnglish
            ? $"Total group network ({Members.Count} processes): {LiveNetworkSample.FormatRate(_usage.NetworkBytesPerSecond)} · Established TCP: {_usage.NetworkEstablishedCount} (public: {_usage.NetworkPublicCount}) · Listening TCP: {_usage.NetworkListeningCount} · UDP endpoints: {_usage.NetworkUdpCount}."
            : $"Rete totale del gruppo ({Members.Count} processi): {LiveNetworkSample.FormatRate(_usage.NetworkBytesPerSecond)} · TCP stabilite: {_usage.NetworkEstablishedCount} (pubbliche: {_usage.NetworkPublicCount}) · TCP in ascolto: {_usage.NetworkListeningCount} · Endpoint UDP: {_usage.NetworkUdpCount}.");
    public string Description => SysPulseLocalization.IsEnglish
        ? "Grouped by executable path and timestamp-verified startup chain. Sums only displayed PIDs; missing counters are excluded with coverage shown. Total Working Set may count shared pages more than once."
        : "Gruppo per percorso eseguibile e catena di avvio con orari compatibili. " +
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
