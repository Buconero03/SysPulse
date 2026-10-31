# Verification scope

The current portable evidence was compiled directly using SDK 8.0.426 Roslyn, .NET 8.0.32, on Linux x64. No normal MSBuild success is claimed: this host's ProcessName/StartTime process lookup prevents the normal CLI/MSBuild launch. No OS identity workaround was used.

`run_native_capture_portable_r17.py` compiles the actual Core sources and each test with Windows collector source linked as in its csproj. Windows acquisition is injected or skipped. It records input hashes and exact build/run logs.

`typecheck_wpf_sources_r17.py` compiles actual Windows/Semantics/App C# and NetworkUiTests/CopyUiTests/SystemProcessUiTests against Microsoft.WindowsDesktop.App.Ref 8.0.32. The script explicitly supplies generated field/InitializeComponent stubs outside the candidate source tree. No stub is shipped as app source. This catches ordinary C# type and argument errors, including calls like the previous ClipboardText overload failure, but DOES NOT compile XAML into BAML, load actual markup, execute tests, verify resources/event routing or measure the GUI. Every result is labelled TYPE CHECK ONLY.

For reproducing that extra check on a non-Windows host, prepare the same --work directory with `wpf-ref/*.dll` extracted from `ref/net8.0/` of the Microsoft.WindowsDesktop.App.Ref 8.0.32 NuGet package and `Microsoft.Data.Sqlite.dll` from `lib/net8.0/` of Microsoft.Data.Sqlite.Core 8.0.20. Also run the portable runner with `--work <same-work>/runtime`. The SDK's ASP.NET 8.0.32 EventLog reference and .NET regex source generator are used. Reference assembly hashes are recorded in wpf-typecheck-evidence.json. On Windows, the required acceptance check remains build-dashboard.ps1, which uses the real WPF build.

The 149 historical source/XML checks were re-run against the current tree, with exact R17 authorized-successor hashes where needed. The 39 R17 checks include mutation probes for limits, pointer ranges, callback roots, resource release, losses, BOOLEAN layout and stale UI results. They are source checks, not Windows runtime checks. No previous UI assertion was removed except replacement of its expected legacy capture view/type/name by the new native capture view/type/name; R17 checks enforce that exact exception.

The full patch excludes only this current audit directory and the self-referential SHA manifest. All other changed files are listed and replayed. Historical audits remain historical; their claims of unexecuted Windows behavior are preserved.
