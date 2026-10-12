import shapefile
from shapely.geometry import shape, Point
from shapely.validation import make_valid
from pyproj import CRS, Transformer
import sys

r = shapefile.Reader('Shapefiles/Provincial/SK/ConstituencyGE30th')
data = list(r.__iter__())
prj = open('Shapefiles/Provincial/SK/ConstituencyGE30th.prj', 'rb').read().decode('utf-8', errors='ignore')
fwd = Transformer.from_crs(CRS.from_wkt(prj), 'EPSG:4326', always_xy=True)
inv = Transformer.from_crs('EPSG:4326', CRS.from_wkt(prj), always_xy=True)

prov = []
for i, d in enumerate(data):
    v = make_valid(shape(d.shape.__geo_interface__))
    prov.append((i, d.record[0], v, d.record[4] / 1e6))

tot = 0
for a in range(len(prov)):
    for b in range(a + 1, len(prov)):
        ix = prov[a][2].intersection(prov[b][2])
        km2 = ix.area / 1e6
        if km2 > 10:
            print(f"{prov[a][1][:22]:24s} x {prov[b][1][:22]:24s} inter={km2:9.1f} km2")
            tot += km2
print('total pairwise overlap >10km2:', round(tot))

for lon10 in range(-1100, -1005, 10):
    for lat10 in range(490, 605, 10):
        lon, lat = lon10 / 10, lat10 / 10
        x, y = inv.transform(lon, lat)
        p = Point(x, y)
        hits = [n for (i, n, g, _) in prov if g.covers(p) or g.contains(p)]
        if hits:
            print(f'({lat:5.1f},{lon:6.1f}): {len(hits)} {hits}')
