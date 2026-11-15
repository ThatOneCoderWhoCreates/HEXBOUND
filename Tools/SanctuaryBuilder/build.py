import numpy as np, math, sys, json, struct, io
sys.path.insert(0, '.')
from geo import *
from PIL import Image

STONE = (0.58, 0.55, 0.60); STONE_D = (0.36, 0.33, 0.40); ROCK = (0.36, 0.32, 0.38); MOSS = (0.30, 0.42, 0.22)
GRASS = (0.38, 0.52, 0.27); LACQ = (0.45, 0.17, 0.14); WOOD = (0.26, 0.17, 0.15); ROOF = (0.20, 0.22, 0.31)
GOLD = (0.86, 0.66, 0.32); BARK = (0.30, 0.21, 0.18); BLOSSOM = (0.97, 0.64, 0.82); PLASTER = (0.46, 0.40, 0.46)
HALF = 12.6

class Node:
    def __init__(self, name, pos=(0, 0, 0), yaw=0.0, parent=None):
        self.name = name; self.pos = np.array(pos, float); self.yaw = yaw; self.parts = []; self.children = []
        if parent: parent.children.append(self)
    def add(self, *ps):
        for p in ps:
            if isinstance(p, list): self.parts += p
            else: self.parts.append(p)

def rot_y(V, yaw):
    c, s = math.cos(yaw), math.sin(yaw)
    V = np.asarray(V, float).copy(); x = V[:, 0].copy(); z = V[:, 2].copy()
    V[:, 0] = x * c + z * s; V[:, 2] = -x * s + z * c
    return V

def moved(p, offset=(0, 0, 0), yaw=0.0):
    q = Part(rot_y(p.V, yaw) + np.asarray(offset), p.F, p.C, p.mat, p.UV, p.flat); return q

def rock_color(V, top):
    t = np.clip((V[:, 1] - (top - 2.5)) / 2.5, 0, 1)
    n = fbm(V * 0.35, 3, 9)
    m = t * (n > 0.45)
    c = np.array(ROCK)[None] * (0.8 + 0.4 * fbm(V * 0.6, 3, 2))[:, None]
    c = c * (1 - m[:, None]) + np.array(MOSS)[None] * m[:, None]
    # darker toward bottom
    c *= (0.7 + 0.3 * np.clip((V[:, 1] - top + 18) / 18, 0, 1))[:, None]
    return np.clip(c, 0, 1)

def grass_color(V):
    n = fbm(V * 0.4, 3, 31); n2 = fbm(V * 2.5, 2, 33)
    c = np.array(GRASS)[None] * (0.8 + 0.4 * n)[:, None]
    petals = (n2 > 0.68)
    c[petals] = c[petals] * 0.3 + np.array(BLOSSOM) * 0.7
    return np.clip(c, 0, 1)

# ---------------- pieces ----------------
def slab(s, yt, yb, w, color, mat='vc'):
    """Extruded profile along local +Z: s positions, top and bottom heights, width w across X."""
    n = len(s); V = []
    for i in range(n):
        for (x, y) in [(-w/2, yt[i]), (w/2, yt[i]), (w/2, yb[i]), (-w/2, yb[i])]:
            V.append((x, y, s[i]))
    V = np.array(V); F = []
    for i in range(n - 1):
        for k in range(4):
            a = i*4 + k; b = i*4 + (k+1) % 4; c = (i+1)*4 + (k+1) % 4; d = (i+1)*4 + k
            F += [[a, b, c], [a, c, d]]
    F += [[0, 2, 1], [0, 3, 2]]; e = (n-1)*4; F += [[e, e+1, e+2], [e, e+2, e+3]]
    return Part(V, np.array(F), tint(V, color, 0.07), mat, flat=True)

