"""Regressions for the portable guard, including the exact failed R10 input."""
import argparse
import copy
import json
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
from button_layout_rules import violations, XNAME

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--baseline-zip', type=Path, required=True)
p.add_argument('--candidate', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
with zipfile.ZipFile(a.baseline_zip) as z:
    baseline = ET.fromstring(z.read('ProcessLens-v0.19.16-R10/src/ProcessLens.App/MainWindow.xaml'))
current = ET.parse(a.candidate / 'src/ProcessLens.App/MainWindow.xaml').getroot()
results = []


def check(name, condition):
    results.append(dict(test=name, status='PASS' if condition else 'FAIL'))
    print(results[-1]['status'], name)


check('delivered R10 failure reproduced: only direct RAM button lacks style',
      [r['name'] for r in violations(baseline)] == ['AnalyzeRamDirectButton'])
check('corrected R10 R1 has no layout-rule violation', not violations(current))
for name in ['MissingStyle', 'UnknownStyle', 'FixedHeight']:
    probe = copy.deepcopy(current)
    button = next(e for e in probe.iter() if e.get(XNAME) == 'AnalyzeRamDirectButton')
    if name == 'MissingStyle':
        button.attrib.pop('Style')
    elif name == 'UnknownStyle':
        button.set('Style', '{StaticResource MissingStyle}')
    else:
        button.set('Height', '20')
    check('reject ' + name, [r['name'] for r in violations(probe)] == ['AnalyzeRamDirectButton'])
check('direct RAM event remains wired',
      next(e for e in current.iter() if e.get(XNAME) == 'AnalyzeRamDirectButton').get('Click') == 'AnalyzeRamDirectButton_Click')
a.output.parent.mkdir(parents=True, exist_ok=True)
a.output.write_text(json.dumps(results, indent=2) + '\n')
raise SystemExit(0 if all(r['status'] == 'PASS' for r in results) else 1)
