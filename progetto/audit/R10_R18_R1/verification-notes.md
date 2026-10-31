# Qualification

This fix changes the CopyUiTests harness, candidate identity and source gate. The complete production C# and XAML content equals the R18 baseline. Every original Check assertion is retained, and the source guard verifies all unrelated fixture code byte-for-byte after stripping only the reviewed helpers, new queued regression, using and initialization comment.

All six actual C# assemblies were type checked with WindowsDesktop references and explicit generated-field/InitializeComponent stubs. This is not XAML/BAML compilation, resource validation, rendering or WPF test execution. The helper runs no UI collection or real clipboard operation, shows no window, and does not inject a template or height.

The 332 portable assertions were not repeated: their R18 execution logs and source hashes were retained and each input hash checked against the current tree. The source/XML/mutation guards were rerun. Current Windows execution remains NOT EXECUTED; the user log records only R17 and preserves its failure.

The Microsoft WPF 8 source supports deferred Background formatting as a plausible mechanism. The exact user environment was not instrumented or reproduced here. Acceptance requires the real Windows pipeline and successful corrected WPF assertions. No native capture/driver compatibility or RAM saving is qualified by this change.
