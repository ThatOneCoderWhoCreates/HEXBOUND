import numpy as np, trimesh, math

# ---------------- noise ----------------
def _hash(ix, iy, iz, seed):
    h = (ix * 374761393 + iy * 668265263 + iz * 2147483647 + seed * 144665) & 0xffffffff
    h = (h ^ (h >> 13)) * 1274126177 & 0xffffffff
    return ((h ^ (h >> 16)) & 0xffffff) / float(0xffffff)

def vnoise(p, seed=0):
    p = np.asarray(p, dtype=np.float64)
    i = np.floor(p).astype(np.int64); f = p - i
    u = f * f * (3 - 2 * f)
    def h(dx, dy, dz):
        return _hash(i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz, seed)
    x00 = h(0,0,0)*(1-u[:,0]) + h(1,0,0)*u[:,0]
    x10 = h(0,1,0)*(1-u[:,0]) + h(1,1,0)*u[:,0]
    x01 = h(0,0,1)*(1-u[:,0]) + h(1,0,1)*u[:,0]
    x11 = h(0,1,1)*(1-u[:,0]) + h(1,1,1)*u[:,0]
    y0 = x00*(1-u[:,1]) + x10*u[:,1]; y1 = x01*(1-u[:,1]) + x11*u[:,1]
    return y0*(1-u[:,2]) + y1*u[:,2]

def fbm(p, oct=4, seed=0):
    p = np.asarray(p, dtype=np.float64); s = 0; a = 0.5; tot = 0
    for o in range(oct):
        s = s + a * vnoise(p * (2 ** o), seed + o * 17); tot += a; a *= 0.5
    return s / tot

# ---------------- mesh part ----------------
class Part:
    def __init__(self, V, F, C, mat='vc', UV=None, flat=False):
        self.V = np.asarray(V, np.float64); self.F = np.asarray(F, np.int64)
        C = np.asarray(C, np.float64)
        if C.ndim == 1: C = np.tile(C, (len(self.V), 1))
        if C.shape[1] == 3: C = np.hstack([C, np.ones((len(C), 1))])
        self.C = C; self.mat = mat; self.UV = UV; self.flat = flat

def merge(parts):
    out = {}
    for p in parts:
        out.setdefault((p.mat, p.flat, p.UV is not None), []).append(p)
    res = []
    for (mat, flat, uv), ps in out.items():
        V = []; F = []; C = []; U = []; off = 0
        for p in ps:
            V.append(p.V); F.append(p.F + off); C.append(p.C); off += len(p.V)
            if uv: U.append(p.UV)
        res.append(Part(np.vstack(V), np.vstack(F), np.vstack(C), mat, np.vstack(U) if uv else None, flat))
    return res

def from_tm(m, color, mat='vc', flat=False):
    return Part(m.vertices, m.faces, color, mat, flat=flat)

def tint(V, base, var=0.08, scale=0.6, seed=1):
    n = fbm(V * scale, 3, seed)[:, None]
    c = np.asarray(base, np.float64)[None, :3] * (1 - var + 2 * var * n)
    return np.clip(c, 0, 1)

def box(center, size, color, yaw=0.0, mat='vc', var=0.06):
    m = trimesh.creation.box(extents=size)
    if yaw: m.apply_transform(trimesh.transformations.rotation_matrix(yaw, [0, 1, 0]))
    m.apply_translation(center)
    return Part(m.vertices, m.faces, tint(m.vertices, color, var), mat, flat=True)

def cyl(p0, p1, r0, r1, color, seg=10, mat='vc', var=0.06, flat=False, caps=True):
    p0 = np.asarray(p0, float); p1 = np.asarray(p1, float)
    d = p1 - p0; L = np.linalg.norm(d); d = d / L
    a = np.array([1.0, 0, 0]) if abs(d[0]) < 0.9 else np.array([0, 0, 1.0])
    u = np.cross(d, a); u /= np.linalg.norm(u); v = np.cross(d, u)
    t = np.linspace(0, 2 * np.pi, seg, endpoint=False)
    ring0 = p0 + r0 * (np.cos(t)[:, None] * u + np.sin(t)[:, None] * v)
    ring1 = p1 + r1 * (np.cos(t)[:, None] * u + np.sin(t)[:, None] * v)
    V = np.vstack([ring0, ring1]); F = []
    for i in range(seg):
        j = (i + 1) % seg
        F += [[i, j, seg + j], [i, seg + j, seg + i]]
    if caps:
        c0 = len(V); V = np.vstack([V, p0, p1])
        for i in range(seg):
            j = (i + 1) % seg
            F += [[c0, j, i], [c0 + 1, seg + i, seg + j]]
    return Part(V, np.array(F), tint(V, color, var), mat, flat=flat)

