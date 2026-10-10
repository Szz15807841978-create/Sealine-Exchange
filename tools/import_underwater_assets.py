"""Extract only meshes/textures from the user's Low Poly package; no plugin scripts."""
import pathlib
import tarfile

root = pathlib.Path(__file__).resolve().parents[1]
package = root / '_local/packages/Low_Poly_Underwater_Pack_v1.02.unitypackage'
destination = root / 'Unity/Assets/ThirdParty/UnderwaterExplore'
with tarfile.open(package, 'r:gz') as archive:
    members = {m.name.lstrip('./'): m for m in archive.getmembers()}
    count = 0
    for name, member in members.items():
        if not name.endswith('/pathname'):
            continue
        path = archive.extractfile(member).read().decode('utf-8-sig').split('\0')[0].strip()
        if '/PackAssets/Models/' in path and path.lower().endswith('.fbx'):
            relative = 'Models/' + path.rsplit('/', 1)[1]
        elif path.endswith('/UnderwaterPackAtlas.png') or path.endswith('/Bubble.png'):
            relative = 'Textures/' + path.rsplit('/', 1)[1]
        else:
            continue
        folder = name.rsplit('/', 1)[0]
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        for payload, suffix in [('asset', ''), ('asset.meta', '.meta')]:
            output = pathlib.Path(str(target) + suffix)
            data = archive.extractfile(members[folder + '/' + payload]).read()
            if output.exists() and output.read_bytes() != data:
                raise RuntimeError('Refusing to overwrite existing asset: ' + str(output))
            output.write_bytes(data)
        count += 1
print('Imported', count, 'mesh/texture assets into', destination)