def heightfield_roof(w, d, H, U, a, color, base_y, thick=0.35, nu=44, nv=18):
    us = np.linspace(-1, 1, nu); vs = np.linspace(-1, 1, nv)
    Uu, Vv = np.meshgrid(us, vs)
    dd = np.maximum(np.abs(Vv), np.clip((np.abs(Uu) - a) / (1 - a), 0, 1))
    h = H * (1 - dd) ** 1.8 + U * (np.abs(Uu) ** 6) * (np.abs(Vv) ** 6) * 1.0 + 0.25 * U * np.abs(Vv) ** 8
    X = Uu * w / 2; Z = Vv * d / 2; Y = base_y + h
    top = np.stack([X, Y, Z], -1).reshape(-1, 3)
    bot = top.copy(); bot[:, 1] -= thick
    V = np.vstack([top, bot]); N = nu * nv; F = []
    for j in range(nv - 1):
        for i in range(nu - 1):
            a0 = j*nu + i; b0 = a0 + 1; c0 = a0 + nu + 1; d0 = a0 + nu
            F += [[a0, d0, c0], [a0, c0, b0]]
            F += [[N+a0, N+c0, N+d0], [N+a0, N+b0, N+c0]]
    edge = [(0, i) for i in range(nu)] + [(j, nu-1) for j in range(nv)] + [(nv-1, i) for i in range(nu-1, -1, -1)] + [(j, 0) for j in range(nv-1, -1, -1)]
    for k in range(len(edge) - 1):
        p = edge[k][0]*nu + edge[k][1]; q = edge[k+1][0]*nu + edge[k+1][1]
        if p == q: continue
        F += [[p, N+p, N+q], [p, N+q, q]]
    V = np.array(V)
    stripes = 0.85 + 0.15 * (np.sin(np.concatenate([Uu.ravel(), Uu.ravel()]) * w * 3.2) > 0)
    C = np.array(color)[None] * stripes[:, None] * (0.9 + 0.2 * fbm(V * 0.8, 2, 3))[:, None]
    C[N:] = np.array((0.42, 0.22, 0.18))
    rim = np.concatenate([dd.ravel() > 0.93, np.zeros(N, bool)])
    C[rim] = np.array(GOLD) * 0.9
    return Part(V, np.array(F), np.clip(C, 0, 1), 'vc', flat=False), (Y.max())

def lantern():
    P = []
    P.append(box((0, 0.075, 0), (0.62, 0.15, 0.62), STONE_D))
    P.append(box((0, 0.28, 0), (0.42, 0.26, 0.42), STONE))
    P.append(cyl((0, 0.4, 0), (0, 1.0, 0), 0.11, 0.09, STONE, seg=8, flat=True))
    P.append(box((0, 1.04, 0), (0.58, 0.08, 0.58), STONE_D))
    for sx in (-1, 1):
        for sz in (-1, 1):
            P.append(box((sx*0.21, 1.31, sz*0.21), (0.07, 0.46, 0.07), STONE_D))
    P.append(box((0, 1.31, 0), (0.34, 0.40, 0.34), (1, 0.8, 0.5), mat='glow', var=0.0))
    roof = cyl((0, 1.54, 0), (0, 1.86, 0), 0.43, 0.02, STONE_D, seg=4, flat=True)
    roof.V = rot_y(roof.V, math.pi/4); P.append(roof)
    P.append(ico((0, 1.92, 0), 0.07, GOLD, sub=1, mat='gold'))
    return P

def cherry_tree(seed, scale=1.0):
    rng = np.random.RandomState(seed); P = []
    p0 = np.array([0, 0, 0.0]); p1 = np.array([rng.uniform(-.3, .3), 1.5, rng.uniform(-.3, .3)]); p2 = p1 + np.array([rng.uniform(-.4, .4), 1.0, rng.uniform(-.4, .4)])
    P.append(cyl(p0 - [0, 0.2, 0], p1, 0.38, 0.27, BARK, seg=9, flat=True))
    P.append(cyl(p1, p2, 0.27, 0.18, BARK, seg=8, flat=True))
    tips = []
    for k in range(5):
        a = k / 5 * 2 * np.pi + rng.uniform(-.3, .3); src = p1 if k % 2 else p2
        tip = src + np.array([math.sin(a) * rng.uniform(1.4, 2.1), rng.uniform(1.0, 1.9), math.cos(a) * rng.uniform(1.4, 2.1)])
        P.append(cyl(src, tip, 0.15, 0.06, BARK, seg=6, flat=True)); tips.append(tip)
    tips.append(p2 + [0, 1.0, 0])
    blobs = []
    for t in tips:
        blobs.append((t, rng.uniform(1.0, 1.35)))
        for j in range(2):
            blobs.append((t + rng.uniform(-0.9, 0.9, 3) * [1, 0.5, 1], rng.uniform(0.7, 1.05)))
    for (c, r) in blobs:
        b = ico(c, r, BLOSSOM, sub=2, namp=0.18, seed=int(rng.randint(1000)), squash=0.78, var=0.05)
        y = (b.V[:, 1] - c[1]) / r
        shade = 0.72 + 0.28 * np.clip(y * 0.8 + 0.5, 0, 1)
        col = np.array(BLOSSOM)[None] * shade[:, None]
        light = fbm(b.V * 1.3, 2, 7) > 0.6
        col[light] = col[light] * 0.6 + 0.4
        b.C = np.hstack([np.clip(col, 0, 1), np.ones((len(col), 1))])
        P.append(b)
    for p in P: p.V = p.V * scale
    return P

