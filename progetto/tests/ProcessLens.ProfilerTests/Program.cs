using ProcessLens.Core;
static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS " + name); }
var example = """
  4,786,378  GC Heap bytes
     63,201  GC Heap objects
Object Bytes     Count  Type
    131,096         1  System.Byte[] (Bytes > 100K)  [System.Private.CoreLib.dll]
     57,756         1  System.String (Bytes > 10K)  [System.Private.CoreLib.dll]
     22,432         9  System.Object[] (Bytes > 10K)  [System.Private.CoreLib.dll]
""";
var parsed = GcDumpHeapStatParser.Parse(example);
Check(parsed.HeapBytes == 4786378 && parsed.HeapObjects == 63201, "heap totals parsed");
Check(parsed.TopTypes.Count == 3 && parsed.TopTypes[0].TypeName.StartsWith("System.Byte[]"), "heap type histogram");
Check(parsed.TopTypes[0].Bytes == 131096 && parsed.TopTypes[1].Count == 1, "heap bytes/count grounded");
// Microsoft documentation for dotnet-gcdump 10 shows a changed heading and
// omits the separate "GC Heap bytes/objects" summary. Those totals must be
// derived from ALL type rows (not from the top-20 display selection).
var current = """
          Size (Bytes) Count       Type
          ============== =====       ====
            1,603,588,000  22,000,000  System.String
              201,096,000   2,010,000  System.Byte[]
              100,000,000   1,000,000  System.Char[]
""";
var modern = GcDumpHeapStatParser.Parse(current, limit: 1);
Check(modern.HeapBytes == 1904684000L && modern.HeapObjects == 25010000L,
    "gcdump 10 documented header and inferred full heap totals");
Check(modern.TopTypes.Count == 1 && modern.TopTypes[0].TypeName == "System.String",
    "top-N report does not truncate inferred totals");
Check(GcDumpHeapStatParser.Parse("       Size (Bytes) Count Type\n       ============ ===== ====\n").TopTypes.Count == 0,
    "empty current-format report does not fabricate types");
Check(GcDumpHeapStatParser.Parse("nonsense").TopTypes.Count == 0, "malformed output not fabricated");
Check(!ProfilerPolicy.SafeToCollect(85) && ProfilerPolicy.SafeToCollect(84.9), "85 percent RAM gate");
Check(ProfilerPolicy.MaxSeconds <= 60 && ProfilerPolicy.MaxGcDumpFileMiB <= 128, "bounded acquisition defaults");
Console.WriteLine("ProcessLens Profiler core tests passed.");
