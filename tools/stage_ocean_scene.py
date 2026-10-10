"""Stage the complete built-in Ocean Environment demo, preserving Unity GUIDs."""
import pathlib
import tarfile

root = pathlib.Path(__file__).resolve().parents[1]
stage = root / '_local/packages/OceanOriginal'
with tarfile.open(root / '_local/packages/Ocean_Environment_Pack_1.01.unitypackage', 'r:gz') as archive:
    members = {m.name.lstrip('./'): m for m in archive.getmembers()}
    count = 0
    for name, member in members.items():
        if not name.endswith('/pathname'):
            continue
        path = archive.extractfile(member).read().decode('utf-8-sig').split('\0')[0].splitlines()[0].strip()
        if not path.startswith('Assets/Davis3D/OceanEnvironmentPack/'):
            continue
        if any(s in path for s in ['/RP/', '/Patches/', 'Demonstration_HD', 'Demonstration_URP', 'Main Camera Profile HDRP']):
            continue
        relative = pathlib.PurePosixPath(path)
        if '..' in relative.parts:
            raise RuntimeError('Invalid package path')
        folder = name.rsplit('/', 1)[0]
        if folder + '/asset' not in members:
            continue
        target = stage / path
        target.parent.mkdir(parents=True, exist_ok=True)
        for payload, suffix in [('asset', ''), ('asset.meta', '.meta')]:
            output = pathlib.Path(str(target) + suffix)
            data = archive.extractfile(members[folder + '/' + payload]).read()
            if output.exists() and output.read_bytes() != data:
                raise RuntimeError('Existing file differs: ' + str(output))
            output.write_bytes(data)
        count += 1
print('Staged', count, 'original demo assets at', stage)
