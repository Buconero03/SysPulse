"""Narrow source guard for the four MemoryTests display cases, not C# execution."""
import re


def mismatches(snapshot, tests):
    labels = re.search(r'MemoryDisplayLabel\s*=>\s*(.*?);', snapshot, re.S)
    descriptions = re.search(r'MemoryDescription\s*=>\s*(.*?);\s*\n\s*//', snapshot, re.S)
    if labels is None or descriptions is None:
        return ['Unrecognized snapshot display expressions']
    literals = lambda s: re.findall(r'"([^"\\]*(?:\\.[^"\\]*)*)"', s)
    label_values = literals(labels[1])
    description_values = literals(descriptions[1])
    if len(label_values) != 3 or len(description_values) != 3:
        return ['Unrecognized snapshot display branches']
    cases = re.findall(r'Test\("[^"\n]+",\s*(\w+),\s*"([^"\n]+)",\s*(?:null|[\d.]+)(?:,\s*"([^"\n]+)")?\s*\);', tests)
    branches = {'privateResident': 0, 'residentFallback': 1, 'baseline': 2, 'genuineZero': 0}
    if len(cases) != 4 or {c[0] for c in cases} != set(branches):
        return ['Expected all four memory display regression cases']
    errors = []
    for name, label, token in cases:
        branch = branches[name]
        if label != label_values[branch]:
            errors.append(name + ': stale label ' + label)
        if token and token not in description_values[branch]:
            errors.append(name + ': stale description fragment ' + token)
    return errors
