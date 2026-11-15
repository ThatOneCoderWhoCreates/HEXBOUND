import numpy as np, math, sys, json, struct, re, trimesh
sys.path.insert(0, '.')
from geo import *

STONE = (0.55, 0.50, 0.42); STONE_D = (0.40, 0.36, 0.31); ROCK = (0.42, 0.39, 0.36); ROCK_D = (0.28, 0.26, 0.25)
MOSS = (0.30, 0.36, 0.17); SNOW = (0.92, 0.93, 0.96); DIRT = (0.36, 0.29, 0.22)
FLOOR_HALF = 36.0
WALL_TILE = 4.0

class Node:
    def __init__(self, name, pos=(0, 0, 0), yaw=0.0, parent=None):
        self.name = name; self.pos = np.array(pos, float); self.yaw = yaw; self.parts = []; self.children = []
        if parent: parent.children.append(self)
    def add(self, *ps):
        for p in ps:
            if isinstance(p, list): self.parts += p
            else: self.parts.append(p)

# ---------------- simulation layout (read from Arena.cs so the visuals always match) ----------------
ARENA_CS = '/home/claude/hex/Scripts/Runtime/Simulation/Collision/Arena.cs'
def read_terrain():
    src = open(ARENA_CS).read()
    half = src[src.index('var ruinsHalf'):src.index('var ruinsTerrain')]
    boxes = []
    def parse(v):
        return dict(x0=v[0]/1000, x1=v[1]/1000, z0=v[2]/1000, z1=v[3]/1000, h=v[4]/1000, ramp=v[5] if len(v) > 5 else 0,
                    group=v[6] if len(v) > 6 else -1, bottom=v[7]/1000 if len(v) > 7 else 0.0)
    for m in re.finditer(r'new TerrainBox\(([-\d, ]+)\)', half):
        boxes.append(parse([int(x) for x in m.group(1).split(',')]))
    mir = []
    for b in boxes:
        r = b['ramp']; r = {1: 2, 2: 1, 3: 4, 4: 3}.get(r, r)
        mir.append(dict(x0=-b['x1'], x1=-b['x0'], z0=-b['z1'], z1=-b['z0'], h=b['h'], ramp=r,
                        group=(b['group'] ^ 1) if b['group'] >= 0 else -1, bottom=b['bottom']))
    altar = re.search(r'ruinsTerrain\[ruinsTerrain.Length - 1\] = new TerrainBox\(([-\d, ]+)\)', src).group(1)
    return boxes + mir + [parse([int(x) for x in altar.split(',')])]

def read_edge():
    src = open(ARENA_CS).read()
    blk = src[src.index('Name = "Sky Ruins"'):]
    blk = blk[blk.index('{') + 1: blk.index('}')]
    raw = [int(x) for x in re.findall(r'\d+', blk)]
    assert len(raw) == 72
    return np.array(raw) / 65536.0

TERRAIN = read_terrain()
EDGE = read_edge()

def edge_radius(a):
    """a: radians, 0 = +Z toward +X (same as the simulation)."""
    t = (a % (2 * np.pi)) / (2 * np.pi) * 72
    i = np.floor(t).astype(int) % 72; f = t - np.floor(t)
    return EDGE[i] * (1 - f) + EDGE[(i + 1) % 72] * f

# ---------------- textured pieces ----------------
def floor_uv(V):
    return np.stack([(V[:, 0] + FLOOR_HALF) / (2 * FLOOR_HALF), (FLOOR_HALF - V[:, 2]) / (2 * FLOOR_HALF)], 1)

def quad_parts(quads, mat):
    """quads: list of 4 corner points (counter-clockwise seen from outside). Returns a flat Part with UVs."""
    V = []; F = []; UV = []
    for q in quads:
        q = np.asarray(q, float); o = len(V)
        V += list(q); F += [[o, o + 1, o + 2], [o, o + 2, o + 3]]
        if mat == 'floor':
            UV += list(floor_uv(q))
        else:
            n = np.cross(q[1] - q[0], q[2] - q[0])
            along = 0 if abs(n[0]) < abs(n[2]) else 2   # wall facing +-Z runs along X, facing +-X runs along Z
            for p in q:
                UV.append((p[along] / WALL_TILE + (0.37 if along == 2 else 0.0), -p[1] / WALL_TILE))
    V = np.array(V)
    return Part(V, np.array(F), np.tile([1, 1, 1], (len(V), 1)), mat, np.array(UV), flat=True)

