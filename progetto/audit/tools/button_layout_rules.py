"""Portable equivalent of the strict R9 WPF button layout predicate, not WPF execution."""
import xml.etree.ElementTree as ET

XNAME = '{http://schemas.microsoft.com/winfx/2006/xaml}Name'
ALLOWED_STYLES = {'{StaticResource FilterButtonStyle}', '{StaticResource ActionButtonStyle}'}


def violations(root):
    return [dict(name=e.get(XNAME, ''), content=e.get('Content', ''),
                 style=e.get('Style', ''), height=e.get('Height'))
            for e in root.iter() if e.tag.rsplit('}', 1)[-1] == 'Button' and
            (e.get('Style') not in ALLOWED_STYLES or 'Height' in e.attrib)]