def floating_cliff(radius, depth, seed, top_y=0.0):
    P = []
    prof = [(radius * 0.0, top_y + 0.25), (radius * 0.6, top_y + 0.35), (radius * 0.98, top_y + 0.1), (radius, top_y - 0.6)]
    top = lathe(prof, 40, GRASS, namp=0.25, nscale=0.4, seed=seed, colfn=grass_color)
    P.append(top)
    rprof = [(radius, top_y - 0.6), (radius * 0.97, top_y - 2.5), (radius * 0.85, top_y - depth * 0.3), (radius * 0.6, top_y - depth * 0.6), (radius * 0.3, top_y - depth * 0.85), (0.3, top_y - depth)]
    P.append(lathe(rprof, 36, ROCK, namp=radius * 0.16, nscale=0.25, seed=seed + 1, colfn=lambda V: rock_color(V, top_y)))
    return P

def mini_pagoda(scale=1.0):
    P = [box((0, 0.2, 0), (3.2, 0.4, 3.2), STONE_D), box((0, 1.4, 0), (2.4, 2.0, 2.4), PLASTER)]
    for sx in (-1, 1):
        for sz in (-1, 1): P.append(box((sx*1.15, 1.4, sz*1.15), (0.25, 2.0, 0.25), LACQ))
    r1, _ = heightfield_roof(4.0, 4.0, 1.3, 0.6, 0.05, ROOF, 2.4, nu=16, nv=16); P.append(r1)
    P.append(box((0, 3.95, 0), (1.4, 0.8, 1.4), PLASTER))
    r2, _ = heightfield_roof(2.6, 2.6, 1.0, 0.45, 0.05, ROOF, 4.35, nu=12, nv=12); P.append(r2)
    P.append(box((0, 1.1, -1.22), (0.8, 1.2, 0.05), (1, 0.8, 0.5), mat='glow', var=0))
    for p in P: p.V *= scale
    return P

# ---------------- scene ----------------
root = Node('Sanctuary')
court = Node('Courtyard', parent=root)

# floor disc (textured)
rs = np.linspace(0, 12.0, 26); seg = 160
ang = np.linspace(0, 2*np.pi, seg, endpoint=False)
V = [[0, 0, 0]]
for r in rs[1:]:
    for a in ang: V.append([math.sin(a)*r, 0, math.cos(a)*r])
V = np.array(V); F = []
for i in range(seg): F.append([0, 1 + i, 1 + (i+1) % seg])
for k in range(len(rs) - 2):
    for i in range(seg):
        a = 1 + k*seg + i; b = 1 + k*seg + (i+1) % seg; c = a + seg; d = b + seg
        F += [[a, c, d], [a, d, b]]
