using ProcessLens.Core;

static void Check(string name, double actual, double expected)
{
    if (Math.Abs(actual - expected) > 0.00001)
        throw new Exception($"{name}: actual={actual} expected={expected}");
    Console.WriteLine($"PASS {name}: {actual:F2}%");
}

// 12 logical CPUs over 1 second: 120m units of 100ns total CPU time.
// One full logical CPU for that second represents 8.3333% machine-wide.
Check("single-core-of-twelve", CpuMath.ProcessPercentOfMachine(10_000_000, 120_000_000), 100d / 12);
Check("two-cores-of-twelve", CpuMath.ProcessPercentOfMachine(20_000_000, 120_000_000), 200d / 12);
Check("system-57-percent", CpuMath.MachineBusyPercent(120_000_000, 51_600_000), 57);
Check("system-idle", CpuMath.MachineBusyPercent(120_000_000, 120_000_000), 0);
Check("counter-unavailable", CpuMath.ProcessPercentOfMachine(-1, 120_000_000), 0);
Check("clamp-counter", CpuMath.ProcessPercentOfMachine(130_000_000, 120_000_000), 100);
Console.WriteLine("CPU math tests completed.");

static void Require(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL R10 R7 CPU: " + label);
    Console.WriteLine("PASS R10 R7 CPU: " + label);
}
var epoch = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
var tracker = new CpuIntervalTracker();
Require(!tracker.Sample(23, epoch, 0, 100, 12).Available, "first counter is a baseline, not zero CPU");
var one = tracker.Sample(23, epoch, 20_000_000, 102, 12);
Check("R7 one busy core of twelve over two seconds", one.Percent, 100d / 12);
Require(one.Available && one.IntervalSeconds == 2, "actual per-PID interval retained");
var zero = tracker.Sample(23, epoch, 20_000_000, 104, 12);
Require(zero.Available && zero.Percent == 0, "two unchanged valid counters establish actual zero");
Check("R7 duration weighted mean", zero.Average, 100d / 24);
Require(!tracker.Sample(23, epoch.AddMinutes(1), 20_000_001, 106, 12).Available, "PID reuse with nondecreasing CPU still resets baseline");
Require(!tracker.Sample(23, epoch.AddMinutes(1), 20_000_002, 120, 12).Available, "pause beyond six seconds does not become a fresh sample");
Require(!tracker.Sample(23, epoch.AddMinutes(1), 20_000_003, 120, 12).Available, "zero elapsed interval is not zero CPU");
Require(!tracker.Sample(23, epoch.AddMinutes(1), 20_000_004, 119, 12).Available, "backwards monotonic input resets baseline");
Require(!tracker.Sample(23, epoch.AddMinutes(1), 1, 121, 12).Available, "counter rollback resets baseline");
Require(!tracker.Sample(23, epoch.AddMinutes(1), 2, 123, 24).Available, "processor-count changes reset normalization");
tracker.Reset();
Require(!tracker.Sample(23, epoch, 20_000_000, 125, 12).Available, "access recovery requires a new baseline");
Require(!tracker.Sample(23, epoch, 20_000_000, 127, 0).Available, "unknown logical count cannot be presented as zero CPU");
Require(!tracker.Sample(23, epoch, 20_000_000, double.NaN, 12).Available, "invalid clock does not produce NaN output");

// A high 0..4 interval overlaps the final 2 seconds of the 2..32 window.
// Subsequent 4..32 intervals are idle: weighted mean is 2/30 * 100 = 6.6667%.
tracker.Reset(); tracker.Sample(23, epoch, 0, 0, 1);
tracker.Sample(23, epoch, 40_000_000, 4, 1);
LiveCpuSample clipped = one;
for (int seconds = 6; seconds <= 32; seconds += 2)
    clipped = tracker.Sample(23, epoch, 40_000_000, seconds, 1);
Check("R7 partially overlapping window interval clipped", clipped.Average, 100d * 2 / 30);
Check("R7 overlapping peak retained", clipped.Peak, 100);
clipped = tracker.Sample(23, epoch, 40_000_000, 34, 1);
Check("R7 expired peak removed", clipped.Peak, 0);

var input = new ProcessSnapshot { Pid = 23, ParentPid = 0, Name = "test.exe", StartTime = epoch,
    CpuSampleAvailable = true, CpuPercent = 90, WorkingSetAvailable = true, WorkingSetBytes = 4096 };
Require(one.Matches(input) && !one.Matches(input with { StartTime = null }) &&
    !one.Matches(input with { StartTime = epoch.AddTicks(1) }), "publication requires exact known creation time");
var applied = one.Apply(input);
Require(applied.CpuPercent == one.Percent && applied.WorkingSetBytes == input.WorkingSetBytes &&
    input.CpuPercent == 90, "CPU overlay preserves memory and original snapshot");
Require(!LiveCpuSample.Unavailable(input).CpuSampleAvailable &&
    !LiveCpuSample.Unavailable(input).CpuCumulativeAvailable, "invalidated overlay cannot retain an old counter");

Require(!one.Apply(input with { StartTime = epoch.AddTicks(1) }).CpuSampleAvailable,
    "direct overlay API refuses a different process creation timestamp");
