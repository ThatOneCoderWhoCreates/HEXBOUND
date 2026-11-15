import trimesh, numpy as np, fast_simplification, sys, json, struct
from scipy.spatial import cKDTree
def load(path):
    s = trimesh.load(path)
    ms = [g.copy() for g in s.dump()] if hasattr(s, 'dump') else [s]
    V = []; F = []; C = []; off = 0
    for m in ms:
        V.append(m.vertices); F.append(m.faces + off); off += len(m.vertices)
        vc = m.visual.vertex_colors if hasattr(m.visual, 'vertex_colors') else None
        C.append(np.asarray(vc)[:, :4] / 255.0 if vc is not None else np.ones((len(m.vertices), 4)))
    return np.vstack(V), np.vstack(F), np.vstack(C)
def decimate(V, F, C, target):
    if len(F) <= target: return V, F, C
    red = 1 - target / len(F)
    v2, f2 = fast_simplification.simplify(V.astype(np.float32), F.astype(np.int32), target_reduction=red)
    tree = cKDTree(V); d, idx = tree.query(v2, k=4)
    w = 1 / (d + 1e-6); w /= w.sum(1, keepdims=True)
    c2 = (C[idx] * w[..., None]).sum(1)
    return v2, f2, c2
if __name__ == '__main__':
    V, F, C = load(sys.argv[1]); print(len(V), len(F), C.min(0), C.max(0), C.mean(0))