UV = np.stack([(V[:, 0] + HALF) / (2*HALF), (HALF - V[:, 2]) / (2*HALF)], 1)
court.add(Part(V, np.array(F), (1, 1, 1), 'floor', UV=UV))
court.add(ring_sector(12.0, 12.35, -0.3, 0.16, 0, 2*np.pi, 160, STONE_D))
court.add(lathe([(12.35, 0.16), (12.35, -0.95), (11.95, -1.35)], 120, STONE_D, flat=True))
court.add(lathe([(11.95, -1.35), (11.6, -2.8), (10.4, -5.5), (8.0, -9.5), (4.5, -13.5), (1.5, -17.0), (0.2, -18.5)], 60, ROCK, namp=1.0, nscale=0.22, seed=5, colfn=lambda V: rock_color(V, -1.35)))
# crystal pedestals at the sanctuary pillars (simulation positions)
D = 4.95
for sx in (-1, 1):
    for sz in (-1, 1):
        court.add(cyl((sx*D, -0.1, sz*D), (sx*D, 0.22, sz*D), 1.05, 0.98, STONE_D, seg=8, flat=True))
        court.add(ring_sector(0.98, 1.06, 0.20, 0.25, 0, 2*np.pi, 24, GOLD).__class__(*(lambda p: (p.V + [sx*D, 0, sz*D], p.F, p.C, 'gold', None, True))(ring_sector(0.98, 1.06, 0.20, 0.25, 0, 2*np.pi, 24, GOLD))))

# railing with four openings
rail = Node('Railing', parent=root)
openings = {0: 18.0, 90: 11.5, 180: 11.5, 270: 11.5}
def in_opening(deg):
    for c, h in openings.items():
        if abs(((deg - c + 180) % 360) - 180) < h: return True
    return False
banner_angles = [45, 135, 225, 315]
for deg in np.arange(0, 360, 5):
    if in_opening(deg) or deg in banner_angles: continue
    a = math.radians(deg); p = (math.sin(a)*12.15, 0, math.cos(a)*12.15)
    rail.add(box((p[0], 0.62, p[2]), (0.26, 0.92, 0.26), STONE, yaw=a))
    rail.add(box((p[0], 1.12, p[2]), (0.36, 0.1, 0.36), STONE_D, yaw=a))
cs = sorted(openings.items()); lantern_spots = []
for i, (c, h) in enumerate(cs):
    c2, h2 = cs[(i + 1) % len(cs)]
    a0 = math.radians(c + h); a1 = math.radians(c2 - h2 + (360 if c2 <= c else 0))
    n = max(4, int((a1 - a0) / math.radians(2.5)))
    rail.add(ring_sector(12.05, 12.25, 0.95, 1.07, a0, a1, n, STONE))
    rail.add(ring_sector(12.07, 12.23, 0.36, 0.46, a0, a1, n, STONE_D))
    for edge in (a0, a1):
        rail.add(box((math.sin(edge)*12.15, 0.75, math.cos(edge)*12.15), (0.5, 1.5, 0.5), STONE_D, yaw=edge))
        lantern_spots.append(edge)

# lanterns flanking each opening, standing on the floor ring outside the playable area
lanterns = Node('Lanterns', parent=root); li = 0
for a in lantern_spots:
    n = Node('Lantern_%d' % li, (math.sin(a)*12.15, 1.5, math.cos(a)*12.15), a, lanterns); n.add(lantern()); li += 1

# banner poles at the diagonals; the cloth is its own node so the game can sway it
banners = Node('Banners', parent=root); poles = Node('BannerPoles', parent=root); bi = 0
def banner_cloth(w=1.15, L=3.4):
    xs = np.linspace(-w/2, w/2, 3); ys = np.linspace(0, -L, 13)
    V = []; UVs = []
    for j, y in enumerate(ys):
        for i, x in enumerate(xs):
            yy = y + (0.55 if (j == len(ys) - 1 and i == 1) else 0)
            V.append((x, yy, 0)); UVs.append(((x + w/2) / w, (-yy) / L))
    V = np.array(V); F = []
    for j in range(len(ys) - 1):
        for i in range(2):
            a = j*3 + i; F += [[a, a+3, a+4], [a, a+4, a+1]]
    return Part(V, np.array(F), (1, 1, 1), 'banner', UV=np.array(UVs))