def block(x0, x1, z0, z1, y0, y1, top=True, top_mat='floor', bottom=False):
    q = []
    q.append([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)][::-1])   # -Z face
    q.append([(x1, y0, z1), (x0, y0, z1), (x0, y1, z1), (x1, y1, z1)][::-1])   # +Z face
    q.append([(x0, y0, z1), (x0, y0, z0), (x0, y1, z0), (x0, y1, z1)][::-1])   # -X face
    q.append([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)][::-1])   # +X face
    parts = [quad_parts(q, 'wall')]
    if top:
        parts.append(quad_parts([[(x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0)]], top_mat))
    return parts

def fix_winding(p):
    """Make every triangle face away from the part's center (boxes built above may come out inside-out)."""
    c = p.V.mean(0); T = p.V[p.F]
    n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]); m = T.mean(1) - c
    flip = (n * m).sum(1) < 0
    p.F[flip] = p.F[flip][:, [0, 2, 1]]
    return p

rng = np.random.RandomState(3)

def floor_part():
    seg = 288
    a = np.linspace(0, 2 * np.pi, seg, endpoint=False)
    r = edge_radius(a)
    rings = [0.0, 0.35, 0.7, 1.0]
    V = [(0.0, 0.0, 0.0)]
    for k in rings[1:]:
        for i in range(seg):
            V.append((np.sin(a[i]) * r[i] * k, 0.0, np.cos(a[i]) * r[i] * k))
    V = np.array(V); F = []
    for i in range(seg):
        j = (i + 1) % seg
        F.append([0, 1 + j, 1 + i])
    for k in range(len(rings) - 2):
        b0 = 1 + k * seg; b1 = 1 + (k + 1) * seg
        for i in range(seg):
            j = (i + 1) % seg
            F += [[b0 + i, b0 + j, b1 + j], [b0 + i, b1 + j, b1 + i]]
    F = np.array(F)
    # faces must point up: check the first triangle
    T = V[F[:5]]; n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    if n[:, 1].mean() < 0: F = F[:, [0, 2, 1]]
    return Part(V, F, np.tile([1, 1, 1], (len(V), 1)), 'floor', floor_uv(V), flat=False)

def cliff_part():
    seg = 288
    a = np.linspace(0, 2 * np.pi, seg, endpoint=False)
    r = edge_radius(a)
    prof = [(0.0, 0.0), (0.12, -0.25), (0.35, -0.9), (0.9, -2.6), (1.6, -5.5), (1.2, -10), (-0.5, -18), (-3.5, -28), (-7, -40), (-10, -55)]
    rows = len(prof)
    V = np.zeros((rows * seg, 3))
    for k, (dr, y) in enumerate(prof):
        rr = r + dr if k < 6 else r * (1 + dr / 14.0)
        V[k*seg:(k+1)*seg, 0] = np.sin(a) * rr; V[k*seg:(k+1)*seg, 2] = np.cos(a) * rr; V[k*seg:(k+1)*seg, 1] = y
    # rocky displacement, none on the top ring (it meets the floor edge)
    dirs = V.copy(); dirs[:, 1] = 0; dirs /= np.linalg.norm(dirs, axis=1, keepdims=True) + 1e-9
    n = fbm(V * 0.22, 4, 5) - 0.5; n2 = fbm(V * 0.9, 3, 7) - 0.5
    amp = np.clip(-V[:, 1] / 2.5, 0, 1) * (2.6 * n + 0.6 * n2)
    V += dirs * amp[:, None]
    V[:, 1] += (fbm(V * 0.3 + 4.4, 3, 9) - 0.5) * 1.6 * np.clip(-V[:, 1] / 3, 0, 1)
    F = []
    for k in range(rows - 1):
        for i in range(seg):
            j = (i + 1) % seg
            p = k*seg + i; q = k*seg + j; s_ = (k+1)*seg + j; t = (k+1)*seg + i
            F += [[p, t, s_], [p, s_, q]]
    F = np.array(F)
    T = V[F[:20]]; nn = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]); out = T.mean(1).copy(); out[:, 1] = 0
    if (nn * out).sum(1).mean() < 0: F = F[:, [0, 2, 1]]
    y = V[:, 1]
    c = np.array(ROCK)[None] * (0.75 + 0.5 * fbm(V * 0.5, 3, 2))[:, None]
    strata = 0.9 + 0.12 * np.sin(y * 1.7 + fbm(V * 0.3, 2, 4) * 6)
    c *= strata[:, None]
    moss = np.clip(1 - (-y) / 4, 0, 1) * (fbm(V * 0.4, 3, 11) > 0.5)
    c = c * (1 - moss[:, None] * 0.7) + np.array(MOSS) * moss[:, None] * 0.7
    c *= (0.6 + 0.4 * np.clip((y + 55) / 50, 0, 1))[:, None]
    return Part(V, F, np.clip(c, 0, 1), 'vc', flat=False)

