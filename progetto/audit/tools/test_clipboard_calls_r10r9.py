"""Source-only regression for the CS7036 reported in R8. Does not access clipboard."""
import argparse
import json
from pathlib import Path
import re
import zipfile


def mask_non_code(text):
    # Preserve offsets; the actual calls use ordinary identifiers. Raw strings
    # are masked first so fake invocations in diagnostics do not become calls.
    pattern = r'"""[\s\S]*?"""|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/'
    return re.sub(pattern, lambda m: ' ' * len(m.group()), text)


def invalid_calls(text):
    code = mask_non_code(text)
    problems = []
    for call in re.finditer(r'\bClipboardText\s*\.\s*Copy\s*\(', code):
        begin = call.end()
        stack = ['(']
        parts = []
        last = begin
        for i in range(begin, len(code)):
            char = code[i]
            if char in '([{':
                stack.append(char)
            elif char in ')]}':
                if not stack or stack.pop() != {')': '(', ']': '[', '}': '{'}[char]:
                    break
                if not stack:
                    parts.append(text[last:i].strip())
                    break
            elif char == ',' and len(stack) == 1:
                parts.append(text[last:i].strip())
                last = i + 1
        else:
            parts = []
        if len(parts) != 2 or not all(parts):
            problems.append({'line': text.count('\n', 0, call.start()) + 1,
                             'argument_count': len(parts)})
    return problems


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--candidate', type=Path, required=True)
    parser.add_argument('--baseline-zip', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    results = []

    def check(ok, label):
        results.append({'test': label, 'status': 'PASS' if ok else 'FAIL'})
        if not ok:
            raise AssertionError(label)
        print('PASS clipboard source:', label)

    sources = sorted((args.candidate / 'src/ProcessLens.App').rglob('*.cs'))
    errors = {p.name: invalid_calls(p.read_text()) for p in sources}
    check(bool(sources) and not any(errors.values()), 'all direct app calls have two nonempty arguments')
    main_text = (args.candidate / 'src/ProcessLens.App/MainWindow.xaml.cs').read_text()
    check('if (_selfResourceReport.Length > 0) ClipboardText.Copy(this, _selfResourceReport);' in main_text,
          'GUI report supplies its window and preserves nonempty guard')
    with zipfile.ZipFile(args.baseline_zip) as archive:
        names = [n for n in archive.namelist() if n.endswith('/src/ProcessLens.App/MainWindow.xaml.cs')]
        check(len(names) == 1 and invalid_calls(archive.read(names[0]).decode('utf-8')) ==
              [{'line': 359, 'argument_count': 1}], 'delivered R8 reproduces the reported one-argument defect')
    check(bool(invalid_calls(main_text.replace('ClipboardText.Copy(this, _selfResourceReport)',
                                              'ClipboardText.Copy(_selfResourceReport)'))),
          'missing owner mutation rejected')
    check(bool(invalid_calls('ClipboardText.Copy(this, text, extra);')) and
          bool(invalid_calls('ClipboardText.Copy(this, );')),
          'extra or empty argument rejected')
    check(not invalid_calls('ClipboardText.Copy(GetOwner(a, b), Format(c, d));'),
          'nested expressions do not add top-level arguments')
    check(not invalid_calls('// ClipboardText.Copy(text);\nvar x = "ClipboardText.Copy(text)";\n'
                            '/* ClipboardText.Copy(); */ ClipboardText.Copy(this, "a,b)");'),
          'comments strings and punctuation within text ignored')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(results, indent=2) + '\n')


if __name__ == '__main__':
    main()