def add_banner_pole(pos, yaw, height=5.8):
    global bi
    p = np.array(pos, float); t = np.array([math.cos(yaw), 0, -math.sin(yaw)])
    poles.add(box(p + [0, 0.6, 0], (0.55, 1.2, 0.55), STONE_D, yaw=yaw))
    poles.add(cyl(p + [0, 1.2, 0], p + [0, height, 0], 0.08, 0.06, WOOD, seg=8))
    poles.add(ico(p + [0, height + 0.12, 0], 0.13, GOLD, sub=1, mat='gold'))
    poles.add(cyl(p + [0, height - 0.35, 0] - t * 0.75, p + [0, height - 0.35, 0] + t * 0.75, 0.05, 0.05, GOLD, seg=6, mat='gold'))
    n = Node('Banner_%d' % bi, p + [0, height - 0.4, 0], yaw, banners); n.add(banner_cloth()); bi += 1
for deg in banner_angles:
    a = math.radians(deg); add_banner_pole((math.sin(a)*12.15, 0, math.cos(a)*12.15), a)

# bridges E, S, W
bridges = Node('Bridges', parent=root)
for deg in (90, 180, 270):
    a = math.radians(deg)
    s = np.linspace(12.3, 24.8, 26); t = (s - s[0]) / (s[-1] - s[0])
    yt = -0.6 * t; yb = yt - 0.55 - 1.4 * np.sin(np.pi * t)
    parts = [slab(s, yt, yb, 4.4, STONE)]
    for side in (-1, 1):
        for k, si in enumerate(np.arange(13.1, 24.5, 1.45)):
            y = -0.6 * (si - 12.3) / 12.5
            parts.append(box((side*2.0, y + 0.5, si), (0.24, 0.9, 0.24), STONE))
        parts.append(slab(s, yt + 1.0, yt + 0.88, 0.22, STONE_D).__class__(*(lambda p: (p.V + [side*2.0, 0, 0], p.F, p.C, 'vc', None, True))(slab(s, yt + 1.0, yt + 0.88, 0.22, STONE_D))))
    for p in parts: bridges.add(moved(p, (0, 0, 0), a))

# outer garden ring (gap at the north for the gate terrace)
outer = Node('OuterRing', parent=root)
a0, a1 = math.radians(34), math.radians(326)
outer.add(lathe([(24.6, -0.9), (24.8, -0.6), (30.8, -0.6), (31.2, -1.0)], 150, GRASS, a0=a0, a1=a1, colfn=grass_color, flat=False))
outer.add(ring_sector(26.9, 28.6, -0.62, -0.52, a0, a1, 150, STONE))
outer.add(lathe([(31.2, -1.0), (31.0, -2.6), (29.6, -4.5), (27.8, -7.5), (26.3, -4.5), (24.8, -2.4), (24.6, -0.9)], 150, ROCK, a0=a0, a1=a1, namp=0.7, nscale=0.3, seed=12, colfn=lambda V: rock_color(V, -0.9)))

# north stairs and gate terrace
gate_g = Node('GateTerrace', parent=root)
for k in range(15):
    z0 = 12.35 + k * 0.6
    gate_g.add(box((0, (0.3 * (k + 1) - 1.6) / 2, z0 + 0.3 + (14 - k) * 0.3), (7.4, 0.3 * (k + 1) + 1.6, 0.6 + (14 - k) * 0.6), STONE if k % 2 == 0 else (0.55, 0.52, 0.58)))
s = np.linspace(12.35, 21.35, 2); 
for side in (-1, 1):
    p = slab(s, np.array([0.9, 5.4]), np.array([-1.6, 3.0]), 0.55, STONE_D)
    p.V += [side * 3.95, 0, 0]; gate_g.add(p)
gate_g.add(box((0, 3.5, 27.5), (27, 2.0, 12.5), STONE_D))
gate_g.add(box((0, 4.42, 27.5), (26.6, 0.16, 12.1), STONE))
terr_rock = lathe([(13.5, 2.5), (12.5, 0.5), (10.5, -3.5), (7.0, -8.5), (3.0, -13.5), (0.3, -16.0)], 40, ROCK, namp=1.0, nscale=0.25, seed=21, colfn=lambda V: rock_color(V, 2.5))
terr_rock.V[:, 0] *= 1.05; terr_rock.V[:, 2] *= 0.6; terr_rock.V += [0, 0, 27.5]; gate_g.add(terr_rock)
rng_b = np.random.RandomState(17)
for row in range(4):
    y0 = 2.55 + row * 0.47
    xs_ = np.arange(-13.5 + (0.45 if row % 2 else 0), 13.5, 0.9)
    for x in xs_:
        if x + 0.9 > 13.5: continue
        c = np.array(STONE_D) * rng_b.uniform(0.85, 1.15)
        gate_g.add(box((x + 0.45, y0 + 0.22, 21.2), (0.86, 0.43, 0.14), tuple(np.clip(c, 0, 1))))