# ---------------- terrain blocks ----------------
def steps(b, top_mat='floor'):
    """Visible stairs for a ramp block: steps of about 0.2 m."""
    n = max(3, int(round(b['h'] / 0.2)))
    parts = []
    for k in range(n):
        y1 = b['h'] * (k + 1) / n
        f0 = k / n; f1 = 1.0
        x0, x1, z0, z1 = b['x0'], b['x1'], b['z0'], b['z1']
        L = (x1 - x0) if b['ramp'] in (1, 2) else (z1 - z0)
        if b['ramp'] == 1: x0 = b['x0'] + L * f0
        elif b['ramp'] == 2: x1 = b['x1'] - L * f0
        elif b['ramp'] == 3: z0 = b['z0'] + L * f0
        else: z1 = b['z1'] - L * f0
        parts += block(x0, x1, z0, z1, 0.0 if k == 0 else b['h'] * k / n - 0.01, y1, True, top_mat)
    return parts

def ragged_wall(b):
    """A broken wall: a solid core up to just below its height, then loose blocks of uneven height on top."""
    parts = []
    h = b['h']; core = h - 0.22
    parts += block(b['x0'], b['x1'], b['z0'], b['z1'], 0.0, core, True, 'wall')
    along_x = (b['x1'] - b['x0']) >= (b['z1'] - b['z0'])
    L = (b['x1'] - b['x0']) if along_x else (b['z1'] - b['z0'])
    s = 0.0
    while s < L - 0.05:
        w = min(L - s, rng.uniform(0.45, 0.9))
        top = core + rng.uniform(0.1, 0.36)
        inset = rng.uniform(0.0, 0.05)
        if rng.rand() < 0.18: s += w; continue      # a missing block
        if along_x: parts += block(b['x0'] + s + 0.01, b['x0'] + s + w - 0.01, b['z0'] + inset, b['z1'] - inset, core, top, True, 'wall')
        else: parts += block(b['x0'] + inset, b['x1'] - inset, b['z0'] + s + 0.01, b['z0'] + s + w - 0.01, core, top, True, 'wall')
        s += w
    return parts

def column(cx, cz, y0, y1, w, broken=False):
    parts = []
    parts += block(cx - w/2 - 0.08, cx + w/2 + 0.08, cz - w/2 - 0.08, cz + w/2 + 0.08, y0, y0 + 0.35, True, 'wall')     # base
    top = y1 - (0.0 if broken else 0.35)
    shaft = cyl((cx, y0 + 0.35, cz), (cx, top, cz), w * 0.5, w * 0.46, STONE, seg=12, flat=True)
    if broken:
        # jagged top: push the top ring's vertices down by noise
        Vt = shaft.V; ring = np.abs(Vt[:, 1] - top) < 1e-6
        Vt[ring, 1] -= np.abs(fbm(Vt[ring] * 3.0 + cx, 2, 5) - 0.3) * 0.6
    parts.append(shaft)
    if not broken:
        parts += block(cx - w/2 - 0.12, cx + w/2 + 0.12, cz - w/2 - 0.12, cz + w/2 + 0.12, y1 - 0.35, y1, True, 'wall')   # capital
    return parts


def platform_level(b):
    # the platform a column stands on (if any)
    cx = (b['x0'] + b['x1']) / 2; cz = (b['z0'] + b['z1']) / 2; best = 0.0
    for o in TERRAIN:
        if o is b or is_column(o) or o['ramp'] or o['bottom'] > 0: continue
        if o['x0'] <= cx <= o['x1'] and o['z0'] <= cz <= o['z1'] and o['h'] < b['h']: best = max(best, o['h'])
    return best

