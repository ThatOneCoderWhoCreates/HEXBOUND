import json, struct, numpy as np, trimesh
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