for x in np.arange(-12, 12.1, 4.0):
    if abs(x) < 4: continue
    gate_g.add(box((x, 3.5, 21.12), (0.5, 2.0, 0.3), STONE))
gate_g.add(box((0, 4.47, 21.1), (27.0, 0.08, 0.12), GOLD, mat='gold'))
for a_ in (math.radians(34), math.radians(326)):
    for k_ in range(4):
        r_ = 25.2 + k_ * 1.7
        court.add(ico((math.sin(a_) * r_, -1.2 + rng_b.uniform(-0.3, 0.3), math.cos(a_) * r_), rng_b.uniform(1.0, 1.5), ROCK, sub=1, namp=0.25, seed=k_ + 60))
for x in np.arange(-13, 13.1, 1.3):
    if abs(x) < 4.4: continue
    gate_g.add(box((x, 5.0, 21.45), (0.24, 0.9, 0.24), STONE))
for side in (-1, 1):
    gate_g.add(box((side * 8.7, 5.47, 21.45), (8.6, 0.12, 0.3), STONE))

# gate building (replaceable by a Modly TempleGate model)
gate = Node('Gate', (0, 4.5, 28.5), math.pi, gate_g)
G = []
G.append(box((0, 0.3, 0), (19.5, 0.6, 7.5), STONE_D))
for side in (-1, 1):
    G.append(box((side * 5.8, 4.05, 0), (5.6, 6.9, 3.0), PLASTER))
    G.append(box((side * 10.6, 3.1, 0.3), (4.2, 5.0, 2.4), PLASTER))
    for x in (3.4, 8.2):
        G.append(cyl((side * x, 0.6, 1.75), (side * x, 7.6, 1.75), 0.42, 0.38, LACQ, seg=12))
        G.append(cyl((side * x, 0.6, 1.75), (side * x, 1.0, 1.75), 0.55, 0.5, GOLD, seg=12, mat='gold'))
        G.append(cyl((side * x, 7.2, 1.75), (side * x, 7.6, 1.75), 0.5, 0.55, GOLD, seg=12, mat='gold'))
# arched opening: spandrels above a 6 m wide opening, arch top at 6.6 m
for x in np.arange(-3.0, 3.0, 0.2):
    xc = x + 0.1; yarch = 0.6 + 3.3 + math.sqrt(max(0.0, 9.0 - xc * xc)) * 0.9
    G.append(box((xc, (yarch + 7.5) / 2, 0), (0.21, 7.5 - yarch, 3.0), PLASTER, var=0.03))
arc = torus_arc(3.05, 0.17, -math.pi/2, math.pi/2, 24, GOLD); arc.V[:, 1] = arc.V[:, 1] * 0.9 + 3.9; arc.V[:, 2] += 1.55; arc.mat = 'gold'; G.append(arc)
G.append(box((0, 7.9, 0.2), (21.0, 0.8, 4.2), WOOD))
G.append(box((0, 7.85, 2.33), (21.0, 0.18, 0.06), GOLD, mat='gold'))
G.append(box((0, 8.5, 0.2), (19.5, 0.4, 3.8), LACQ))
r1, top1 = heightfield_roof(24.5, 9.0, 2.6, 1.6, 0.55, ROOF, 8.7); r1.V[:, 2] += 0.2; G.append(r1)
G.append(box((0, top1 + 0.12, 0.2), (24.5 * 0.55, 0.3, 0.35), GOLD, mat='gold'))
G.append(box((0, 11.9, 0.2), (10.0, 2.0, 4.2), PLASTER))
for x in (-4.6, -1.6, 1.6, 4.6): G.append(cyl((x, 10.9, 2.35), (x, 12.9, 2.35), 0.2, 0.2, LACQ, seg=8))
G.append(box((0, 11.9, 2.32), (2.4, 1.0, 0.06), (1, 0.8, 0.5), mat='glow', var=0))
r2, top2 = heightfield_roof(14.0, 6.6, 2.1, 1.1, 0.45, ROOF, 12.9); r2.V[:, 2] += 0.2; G.append(r2)
G.append(box((0, top2 + 0.1, 0.2), (14 * 0.45, 0.26, 0.3), GOLD, mat='gold'))
for side in (-1, 1):
    rr, _ = heightfield_roof(5.6, 4.0, 1.2, 0.6, 0.15, ROOF, 5.55); rr.V[:, 0] += side * 10.6; rr.V[:, 2] += 0.3; G.append(rr)
    for end in (-1, 1):
        G.append(cyl((side * 0 + end * 24.5 * 0.55 / 2, top1, 0.2), (end * (24.5 * 0.55 / 2 + 0.5), top1 + 1.0, 0.2), 0.16, 0.05, GOLD, seg=6, mat='gold'))
