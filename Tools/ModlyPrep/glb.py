import json, struct, numpy as np
CT = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8, 5122: np.int16, 5120: np.int8}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}
def read(path):
    raw = open(path, 'rb').read()
    jl = struct.unpack('<I', raw[12:16])[0]; j = json.loads(raw[20:20 + jl])
    bo = 20 + jl; bl = struct.unpack('<I', raw[bo:bo + 4])[0]; b = raw[bo + 8:bo + 8 + bl]
    def acc(i):
        a = j['accessors'][i]; v = j['bufferViews'][a['bufferView']]
        dt = CT[a['componentType']]; n = NC[a['type']]
        off = v.get('byteOffset', 0) + a.get('byteOffset', 0)
        stride = v.get('byteStride', 0); isz = np.dtype(dt).itemsize * n
        if stride and stride != isz:
            arr = np.frombuffer(b, np.uint8, count=stride * a['count'], offset=off).reshape(a['count'], stride)[:, :isz].copy().view(dt).reshape(a['count'], n)
        else:
            arr = np.frombuffer(b, dt, count=a['count'] * n, offset=off).reshape(a['count'], n)
        arr = arr.astype(np.float64)
        if a.get('normalized'): arr /= np.iinfo(dt).max
        return arr
    Vs = []; Fs = []; Cs = []; off = 0
    for m in j['meshes']:
        for p in m['primitives']:
            V = acc(p['attributes']['POSITION'])
            F = acc(p['indices']).astype(np.int64).reshape(-1, 3)
            C = acc(p['attributes']['COLOR_0']) if 'COLOR_0' in p['attributes'] else np.ones((len(V), 4))
            if C.shape[1] == 3: C = np.hstack([C, np.ones((len(C), 1))])
            Vs.append(V); Fs.append(F + off); Cs.append(C); off += len(V)
    return np.vstack(Vs), np.vstack(Fs), np.vstack(Cs), j

def read_textured(path):
    """Like read(), but bakes the base color texture into per-vertex colors (bilinear sample at each vertex UV)."""
    import io
    from PIL import Image
    raw = open(path, 'rb').read()
    jl = struct.unpack('<I', raw[12:16])[0]; j = json.loads(raw[20:20 + jl])
    bo = 20 + jl; bl = struct.unpack('<I', raw[bo:bo + 4])[0]; b = raw[bo + 8:bo + 8 + bl]
    V, F, C, _ = read(path)
    imgs = []
    for im in j.get('images', []):
        v = j['bufferViews'][im['bufferView']]
        imgs.append(np.asarray(Image.open(io.BytesIO(b[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']])).convert('RGBA'), np.float64) / 255)
    if not imgs: return V, F, C, j
    def acc(i):
        a = j['accessors'][i]; v = j['bufferViews'][a['bufferView']]
        dt = CT[a['componentType']]; n = NC[a['type']]; off = v.get('byteOffset', 0) + a.get('byteOffset', 0)
        arr = np.frombuffer(b, dt, count=a['count'] * n, offset=off).reshape(a['count'], n).astype(np.float64)
        if a.get('normalized'): arr /= np.iinfo(dt).max
        return arr
    UV = []
    for m in j['meshes']:
        for p in m['primitives']:
            UV.append(acc(p['attributes']['TEXCOORD_0']))
    UV = np.vstack(UV)
    tex = imgs[0]; h, w = tex.shape[:2]
    x = np.clip((UV[:, 0] % 1.0) * (w - 1), 0, w - 1); y = np.clip((UV[:, 1] % 1.0) * (h - 1), 0, h - 1)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int); x1 = np.minimum(x0 + 1, w - 1); y1 = np.minimum(y0 + 1, h - 1)
    fx = (x - x0)[:, None]; fy = (y - y0)[:, None]
    c = tex[y0, x0] * (1 - fx) * (1 - fy) + tex[y0, x1] * fx * (1 - fy) + tex[y1, x0] * (1 - fx) * fy + tex[y1, x1] * fx * fy
    return V, F, c, j
