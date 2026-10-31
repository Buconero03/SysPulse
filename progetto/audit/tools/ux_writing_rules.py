"""Reviewed editorial scope for R11. Hash checks do not replace compilation."""
import hashlib
import json
import re
from test_clipboard_calls_r10r9 import mask_non_code


def approved_editorial(root, relative):
    manifest = root / 'audit/R10_R11/authorized-source-edits.json'
    if not manifest.exists():
        return False
    rows = json.loads(manifest.read_text())['files']
    return any(row['file'] == relative and row['sha256_after'] ==
               hashlib.sha256((root / relative).read_bytes()).hexdigest() for row in rows)


def approved_revision(root, relative):
    """R11 editorial delta or separately recorded, user-requested R12/R13/R14 feature delta."""
    if approved_editorial(root, relative):
        return True
    return any(approved_feature(root, relative, revision) for revision in ('R10_R12', 'R10_R13', 'R10_R14', 'R10_R14_R1', 'R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))


def approved_feature(root, relative, revision):
    manifest = root / ('audit/' + revision + '/authorized-source-edits.json')
    if not manifest.exists():
        return False
    rows = json.loads(manifest.read_text())['files']
    return any(row['file'] == relative and row['sha256_after'] ==
               hashlib.sha256((root / relative).read_bytes()).hexdigest() for row in rows)


def conditions(source):
    """Actual control conditions, including recognition strings and thresholds.

    Read parenthesis boundaries in masked code, compare original text inside them.
    This is a source regression, not a C# parser or proof of semantic equivalence.
    """
    masked = mask_non_code(source)
    result = []
    for hit in re.finditer(r'\b(if|for|foreach|while|switch)\s*\(', masked):
        depth = 1
        for i in range(hit.end(), len(masked)):
            if masked[i] == '(':
                depth += 1
            elif masked[i] == ')':
                depth -= 1
                if depth == 0:
                    result.append((hit[1], re.sub(r'\s+', ' ', source[hit.end():i]).strip()))
                    break
        else:
            raise ValueError('Unbalanced control condition')
    return result


def thresholds(source):
    # Also covers ternary decisions, which are not if-statements. Literal text
    # is masked so quantities inside prose do not count as executable thresholds.
    return re.findall(r'\b([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*(>=|<=|>|<|==|!=)\s*(-?\d+(?:\.\d+)?)',
                      mask_non_code(source))