# wall banners (static)
def wall_banner(x):
    w, L = 1.6, 4.6
    V = np.array([(x - w/2, 6.4, 1.53), (x + w/2, 6.4, 1.53), (x + w/2, 6.4 - L, 1.53), (x, 6.4 - L + 0.6, 1.53), (x - w/2, 6.4 - L, 1.53)])
    UVs = np.array([(0, 0), (1, 0), (1, 1), (0.5, 1 - 0.6 / L), (0, 1)])
    F = np.array([[0, 2, 1], [0, 3, 2], [0, 4, 3]])
    return Part(V, F, (1, 1, 1), 'banner', UV=UVs)
G += [wall_banner(-5.8), wall_banner(5.8)]
gate.add(G)
for side in (-1, 1):
    n = Node('Lantern_%d' % li, (side * 4.6, 4.5, 21.9), math.pi, lanterns); n.add(lantern()); li += 1
    add_banner_pole((side * 12.2, 4.5, 21.5), math.pi, 6.4)

# celestial broken ring behind the gate
ring = Node('CelestialRing', (0, 15, 54), 0.0, root)
for (s0, s1) in [(-150, -62), (-47, 38), (52, 150)]:
    ring.add(torus_arc(25, 1.5, math.radians(s0), math.radians(s1), 28, (0.55, 0.52, 0.60), seed=s0 + 200, namp=0.25))
    g = torus_arc(23.2, 0.22, math.radians(s0 + 3), math.radians(s1 - 3), 28, GOLD); g.V[:, 2] -= 1.2; g.mat = 'gold'; ring.add(g)
rng = np.random.RandomState(3)
for ang_ in (-55, 45, 160, -165):
    for k in range(3):
        a = math.radians(ang_ + rng.uniform(-6, 6)); r = 25 + rng.uniform(-2, 2)
        ring.add(box((math.sin(a) * r, math.cos(a) * r, rng.uniform(-1, 1)), tuple(rng.uniform(0.6, 1.8, 3)), (0.55, 0.52, 0.60), yaw=rng.uniform(0, 3)))

# trees
trees = Node('Trees', parent=root); ti = 0
tree_spots = [(52, 28.5), (75, 29.0), (108, 28.2), (128, 29.6), (157, 26.0), (203, 29.4), (232, 28.0), (250, 25.8), (282, 29.0), (305, 28.4)]
for k, (deg, r) in enumerate(tree_spots):
    a = math.radians(deg)
    n = Node('Tree_%d' % ti, (math.sin(a) * r, -0.6, math.cos(a) * r), a * 1.7 + k, trees); n.add(cherry_tree(100 + k, 0.95 + 0.3 * ((k * 37) % 7) / 7)); ti += 1
for side in (-1, 1):
    n = Node('Tree_%d' % ti, (side * 11.2, 4.5, 30.5), side, trees); n.add(cherry_tree(300 + ti, 1.15)); ti += 1

