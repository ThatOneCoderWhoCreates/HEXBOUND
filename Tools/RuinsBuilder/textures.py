import numpy as np, sys
from PIL import Image, ImageFilter
from scipy.spatial import cKDTree
sys.path.insert(0, '.')
from geo import fbm

FLOOR_HALF = 36.0   # RuinsFloor covers x, z in [-36, 36] (world UV)

def noise2(X, Z, scale, oct, seed):
    pts = np.stack([X.ravel() * scale, np.zeros(X.size), Z.ravel() * scale], 1)
    return fbm(pts, oct, seed).reshape(X.shape)

def floor_texture(size=2048, path='RuinsFloor.jpg'):
    s = size
    xs = (np.arange(s) + 0.5) / s * 2 * FLOOR_HALF - FLOOR_HALF
    X, Z = np.meshgrid(xs, -xs)                       # row 0 = +Z
    rng = np.random.RandomState(7)
    # domain warp so the stone joints are not ruler-straight
    wx = X + (noise2(X, Z, 0.8, 2, 71) - 0.5) * 0.25
    wz = Z + (noise2(X, Z, 0.8, 2, 73) - 0.5) * 0.25
    # rectangular flagstones in rows of varying depth, staggered
    edges_z = [-FLOOR_HALF - 1.0]
    while edges_z[-1] < FLOOR_HALF + 1:
        edges_z.append(edges_z[-1] + rng.uniform(0.55, 1.05))
    edges_z = np.array(edges_z)
    r = np.clip(np.searchsorted(edges_z, wz) - 1, 0, len(edges_z) - 2)
    z0 = edges_z[r]; z1 = edges_z[r + 1]
    ez = np.minimum(wz - z0, z1 - wz)
    ex = np.zeros_like(X); sid = np.zeros(X.shape, np.int64)
    for k in range(len(edges_z) - 1):
        m = r == k
        if not m.any(): continue
        cuts = [-FLOOR_HALF - 1.0 - rng.uniform(0, 1)]
        while cuts[-1] < FLOOR_HALF + 1:
            cuts.append(cuts[-1] + rng.uniform(0.5, 1.4))
        cuts = np.array(cuts)
        xx = wx[m]
        c = np.clip(np.searchsorted(cuts, xx) - 1, 0, len(cuts) - 2)
        ex[m] = np.minimum(xx - cuts[c], cuts[c + 1] - xx)
        sid[m] = k * 1000 + c
    edge = np.minimum(ex, ez)
    ids, inv = np.unique(sid, return_inverse=True)
    tint = rng.uniform(-0.10, 0.07, len(ids))[inv].reshape(s, s)
    warm = rng.uniform(-0.02, 0.02, len(ids))[inv].reshape(s, s)
    base = np.array([0.52, 0.48, 0.42])
    img = base + tint[..., None] + warm[..., None] * np.array([1.0, 0.2, -0.8])
    n1 = noise2(X, Z, 1.6, 4, 3); n2 = noise2(X, Z, 7.0, 3, 5); n3 = noise2(X, Z, 0.35, 4, 9)
    img *= (0.82 + 0.3 * n1)[..., None] * (0.92 + 0.14 * n2)[..., None]
    # joints, with chipped corners
    chip = (noise2(X, Z, 6.0, 2, 81) - 0.5) * 0.05
    e2 = edge + chip
    g = np.clip(e2 / 0.035, 0, 1)
    img *= (0.3 + 0.7 * g)[..., None]
    img *= (0.86 + 0.14 * np.clip(e2 / 0.15, 0, 1))[..., None]
    # a few long cracks
    rid = 1 - np.abs(noise2(X, Z, 0.45, 3, 21) - 0.5) * 2
    crack = np.clip((rid - 0.975) / 0.025, 0, 1) * (noise2(X, Z, 0.3, 2, 23) > 0.5)
    img *= (1 - 0.4 * crack)[..., None]
    # dirt and mud where stones are missing
    dirt = np.clip((n3 + (n2 - 0.5) * 0.08 - 0.6) / 0.05, 0, 1)
    mud = np.array([0.34, 0.27, 0.20]) * (0.72 + 0.45 * n2)[..., None]
    img = img * (1 - dirt[..., None]) + mud * dirt[..., None]
    peb = (noise2(X, Z, 14.0, 2, 33) > 0.74) * dirt
    img = img * (1 - 0.5 * peb[..., None]) + np.array([0.56, 0.53, 0.48]) * 0.5 * peb[..., None]
    pud = np.clip((noise2(X, Z, 0.55, 3, 41) - 0.64) / 0.03, 0, 1) * np.clip(dirt * 1.5 - 0.3, 0, 1)
    img = img * (1 - 0.8 * pud[..., None]) + np.array([0.24, 0.28, 0.33]) * 0.8 * pud[..., None]
    # grass in the joints and around the mud
    joint = 1 - g
    grassy = joint * (noise2(X, Z, 0.7, 3, 51) > 0.52) + np.clip(dirt * (1 - dirt) * 4, 0, 1) * (n2 > 0.45)
    grassy = np.clip(grassy, 0, 1) * (noise2(X, Z, 12.0, 2, 61) > 0.4)
    gcol = np.array([0.34, 0.40, 0.17]) * (0.8 + 0.4 * n2)[..., None]
    img = img * (1 - 0.8 * grassy[..., None]) + gcol * 0.8 * grassy[..., None]
    img = np.clip(img, 0, 1)
    Image.fromarray((img * 255).astype(np.uint8)).save(path, quality=88)
    return img

