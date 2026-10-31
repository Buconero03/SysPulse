using ProcessLens.Core;

static void Check(bool predicate, string label)
{
    if (!predicate) throw new Exception("FAIL: " + label);
    Console.WriteLine("PASS " + label);
}

var policy = new MemoryAlertPolicy(85, 80);
Check(!policy.Observe(79), "initial below threshold");
Check(!policy.Observe(84.99), "below 85 does not alert");
Check(policy.Observe(85), "alert exactly at 85");
Check(!policy.Observe(92) && !policy.Observe(85), "no repeated alert above threshold");
Check(!policy.Observe(80) && !policy.Armed, "exactly 80 does not rearm");
Check(!policy.Observe(79.99) && policy.Armed, "strictly below 80 rearms");
Check(policy.Observe(86), "subsequent crossing alerts");
Check(!policy.Observe(double.NaN) && !policy.Armed, "invalid readings cannot reset latch");
Check(new MemoryAlertPolicy().Observe(95), "already above threshold at start alerts");
try { _ = new MemoryAlertPolicy(80, 85); throw new Exception("invalid thresholds accepted"); }
catch (ArgumentOutOfRangeException) { Console.WriteLine("PASS invalid threshold configuration rejected"); }
