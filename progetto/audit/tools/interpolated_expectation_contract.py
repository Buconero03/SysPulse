"""Narrow source triage for old interpolated strings in literal equality assertions.

Not a C# parser or runtime evaluator. Multiline/concatenated expressions and
formatting semantics are outside this guard. Legacy input fixtures are retained.
"""
import re

STRINGS = re.compile(r'\$"([^"\\]*(?:\\.[^"\\]*)*)"')
LITERALS = re.compile(r'"([^"\\]{12,})"')


def removed_templates(old_sources, current_sources):
    removed = []
    for rel, before in old_sources.items():
        after = current_sources[rel]
        for template in STRINGS.findall(before):
            if template in after:
                continue
            parts = re.split(r'(\{[^{}]*\})', template)
            if sum(len(p) for p in parts if not p.startswith('{')) < 12:
                continue
            expression = ''.join('.+?' if part.startswith('{') else re.escape(part) for part in parts)
            removed.append((rel, template, re.compile('^' + expression + '$')))
    return removed


def mismatches(templates, tests):
    errors = []
    for rel, source in tests.items():
        for number, line in enumerate(source.splitlines(), 1):
            if '==' not in line:
                continue
            for expected in LITERALS.findall(line):
                for origin, template, expression in templates:
                    if expression.fullmatch(expected):
                        errors.append({'test_file': rel, 'line': number,
                                       'expected': expected, 'old_source': origin,
                                       'old_template': template})
    return errors
