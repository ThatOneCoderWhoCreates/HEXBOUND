import numpy as np, math, sys, pickle
sys.path.insert(0, '.')
from PIL import Image, ImageDraw
import build
from build import Part
tex = {'floor': np.asarray(Image.open('RuinsFloor.jpg'), float) / 255, 'wall': np.asarray(Image.open('RuinsWall.jpg'), float) / 255}
build.rng = np.random.RandomState(3)
root = build.build_root()

def rot_y(V, yaw):
    c, s = math.cos(yaw), math.sin(yaw); V = np.asarray(V, float).copy(); x = V[:, 0].copy(); z = V[:, 2].copy()
    V[:, 0] = x * c + z * s; V[:, 2] = -x * s + z * c; return V

def collect(n, pos=np.zeros(3), yaw=0.0, out=None):
    if out is None: out = []
    p = pos + rot_y(n.pos[None], yaw)[0]; y = yaw + n.yaw
    for part in n.parts:
        V = rot_y(part.V, y) + p; tri = V[part.F]
        if part.UV is not None:
            uv = part.UV[part.F].mean(1) % 1.0; t = tex[part.mat]; h, w = t.shape[:2]
            col = t[np.clip((uv[:, 1] * h).astype(int), 0, h - 1), np.clip((uv[:, 0] * w).astype(int), 0, w - 1)]
        else:
            col = part.C[part.F].mean(1)[:, :3]
        out.append((tri, col))
    for c in n.children: collect(c, p, y, out)
    return out

def fighters():
    out = []
    for z in (-14.5, 14.5):
        c = build.cyl((0, 1.6, z), (0, 3.4, z), 0.35, 0.3, (0.8, 0.2, 0.2) if z < 0 else (0.2, 0.3, 0.9), seg=8)
        out.append((c.V[c.F], c.C[c.F].mean(1)[:, :3]))
    return out

def render(cam, target, fov=55, W=1280, H=720, out='view.png'):
    tris = collect(root) + fighters()
    T = np.concatenate([t[0] for t in tris]); C = np.concatenate([t[1] for t in tris])
    cam = np.array(cam, float); f = np.array(target, float) - cam; f /= np.linalg.norm(f)
    r = np.cross([0, 1, 0], f); r /= np.linalg.norm(r); u = np.cross(f, r)
    P = T - cam; x = P @ r; y = P @ u; z = P @ f
    vis = (z > 0.3).all(1)
    n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]); n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-12
    facing = (n * (cam - T.mean(1))).sum(1) > 0
    vis &= facing
    sun = np.array([-0.45, 0.7, -0.55]); sun /= np.linalg.norm(sun)
    lam = np.clip(n @ sun, 0, 1); sky = np.clip(n[:, 1], 0, 1)
    shade = 0.38 + 0.8 * lam + 0.15 * sky
    col = C * shade[:, None] * np.array([1.0, 0.96, 0.9])
    dist = z.mean(1)
    fog = np.clip((dist - 60) / 320, 0, 0.8)[:, None]
    col = col * (1 - fog) + np.array([0.70, 0.74, 0.80]) * fog
    k = (W / 2) / math.tan(math.radians(fov) / 2)
    sx = W / 2 + x / z * k; sy = H / 2 - y / z * k
    order = np.argsort(-dist)
    img = Image.new('RGB', (W, H)); d = ImageDraw.Draw(img)
    for yy in range(H):
        t = yy / H; c = np.array([0.42, 0.55, 0.72]) * (1 - t) + np.array([0.86, 0.86, 0.86]) * t
        d.line([(0, yy), (W, yy)], fill=tuple((c * 255).astype(int)))
    cols = (np.clip(col, 0, 1) * 255).astype(int)
    for i in order:
        if not vis[i]: continue
        if abs(sx[i]).max() > 4 * W or abs(sy[i]).max() > 4 * H: continue
        d.polygon([(sx[i, 0], sy[i, 0]), (sx[i, 1], sy[i, 1]), (sx[i, 2], sy[i, 2])], fill=tuple(cols[i]))
    img.save(out); print('saved', out)

if __name__ == '__main__':
    render((-22, 20, -30), (0, 0, 2), fov=60, out='/tmp/r1.png')
    render((0, 6, -24), (0, 1, 0), fov=60, out='/tmp/r2.png')
    render((0, 70, -0.01), (0, 0, 0), fov=45, out='/tmp/r3.png')
