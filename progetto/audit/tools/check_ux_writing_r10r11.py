"""Source-only R11 checks; never reports C#/PowerShell/WPF execution."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile
from ux_writing_rules import conditions, thresholds, approved_revision
from test_clipboard_calls_r10r9 import invalid_calls
from memory_text_contract import mismatches as memory_text_mismatches
from interpolated_expectation_contract import removed_templates, mismatches as stale_expectations


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--candidate', type=Path, default=Path('.'))
    p.add_argument('--baseline-zip', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    root = args.candidate.resolve()
    assert hashlib.sha256(args.baseline_zip.read_bytes()).hexdigest() == '622664bf767505d06fab1ed76626415296d3b719803a8bbdc667e34170838eda'
    with zipfile.ZipFile(args.baseline_zip) as z:
        old = {n.split('/', 1)[1]: z.read(n) for n in z.namelist() if not n.endswith('/')}
    results = []
    def check(label, ok):
        results.append({'test': label, 'status': 'PASS' if ok else 'FAIL', 'kind': 'source/XML only'})
        print(results[-1]['status'], label)
    text = lambda rel: (root / rel).read_text(encoding='utf-8-sig')
    protected = [rel for rel in old if rel.startswith(('src/ProcessLens.Windows/', 'src/ProcessLens.Guardian.',
                 'src/ProcessLens.MemoryAgent/', 'src/ProcessLens.Profiler/')) or rel.endswith(
                 ('GuardianDashboardReader.cs', 'GuardianLineageReader.cs', 'RamMonitor/Measurement.cs', 'RamMonitor/NativeSampler.cs'))]
    check('native collectors services profiler SQLite readers and meter acquisition unchanged',
          all((root / rel).read_bytes() == old[rel] for rel in protected))
    changed = [rel for rel in old if rel.startswith('src/') and (root / rel).read_bytes() != old[rel]]
    check('every source edit belongs to the recorded editorial or authorized feature delta', all(approved_revision(root, rel) for rel in changed))
    modules = ['Semantics/RoleClassifier', 'Semantics/ProcessIntelligenceAnalyzer', 'Semantics/ProcessOriginAnalyzer',
               'Semantics/ProcessBehaviorExplainer', 'Semantics/ProcessQueryEngine', 'Core/WindowsAllocationEvidence',
               'Core/StructuredDiagnostics', 'Core/ServiceExplanation', 'Core/ServiceHistoryInterpretation']
    for name in modules:
        rel = 'src/ProcessLens.' + name + '.cs'
        check('control conditions unchanged ' + name,
              conditions(old[rel].decode('utf-8-sig')) == conditions(text(rel)))
    check('numeric comparisons including ternary thresholds unchanged', all(
          thresholds(old['src/ProcessLens.' + name + '.cs'].decode('utf-8-sig')) ==
          thresholds(text('src/ProcessLens.' + name + '.cs')) for name in modules))
    q = text('src/ProcessLens.Semantics/ProcessQueryEngine.cs')
    check('threshold mutation is rejected by the real condition comparison',
          thresholds(q) != thresholds(q.replace('used >= 85', 'used >= 90')))
    def rule_core(data):
        return [{key: value for key, value in row.items() if key not in
                 {'summary', 'meaning', 'whyRunning', 'closeImpact'}} for row in data]
    rules = 'src/ProcessLens.Semantics/knowledge/role-signatures.json'
    check('knowledge IDs scores criteria labels and risk codes unchanged',
          rule_core(json.loads(old[rules].decode('utf-8-sig'))) == rule_core(json.loads(text(rules))))
    for rel in ['src/ProcessLens.Core/MemoryEvidence.cs', 'src/ProcessLens.App/ProcessDisplayFormatter.cs']:
        check('metric types or numeric formatter already concise and unchanged ' + Path(rel).name,
              (root / rel).read_bytes() == old[rel])
    for pth in [*root.rglob('*.xaml'), *root.rglob('*.csproj')]:
        ET.parse(pth)
    check('all XAML and project XML parse', True)
    main = text('src/ProcessLens.App/MainWindow.xaml')
    terms = text('src/ProcessLens.Core/ProcessTerminology.cs')
    check('redundant badge and sort narration removed', 'Analisi basata su evidenze' not in main and
          'I processi più recenti sono mostrati per primi.' not in main and 'Text="Seleziona un processo per analizzarlo."' in main)
    check('required labels and subprocess summary present', '"Ruolo stimato"' in terms and '"Funzione sconosciuta"' in terms and
          '"WORKING SET PRIVATO"' in main and '{p.SameNameChildCount} sottoprocessi' in text('src/ProcessLens.Semantics/RoleClassifier.cs'))
    check('memory summary has separate numbers and no nested driver parentheses',
          '\\n• Pagine private in lettura/scrittura (RW):' in q and '\\n• Blocchi ≥4 MiB:' in q and
          'heap/strutture dati dinamiche RW' not in q and 'La quota principale dello spazio impegnato' not in q)
    check('trends retain duration excursions and unknown causes', 'SALDO ' not in q and 'CONSUMO IN CALO:' in q and
          'negli ultimi {trend.Seconds / 60d:F1} min' in q and 'oscillazioni intermedie' in q and
          'non basta per diagnosticare un memory leak' in q and 'non è una misura delle pagine fuori RAM' in q)
    audit = text('src/ProcessLens.App/MemoryAnalysisAudit.cs')
    parser = text('src/ProcessLens.App/AuditLogPresentation.cs')
    check('new RAM audit grammar and legacy lossless parser connected', 'FormatRamEntry(timestamp, stage, status)' in audit and
          'RAM|VERIFICA RAM' in parser and 'source.Replace("\\r\\n", "\\n"' in parser and
          'previous.Source + "\\n" + line' in parser)
    check('RAM audit numeric diagnostic event identity preserved', all(token in audit for token in
          ['EventId = DiagnosticEventIds.RamMeasurement', 'ReasonCode = "RAM_AUDIT_STEP"', 'TargetPid = _pid',
           'OperationId = _operationId', '"memory-analysis"']))
    logs = text('src/ProcessLens.RamMonitor/LogSession.cs')
    old_logs = old['src/ProcessLens.RamMonitor/LogSession.cs'].decode('utf-8')
    headers = lambda s: re.findall(r'_(?:totals|processes)\.WriteLine\("([^"\n]+)"\)', s)
    check('machine-readable monitor CSV columns unchanged', headers(logs) == headers(old_logs))
    check('monitor events have timestamp component partial values and excluded meter',
          '[RAM Monitor]' in logs and 'Line("Working Set privato totale"' in logs and 'N/D · parziale' in logs and
          'Line("Monitor escluso"' in logs and 'capture.EnumerationError' in logs)
    check('clipboard calls retain both required arguments', not any(invalid_calls(pth.read_text()) for pth in
          (root / 'src/ProcessLens.App').rglob('*.cs')))
    copy = text('tests/ProcessLens.CopyTests/Program.cs')
    behavior = text('tests/ProcessLens.BehaviorTests/Program.cs')
    check('current/legacy log and culture regressions supplied', 'FormatRamEntry(new DateTime(' in copy and
          'R11 legacy RAM logs remain readable' in copy and 'it-IT' in copy and 'en-US' in copy)
    check('nested allocation and semantic-limit regressions supplied', '638.3' in behavior and '560.8' in behavior and
          '407.5' in behavior and 'R11 no nested parentheses or inferred heap objects' in behavior and
          'non è una misura delle pagine fuori RAM' in behavior)
    check('new Windows source checks are wired into the build', 'check-ux-writing-r10r11.ps1' in text('build-dashboard.ps1'))
    snapshot = text('src/ProcessLens.Core/ProcessSnapshot.cs')
    memory_tests = text('tests/ProcessLens.MemoryTests/Program.cs')
    check('all four RAM display cases match current labels and description fragments',
          not memory_text_mismatches(snapshot, memory_tests))
    stale_tests = memory_tests.replace('26.0, "Pagine private presenti in RAM"', '26.0, "Private Working Set"')
    check('RAM description guard rejects the actual R11 stale-token regression',
          stale_tests != memory_tests and any('stale description fragment Private Working Set' in error
          for error in memory_text_mismatches(snapshot, stale_tests)))
    old_sources = {rel: data.decode('utf-8-sig') for rel, data in old.items()
                   if rel.startswith('src/') and rel.endswith('.cs')}
    templates = removed_templates(old_sources, {rel: text(rel) for rel in old_sources})
    test_sources = {pth.relative_to(root).as_posix(): pth.read_text(encoding='utf-8-sig')
                    for pth in (root / 'tests').rglob('*.cs')}
    check('literal equality expectations do not use removed interpolated wording',
          not stale_expectations(templates, test_sources))
    copy_path = 'tests/ProcessLens.CopyTests/Program.cs'
    changed_tests = dict(test_sources)
    changed_tests[copy_path] = changed_tests[copy_path].replace(
        '"3 voci · ultima: ESITO · 23:31:34.611"',
        '"3 voci nel registro. Ultima voce: ESITO · 23:31:34.611."')
    check('interpolated expectation guard rejects the actual R11 R1 overview regression',
          changed_tests[copy_path] != test_sources[copy_path] and any(
              row['expected'] == '3 voci nel registro. Ultima voce: ESITO · 23:31:34.611.'
              for row in stale_expectations(templates, changed_tests)))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(results, indent=2) + '\n')
    raise SystemExit(0 if all(row['status'] == 'PASS' for row in results) else 1)


if __name__ == '__main__':
    main()
