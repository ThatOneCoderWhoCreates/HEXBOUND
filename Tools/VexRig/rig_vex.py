import json, struct, numpy as np, trimesh
d = np.load('vex_m.npz'); V = d['V'].astype(np.float64); F = d['F'].astype(np.int64); C = np.clip(d['C'][:, :3], 0, 1)
mesh = trimesh.Trimesh(V, F, process=False); Nrm = np.asarray(mesh.vertex_normals, dtype=np.float64)
# Joints (glTF space, +Y up, model faces +Z; +X is the character's LEFT side). Height 1.75 m.
P = {k: np.array(v, float) for k, v in dict(
    pelvis=(0.0, 0.97, 0.0), chest=(0.0, 1.30, 0.02), neck=(0.0, 1.45, 0.03), head=(0.0, 1.58, 0.02), tail=(0.0, 1.25, -0.2),
    cL=(0.13, 1.33, 0.03), cR=(-0.13, 1.33, 0.03), coatF=(0.0, 0.5, 0.12), coatB=(0.0, 0.5, -0.12), coatL=(0.3, 0.5, 0.0), coatR=(-0.3, 0.5, 0.0),
    shL=(0.165, 1.36, 0.03), elL=(0.21, 1.12, 0.045), haL=(0.26, 0.95, 0.07), fiL=(0.29, 0.82, 0.09),
    shR=(-0.165, 1.36, 0.03), elR=(-0.21, 1.12, 0.045), haR=(-0.26, 0.95, 0.07), fiR=(-0.29, 0.82, 0.09),
    hipL=(0.09, 0.93, 0.0), knL=(0.125, 0.48, 0.03), ftL=(0.19, 0.08, 0.0), toL=(0.2, 0.02, 0.12),
    hipR=(-0.09, 0.93, 0.0), knR=(-0.125, 0.48, 0.03), ftR=(-0.19, 0.08, 0.0), toR=(-0.2, 0.02, 0.12)).items()}
bones = [('pelvis', None, P['pelvis']), ('spine_01', 'pelvis', P['pelvis'] + (P['chest'] - P['pelvis']) * 0.05), ('spine_03', 'spine_01', P['chest']),
         ('head', 'spine_03', np.array([0.0, 1.47, 0.03])), ('head_end', 'head', np.array([0.0, 1.75, 0.02]))]
for side, s in (('L', 'l'), ('R', 'r')):
    bones += [('upperarm_' + s, 'spine_03', P['sh' + side]), ('lowerarm_' + s, 'upperarm_' + s, P['el' + side]), ('hand_' + s, 'lowerarm_' + s, P['ha' + side]),
              ('thigh_' + s, 'pelvis', P['hip' + side]), ('calf_' + s, 'thigh_' + s, P['kn' + side]), ('foot_' + s, 'calf_' + s, P['ft' + side])]
names = [b[0] for b in bones]; pos = {b[0]: b[2] for b in bones}; par = {b[0]: b[1] for b in bones}
S = [('hipL', 'hipR', 0.14, 'pelvis'), ('pelvis', 'coatF', 0.15, 'pelvis'), ('pelvis', 'coatB', 0.15, 'pelvis'), ('pelvis', 'coatL', 0.15, 'pelvis'), ('pelvis', 'coatR', 0.15, 'pelvis'),
     ('pelvis', 'chest', 0.26, 'spine_01'), ('chest', 'neck', 0.22, 'spine_03'), ('cL', 'cR', 0.14, 'spine_03'),
     ('neck', 'head', 0.12, 'head'), ('head', 'tail', 0.10, 'head'),
     ('shL', 'elL', 0.065, 'upperarm_l'), ('elL', 'haL', 0.055, 'lowerarm_l'), ('haL', 'fiL', 0.05, 'hand_l'),
     ('shR', 'elR', 0.065, 'upperarm_r'), ('elR', 'haR', 0.055, 'lowerarm_r'), ('haR', 'fiR', 0.05, 'hand_r'),
     ('hipL', 'knL', 0.10, 'thigh_l'), ('knL', 'ftL', 0.065, 'calf_l'), ('ftL', 'toL', 0.06, 'foot_l'),
     ('hipR', 'knR', 0.10, 'thigh_r'), ('knR', 'ftR', 0.065, 'calf_r'), ('ftR', 'toR', 0.06, 'foot_r')]
def segd(p, a, b):
    ab = b - a; t = np.clip(((p - a) @ ab) / (ab @ ab), 0, 1); return np.linalg.norm(p - (a + t[:, None] * ab), axis=1)
