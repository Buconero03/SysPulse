# Verification scope

Direct Roslyn compilation on Linux using .NET SDK 8.0.426 / runtime 8.0.32. The portable runner compiles actual Core and linked Windows source with synthetic interfaces and owned fake sockets. It does not enumerate the live host network or change system configuration. Actual test logs and input hashes are retained.

Normal CLI/MSBuild is unavailable on this host because its process identity lookup fails. No PID/OS identity workaround was used. No normal Windows build success is claimed.

The additional C# type check uses Microsoft.WindowsDesktop.App.Ref 8.0.32 and explicit generated-field/InitializeComponent stubs outside the source tree, as in the R17 methodology. Those stubs are not production code. Actual Windows, Semantics, App C# and the three UI test projects were type checked with zero compiler warnings. This does not validate BAML/resources/event routing, render the UI or execute WPF tests. The current type-check tool, source hashes and reference hashes are included. The real acceptance pipeline is build-dashboard.ps1 on Windows.

Historical guard acceptance was extended only to exact hashes listed in the R18 authorized-source ledger. Their prior runtime logs remain historical. Current portable and type-check evidence validates all current input hashes separately. The new raw collector requires an independent fixture suite and explicit source checks for owned lifecycle, source identity, no transmission, no promiscuous mode, bounds and unavailable counters.

Patch replay excludes only current R18 audit metadata and the self-referential manifest. Everything else changed since R17 is included and replayed with byte comparison. Source tree remains singular; build outputs and reference packages stay outside the release.