def block_parts(b):
    """Visual for one simulation block."""
    parts = []
    w = b['x1'] - b['x0']; d = b['z1'] - b['z0']
    if b['ramp']:
        parts += steps(b)
    elif b['bottom'] > 0:
        # roof or bridge deck: exact solid slab (walkable top), with a slightly wider stone course along its underside
        parts += block(b['x0'], b['x1'], b['z0'], b['z1'], b['bottom'], b['h'], True, 'floor')
        parts += block(b['x0'] - 0.12, b['x1'] + 0.12, b['z0'] - 0.12, b['z1'] + 0.12, b['bottom'], b['bottom'] + 0.18, False)
    elif is_column(b):
        base = platform_level(b)
        if b['group'] >= 0:
            # square stone pier holding up a roof or deck
            parts += block(b['x0'] - 0.08, b['x1'] + 0.08, b['z0'] - 0.08, b['z1'] + 0.08, base, base + 0.3, True, 'wall')
            parts += block(b['x0'], b['x1'], b['z0'], b['z1'], base + 0.3, b['h'] - 0.25, False)
            parts += block(b['x0'] - 0.1, b['x1'] + 0.1, b['z0'] - 0.1, b['z1'] + 0.1, b['h'] - 0.25, b['h'], False)
        else:
            parts += column((b['x0'] + b['x1']) / 2, (b['z0'] + b['z1']) / 2, base, b['h'], min(w, d), broken=True)
    elif min(w, d) < 1.6:
        parts += ragged_wall(b)
    else:
        parts += block(b['x0'], b['x1'], b['z0'], b['z1'], 0.0, b['h'], True, 'floor')
        parts += block(b['x0'] - 0.04, b['x1'] + 0.04, b['z0'] - 0.04, b['z1'] + 0.04, b['h'] - 0.28, b['h'] - 0.02, False)
    return parts

def terrain_parts(group=-1):
    parts = []
    for b in TERRAIN:
        if b['group'] == group: parts += block_parts(b)
    return parts

def is_column(b):
    return (b['x1'] - b['x0']) < 1.6 and (b['z1'] - b['z0']) < 1.6

def rubble_parts():
    parts = []
    # rubble at the foot of walls and columns, kept low (under 0.2 m) so it never looks solid
    for b in TERRAIN:
        if b['ramp'] or b['group'] >= 0 or (b['h'] > 2.0 and not is_column(b)): continue
        for k in range(4 if is_column(b) else 6):
            side = rng.randint(4)
            if side == 0: x, z = rng.uniform(b['x0'], b['x1']), b['z0'] - rng.uniform(0.15, 0.6)
            elif side == 1: x, z = rng.uniform(b['x0'], b['x1']), b['z1'] + rng.uniform(0.15, 0.6)
            elif side == 2: x, z = b['x0'] - rng.uniform(0.15, 0.6), rng.uniform(b['z0'], b['z1'])
            else: x, z = b['x1'] + rng.uniform(0.15, 0.6), rng.uniform(b['z0'], b['z1'])
            r = rng.uniform(0.1, 0.22)
            parts.append(ico((x, r * 0.4, z), r, STONE_D, 1, 0.35, seed=k, squash=0.6))
    return parts

# ---------------- outside the fighting area ----------------
def descending_stairs(cx, start, sign, axis, width, y_top, length, n):
    """Stairs leaving the map: from y_top at `start` going outward (sign) along axis 'z' or 'x', down `length`."""
    parts = []
    drop = y_top + 7.0
    for k in range(n):
        a0 = start + sign * length * k / n; a1 = start + sign * length * (k + 1) / n
        y = y_top - drop * (k + 1) / n
        lo, hi = min(a0, a1), max(a0, a1)
        if axis == 'z': parts += block(cx - width / 2, cx + width / 2, lo, hi, y - 1.2, y, True, 'floor')
        else: parts += block(lo, hi, cx - width / 2, cx + width / 2, y - 1.2, y, True, 'floor')
    # side walls
    for s in (-1, 1):
        for k in range(0, n, 2):
            a0 = start + sign * length * k / n; a1 = start + sign * length * min(n, k + 2) / n
            lo, hi = min(a0, a1), max(a0, a1)
            y = y_top - drop * (k + 2) / n
            off = s * (width / 2 + 0.3)
            if axis == 'z': parts += block(cx + off - 0.3, cx + off + 0.3, lo, hi, y - 1.0, y + 0.9 + rng.uniform(0, 0.3), True, 'wall')
            else: parts += block(lo, hi, cx + off - 0.3, cx + off + 0.3, y - 1.0, y + 0.9 + rng.uniform(0, 0.3), True, 'wall')
    return parts

