import shapefile
from shapely.geometry import shape, Point
from shapely.validation import make_valid
from pyproj import CRS, Transformer

r = shapefile.Reader('Shapefiles/Provincial/SK/ConstituencyGE30th')
data = list(r.__iter__())
prj = open('Shapefiles/Provincial/SK/ConstituencyGE30th.prj', 'rb').read().decode('utf-8', errors='ignore')
inv = Transformer.from_crs('EPSG:4326', CRS.from_wkt(prj), always_xy=True)

feats = []
for i, d in enumerate(data):
    v = make_valid(shape(d.shape.__geo_interface__))
    feats.append((d.record[0][:1], v))

# map lon -110..-101, lat 48.9..60.2 ; cell = 0.25 deg
print('  ' + ''.join(f'{l}' for l in range(101, 111)))
for lat10 in range(602, 488, -2):
    lat = lat10 / 10
    row = f'{lat:5.1f} '
    for lon10 in range(-1100, -1010, 2):
        lon = lon10 / 10
        x, y = inv.transform(lon, lat)
        p = Point(x, y)
        hits = [n for n, g in feats if g.covers(p) or g.contains(p)]
        if len(hits) == 1:
            row += hits[0]
        elif len(hits) == 0:
            row += '.'
        else:
            row += 'X'
    print(row)
