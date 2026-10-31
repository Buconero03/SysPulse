using ProcessLens.Core;

static void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL refresh policy: " + name);
    Console.WriteLine("PASS refresh policy: " + name);
}

Check(MonitorRefreshPolicy.NormalProcessInterval >= TimeSpan.FromSeconds(15), "normal scans not aggressive");
Check(MonitorRefreshPolicy.ServiceCatalogInterval >= TimeSpan.FromMinutes(2), "service catalog rate limit");
Check(MonitorRefreshPolicy.ServiceAssociationInterval >= TimeSpan.FromMinutes(1), "SCM mapping rate limit");
Check(MonitorRefreshPolicy.ForProcesses(4, 22) == MonitorRefreshPolicy.NormalProcessInterval,
    "normal resources: normal interval");
Check(MonitorRefreshPolicy.ForProcesses(72, 40) == MonitorRefreshPolicy.HighLoadProcessInterval,
    "high CPU: slow down GUI");
Check(MonitorRefreshPolicy.ForProcesses(20, 90) == MonitorRefreshPolicy.HighLoadProcessInterval,
    "high RAM: slow down GUI");
Check(MonitorRefreshPolicy.ForProcesses(null, null) == MonitorRefreshPolicy.NormalProcessInterval,
    "missing counters are not invented");
Check(MonitorRefreshPolicy.ForProcesses(double.NaN, double.NaN) == MonitorRefreshPolicy.NormalProcessInterval,
    "invalid counters ignored");
Console.WriteLine("Refresh policy checks passed.");

Check(MonitorRefreshPolicy.LiveMetricsInterval == TimeSpan.FromSeconds(1), "R24 live counters have their own one-second interval");