def outside_parts():
    parts = []
    # stairs down the mountain behind both spawn platforms (the walled exits)
    for s in (-1, 1):
        parts += descending_stairs(0.0, 34.0 * s, s, 'z', 6.0, 1.6, 14.0, 28)
        # spawn platform: extend its stone base down into the cliff so it reads as built on the rock
        parts += block(-9.0, 9.0, 24.0 if s > 0 else -34.3, 34.3 if s > 0 else -24.0, -6.0, -0.02, False)
        # alcove exits: a path down to the east and west
        parts += descending_stairs(0.0, 27.0 * s, s, 'x', 5.0, 0.0, 10.0, 18)
        # landings at the foot of both flights (the Modly arches stand there)
        parts += block(-4.5, 4.5, 48.0 if s > 0 else -53.0, 53.0 if s > 0 else -48.0, -9.0, -7.0, True, 'floor')
        parts += block(37.0 if s > 0 else -42.0, 42.0 if s > 0 else -37.0, -4.0, 4.0, -9.0, -7.0, True, 'floor')
        parts += block(20.0 if s > 0 else -27.3, 27.3 if s > 0 else -20.0, -7.0, 7.0, -5.0, -0.02, False)
    # broken wall fragments just outside the edge, low enough not to hide the fighters
    for k in range(30):
        a = rng.uniform(0, 2 * np.pi)
        deg = math.degrees(a) % 360
        if min(abs(deg - 0), abs(deg - 360), abs(deg - 90), abs(deg - 180), abs(deg - 270)) < 22: continue
        r = edge_radius(np.array([a]))[0] + rng.uniform(0.6, 1.6)
        x, z = math.sin(a) * r, math.cos(a) * r
        L = rng.uniform(1.5, 4.0); h = rng.uniform(0.3, 1.1)
        tangent = (math.cos(a), -math.sin(a))
        p = block(-L / 2, L / 2, -0.3, 0.3, -1.0, h, True, 'wall')
        for q in p:
            V = q.V.copy(); yaw = a
            c, s_ = math.cos(yaw), math.sin(yaw)
            x0 = V[:, 0].copy(); z0 = V[:, 2].copy()
            V[:, 0] = x0 * c + z0 * s_ + x; V[:, 2] = -x0 * s_ + z0 * c + z
            q.V = V
            parts.append(q)
    return parts

def spire(x, z, top, radius, seed):
    prof = [(0.0, top + 0.6), (radius * 0.75, top + 0.5), (radius, top), (radius * 1.08, top - 3), (radius * 0.95, top - 10),
            (radius * 0.75, top - 22), (radius * 0.45, top - 40), (radius * 0.15, top - 60)]
    def col(V):
        y = V[:, 1]
        c = np.array(ROCK)[None] * (0.75 + 0.5 * fbm(V * 0.4, 3, seed))[:, None]
        moss = np.clip(1 - (top - y) / 4, 0, 1) * (fbm(V * 0.3, 3, seed + 3) > 0.42)
        c = c * (1 - moss[:, None] * 0.75) + np.array(MOSS) * moss[:, None] * 0.75
        c *= (0.55 + 0.45 * np.clip((y - top + 60) / 60, 0, 1))[:, None]
        return np.clip(c, 0, 1)
    p = lathe(prof, 22, ROCK, namp=radius * 0.35, nscale=0.18, seed=seed, colfn=col)
    p.V[:, 0] += x; p.V[:, 2] += z
    return p

