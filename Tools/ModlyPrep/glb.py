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
