using System;
using System.Collections.Generic;
using System.Threading;

namespace ProcessLens.ProfilerFixture;

// Synthetic objects only: no user files, connections, credentials or payloads.
// An explicit validation script starts this target; no ProcessLens component starts it automatically.
public sealed class KnownPayload
{
    public long A0, A1, A2, A3, A4, A5, A6, A7;
    public long B0, B1, B2, B3, B4, B5, B6, B7;
    public string Name { get; }

    public KnownPayload(int index) => Name = $"fixture-{index:D4}";
}

internal static class Program
{
    private static List<KnownPayload>? _retained;

    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--seconds" ||
            !int.TryParse(args[1], out int seconds) || seconds < 30 || seconds > 180)
        {
            Console.Error.WriteLine("Use: ProcessLens.ProfilerFixture --seconds N (30..180)");
            return 2;
        }

        _retained = new List<KnownPayload>(4096);
        for (int index = 0; index < 4096; index++)
            _retained.Add(new KnownPayload(index));

        Console.WriteLine($"FIXTURE_READY PID={Environment.ProcessId} COUNT={_retained.Count}");
        Console.Out.Flush();

        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            Thread.Sleep(500);
            GC.KeepAlive(_retained);
        }
        Console.WriteLine("FIXTURE_FINISHED");
        return 0;
    }
}
