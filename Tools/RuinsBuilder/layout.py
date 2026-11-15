# Ruins map: outline -> polar edge table (72 samples, Fix raw), plus a top-down debug image.
import math, json
from shapely.geometry import box, Point, LineString
from shapely.ops import unary_union

HW, HL, CR = 22.0, 26.0, 10.0     # main courtyard half width (x), half length (z), corner radius
main = box(-HW+CR, -HL, HW-CR, HL).union(box(-HW, -HL+CR, HW, HL-CR))
for sx in (-1, 1):
    for sz in (-1, 1):
        main = main.union(Point(sx*(HW-CR), sz*(HL-CR)).buffer(CR, 64))
spawnN = box(-9.0, 24.0, 9.0, 34.0)
spawnS = box(-9.0, -34.0, 9.0, -24.0)
wingE = box(20.0, -7.0, 27.0, 7.0)
wingW = box(-27.0, -7.0, -20.0, 7.0)
outline = unary_union([main, spawnN, spawnS, wingE, wingW])

FULL = 65536
radii = []
for i in range(72):
    a = i * 2*math.pi/72   # angle 0 = +Z, increasing toward +X
    dx, dz = math.sin(a), math.cos(a)
    ray = LineString([(0,0), (dx*80, dz*80)])
    inter = ray.intersection(outline)
    r = inter.length if inter.geom_type == 'LineString' else max(g.length for g in inter.geoms)
    radii.append(r)
raw = [int(round(r*65536)) for r in radii]
json.dump({'radii': radii, 'outline': list(outline.exterior.coords)}, open('ruins_outline.json','w'))
lines = []
for k in range(0, 72, 8):
    lines.append('            ' + ', '.join(str(v) for v in raw[k:k+8]) + ',')
open('edge_table.txt','w').write('\n'.join(lines))
print(min(radii), max(radii))
print(open('edge_table.txt').read())
