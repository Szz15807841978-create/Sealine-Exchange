"""Stage downloaded Unity packages; optionally import assets without duplicate GUIDs."""
import pathlib
import re
import sys
import tarfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
STAGE = ROOT / '_local/packages/AdditionalOriginal'
PACKAGES = {
    'Low_Poly_Underwater_Pack_v1.02': 'Assets/Ventuar/UnderwaterPack/',
    'Underwater_life_deluxe_v1.0': 'Assets/Underwater life deluxe/',
}

def stage():
    for package, prefix in PACKAGES.items():
        count = 0
        paths = {}
        for pathname in (ROOT / '_local/packages' / (package + '_meta')).glob('*/pathname'):
                path = pathname.read_text(encoding='utf-8-sig').split('\0')[0].splitlines()[0].strip()
                relative = pathlib.PurePosixPath(path)
                if not path.startswith(prefix) or '..' in relative.parts or relative.is_absolute():
                    continue
                paths[pathname.parent.name] = path
        with tarfile.open(ROOT / '_local/packages' / (package + '.unitypackage'), 'r|gz') as archive:
            for member in archive:
                name = member.name.lstrip('./')
                if '/' not in name or not member.isfile():
                    continue
                folder, payload = name.rsplit('/', 1)
                if folder not in paths or payload not in ('asset', 'asset.meta'):
                    continue
                data = archive.extractfile(member).read()
                output = pathlib.Path(str(STAGE / paths[folder]) + ('.meta' if payload == 'asset.meta' else ''))
                output.parent.mkdir(parents=True, exist_ok=True)
                if output.exists() and output.read_bytes() != data:
                    raise RuntimeError('Existing staging file differs: ' + str(output))
                if not output.exists():
                    output.write_bytes(data)
                count += payload == 'asset'
        print(package, 'staged', count, 'assets')

def import_assets():
    destination = ROOT / 'Unity'
    guids = {}
    for meta in (destination / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})', meta.read_text(encoding='utf-8-sig'), re.M)
        if match:
            guids[match[1]] = pathlib.Path(str(meta)[:-5])
    copied = reused = 0
    for source in sorted((STAGE / 'Assets').rglob('*')):
        if not source.is_file() or source.suffix == '.meta':
            continue
        # Demo cameras and obsolete rendering scripts are replaced in playable copies.
        if source.suffix in ('.cs', '.shadergraph'):
            continue
        meta = pathlib.Path(str(source) + '.meta')
        guid = re.search(r'^guid: ([0-9a-f]{32})', meta.read_text(encoding='utf-8-sig'), re.M)[1]
        if guid in guids:
            reused += 1
            continue
        target = destination / source.relative_to(STAGE)
        for input_path, output_path in [(source, target), (meta, pathlib.Path(str(target) + '.meta'))]:
            if output_path.exists() and output_path.read_bytes() != input_path.read_bytes():
                raise RuntimeError('Refusing to overwrite project asset: ' + str(output_path))
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
        pathlib.Path(str(target) + '.meta').write_bytes(meta.read_bytes())
        guids[guid] = target
        copied += 1
    print('Imported', copied, 'assets; reused', reused, 'existing GUIDs')

if __name__ == '__main__':
    if '--import' in sys.argv:
        import_assets()
    else:
        stage()