def mountain(x, z, h, r, seed, snow=True):
    prof = [(0.0, h)]
    for k in range(1, 9):
        t = k / 8.0
        prof.append((r * t ** 1.1, h * (1 - t ** 0.7) - 170 * t))
    def col(V):
        y = V[:, 1]
        c = np.array((0.36, 0.37, 0.42))[None] * (0.65 + 0.55 * fbm(V * 0.03, 4, seed))[:, None]
        c *= (0.85 + 0.15 * np.sin(y * 0.15 + fbm(V * 0.02, 2, seed + 5) * 8))[:, None]
        if snow:
            sn = np.clip((y - h * 0.55 - (fbm(V * 0.03, 3, seed + 1) - 0.5) * 40) / 12, 0, 1)
            c = c * (1 - sn[:, None]) + np.array(SNOW) * sn[:, None]
        return np.clip(c, 0, 1)
    p = lathe(prof, 40, ROCK, namp=r * 0.3, nscale=0.014, seed=seed, colfn=col)
    p.V[:, 0] += x; p.V[:, 2] += z
    return p

def build_root():
    root = Node('Sky Ruins')
    Node('Floor', parent=root).add(floor_part())
    Node('Cliff', parent=root).add(cliff_part())
    Node('Ruins', parent=root).add(terrain_parts(-1))
    for g in range(4):
        Node('Structure %d' % g, parent=root).add(terrain_parts(g))
    Node('Rubble', parent=root).add(rubble_parts())
    Node('Exits', parent=root).add(outside_parts())
    near = Node('Spires', parent=root)
    srng = np.random.RandomState(21)
    for k in range(9):
        a = k / 9 * 2 * np.pi + srng.uniform(-0.2, 0.2)
        d = srng.uniform(70, 110)
        near.add(spire(math.sin(a) * d, math.cos(a) * d, srng.uniform(-20, -6), srng.uniform(9, 16), 40 + k))
    # rock pillars under the landings at the foot of the four stairways
    for (lx, lz) in [(0.0, 50.5), (0.0, -50.5), (39.5, 0.0), (-39.5, 0.0)]:
        near.add(spire(lx, lz, -9.3, 5.0, 70 + int(lx + lz)))
    # two flat-topped peaks that carry the Modly temples (placed by ArenaView.Ruins at these points)
    near.add(spire(-60.0, 85.0, -9.05, 9.5, 90))
    near.add(spire(95.0, -70.0, -16.05, 8.0, 91))
    far = Node('Mountains', parent=root)
    for k in range(14):
        a = k / 14 * 2 * np.pi + srng.uniform(-0.12, 0.12)
        d = srng.uniform(300, 430)
        cx, cz = math.sin(a) * d, math.cos(a) * d
        tx, tz = math.cos(a), -math.sin(a)          # along the horizon
        # a ridge: a main peak and two lower shoulders
        h = srng.uniform(45, 110)
        far.add(mountain(cx, cz, h, srng.uniform(120, 170), 60 + k))
        for side in (-1, 1):
            off = srng.uniform(70, 120) * side
            far.add(mountain(cx + tx * off + math.sin(a) * srng.uniform(-30, 40), cz + tz * off + math.cos(a) * srng.uniform(-30, 40),
                             h * srng.uniform(0.45, 0.75), srng.uniform(80, 120), 160 + k * 2 + (side > 0)))
    return root

# ---------------- glb export (Unity space -> glTF: mirror X) ----------------
MATS = {
    'vc': dict(base=(1, 1, 1, 1), rough=0.92, metal=0.0),
    'floor': dict(base=(1, 1, 1, 1), rough=0.85, metal=0.0, tex='RuinsFloor.jpg', sampler=0),
    'wall': dict(base=(1, 1, 1, 1), rough=0.9, metal=0.0, tex='RuinsWall.jpg', sampler=1),
}

