"""Build an atomically published ZIP and check its exact file set and hashes.

Usage: python package_checked_zip.py SHELL_DIRECTORY OUTPUT_ZIP
Only the two hash manifests are regenerated; no application source is edited.
"""
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
import zipfile

def digest(data):
    return hashlib.sha256(data).hexdigest()

def check_manifest(z, prefix, manifest_name):
    rows = z.read(prefix + manifest_name).decode('utf-8').splitlines()
    for row in rows:
        expected, name = row.split('  ', 1)
        assert digest(z.read(prefix + name)) == expected, name
    return len(rows)

def verify(path, expected):
    with zipfile.ZipFile(path) as z:
        names = z.namelist()
        assert len(names) == len(set(names)) == len(expected)
        assert set(names) == set(expected)
        assert z.testzip() is None
        for info in z.infolist():
            assert info.compress_type == zipfile.ZIP_DEFLATED
            assert not info.flag_bits & 1
            assert info.extract_version <= 20
            assert z.read(info.filename) == expected[info.filename]
        assert check_manifest(z, 'ProcessLens-Leggero/', 'SHA256SUMS.txt') == len(names)-1
        check_manifest(z, 'ProcessLens-Leggero/progetto/', 'SHA256SUMS_R10.txt')
        assert not z.comment

def main():
    shell, output = map(lambda x: Path(x).resolve(), sys.argv[1:])
    assert shell.name == 'ProcessLens-Leggero'
    project = shell/'progetto'
    for base, manifest in [(project, 'SHA256SUMS_R10.txt'), (shell, 'SHA256SUMS.txt')]:
        files = sorted(p for p in base.rglob('*') if p.is_file() and p != base/manifest)
        assert not any(p.suffix in {'.pyc', '.dll', '.exe', '.pdb', '.ok'} for p in files)
        (base/manifest).write_text(''.join(digest(p.read_bytes())+'  '+p.relative_to(base).as_posix()+'\n' for p in files), encoding='utf-8')
    expected = {'ProcessLens-Leggero/'+p.relative_to(shell).as_posix(): p.read_bytes() for p in shell.rglob('*') if p.is_file()}
    output.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=output.stem+'-', suffix='.partial', dir=output.parent)
    try:
        with os.fdopen(fd, 'w+b') as stream:
            with zipfile.ZipFile(stream, 'w', zipfile.ZIP_DEFLATED, compresslevel=9, allowZip64=False) as z:
                for name, data in sorted(expected.items()):
                    info = zipfile.ZipInfo(name, date_time=(2026,10,27,12,0,0))
                    info.compress_type = zipfile.ZIP_DEFLATED
                    info.create_system = 0
                    info.external_attr = 0x20
                    z.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
            stream.flush()
            os.fsync(stream.fileno())
        verify(temporary, expected)
        os.replace(temporary, output)
        verify(output, expected)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)
    print(json.dumps({'path':str(output),'files':len(expected),'bytes':output.stat().st_size,'sha256':digest(output.read_bytes()),'zip_crc_and_exact_file_set':'PASS','both_sha256_manifests':'PASS','atomic_publication':'PASS'}, indent=2))

if __name__ == '__main__':
    main()