def lathe(profile, seg, color, a0=0.0, a1=2*np.pi, namp=0.0, nscale=0.3, seed=3, mat='vc', flat=True, colfn=None, closed_top=False, closed_bottom=False):
    """profile: list of (r, y). Revolved around Y. Angle 0 points +Z, increasing toward +X."""
    full = abs(a1 - a0 - 2 * np.pi) < 1e-6
    n = seg if full else seg + 1
    ang = np.linspace(a0, a1, seg, endpoint=False) if full else np.linspace(a0, a1, seg + 1)
    pr = np.asarray(profile, float); rows = len(pr)
    V = np.zeros((rows * n, 3))
    for k, (r, y) in enumerate(pr):
        V[k*n:(k+1)*n, 0] = np.sin(ang) * r; V[k*n:(k+1)*n, 2] = np.cos(ang) * r; V[k*n:(k+1)*n, 1] = y
    if namp:
        dirs = V.copy(); dirs[:, 1] = 0; ln = np.linalg.norm(dirs, axis=1, keepdims=True) + 1e-9
        nn = fbm(V * nscale, 4, seed) - 0.5
        rmask = (pr[:, 0] > 0.01).repeat(n)
        V += (dirs / ln) * (nn * namp * 2)[:, None] * rmask[:, None]
        V[:, 1] += (fbm(V * nscale + 7.7, 3, seed + 5) - 0.5) * namp * 0.6 * rmask
    F = []
    for k in range(rows - 1):
        for i in range(seg):
            j = (i + 1) % n if full else i + 1
            a = k*n + i; b = k*n + j; c = (k+1)*n + j; d = (k+1)*n + i
            F += [[a, d, c], [a, c, b]]
    F = np.array(F)
    C = colfn(V) if colfn else tint(V, color, 0.1)
    return Part(V, F, C, mat, flat=flat)

def ico(center, radius, color, sub=2, namp=0.0, seed=0, squash=1.0, mat='vc', var=0.1, flat=True):
    m = trimesh.creation.icosphere(subdivisions=sub, radius=1.0)
    V = m.vertices.copy()
    if namp: V *= (1 + (fbm(V * 1.7 + seed, 3, seed) - 0.5) * namp * 2)[:, None]
    V[:, 1] *= squash; V = V * radius + np.asarray(center)
    return Part(V, m.faces, tint(V, color, var, 0.8, seed), mat, flat=flat)

def ring_sector(r0, r1, y0, y1, a0, a1, seg, color, mat='vc', var=0.06):
    """Solid annular sector between radii and heights."""
    t = np.linspace(a0, a1, seg + 1)
    V = []; F = []
    for (r, y) in [(r0, y1), (r1, y1), (r1, y0), (r0, y0)]:
        V.append(np.stack([np.sin(t) * r, np.full_like(t, y), np.cos(t) * r], 1))
    V = np.vstack(V); n = seg + 1
    for ring in range(4):
        nr = (ring + 1) % 4
        for i in range(seg):
            a = ring*n + i; b = ring*n + i + 1; c = nr*n + i + 1; d = nr*n + i
            F += [[a, b, c], [a, c, d]]
    # end caps
    for i in [0, seg]:
        q = [0*n+i, 1*n+i, 2*n+i, 3*n+i]
        if i == 0: F += [[q[0], q[2], q[1]], [q[0], q[3], q[2]]]
        else: F += [[q[0], q[1], q[2]], [q[0], q[2], q[3]]]
    F = np.array(F)
    return Part(V, F, tint(V, color, var), mat, flat=True)

def torus_arc(R, r, a0, a1, seg, color, seed=0, namp=0.0):
    """Arc in the XY plane (vertical), square-ish cross section of half-size r, centered at origin. Angle from +Y toward +X."""
    t = np.linspace(a0, a1, seg + 1)
    cs = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    V = []
    for ti in t:
        c = np.array([np.sin(ti) * R, np.cos(ti) * R, 0.0]); out = np.array([np.sin(ti), np.cos(ti), 0.0]); z = np.array([0, 0, 1.0])
        for (u, w) in cs:
            V.append(c + out * u * r + z * w * r * 0.8)
    V = np.array(V); F = []
    for i in range(seg):
        for k in range(4):
            a = i*4 + k; b = i*4 + (k+1) % 4; c = (i+1)*4 + (k+1) % 4; d = (i+1)*4 + k
            F += [[a, d, c], [a, c, b]]
    F += [[0, 2, 1], [0, 3, 2]]; e = seg * 4; F += [[e, e+1, e+2], [e, e+2, e+3]]
    V = V + ((fbm(V * 0.4, 3, seed) - 0.5) * namp)[:, None] if namp else V
    return Part(V, np.array(F), tint(V, color, 0.12, 0.3, seed), 'vc', flat=True)
