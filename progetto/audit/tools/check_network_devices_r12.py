"""Portable R12 source, XML and mutation checks. No C#/WPF or live network execution."""
import argparse, hashlib, json, re, zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
from network_device_rules import violations
from ux_writing_rules import approved_revision
from test_clipboard_calls_r10r9 import invalid_calls

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--candidate', type=Path, required=True)
p.add_argument('--baseline-zip', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args(); root = a.candidate
assert hashlib.sha256(a.baseline_zip.read_bytes()).hexdigest() == 'b46c5acc89186d05b872b825471845c7ff8bccf53fb0c2de928feb591dca1104'
with zipfile.ZipFile(a.baseline_zip) as archive:
    old = {n.split('/', 1)[1]: archive.read(n) for n in archive.namelist() if not n.endswith('/')}
files = {f.relative_to(root).as_posix(): f.read_text(encoding='utf-8-sig') for f in root.rglob('*') if f.suffix in {'.cs', '.xaml', '.ps1'}}
results = []
def check(label, ok):
    results.append({'test': label, 'status': 'PASS' if ok else 'FAIL', 'kind': 'source/XML or source guard mutation only'})
    print(results[-1]['status'], label)
check('network feature satisfies all source guards', not violations(files))
approved = json.loads((root / 'audit/R10_R12/authorized-source-edits.json').read_text())['files']
allowed_existing = {'src/ProcessLens.App/MainWindow.xaml', 'src/ProcessLens.App/MainWindow.xaml.cs',
                    'src/ProcessLens.App/ProcessLens.App.csproj', 'src/ProcessLens.App/CopyTestVisibility.cs'}
changed = {rel for rel in old if rel.startswith('src/') and (root / rel).read_bytes() != old[rel]}
check('only four authorized existing UI sources differ from R11 R2', changed == allowed_existing)
check('all prior collectors Core semantics Guardian profiler and RAM monitor are byte identical',
      all((root / rel).read_bytes() == data for rel, data in old.items() if rel.startswith('src/') and rel not in allowed_existing))
check('every network source matches exact R12 or authorized R13/R14 feature hashes',
      all(approved_revision(root, row['file']) for row in approved))
w = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
x = '{http://schemas.microsoft.com/winfx/2006/xaml}'
local = '{clr-namespace:ProcessLens.App}'
for f in [*root.rglob('*.xaml'), *root.rglob('*.csproj')]: ET.parse(f)
check('all WPF markup and project XML parse', True)
view = ET.parse(root / 'src/ProcessLens.App/NetworkDevicesView.xaml').getroot()
names = [e.get(x+'Name') for e in view.iter() if e.get(x+'Name')]
check('network view names are unique and handlers exist', len(names) == len(set(names)) and all(
      re.search(r'\b' + re.escape(value) + r'\s*\(', files['src/ProcessLens.App/NetworkDevicesView.xaml.cs'])
      for e in view.iter() for key, value in e.attrib.items() if key in {'Click', 'SelectionChanged'}))
grid = next(e for e in view.iter(w+'DataGrid'))
check('network table read-only copied and virtualized', grid.get('IsReadOnly') == 'True' and
      grid.get(local+'GridCopySupport.Enabled') == 'True' and grid.get('VirtualizingPanel.VirtualizationMode') == 'Recycling')
check('all network outputs wrap and buttons avoid fixed heights',
      all(e.get('TextWrapping') == 'Wrap' for e in view.iter(local+'SelectableText')) and
      all(e.get('Style') == '{StaticResource NetworkAction}' and 'Height' not in e.attrib for e in view.iter(w+'Button')))
check('every application clipboard call retains two required arguments', not any(
      invalid_calls(f.read_text()) for f in (root/'src/ProcessLens.App').rglob('*.cs')))
tests = files['tests/ProcessLens.NetworkDeviceTests/Program.cs']
ui_tests = files['tests/ProcessLens.NetworkUiTests/Program.cs']
check('independent ABI byte fixtures and culture/missing-data regressions supplied', all(s in tests for s in
      ['Marshal.WriteInt32(first, 28, 5)', 'IntPtr.Add(first, 40)', 'IntPtr.Add(first, 88)', 'fe80::2%5',
       'it-IT', 'en-US', 'corrupt registry cannot be silently replaced', 'partial acquisition cannot become green']))
check('WPF fixture tests use a fake source and cover lifecycle copy and navigation', all(s in ui_tests for s in
      ['new NetworkDevicesView(source,', 'view.SaveSelectionAsync', 'GridCopySupport.CaptureTable',
       'source.Release.Set(); Complete(pending)', 'NavNetworkButton', 'new MainWindow()']) and
      not re.search(r'\.(?:Show|Run|SetText)\(', ui_tests))
mutations = [
 ('IPv6 omitted', 'native', 'GetIpNetTable2(0, out table)', 'GetIpNetTable2(2, out table)'),
 ('native allocation leaked', 'native', 'FreeMibTable(table); }', 'KeepTable(table); }'),
 ('table padding ignored', 'native', 'Marshal.OffsetOf<NativeHeader>(nameof(NativeHeader.First))', 'Marshal.SizeOf<int>()'),
 ('enumeration cap removed', 'native', 'Math.Min(count, (uint)MaximumRows)', 'count'),
 ('network change keeps old scope', 'native', 'changed ? ""', 'false ? ""'),
 ('UI publication accepts old generation', 'view', 'generation != _generation', 'false'),
 ('refresh coalescing removed', 'view', 'if (!_active || _busy) return;', 'if (!_active) return;'),
 ('deactivation retains displayed rows', 'view', 'NetworkDevicesTable.ItemsSource = null;', 'KeepRows();'),
 ('partial capture allows recognition', 'view', '_snapshot.Available && !_snapshot.Partial', '_snapshot.Available'),
 ('partial result becomes complete', 'core', 'capture.Partial ||', ''),
 ('unavailable count becomes zero', 'core', '"Dispositivi: N/D"', '"0 dispositivi"'),
 ('cached entry claims current connection', 'core', 'In cache · presenza non confermata', 'Collegato adesso'),
 ('recognition ignores network', 'core', 'RecognitionKey(n.NetworkKey, mac)', 'RecognitionKey("", mac)'),
 ('unbounded novelty history', 'core', 'current.Count > 1024', 'false'),
 ('registry size cap removed', 'core', 'stream.Length > MaximumBytes', 'false'),
 ('corrupt file overwritten', 'core', 'if (!existing.Available) return existing.Error;', ''),
 ('automatic acquisition added', 'view', 'public void SetActive(bool active)\n    {', 'public void SetActive(bool active)\n    {\n        _source.Capture();'),
]
paths = {'native': 'src/ProcessLens.Windows/WindowsNetworkDevices.cs', 'core': 'src/ProcessLens.Core/NetworkDevices.cs',
         'view': 'src/ProcessLens.App/NetworkDevicesView.xaml.cs'}
for label, kind, before, after in mutations:
    path = paths[kind]; assert before in files[path], (label, before)
    changed_files = dict(files); changed_files[path] = changed_files[path].replace(before, after)
    check('reject ' + label, bool(violations(changed_files)))
a.output.parent.mkdir(parents=True, exist_ok=True)
a.output.write_text(json.dumps(results, indent=2) + '\n')
raise SystemExit(0 if all(row['status'] == 'PASS' for row in results) else 1)
