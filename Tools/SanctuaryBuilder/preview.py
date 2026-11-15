import numpy as np, math, sys, pickle
sys.path.insert(0, '.')
from PIL import Image, ImageDraw
from build import root, rot_y, Part
import build
tex = {'floor': np.asarray(Image.open('Floor.jpg'), float) / 255, 'banner': np.asarray(Image.open('Banner.jpg'), float) / 255}

def collect(n, pos=np.zeros(3), yaw=0.0, out=None, skip=()):
    if out is None: out = []
    if n.name in skip: return out
    p = pos + rot_y(n.pos[None], yaw)[0]; y = yaw + n.yaw
    for part in n.parts:
        V = rot_y(part.V, y) + p
        tri = V[part.F]
        if part.UV is not None:
            uv = part.UV[part.F].mean(1); t = tex[part.mat]
            h, w = t.shape[:2]
            col = t[np.clip((uv[:, 1] * h).astype(int), 0, h - 1), np.clip((uv[:, 0] * w).astype(int), 0, w - 1)]
        else:
            col = part.C[part.F].mean(1)[:, :3]
        emis = np.full(len(tri), part.mat == 'glow')
        out.append((tri, col, emis, part.mat == 'banner'))
    for c in n.children: collect(c, p, y, out, skip)
    return out

def extras():
    out = []
    D = 4.95
    for sx in (-1, 1):
        for sz in (-1, 1):
            for k, (dx, dz, h, r) in enumerate([(0, 0, 3.2, 0.42), (0.45, 0.2, 2.0, 0.28), (-0.35, 0.35, 1.6, 0.25), (0.1, -0.45, 1.4, 0.22)]):
                c = np.array([sx * D + dx, 0.2, sz * D + dz])
                P = build.cyl(c, c + [0, h * 0.75, 0], r, r * 0.9, (0.7, 0.45, 1.0), seg=6)
                Q = build.cyl(c + [0, h * 0.75, 0], c + [0, h, 0], r * 0.9, 0.01, (0.85, 0.6, 1.0), seg=6)
                for X in (P, Q):
                    tri = X.V[X.F]; out.append((tri, np.tile([0.75, 0.5, 1.0], (len(tri), 1)), np.ones(len(tri), bool), False))
    return out

def render(cam, target, fov=55, W=1280, H=720, out='view.png', skip=()):
    tris = collect(root, skip=skip) + extras()
    T = np.concatenate([t[0] for t in tris]); C = np.concatenate([t[1] for t in tris]); E = np.concatenate([t[2] for t in tris]); B = np.concatenate([np.full(len(t[0]), t[3]) for t in tris])
    cam = np.array(cam, float); f = np.array(target, float) - cam; f /= np.linalg.norm(f)
    r = np.cross([0, 1, 0], f); r /= np.linalg.norm(r); u = np.cross(f, r)
    P = T - cam; x = P @ r; y = P @ u; z = P @ f
    vis = (z > 0.3).all(1)
    n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]); n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-12
    facing = (n * (cam - T.mean(1))).sum(1) > 0
    vis &= facing | B
    n[~facing] *= -1
    sun = np.array([0.45, 0.75, -0.5]); sun /= np.linalg.norm(sun)
    lam = np.clip(n @ sun, 0, 1)
    sky = np.clip(n[:, 1], 0, 1)
    shade = 0.42 + 0.75 * lam + 0.15 * sky
    col = C * shade[:, None] * np.array([1.0, 0.93, 0.95])
    col[E] = np.clip(C[E] * 1.5, 0, 1)
    dist = z.mean(1)
    fog = np.clip((dist - 40) / 220, 0, 0.85)[:, None]
    col = col * (1 - fog) + np.array([0.80, 0.70, 0.88]) * fog
    k = (W / 2) / math.tan(math.radians(fov) / 2)
    sx = W / 2 + x / z * k; sy = H / 2 - y / z * k
    order = np.argsort(-dist)
    img = Image.new('RGB', (W, H)); d = ImageDraw.Draw(img)
    for yy in range(H):
        t = yy / H; c = np.array([0.30, 0.22, 0.52]) * (1 - t) + np.array([0.92, 0.74, 0.86]) * t
        d.line([(0, yy), (W, yy)], fill=tuple((c * 255).astype(int)))
    # cloud sea
    d.rectangle([0, int(H / 2 - (-8 - cam[1]) / 200 * k * 0 + 0), W, H], fill=None)
    cols = (np.clip(col, 0, 1) * 255).astype(int)
    for i in order:
        if not vis[i]: continue
        if abs(sx[i]).max() > 4 * W or abs(sy[i]).max() > 4 * H: continue
        d.polygon([(sx[i, 0], sy[i, 0]), (sx[i, 1], sy[i, 1]), (sx[i, 2], sy[i, 2])], fill=tuple(cols[i]))
    img.save(out); print('saved', out)

if __name__ == '__main__':
    render((0, 3.2, -11), (0, 4, 20), out='/tmp/v1.png')
    render((0, 3.2, 11), (0, 1.5, -20), out='/tmp/v2.png')
    render((-30, 22, -42), (0, 0, 8), fov=60, out='/tmp/v3.png')
    render((0, 120, -0.01), (0, 0, 0), fov=50, out='/tmp/v4.png')
