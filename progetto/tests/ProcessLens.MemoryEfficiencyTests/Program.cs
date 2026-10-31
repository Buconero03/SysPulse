using ProcessLens.App;
using ProcessLens.Core;

static void Check(bool passed, string name)
{
    if (!passed) throw new Exception("FAIL R8 " + name);
    Console.WriteLine("PASS R8 " + name);
}

var cache = new BoundedCache<string, object>(2, StringComparer.OrdinalIgnoreCase);
var first = new object(); var second = new object();
cache["first"] = first; cache["second"] = second;
Check(cache.TryGetValue("FIRST", out var same) && ReferenceEquals(same, first), "cache comparer and value identity");
cache["third"] = new object();
Check(cache.Count == 2 && !cache.TryGetValue("second", out _), "least recently used entry evicted");
cache["first"] = second;
Check(cache.Count == 2 && cache.TryGetValue("first", out same) && ReferenceEquals(same, second), "update replaces value without growing cache");
Check(cache.Remove("FIRST") && !cache.Remove("missing") && cache.Count == 1, "removal unlinks cache entry");
for (int n = 0; n < 10000; n++) cache[n.ToString()] = new object();
Check(cache.Count == 2 && cache.Keys.Length == 2, "large churn cannot grow cache past capacity");
var concurrent = new BoundedCache<int, object>(8);
Parallel.For(0, 4000, n => { concurrent[n] = new object(); concurrent.TryGetValue(n, out _); });
Check(concurrent.Count <= 8, "concurrent icon-style access remains bounded");

var start = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
var buffer = new MemoryObservationBuffer();
for (int n = 0; n < 200; n++) buffer.Add(start.AddSeconds(n), n);
var exported = buffer.ToArray();
Check(buffer.Count == 160 && exported[0].Bytes == 40 && exported[^1].Bytes == 199, "sample count retention matches R7");
buffer.Add(start.AddSeconds(499), 499);
exported = buffer.ToArray();
Check(exported.Length == 2 && exported[0].Bytes == 199 && exported[1].Bytes == 499, "five minute retention includes exact cutoff");
exported[0] = new MemoryObservation(start, -1);
Check(buffer.ToArray()[0].Bytes == 199, "export does not mutate retained samples");
var baseline = new List<MemoryObservation>();
var optimized = new MemoryObservationBuffer();
for (int n = 0; n < 220; n++)
{
    var timestamp = start.AddSeconds(n * 7);
    baseline.Add(new MemoryObservation(timestamp, n * 1048576L));
    baseline.RemoveAll(o => o.TimestampUtc < timestamp.AddMinutes(-5));
    if (baseline.Count > 160) baseline.RemoveRange(0, baseline.Count - 160);
    optimized.Add(timestamp, n * 1048576L);
}
Check(baseline.SequenceEqual(optimized.ToArray()), "all exported timestamp and byte values match original retention");
Check(MemoryTrendAnalyzer.Analyze("Working Set privato", baseline) ==
      MemoryTrendAnalyzer.Analyze("Working Set privato", optimized.ToArray()), "trend output is unchanged");

// Synthetic allocation evidence, never a measurement of WPF/physical RAM or the user's target process.
static void OriginalPolling()
{
    var histories = new List<Queue<MemoryObservation>>();
    var time = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
    for (int p = 0; p < 1000; p++)
    {
        var history = new Queue<MemoryObservation>();
        for (int n = 0; n < 20; n++) history.Enqueue(new MemoryObservation(time.AddSeconds(n * 15), n));
        histories.Add(history);
    }
    GC.KeepAlive(histories);
}
static void OptimizedPolling()
{
    var histories = new List<MemoryObservationBuffer>();
    var time = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
    for (int p = 0; p < 1000; p++)
    {
        var history = new MemoryObservationBuffer();
        for (int n = 0; n < 20; n++) history.Add(time.AddSeconds(n * 15), n);
        histories.Add(history);
    }
    GC.KeepAlive(histories);
}
static long Measure(Action operation)
{
    long before = GC.GetAllocatedBytesForCurrentThread();
    operation();
    return GC.GetAllocatedBytesForCurrentThread() - before;
}
OriginalPolling(); OptimizedPolling(); // JIT warmup, no forced collection or working-set manipulation.
long originalBytes = Measure(OriginalPolling);
long optimizedBytes = Measure(OptimizedPolling);
Console.WriteLine($"SYNTHETIC_ALLOCATIONS original_bytes={originalBytes} optimized_bytes={optimizedBytes} processes=1000 samples=20");
if (IntPtr.Size == 8)
    Check(optimizedBytes < originalBytes, "synthetic x64 global polling allocates fewer managed bytes (not a RAM claim)");
else
    Console.WriteLine("INFO R8 allocation comparison recorded; relative allocation assertion is scoped to Windows x64 deployment.");
Console.WriteLine("R8 memory efficiency tests passed; Windows GUI RAM must be measured separately.");

// Independent bound on the collector's new cache. No claim about physical RAM saved.
var metadata = new BoundedMetadataCache<object>(512);
var marker = new object();
metadata["C:\\Tool.exe"] = marker;
Check(metadata.TryGetValue("c:\\TOOL.EXE", out var hit) && ReferenceEquals(hit, marker),
    "R7 native metadata cache preserves case-insensitive path and object identity");
for (int n = 0; n < 10000; n++) metadata[$"C:\\Tool-{n}.exe"] = new object();
Check(metadata.Count == 512, "R7 metadata path churn has a hard 512-entry bound");
metadata["C:\\Tool-9999.exe"] = marker;
Check(metadata.Count == 512 && metadata.TryGetValue("C:\\Tool-9999.exe", out hit) && ReferenceEquals(hit, marker),
    "R7 metadata update does not evict or grow the cache");
metadata["C:\\Tool.exe"] = marker;
Check(metadata.Count == 512 && metadata.TryGetValue("C:\\Tool.exe", out hit),
    "R7 evicted file metadata can be loaded again");
bool rejected = false;
try { _ = new BoundedMetadataCache<object>(0); } catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected, "R7 zero-capacity cache rejected");
