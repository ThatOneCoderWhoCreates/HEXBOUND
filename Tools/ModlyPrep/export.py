import sys, json, struct, numpy as np, trimesh, glob
sys.path.insert(0, '.')
from glb import read
from proc import decimate
plan = {'01_temple_gate': ('TempleGate', 80000), '02_cherry_blossom_tree': ('CherryTree', 16000), '03_floating_island': ('FloatingIsland', 20000),
        '04_celestial_arch': ('CelestialArch', 30000), '05_waterfall_cliff': ('WaterfallCliff', 30000), '06_shrine_lantern': ('Lantern', 9000),
        '07_purple_banner': ('Banner', 8000), '08_stone_pillar': ('StonePillar', 18000)}
def write(name, V, F, C, path, glow=None):
    tm = trimesh.Trimesh(V, F, process=False); N = np.asarray(tm.vertex_normals)
    bin_ = bytearray(); j = {'asset': {'version': '2.0', 'generator': 'HEXBOUND Modly prep (decimated)'}, 'scene': 0, 'scenes': [{'nodes': [0]}],
         'nodes': [{'name': name, 'mesh': 0}], 'buffers': [{}], 'bufferViews': [], 'accessors': [],
         'materials': [{'name': name, 'pbrMetallicRoughness': {'baseColorFactor': [1, 1, 1, 1], 'metallicFactor': 0.0, 'roughnessFactor': 0.85}}]}
    def acc(arr, ctype, typ, target, norm=False, mm=False):
        while len(bin_) % 4: bin_.append(0)
        off = len(bin_); bin_.extend(arr.tobytes())
        j['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes, 'target': target})
        a = {'bufferView': len(j['bufferViews']) - 1, 'componentType': ctype, 'count': int(arr.shape[0]), 'type': typ}
        if norm: a['normalized'] = True
        if mm: a['min'] = arr.min(0).tolist(); a['max'] = arr.max(0).tolist()
        j['accessors'].append(a); return len(j['accessors']) - 1
    attrs = {'POSITION': acc(V.astype(np.float32), 5126, 'VEC3', 34962, mm=True), 'NORMAL': acc(N.astype(np.float32), 5126, 'VEC3', 34962),
             'COLOR_0': acc((np.clip(C[:, :4], 0, 1) * 255).round().astype(np.uint8), 5121, 'VEC4', 34962, norm=True)}
    prims = []
    if glow is not None and glow.any():
        j['materials'].append({'name': name + '_Glow', 'pbrMetallicRoughness': {'baseColorFactor': [1, 0.9, 0.7, 1], 'metallicFactor': 0.0, 'roughnessFactor': 0.6}, 'emissiveFactor': [1.0, 0.72, 0.38]})
        prims.append({'attributes': attrs, 'indices': acc(F[glow].astype(np.uint32).reshape(-1), 5125, 'SCALAR', 34963), 'material': 1})
        F = F[~glow]
    prims.insert(0, {'attributes': attrs, 'indices': acc(F.astype(np.uint32).reshape(-1), 5125, 'SCALAR', 34963), 'material': 0})
    j['meshes'] = [{'name': name, 'primitives': prims}]
    while len(bin_) % 4: bin_.append(0)
    j['buffers'][0]['byteLength'] = len(bin_)
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    out = struct.pack('<III', 0x46546C67, 2, 28 + len(js) + len(bin_)) + struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(bin_), 0x004E4942) + bytes(bin_)
    open(path, 'wb').write(out); return len(out)
import os; os.makedirs('out', exist_ok=True)
for f in sorted(glob.glob('/mnt/user-data/uploads/*_final.glb')):
    key = f.split('/')[-1].split('-', 1)[1].replace('_final.glb', '')
    name, target = plan[key]
    V, F, C, _ = read(f)
    v, fc, c = decimate(V, F, C, target)
    glow = None
    if name == 'Lantern':
        fcol = c[fc].mean(1); cy = v[fc].mean(1)[:, 1]
        glow = (fcol[:, 0] > 0.85) & (fcol[:, 1] > 0.68) & (fcol[:, 2] < 0.62) & (cy > -0.22) & (cy < 0.32)
    size = write(name, v, fc, c, 'out/%s.glb' % name, glow)
    np.savez('%s.npz' % key, V=v, F=fc, C=c)
    print(name, len(fc), 'tris', size // 1024, 'KB')
