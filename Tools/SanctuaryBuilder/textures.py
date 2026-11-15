import numpy as np, math, sys
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, '.')
from geo import fbm

HALF = 12.6  # floor texture covers x,z in [-HALF, HALF]

def floor_texture(size=2048):
    s = size
    xs = (np.arange(s) + 0.5) / s * 2 * HALF - HALF
    X, Z = np.meshgrid(xs, -xs)  # row 0 = +Z (v up)
    R = np.hypot(X, Z); A = np.arctan2(X, Z) % (2 * np.pi)
    rings = [0, 1.9, 3.3, 4.6, 5.9, 7.2, 8.5, 9.8, 11.0, 12.0, 12.6]
    stone_id = np.zeros_like(R, dtype=np.int64); grout = np.zeros_like(R)
    edge_d = np.full_like(R, 9.0)
    rng = np.random.RandomState(4)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        m = (R >= r0) & (R < r1)
        if k == 0:
            stone_id[m] = 1; edge_d[m] = np.minimum(edge_d[m], r1 - R[m]); continue
        rm = (r0 + r1) / 2
        n = max(6, int(round(2 * np.pi * rm / (1.25 if k < 8 else 0.9))))
        off = rng.rand() * 2 * np.pi / n
        aa = (A[m] - off) % (2 * np.pi)
        idx = np.floor(aa / (2 * np.pi / n)).astype(int)
        stone_id[m] = 1000 * k + idx + 2
        frac = aa / (2 * np.pi / n) - idx
        da = np.minimum(frac, 1 - frac) * (2 * np.pi / n) * R[m]
        dr = np.minimum(R[m] - r0, r1 - R[m])
        edge_d[m] = np.minimum(da, dr)
    # per-stone tint
    ids = np.unique(stone_id)
    tintmap = {i: rng.uniform(-0.015, 0.015, 3) + rng.uniform(-0.07, 0.06) for i in ids}
    base = np.array([0.56, 0.53, 0.56])
    border = (R >= 11.0)
    img = np.zeros((s, s, 3))
    T = np.zeros((s, s, 3))
    for i in ids:
        T[stone_id == i] = tintmap[i]
    img[:] = base
    img[border] = np.array([0.36, 0.33, 0.40])
    img += T
    pts = np.stack([X.ravel(), np.zeros(X.size), Z.ravel()], 1)
    n1 = fbm(pts * 2.2, 4, 11).reshape(s, s)
    n2 = fbm(pts * 9.0, 3, 23).reshape(s, s)
    img *= (0.86 + 0.22 * n1)[..., None] * (0.94 + 0.1 * n2)[..., None]
    # bevel / grout
    g = np.clip(edge_d / 0.05, 0, 1)
    bev = np.clip(edge_d / 0.16, 0, 1)
    img *= (0.35 + 0.65 * g)[..., None]
    img *= (0.85 + 0.15 * bev)[..., None]
    # subtle moss in grout near outer rings
    moss = (1 - g) * np.clip((R - 8) / 5, 0, 1) * (fbm(pts * 0.8, 3, 41).reshape(s, s) > 0.55)
    img = img * (1 - moss[..., None] * 0.6) + np.array([0.22, 0.32, 0.16]) * moss[..., None] * 0.6
    # gold inlay rings
    for rr, w in [(1.9, 0.05), (4.6, 0.035), (9.8, 0.05), (11.0, 0.04), (11.92, 0.08)]:
        gm = np.clip(1 - np.abs(R - rr) / w, 0, 1)
        img = img * (1 - gm[..., None]) + np.array([0.80, 0.62, 0.30]) * gm[..., None]
    # faint engraved rune lines (the glowing overlay sits on top in game)
    rune = np.asarray(Image.open('Rune.png').split()[3].resize((int(s * 9.2 / HALF / 2 * 2), int(s * 9.2 / HALF / 2 * 2)), Image.LANCZOS), float) / 255
    rs = rune.shape[0]; o = (s - rs) // 2
    eng = np.zeros((s, s)); eng[o:o+rs, o:o+rs] = rune
    img = img * (1 - 0.55 * eng[..., None]) + np.array([0.72, 0.56, 0.30]) * 0.55 * eng[..., None]
    img = np.clip(img, 0, 1)
    Image.fromarray((img * 255).astype(np.uint8)).save('Floor.jpg', quality=88)

