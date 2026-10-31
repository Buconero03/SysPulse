using ProcessLens.Guardian.Core;

static void Check(bool valid,string name)
{ if(!valid) throw new Exception("FAIL: "+name); Console.WriteLine("PASS "+name); }
var s = new GuardianSettings { SampleSeconds=1,PortScanSeconds=999,RetentionDays=-3,MaxDatabaseMb=1};s.Normalize();
Check(s.SampleSeconds==10 && s.PortScanSeconds==300 && s.RetentionDays==1 && s.MaxDatabaseMb==64,"settings clamped");
Check(Math.Abs(GuardianMath.ProcessCpuPercent(TimeSpan.FromSeconds(2),TimeSpan.FromSeconds(1),1,12)!.Value-100d/12)<.01,"single core of 12");
Check(GuardianMath.ProcessCpuPercent(TimeSpan.Zero,TimeSpan.FromSeconds(1),1,12) is null,"counter decrease not zero");
var t = new DateTime(2026,10,9,12,3,21,DateTimeKind.Utc);
Check(GuardianMath.FiveMinuteBucket(t) == new DateTimeOffset(new DateTime(2026,10,9,12,0,0,DateTimeKind.Utc)).ToUnixTimeSeconds(),"five minute bucket");
var rec=new ProcessEventReconciler();
var a=new ProcessObserved(42,100,1,"test",0,1024,2048);
Check(rec.Reconcile(new[]{a},t).Single().Kind==GuardianKind.ProcessObservedAtStartup,"startup inventory not falsely classified as launch");
Check(rec.Reconcile(new[]{a},t).Count==0,"no duplicate poll event");
var b=a with {StartTicks=200};
var events=rec.Reconcile(new[]{b},t);
Check(events.Count==2 && events[0].Kind==GuardianKind.ProcessExited && events[1].Kind==GuardianKind.ProcessStarted,"PID reuse creates end and start");
Check(rec.FromEtw(true,42,1,"test",t) is null,"ETW duplicate suppressed");
Check(rec.FromEtw(false,42,1,"test",t)?.Kind==GuardianKind.ProcessExited,"ETW exit");
var etw=rec.FromEtw(true,43,1,"short",t);
Check(etw?.Kind==GuardianKind.ProcessStarted,"ETW observes short process");
Check(rec.Reconcile(new[]{new ProcessObserved(43,333,1,"short",0,0,0)},t).Count==0,"ETW start upgraded without duplicate");
var detector=new PortChangeDetector();
var local=new PortIdentity(42,"TCP4","127.0.0.1",7777,"",0,"LISTEN");
var net=new PortIdentity(42,"TCP4","0.0.0.0",8080,"",0,"LISTEN");
Check(detector.Reconcile(new[]{local},t).Single().Kind==GuardianKind.PortObservedAtStartup,"socket inventory not false open event");
Check(detector.Reconcile(new[]{local,net},t).Single().Kind==GuardianKind.PortOpened,"new sampled port");
Check(detector.Reconcile(new[]{net},t).Single().Kind==GuardianKind.PortClosed,"missing sampled port");
Check(GuardianMath.ExplanationForPort(net).Contains("non e' di per se' una minaccia"),"no fabricated malware conclusion");
Check(GuardianMath.ExplanationForPort(local).Contains("loopback"),"loopback distinction");
Console.WriteLine("Guardian core tests passed.");