def wall_texture(size=1024, tile=4.0, path='RuinsWall.jpg'):
    """Tileable masonry, covering tile x tile meters (u along the wall, v up)."""
    s = size
    u = (np.arange(s) + 0.5) / s * tile; v = (np.arange(s) + 0.5) / s * tile
    U, Vv = np.meshgrid(u, v[::-1])                   # row 0 = top
    rng = np.random.RandomState(12)
    rows = 8; rh = tile / rows
    row = np.floor(Vv / rh).astype(int)
    fy = Vv / rh - row
    img = np.zeros((s, s, 3)); eu = np.zeros((s, s))
    ids = np.zeros((s, s), int)
    for r in range(rows):
        m = row == r
        # block boundaries along u for this row (tileable)
        cuts = [0.0]
        while cuts[-1] < tile - 0.6:
            cuts.append(cuts[-1] + rng.uniform(0.55, 1.15))
        cuts[-1] = tile
        cuts = np.array(cuts) + rng.uniform(0, 0.4)
        uu = U[m]
        k = np.searchsorted(cuts, uu % tile + (uu % tile < cuts[0]) * tile) - 1
        k = np.clip(k, 0, len(cuts) - 2)
        a = cuts[k]; b = cuts[k + 1]
        uw = uu % tile + (uu % tile < cuts[0]) * tile
        eu[m] = np.minimum(uw - a, b - uw)
        ids[m] = r * 100 + k
    ev = np.minimum(fy, 1 - fy) * rh
    edge = np.minimum(eu, ev)
    tint = {i: rng.uniform(-0.09, 0.07) for i in np.unique(ids)}
    T = np.vectorize(tint.get)(ids)
    base = np.array([0.55, 0.49, 0.40])
    img[:] = base
    img += T[..., None] * np.array([1.0, 0.95, 0.85])
    pts = np.stack([np.cos(U / tile * 2 * np.pi) * tile / 6.28, Vv, np.sin(U / tile * 2 * np.pi) * tile / 6.28], -1).reshape(-1, 3)
    n1 = fbm(pts * 2.0, 4, 2).reshape(s, s); n2 = fbm(pts * 8.0, 3, 4).reshape(s, s)
    img *= (0.8 + 0.32 * n1)[..., None] * (0.9 + 0.16 * n2)[..., None]
    g = np.clip(edge / 0.035, 0, 1)
    img *= (0.3 + 0.7 * g)[..., None]
    img *= (0.84 + 0.16 * np.clip(edge / 0.12, 0, 1))[..., None]
    # moss streaks and stains (darker toward the bottom of each tile is avoided: tileable vertically too)
    moss = np.clip((fbm(pts * 1.2 + 3.3, 3, 8).reshape(s, s) - 0.58) / 0.08, 0, 1) * (n2 > 0.45)
    img = img * (1 - 0.6 * moss[..., None]) + np.array([0.30, 0.36, 0.17]) * 0.6 * moss[..., None]
    img = np.clip(img, 0, 1)
    Image.fromarray((img * 255).astype(np.uint8)).save(path, quality=88)
    return img

if __name__ == '__main__':
    floor_texture(); wall_texture()
    print('ok')
