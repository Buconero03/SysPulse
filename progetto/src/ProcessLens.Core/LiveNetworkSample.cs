using System.Globalization;

namespace ProcessLens.Core;

/// <summary>Live per-PID socket and network throughput sample paired with process creation time.</summary>
public sealed record LiveNetworkSample(
    int Pid,
    DateTime? StartUtc,
    int TcpEstablishedCount,
    int TcpListeningCount,
    int UdpEndpointCount,
    int PublicRemoteConnectionCount,
    double BytesPerSecond,
    double SendBytesPerSecond,
    double ReceiveBytesPerSecond,
    bool BandwidthAvailable,
    bool SocketsAvailable,
    double CapturedAtMonotonicSeconds,
    DateTime CapturedUtc)
{
    public bool Matches(ProcessSnapshot p) =>
        p.Pid == Pid &&
        (!StartUtc.HasValue || !p.StartTime.HasValue || StartUtc.Value == p.StartTime.Value.ToUniversalTime());

    public bool IsFresh(double now) =>
        double.IsFinite(now) && double.IsFinite(CapturedAtMonotonicSeconds) &&
        now - CapturedAtMonotonicSeconds is >= 0 and <= CpuIntervalTracker.MaximumGapSeconds;

    public ProcessSnapshot Apply(ProcessSnapshot p)
    {
        if (!Matches(p))
            return Unavailable(p);
        if (p.NetworkSampledAtMonotonicSeconds > CapturedAtMonotonicSeconds)
            return p;

        double totalRate = BandwidthAvailable && double.IsFinite(BytesPerSecond) && BytesPerSecond >= 0 ? BytesPerSecond : 0;
        double sendRate = BandwidthAvailable && double.IsFinite(SendBytesPerSecond) && SendBytesPerSecond >= 0 ? SendBytesPerSecond : 0;
        double receiveRate = BandwidthAvailable && double.IsFinite(ReceiveBytesPerSecond) && ReceiveBytesPerSecond >= 0 ? ReceiveBytesPerSecond : 0;

        return p with
        {
            TcpEstablishedCount = SocketsAvailable ? Math.Max(0, TcpEstablishedCount) : p.TcpEstablishedCount,
            TcpListeningCount = SocketsAvailable ? Math.Max(0, TcpListeningCount) : p.TcpListeningCount,
            UdpEndpointCount = SocketsAvailable ? Math.Max(0, UdpEndpointCount) : p.UdpEndpointCount,
            PublicRemoteConnectionCount = SocketsAvailable ? Math.Max(0, PublicRemoteConnectionCount) : p.PublicRemoteConnectionCount,
            NetworkBytesPerSecond = totalRate,
            NetworkSendBytesPerSecond = sendRate,
            NetworkReceiveBytesPerSecond = receiveRate,
            NetworkBandwidthAvailable = BandwidthAvailable,
            NetworkLiveAvailable = SocketsAvailable || BandwidthAvailable,
            NetworkSampledAtMonotonicSeconds = CapturedAtMonotonicSeconds
        };
    }

    public static ProcessSnapshot Unavailable(ProcessSnapshot p) => p with
    {
        NetworkBytesPerSecond = 0,
        NetworkSendBytesPerSecond = 0,
        NetworkReceiveBytesPerSecond = 0,
        NetworkBandwidthAvailable = false,
        NetworkLiveAvailable = false,
        NetworkSampledAtMonotonicSeconds = 0
    };

    public static string FormatRate(double bytesPerSecond)
    {
        if (!double.IsFinite(bytesPerSecond) || bytesPerSecond < 0)
            return "N/D";
        const string space = "\u00a0";
        if (bytesPerSecond >= 1048576d)
            return (bytesPerSecond / 1048576d).ToString("F1", CultureInfo.CurrentCulture) + space + "MB/s";
        if (bytesPerSecond >= 1024d)
            return (bytesPerSecond / 1024d).ToString("F1", CultureInfo.CurrentCulture) + space + "KB/s";
        if (bytesPerSecond >= 64d)
            return Math.Max(0.1, bytesPerSecond / 1024d).ToString("F1", CultureInfo.CurrentCulture) + space + "KB/s";
        return 0d.ToString("F1", CultureInfo.CurrentCulture) + space + "KB/s";
    }
}