def export(root, path):
    bin_ = bytearray(); j = {'asset': {'version': '2.0', 'generator': 'HEXBOUND sky ruins builder'}, 'scene': 0, 'scenes': [{'nodes': [0]}],
         'nodes': [], 'meshes': [], 'materials': [], 'buffers': [{}], 'bufferViews': [], 'accessors': [], 'textures': [], 'images': [], 'samplers': [{'magFilter': 9729, 'minFilter': 9987, 'wrapS': 33071, 'wrapT': 33071}, {'magFilter': 9729, 'minFilter': 9987, 'wrapS': 10497, 'wrapT': 10497}]}
    def view(data, target=None):
        while len(bin_) % 4: bin_.append(0)
        off = len(bin_); bin_.extend(data)
        v = {'buffer': 0, 'byteOffset': off, 'byteLength': len(data)}
        if target: v['target'] = target
        j['bufferViews'].append(v); return len(j['bufferViews']) - 1
    def acc(arr, ctype, typ, target, norm=False, minmax=False):
        vi = view(arr.tobytes(), target)
        a = {'bufferView': vi, 'componentType': ctype, 'count': int(arr.shape[0]), 'type': typ}
        if norm: a['normalized'] = True
        if minmax: a['min'] = arr.min(0).tolist(); a['max'] = arr.max(0).tolist()
        j['accessors'].append(a); return len(j['accessors']) - 1
    mat_index = {}
    def material(key):
        if key in mat_index: return mat_index[key]
        m = MATS[key]; d = {'name': 'Ruins_' + key, 'pbrMetallicRoughness': {'baseColorFactor': list(m['base']), 'roughnessFactor': m['rough'], 'metallicFactor': m['metal']}}
        if 'tex' in m:
            data = open(m['tex'], 'rb').read()
            iv = view(data); j['images'].append({'bufferView': iv, 'mimeType': 'image/jpeg', 'name': m['tex']})
            j['textures'].append({'source': len(j['images']) - 1, 'sampler': m.get('sampler', 0)})
            d['pbrMetallicRoughness']['baseColorTexture'] = {'index': len(j['textures']) - 1}
        if 'emis' in m: d['emissiveFactor'] = list(m['emis'])
        if m.get('double'): d['doubleSided'] = True
        j['materials'].append(d); mat_index[key] = len(j['materials']) - 1; return mat_index[key]
    stats = {'tris': 0}
    def add_node(n):
        idx = len(j['nodes']); nd = {'name': n.name}; j['nodes'].append(nd)
        if np.any(n.pos != 0): nd['translation'] = [-float(n.pos[0]), float(n.pos[1]), float(n.pos[2])]
        if n.yaw: nd['rotation'] = [0.0, math.sin(-n.yaw / 2), 0.0, math.cos(-n.yaw / 2)]
        if n.parts:
            prims = []
            for p in merge(n.parts):
                V = p.V.copy(); F = p.F.copy(); C = p.C.copy(); UV = p.UV
                if p.flat:
                    V = V[F].reshape(-1, 3); C = C[F].reshape(-1, 4); UV = UV[F].reshape(-1, 2) if UV is not None else None; F = np.arange(len(V)).reshape(-1, 3)
                tm = trimesh.Trimesh(V, F, process=False)
                N = tm.vertex_normals if not p.flat else np.repeat(tm.face_normals, 3, axis=0)
                V = V.copy(); V[:, 0] *= -1; N = np.array(N).copy(); N[:, 0] *= -1; F = F[:, [0, 2, 1]]
                attrs = {'POSITION': acc(V.astype(np.float32), 5126, 'VEC3', 34962, minmax=True),
                         'COLOR_0': acc((np.hstack([np.clip(C[:, :3], 0, 1) ** 2.2, np.clip(C[:, 3:], 0, 1)]) * 255).round().astype(np.uint8), 5121, 'VEC4', 34962, norm=True)}
                if not p.flat: attrs['NORMAL'] = acc(N.astype(np.float32), 5126, 'VEC3', 34962)
                if UV is not None: attrs['TEXCOORD_0'] = acc(np.asarray(UV, np.float32), 5126, 'VEC2', 34962)
                ind = acc(F.astype(np.uint32).reshape(-1), 5125, 'SCALAR', 34963)
                prims.append({'attributes': attrs, 'indices': ind, 'material': material(p.mat)})
                stats['tris'] += len(F)
            j['meshes'].append({'name': n.name, 'primitives': prims}); nd['mesh'] = len(j['meshes']) - 1
        kids = [add_node(c) for c in n.children]
        if kids: nd['children'] = kids
        return idx
    add_node(root)
    for k in ['textures', 'images']:
        if not j[k]: del j[k]
    while len(bin_) % 4: bin_.append(0)
    j['buffers'][0]['byteLength'] = len(bin_)
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    out = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_)) + struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(bin_), 0x004E4942) + bytes(bin_)
    open(path, 'wb').write(out); print('wrote', path, len(out) // 1024, 'KB, triangles', stats['tris'])


if __name__ == '__main__':
    root = build_root()
    export(root, 'Ruins.glb')
    import pickle; pickle.dump(root, open('scene.pkl', 'wb'))
