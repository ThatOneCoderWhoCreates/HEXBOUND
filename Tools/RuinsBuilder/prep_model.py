"""Modly shape-only output -> decimated, stone-colored glb (vertex colors: sandstone, crevice darkening, moss on top faces)."""
import sys, numpy as np, trimesh
sys.path.insert(0, '/home/claude/work/modly'); sys.path.insert(0, '/home/claude/work'); sys.path.insert(0, '.')
from proc import load, decimate
from glbwrite import write
from geo import fbm
from render import render, roty

def stone_colors(V, F, seed=1, moss_amount=0.5):
    tm = trimesh.Trimesh(V, F, process=False)
    N = np.asarray(tm.vertex_normals)
    size = np.ptp(V, 0).max()
    P = V / size * 6.0
    base = np.array([0.56, 0.50, 0.41])
    n1 = fbm(P * 1.5, 4, seed); n2 = fbm(P * 6.0, 3, seed + 3)
    c = base[None] * (0.78 + 0.32 * n1)[:, None] * (0.9 + 0.16 * n2)[:, None]
    warm = (fbm(P * 0.8, 2, seed + 7) - 0.5)[:, None] * np.array([0.06, 0.02, -0.04])
    c = c + warm
    # crevices: vertices sitting below the average of their neighbors (concave) get darker
    adj = tm.vertex_neighbors
    mean_nb = np.array([V[a].mean(0) if len(a) else V[i] for i, a in enumerate(adj)])
    concav = ((mean_nb - V) * N).sum(1) / (size * 0.01)
    cav = np.clip(concav, 0, 1.5)
    c *= (1 - 0.35 * cav / 1.5)[:, None]
    # moss and grime on upward faces near the bottom and on ledges
    up = np.clip(N[:, 1], 0, 1)
    y = (V[:, 1] - V[:, 1].min()) / max(np.ptp(V[:, 1]), 1e-6)
    moss = np.clip(up * 1.4 - 0.6, 0, 1) * (fbm(P * 2.2, 3, seed + 11) > 0.5) * moss_amount
    moss = np.maximum(moss, np.clip(0.25 - y, 0, 0.25) * 2 * (fbm(P * 3, 2, seed + 13) > 0.45) * moss_amount)
    c = c * (1 - moss[:, None]) + np.array([0.30, 0.36, 0.16]) * moss[:, None]
    # darker toward the ground
    c *= (0.8 + 0.2 * np.clip(y * 3, 0, 1))[:, None]
    return np.hstack([np.clip(c, 0, 1), np.ones((len(V), 1))])

if __name__ == '__main__':
    src, name, target = sys.argv[1], sys.argv[2], int(sys.argv[3])
    V, F, C = load(src)
    v, f, c = decimate(V, F, C, target)
    # clean: keep the largest pieces only (drop tiny floating bits)
    tm = trimesh.Trimesh(v, f, process=True)
    parts = tm.split(only_watertight=False)
    if len(parts) > 1:
        big = [p for p in parts if len(p.faces) > len(tm.faces) * 0.01]
        tm = trimesh.util.concatenate(big)
    v = np.asarray(tm.vertices); f = np.asarray(tm.faces)
    col = stone_colors(v, f)
    write(name, v, f, col, 'models/%s.glb' % name)
    views = [('front +Z', roty(0)), ('right', roty(-90)), ('back', roty(180)), ('top', np.array([[1, 0, 0], [0, 0, -1], [0, 1, 0]]))]
    render(v, f, col, views, '/tmp/m_%s.png' % name, size=4)
    print(name, len(f), 'bounds', v.min(0).round(2), v.max(0).round(2))