# cliffs with pagodas, trees and waterfall anchors
cliffs = Node('Cliffs', parent=root); anchors = Node('Anchors', parent=root); wi = 0
cliff_defs = [((-36, 1.5, -24), 8.5, 30, True, True), ((37, 3.0, -21), 6.5, 24, False, True), ((42, 6.5, 22), 5.5, 20, True, False), ((-42, 8.5, 25), 6.0, 22, False, True), ((-6, -3.0, -48), 7.0, 22, True, True), ((22, 10.0, -52), 5.0, 16, True, False)]
for k, (c, rad, depth, pagoda, tree) in enumerate(cliff_defs):
    n = Node('Cliff_%d' % k, c, 0.0, cliffs)
    n.add(floating_cliff(rad, depth, 50 + k * 3))
    toward = -np.array([c[0], 0, c[2]], float); toward /= np.linalg.norm(toward)
    if pagoda:
        pp = mini_pagoda(1.0 if rad > 6 else 0.8)
        yaw = math.atan2(toward[0], toward[2]) + math.pi
        for p in pp: n.add(moved(p, (-toward[0] * rad * 0.2, 0.3, -toward[2] * rad * 0.2), yaw))
    if tree:
        nt = Node('Tree_%d' % ti, (c[0] + toward[2] * rad * 0.45, c[1] + 0.3, c[2] - toward[0] * rad * 0.45), k * 1.3, trees)
        nt.add(cherry_tree(500 + k, 1.0)); ti += 1
    wp = np.array(c) + toward * (rad * 0.97) + [0, -0.4, 0]
    Node('Anchor_Waterfall_%d_L%d_W%d' % (wi, int(depth * 0.9), 2 if rad < 6.5 else 3), wp, math.atan2(toward[0], toward[2]), anchors); wi += 1

# distant mountains
mount = Node('Mountains', parent=root)
rng = np.random.RandomState(9)
for k in range(11):
    a = k / 11 * 2 * np.pi + rng.uniform(-0.2, 0.2); d = rng.uniform(210, 300); h = rng.uniform(38, 85)
    prof = [(0.0, h), (h * 0.12, h * 0.85), (h * 0.3, h * 0.45), (h * 0.5, 0.0), (h * 0.5, -40)]
    m = lathe(prof, 14, (0.56, 0.48, 0.66), namp=h * 0.06, nscale=0.05, seed=70 + k, flat=True)
    m.V += [math.sin(a) * d, -30, math.cos(a) * d]; mount.add(m)

Node('Anchor_Portal', (0, 4.5 + 3.5, 28.5 - 1.6), math.pi, anchors)
Node('Anchor_GateWindow', (0, 4.5 + 11.9, 28.5 - 2.6), math.pi, anchors)

# ---------------- glb export (Unity space -> glTF: mirror X) ----------------
MATS = {
    'vc': dict(base=(1, 1, 1, 1), rough=0.88, metal=0.0),
    'floor': dict(base=(1, 1, 1, 1), rough=0.8, metal=0.0, tex='Floor.jpg'),
    'banner': dict(base=(1, 1, 1, 1), rough=0.9, metal=0.0, tex='Banner.jpg', double=True),
    'gold': dict(base=(1, 1, 1, 1), rough=0.35, metal=0.75),
    'glow': dict(base=(1, 0.85, 0.6, 1), rough=0.6, metal=0.0, emis=(1.0, 0.72, 0.38)),
}

def export(root, path):
    bin_ = bytearray(); j = {'asset': {'version': '2.0', 'generator': 'HEXBOUND sanctuary builder'}, 'scene': 0, 'scenes': [{'nodes': [0]}],
         'nodes': [], 'meshes': [], 'materials': [], 'buffers': [{}], 'bufferViews': [], 'accessors': [], 'textures': [], 'images': [], 'samplers': [{'magFilter': 9729, 'minFilter': 9987, 'wrapS': 33071, 'wrapT': 33071}]}
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
        m = MATS[key]; d = {'name': 'Sanctuary_' + key, 'pbrMetallicRoughness': {'baseColorFactor': list(m['base']), 'roughnessFactor': m['rough'], 'metallicFactor': m['metal']}}
        if 'tex' in m:
            data = open(m['tex'], 'rb').read()
            iv = view(data); j['images'].append({'bufferView': iv, 'mimeType': 'image/jpeg', 'name': m['tex']})
            j['textures'].append({'source': len(j['images']) - 1, 'sampler': 0})
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
    export(root, 'Sanctuary.glb')
    import pickle; pickle.dump(root, open('scene.pkl', 'wb'))