D = np.stack([segd(V, P[a], P[b]) / r for a, b, r, _ in S], 1)
W = np.exp(-(D - D.min(1, keepdims=True)) / 0.08); W[W < 0.02] = 0
nb = len(names); BW = np.zeros((len(V), nb))
for k, (_, _, _, bn) in enumerate(S): bi = names.index(bn); BW[:, bi] = np.maximum(BW[:, bi], W[:, k])
idx = np.argsort(-BW, 1)[:, :4]; wt = np.take_along_axis(BW, idx, 1); wt /= wt.sum(1, keepdims=True); wt[wt < 1e-4] = 0; wt /= wt.sum(1, keepdims=True); idx[wt == 0] = 0
np.save('vex_weights.npy', np.concatenate([idx, wt], 1))
if __name__ == '__main__':
    Cl = np.clip(C, 0, 1) ** 2.2  # texture colors are sRGB; glTF vertex colors are linear
    bin_ = bytearray(); j = {'asset': {'version': '2.0', 'generator': 'HEXBOUND auto-rig (Vex, from launcher_final.glb, decimated)'}, 'buffers': [{}], 'bufferViews': [], 'accessors': []}
    def align():
        while len(bin_) % 4: bin_.append(0)
    def view(data, target=None):
        align(); o = len(bin_); bin_.extend(data); v = {'buffer': 0, 'byteOffset': o, 'byteLength': len(data)}
        if target: v['target'] = target
        j['bufferViews'].append(v); return len(j['bufferViews']) - 1
    def acc(vw, ct, count, typ, **kw):
        a = {'bufferView': vw, 'componentType': ct, 'count': count, 'type': typ}; a.update(kw); j['accessors'].append(a); return len(j['accessors']) - 1
    n = len(V)
    aP = acc(view(V.astype(np.float32).tobytes(), 34962), 5126, n, 'VEC3', min=[float(x) for x in V.min(0)], max=[float(x) for x in V.max(0)])
    aN = acc(view(Nrm.astype(np.float32).tobytes(), 34962), 5126, n, 'VEC3')
    aC = acc(view(np.concatenate([Cl, np.ones((n, 1))], 1).astype(np.float32).tobytes(), 34962), 5126, n, 'VEC4')
    aJ = acc(view(idx.astype(np.uint8).tobytes(), 34962), 5121, n, 'VEC4')
    aW = acc(view(wt.astype(np.float32).tobytes(), 34962), 5126, n, 'VEC4')
    aI = acc(view(F.astype(np.uint32).tobytes(), 34963), 5125, F.size, 'SCALAR')
    j['materials'] = [{'name': 'VexBody', 'pbrMetallicRoughness': {'baseColorFactor': [1, 1, 1, 1], 'metallicFactor': 0.25, 'roughnessFactor': 0.6}, 'doubleSided': True}]
    j['meshes'] = [{'name': 'VexMesh', 'primitives': [{'attributes': {'POSITION': aP, 'NORMAL': aN, 'COLOR_0': aC, 'JOINTS_0': aJ, 'WEIGHTS_0': aW}, 'indices': aI, 'material': 0}]}]
    ibm = []
    for name in names:
        m = np.eye(4, dtype=np.float32); m[:3, 3] = -pos[name]; ibm.append(m.T.flatten())
    ia = acc(view(np.array(ibm, dtype=np.float32).tobytes()), 5126, nb, 'MAT4')
    nodes = [{'name': 'Vex', 'children': [1, 2]}, {'name': 'VexMesh', 'mesh': 0, 'skin': 0}, {'name': 'Armature', 'children': []}]
    base = len(nodes)
    for name in names:
        p = par[name]; local = pos[name] - (pos[p] if p else 0)
        nodes.append({'name': name, 'translation': [float(x) for x in local]})
    for i, name in enumerate(names):
        p = par[name]; (nodes[2] if p is None else nodes[base + names.index(p)]).setdefault('children', []).append(base + i)
    j['nodes'] = nodes; j['scenes'] = [{'name': 'Vex', 'nodes': [0]}]; j['scene'] = 0
    j['skins'] = [{'name': 'VexSkin', 'joints': [base + i for i in range(nb)], 'inverseBindMatrices': ia, 'skeleton': base}]
    align(); j['buffers'][0]['byteLength'] = len(bin_)
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    out = bytearray(b'glTF') + struct.pack('<II', 2, 12 + 8 + len(js) + 8 + len(bin_)) + struct.pack('<I', len(js)) + b'JSON' + js + struct.pack('<I', len(bin_)) + b'BIN\x00' + bin_
    open('out/Launcher.glb', 'wb').write(out); print('wrote', len(out) // 1024, 'KB, bones', nb, 'tris', len(F))
