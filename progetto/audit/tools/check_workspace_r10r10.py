"""Portable source/XML checks only; not C# compilation, WPF layout, or measurements."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile
from button_layout_rules import violations as button_violations
from test_clipboard_calls_r10r9 import invalid_calls
from ux_writing_rules import approved_revision

X = '{http://schemas.microsoft.com/winfx/2006/xaml}'
LOCAL = '{clr-namespace:ProcessLens.App}'
W = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'


def checks(root, old):
    results = []
    def check(label, ok):
        results.append({'test': label, 'status': 'PASS' if ok else 'FAIL', 'kind': 'source/XML only'})
    app = root / 'src/ProcessLens.App'
    main = ET.parse(app / 'MainWindow.xaml').getroot()
    guardian = ET.parse(app / 'GuardianDashboardView.xaml').getroot()
    named = lambda tree: {e.get(X + 'Name'): e for e in tree.iter() if e.get(X + 'Name')}
    mn, gn = named(main), named(guardian)
    source = (app / 'MainWindow.xaml.cs').read_text()
    render = (app / 'GuardianDashboardView.xaml.cs').read_text()
    context = (app / 'WindowsComponentContext.cs').read_text()
    diagnostic = (app / 'GuardianDiagnosticPresentation.cs').read_text()
    allowed = {'src/ProcessLens.App/MainWindow.xaml', 'src/ProcessLens.App/MainWindow.xaml.cs',
               'src/ProcessLens.App/GuardianDashboardView.xaml', 'src/ProcessLens.App/GuardianDashboardView.xaml.cs',
               'src/ProcessLens.App/ProcessLens.App.csproj'}
    check('existing source outside UI is exact or explicitly reviewed UX or network feature',
          all((root / rel).read_bytes() == data or approved_revision(root,rel) for rel, data in old.items() if rel.startswith('src/') and rel not in allowed))
    for filename in ['MainWindow.xaml', 'GuardianDashboardView.xaml', 'MemoryDashboardView.xaml']:
        doc = ET.parse(app / filename).getroot()
        names = [e.get(X + 'Name') for e in doc.iter() if e.get(X + 'Name')]
        check('unique WPF names ' + filename, len(names) == len(set(names)))
        check('selectable one-way outputs and table copy ' + filename,
              all(('LineHeight' not in e.attrib and 'TextTrimming' not in e.attrib and
                   (not e.get('Text', '').startswith('{Binding') or 'Mode=OneWay' in e.get('Text', '')))
                  for e in doc.iter(LOCAL + 'SelectableText')) and
              all(e.get(LOCAL + 'GridCopySupport.Enabled') == 'True' for e in doc.iter(W + 'DataGrid')))
    check('main buttons retain wrapped styles and no fixed heights', not button_violations(main))
    baseline_main = ET.fromstring(old['src/ProcessLens.App/MainWindow.xaml'])
    for name in ['ProcessList', 'ServiceList']:
        check('list template and selection unchanged ' + name,
              ET.tostring(mn[name]) == ET.tostring(named(baseline_main)[name]))
    for filename in ['GuardianDashboardView.xaml', 'MemoryDashboardView.xaml']:
        before = list(ET.fromstring(old['src/ProcessLens.App/' + filename]).iter(W + 'DataGrid'))
        after = list(ET.parse(app / filename).iter(W + 'DataGrid'))
        check('all data-grid bindings copy and selection unchanged ' + filename,
              len(before) == len(after) and all(ET.tostring(a) == ET.tostring(b) for a, b in zip(before, after)))
    check('process and service column splitters are keyboard focusable',
          len([e for e in main.iter(W + 'GridSplitter') if e.get('ResizeDirection') == 'Columns' and
               e.get('Focusable') == 'True' and e.get('ResizeBehavior') == 'PreviousAndNext']) == 2)
    check('focus and return preserve existing pane and list width',
          all(t in source for t in ['_listWidthBeforeFocus = column.Width;', 'column.MinWidth = 0;',
              'column.Width = _listWidthBeforeFocus.Value;', 'ProcessListPanel.Visibility = Visibility.Collapsed;',
              'ProcessListPanel.Visibility = Visibility.Visible;', 'ToggleDetailsFocusButton.Content = "Mostra elenco";']))
    check('secondary GUI counters start collapsed and copy remains enabled by real samples',
          mn['GuiResourcesExpander'].get('IsExpanded') == 'False' and 'SelfResourcesText' in named(mn['GuiResourcesExpander']) and
          'ClipboardText.Copy(this, _selfResourceReport)' in source)
    check('analysis is earlier than identification and uptime sections',
          (app / 'MainWindow.xaml').read_text().index('x:Name="ProcessQuestionBox"') <
          (app / 'MainWindow.xaml').read_text().index('x:Name="DetailStartTimeText"'))
    check('Guardian tabs have padded selectable and keyboard focus states',
          gn['GuardianTabs'].get('ItemContainerStyle') == '{StaticResource GuardianTab}' and
          'Property="IsSelected"' in (app / 'GuardianDashboardView.xaml').read_text() and
          'Property="IsKeyboardFocused"' in (app / 'GuardianDashboardView.xaml').read_text())
    check('diagnostic translated and raw panels are selectable one-way bindings',
          all(gn[n].tag == LOCAL + 'SelectableText' and 'Mode=OneWay' in gn[n].get('Text', '') for n in
              ['DiagnosticDetailsText', 'DiagnosticOriginalText']) and
          'SelectedItem.OriginalMessage' in gn['DiagnosticOriginalText'].get('Text', ''))
    check('diagnostic selection survives unchanged snapshots and preserves raw identity',
          all(t in render for t in ['value.Diagnostics.Select(GuardianDiagnosticPresentation.From)',
                                   '!current.SequenceEqual(diagnostics)', 'x.OriginalMessage == previous?.OriginalMessage']))
    check('diagnostic decoder only accepts exact health grammar and retains originals',
          all(t in diagnostic for t in ['row.Source == "health"', 'row.Message.Length <= 256', r'\Aaccepted=',
                                       r'\z"', 'row.Source, row.Message', 'values.Groups["ram"].Value']) and
          not re.search(r'\b(?:Parse|TryParse)\(', diagnostic))
    check('diagnostic labels distinguish cumulative coverage and shared resident RAM',
          all(t in diagnostic for t in ['cumulativi', 'non prova la completezza', 'non è RAM privata',
                                       'non limitati al periodo', 'Working Set']))
    check('Windows reference uses exact name and separates identity from static role',
          all(t in context for t in ['EndsWith(".exe", StringComparison.OrdinalIgnoreCase)',
              'name.ToLowerInvariant() switch', 'Identità e attività attuale non verificate', 'Firma digitale e autenticità: non verificate',
              'assenza non confermata', 'Funzione sconosciuta', 'p.ServiceNames.Take(16)']) and
          'p.CommandLine' not in context and '.Contains("svchost' not in context)
    check('Windows context clears prior report without replacing an active query',
          '_queryBusy || _currentDetailRow is not' in source and 'ShowQueryResult(new ProcessQueryResult(' in source and
          'ClearQueryAnswer();' in source[source.index('private void ShowWindowsContext_Click'):source.index('private void CopySelfResourcesButton_Click')])
    check('all clipboard calls retain the two-argument contract',
          not any(invalid_calls(p.read_text()) for p in app.rglob('*.cs')))
    for filename in ['MainWindow', 'GuardianDashboardView', 'MemoryDashboardView']:
        doc = ET.parse(app / (filename + '.xaml'))
        code = (app / (filename + '.xaml.cs')).read_text()
        events = {'Click','SelectionChanged','Expanded','Collapsed','MouseEnter','MouseDoubleClick','TextChanged','KeyDown','SizeChanged'}
        check('XAML event handlers exist ' + filename,
              all(re.search(r'\b' + re.escape(v) + r'\s*\(', code) for e in doc.iter() for k, v in e.attrib.items() if k in events))
    test = (root / 'tests/ProcessLens.CopyUiTests/Program.cs').read_text()
    check('WPF regressions remain unshown and use a fake clipboard',
          'focusButton.RaiseEvent' in test and 'SelectedItem = diagnostic' in test and
          not re.search(r'\.(?:Show|Run|SetActive)\(', test) and 'new SelectableText(value => copied = value)' in test)
    build = (root / 'build-dashboard.ps1').read_text()
    check('new source and executable regressions wired into existing build',
          'check-workspace-r10r10.ps1' in build and 'Tests.ProcessLens.GuardianDashboardTests' in build and
          'Tests.ProcessLens.CopyUiTests' in build)
    return results


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--candidate', type=Path, required=True)
    p.add_argument('--baseline-zip', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    assert hashlib.sha256(a.baseline_zip.read_bytes()).hexdigest() == '5e4aa34fcc863015e39a136442b33139a103e5fd0d4a068d356fb5d556b92fc1'
    with zipfile.ZipFile(a.baseline_zip) as archive:
        old = {n.split('/', 1)[1]: archive.read(n) for n in archive.namelist() if not n.endswith('/')}
    results = checks(a.candidate, old)
    for result in results:
        print(result['status'], result['test'])
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.write_text(json.dumps(results, indent=2) + '\n')
    raise SystemExit(0 if all(r['status'] == 'PASS' for r in results) else 1)


if __name__ == '__main__':
    main()