def rune_texture(size=2048):
    S = size * 2
    im = Image.new('L', (S, S), 0); d = ImageDraw.Draw(im)
    c = S / 2; R = S / 2 * 0.98
    def circ(r, w):
        d.ellipse([c - r * R, c - r * R, c + r * R, c + r * R], outline=255, width=int(w * S / 1000))
    def poly(pts, w):
        d.line(pts + [pts[0]], fill=255, width=int(w * S / 1000), joint='curve')
    circ(1.0, 5); circ(0.955, 2.5); circ(0.80, 3); circ(0.62, 2.5); circ(0.58, 4); circ(0.30, 3); circ(0.12, 3)
    # glyph band between 0.80 and 0.955
    rng = np.random.RandomState(7)
    for i in range(48):
        a = i / 48 * 2 * math.pi
        rr0, rr1 = 0.83, 0.93
        kind = i % 4
        cx = c + math.sin(a) * (rr0 + rr1) / 2 * R; cy = c - math.cos(a) * (rr0 + rr1) / 2 * R
        sz = 0.035 * R
        ux, uy = math.cos(a), math.sin(a); vx, vy = math.sin(a), -math.cos(a)
        def P(u, v): return (cx + ux * u * sz + vx * v * sz, cy + uy * u * sz + vy * v * sz)
        w = int(2.2 * S / 1000)
        if kind == 0: d.line([P(-1, -1), P(0, 1), P(1, -1)], fill=255, width=w)
        elif kind == 1: d.ellipse([cx - sz * 0.5, cy - sz * 0.5, cx + sz * 0.5, cy + sz * 0.5], outline=255, width=w); d.line([P(0, -1.1), P(0, 1.1)], fill=255, width=w)
        elif kind == 2: d.line([P(-1, 0), P(1, 0)], fill=255, width=w); d.line([P(-0.6, -1), P(-0.6, 1)], fill=255, width=w); d.line([P(0.6, -1), P(0.6, 1)], fill=255, width=w)
        else: d.line([P(-1, 1), P(0, -1), P(1, 1), P(-1, 1)], fill=255, width=w)
    # 8-point star from two squares, between 0.58 and 0.80
    for rot in [0, math.pi / 4]:
        pts = [(c + math.sin(rot + k * math.pi / 2) * 0.80 * R, c - math.cos(rot + k * math.pi / 2) * 0.80 * R) for k in range(4)]
        poly(pts, 3.5)
    # inner hexagram
    for rot in [0, math.pi / 3]:
        pts = [(c + math.sin(rot + k * 2 * math.pi / 3) * 0.58 * R, c - math.cos(rot + k * 2 * math.pi / 3) * 0.58 * R) for k in range(3)]
        poly(pts, 3)
    # small circles on the 0.69 ring
    for i in range(12):
        a = i / 12 * 2 * math.pi + math.pi / 12
        cx = c + math.sin(a) * 0.69 * R; cy = c - math.cos(a) * 0.69 * R; r = 0.035 * R
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=255, width=int(2.5 * S / 1000))
    # rays in the center
    for i in range(16):
        a = i / 16 * 2 * math.pi
        r0, r1 = (0.14, 0.28) if i % 2 == 0 else (0.14, 0.22)
        d.line([(c + math.sin(a) * r0 * R, c - math.cos(a) * r0 * R), (c + math.sin(a) * r1 * R, c - math.cos(a) * r1 * R)], fill=255, width=int(2.5 * S / 1000))
    im = im.filter(ImageFilter.GaussianBlur(1.2)).resize((size, size), Image.LANCZOS)
    out = Image.new('RGBA', (size, size), (255, 255, 255, 0)); out.putalpha(im)
    out.save('Rune.png')

def banner_texture(w=256, h=1024):
    img = np.zeros((h, w, 3)); img[:] = [0.30, 0.15, 0.52]
    yy, xx = np.mgrid[0:h, 0:w]
    pts = np.stack([xx.ravel() / 40.0, yy.ravel() / 40.0, np.zeros(xx.size)], 1)
    img *= (0.85 + 0.25 * fbm(pts, 3, 5).reshape(h, w))[..., None]
    img *= (0.9 + 0.1 * np.sin(xx / w * 40))[..., None]
    gold = np.array([0.86, 0.68, 0.32])
    border = ((xx < 14) | (xx > w - 15) | (yy < 18)) & ~((xx < 6) | (xx > w - 7))
    img[border] = gold
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)); d = ImageDraw.Draw(im)
    cx, cy, r = w / 2, h * 0.40, w * 0.30
    g = tuple(int(v * 255) for v in gold)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=g, width=7)
    d.ellipse([cx - r * 0.45, cy - r * 0.45, cx + r * 0.45, cy + r * 0.45], outline=g, width=6)
    for k in range(8):
        a = k * math.pi / 4; l = r * (1.45 if k % 2 == 0 else 1.1)
        d.line([(cx + math.sin(a) * r * 0.45, cy - math.cos(a) * r * 0.45), (cx + math.sin(a) * l, cy - math.cos(a) * l)], fill=g, width=7)
    d.polygon([(cx, cy - r * 0.25), (cx + r * 0.25, cy), (cx, cy + r * 0.25), (cx - r * 0.25, cy)], fill=g)
    for k in range(3):
        y = h * (0.72 + k * 0.05); d.line([(w * 0.22, y), (w * 0.78, y)], fill=g, width=5)
    im.save('Banner.jpg', quality=90)

if __name__ == '__main__':
    import os
    if not os.path.exists('Rune.png'): rune_texture()
    if not os.path.exists('Banner.jpg'): banner_texture()
    floor_texture()
    print('textures done')
