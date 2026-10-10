"""Import only art from the user-requested local Unity packages; keep existing GUIDs."""
import json
import pathlib
import re
import shutil
import sys
import tarfile
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
STAGE = ROOT / '_local/packages/Expansion20261007'
PACKS = {
    'Corals2': pathlib.Path('E:/鲸落/海底世界场景 珊瑚贝壳海藻海星模型包_爱给网_aigei_com.zip'),
    'PAShark': pathlib.Path('E:/鲸落/121unity鲨鱼资源包PASharkv1.1_爱给网_aigei_com.zip'),
}
PACK_MEMBERS = {}

def manifest_of(package):
    paths = {}
    with tarfile.open(package, 'r:gz') as archive:
        for member in archive:
            name = member.name.lstrip('./')
            if not member.isfile() or not name.endswith('/pathname'):
                continue
            path = archive.extractfile(member).read().decode('utf-8-sig').split('\0')[0].splitlines()[0].strip()
            relative = pathlib.PurePosixPath(path)
            if path.startswith('Assets/') and '..' not in relative.parts and not relative.is_absolute():
                paths[name.split('/')[0]] = path
    return paths

def unpack_manifest():
    STAGE.mkdir(parents=True, exist_ok=True)
    for label, source in PACKS.items():
        package = STAGE / (label + '.unitypackage')
        if not package.exists():
            with zipfile.ZipFile(source) as archive:
                members = [m for m in archive.infolist() if m.filename.endswith('.unitypackage')]
                if label in PACK_MEMBERS:
                    members = [m for m in members if m.filename == PACK_MEMBERS[label]]
                if len(members) != 1:
                    raise ValueError('Expected one Unity package: ' + str(source))
                with archive.open(members[0]) as incoming, package.open('wb') as output:
                    shutil.copyfileobj(incoming, output)
        paths = manifest_of(package)
        nested = [guid for guid, path in paths.items() if path.endswith('.unitypackage')]
        if nested:
            if len(nested) != 1:
                raise ValueError('Select a single nested render-pipeline package explicitly.')
            content = STAGE / (label + '-content.unitypackage')
            if not content.exists():
                with tarfile.open(package, 'r:gz') as archive:
                    member = archive.getmember(nested[0] + '/asset')
                    with archive.extractfile(member) as incoming, content.open('wb') as output:
                        shutil.copyfileobj(incoming, output)
            package = content
            paths = manifest_of(package)
        (STAGE / (label + '-manifest.json')).write_text(json.dumps(paths, ensure_ascii=False, indent=2), encoding='utf-8')
        print(label, len(paths), 'entries', flush=True)
        for path in paths.values():
            if pathlib.PurePosixPath(path).suffix.lower() in ('.unity', '.prefab', '.controller', '.cs'):
                print(path, flush=True)

def import_art():
    destination = ROOT / 'Unity'
    guids = {}
    for meta in (destination / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})', meta.read_text(encoding='utf-8-sig'), re.M)
        if match:
            guids[match[1]] = pathlib.Path(str(meta)[:-5])
    for label in PACKS:
        paths = json.loads((STAGE / (label + '-manifest.json')).read_text(encoding='utf-8'))
        eligible = {guid: path for guid, path in paths.items()
                    if guid not in guids and pathlib.PurePosixPath(path).suffix.lower()
                    not in ('.cs', '.js', '.boo', '.dll', '.exe', '.asmdef', '.asmref')
                    and '/Editor/' not in path and '/Plugins/' not in path}
        copied = 0
        package = STAGE / (label + '-content.unitypackage')
        if not package.exists():
            package = STAGE / (label + '.unitypackage')
        with tarfile.open(package, 'r:gz') as archive:
            for member in archive:
                name = member.name.lstrip('./')
                if not member.isfile() or '/' not in name:
                    continue
                guid, payload = name.rsplit('/', 1)
                if guid not in eligible or payload not in ('asset', 'asset.meta'):
                    continue
                path = eligible[guid]
                # A folder metadata entry is unnecessary; Unity keeps existing folder GUIDs.
                if not pathlib.PurePosixPath(path).suffix:
                    continue
                target = destination / (path + ('.meta' if payload == 'asset.meta' else ''))
                data = archive.extractfile(member).read()
                if target.exists():
                    if target.read_bytes() != data:
                        raise RuntimeError('Refusing to replace an existing asset: ' + str(target))
                    continue
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
                copied += payload == 'asset'
        print(label, 'imported', copied, 'art assets', flush=True)

if __name__ == '__main__':
    if '--sunken-ship' in sys.argv:
        STAGE = ROOT / '_local/packages/SunkenShip20261007'
        PACKS = {'SunkenShip': pathlib.Path('E:/鲸落/Unity水下沉船场景Underwater Sunken S_爱给网_aigei_com.zip')}
        PACK_MEMBERS = {'SunkenShip': 'UnderwaterShip_Builtin_2021.3.6f1.unitypackage'}
    import_art() if '--import' in sys.argv else unpack_manifest()
