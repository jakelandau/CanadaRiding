# Ground-truth fixture generator for CanadaRidingTagger tests.
# Emits into tests/CanadaRidingTagger.Tests/Fixtures/<key>/:
#   proj.csv       lat,lon,x,y   -- WGS84 grid, forward-projected (projected datasets only)
#   geo.csv        feature_index,ring_index,vertex_index,lon,lat  -- ring-0 first 20 vertices
#   ring_counts.csv feature_index,ring_index,vertex_count
#   pip.csv        lat,lon,id,name   -- interior points with expected district
#   meta.txt       key/path/kind/fields/records/districts

import math, csv, os
import shapefile
from pyproj import CRS, Transformer
from pyproj.exceptions import CRSError

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'tests', 'CanadaRidingTagger.Tests', 'Fixtures')
WGS84 = 'EPSG:4326'

PROJ4 = {
    'federal': '+proj=lcc +lat_1=49 +lat_2=77 +lat_0=63.390675 +lon_0=-91.86666666666666 +x_0=6200000 +y_0=3000000 +a=6378137 +rf=298.257222101 +units=m +no_defs',
    'ab':      '+proj=tmerc +lat_0=0 +lon_0=-115 +k=0.9992 +x_0=0 +y_0=0 +a=6378137 +rf=298.257222101 +units=m +no_defs',
    'mb':      '+proj=tmerc +lat_0=0 +lon_0=-99 +k=0.9996 +x_0=500000 +y_0=0 +a=6378137 +rf=298.257222101 +units=m +no_defs',
    'nb':      '+proj=stere +lat_0=46.5 +lon_0=-66.5 +k=0.999912 +x_0=2500000 +y_0=7500000 +a=6378137 +rf=298.257222101 +units=m +no_defs',
    'ns':      '+proj=tmerc +lat_0=0 +lon_0=-63 +k=0.9996 +x_0=500000 +y_0=0 +a=6378137 +rf=298.257222101 +units=m +no_defs',
    'nwt':     '+proj=lcc +lat_1=50 +lat_2=70 +lat_0=40 +lon_0=-96 +x_0=0 +y_0=0 +a=6378137 +rf=298.257222101 +units=m +no_defs',
    'yt':      '+proj=aea +lat_1=61.66666666666666 +lat_2=68 +lat_0=59 +lon_0=-132.5 +x_0=500000 +y_0=500000 +a=6378137 +rf=298.257222101 +units=m +no_defs',
    'qc':      '+proj=lcc +lat_1=46 +lat_2=50 +lat_0=44 +lon_0=-70 +x_0=800000 +y_0=0 +a=6378137 +rf=298.257222101 +units=m +no_defs',
    'sk':      '+proj=tmerc +lat_0=0 +lon_0=-105 +k=0.9996 +x_0=500000 +y_0=0 +a=6378137 +rf=298.257222101 +units=m +no_defs',
    'on':      '+proj=lcc +lat_1=44.5 +lat_2=54.5 +lat_0=0 +lon_0=-84 +x_0=1000000 +y_0=0 +a=6378137 +rf=298.257222101 +units=m +no_defs',
    'pei':     'EPSG:3857',
}

SHP = 'Shapefiles/'
DATASETS = [
    dict(key='federal', path=SHP + 'Federal/SHP/FED_CA_2025_EN.shp', id_field='FED_NUM', name_en='ED_NAMEE', name_fr='ED_NAMEF'),
    dict(key='ab', path=SHP + 'Provincial/AB/EDS_ENACTED_BILL33_15DEC2017.shp', id_field='EDNumber20', name_en='EDName2017', name_fr=None),
    dict(key='bc', path=SHP + 'Provincial/BC/EBC_ELECTORAL_DISTS_BS11_SVW/EBC_ED_23_polygon.shp', id_field='ED_ID', name_en='ED_NAME', name_fr=None),
    dict(key='mb', path=SHP + 'Provincial/MB/VA_MB2023_public.shp', id_field='EDNUM', name_en='EDNAMEEN', name_fr='EDNAMEFR'),
    dict(key='sk', path=SHP + 'Provincial/SK/ConstituencyGE30th.shp', id_field='Constitu_1', name_en='Constituen', name_fr=None),
    dict(key='on', path=SHP + 'Provincial/ON/Electoral District Shapefile - 2022 General Election/ELECTORAL_DISTRICT.shp', id_field='ED_ID', name_en='ENGLISH_NA', name_fr='FRENCH_NAM'),
    dict(key='nb', path=SHP + 'Provincial/NB/Prov_PED_2024.shp', id_field='DIST_ID', name_en='PED_Name_E', name_fr='PED_Name_F'),
    dict(key='ns', path=SHP + 'Provincial/NS/NS_2026ED_Bnds.shp', id_field='ED_NO', name_en='ED_NAME', name_fr=None),
    dict(key='nl', path=SHP + 'Provincial/NL/NL_Provincial_Electoral_Districts.geojson', id_field='id', name_en='dist_name', name_fr=None),
    dict(key='nu', path=SHP + 'Territorial/NU/EN_FUTURE_NU_Constituencies.shp', id_field=None, name_en='Name_E', name_fr='Name_Fr'),
    dict(key='nwt', path=SHP + 'Territorial/NWT/ElectoralDistricts.shp', id_field=None, name_en='ED', name_fr=None),
    dict(key='yt', path=SHP + 'Territorial/YT/Yukon_Electoral_Districts.shp', id_field=None, name_en='ELECT_NAME', name_fr='FR_ELECNAM'),
    dict(key='pei', path=SHP + 'Provincial/PEI/Electoral_District_Boundaries.shp', id_field='DIST_NO', name_en='DISTRICT', name_fr=None),
    dict(key='qc', path=SHP + 'Provincial/QC/circonscriptions_électorales_2026_shapefile/Circonscription_electorales_2026_shapefile.shp', id_field='CO_CEP', name_en='NM_CEP', name_fr=None),
]

def norm_id(raw):
    s = str(raw).strip()
    return s[:-1] if s.endswith('-') else s

def key_of(ds, id_field, name, raw):
    if id_field:
        return norm_id(raw)
    return name

def pip_pt(rings):
    """Return a point inside all rings (even-odd), else None."""
    minlon = min(min(p[0] for p in r) for r in rings)
    maxlon = max(max(p[0] for p in r) for r in rings)
    minlat = min(min(p[1] for p in r) for r in rings)
    maxlat = max(max(p[1] for p in r) for r in rings)
    def inside(pt, rings):
        cnt = 0
        for ring in rings:
            x, y = pt
            n = len(ring)
            for i in range(n):
                x1, y1 = ring[i]
                x2, y2 = ring[(i + 1) % n]
                if (y1 > y) != (y2 > y):
                    xin = x1 + (y - y1) / (y2 - y1) * (x2 - x1)
                    if xin > x:
                        cnt += 1
        return cnt % 2 == 1
    cand = [(minlon + maxlon) / 2, (minlat + maxlat) / 2]
    if inside(cand, rings):
        return cand
    step = max(1, len(rings[0]) // 200)
    for i in range(0, len(rings[0]), step):
        c = (rings[0][i][0], rings[0][i][1])
        if inside(c, rings):
            return c
    return None

def load_shapefile(path, tr):
    sr = shapefile.Reader(path, encoding='UTF-8', encodingErrors='strict')
    fields = [f[0] for f in sr.fields[1:]]
    recs = list(sr.records())
    feats = []
    for rec, shp in zip(recs, sr.shapes()):
        rings = []
        for i in range(len(shp.parts)):
            end = shp.parts[i + 1] if i + 1 < len(shp.parts) else len(shp.points)
            pts = shp.points[i:end]
            if tr is not None and pts:
                lons, lats = tr.transform([p[0] for p in pts], [p[1] for p in pts])
                pts = list(zip(lons, lats))
            rings.append(pts)
        feats.append((rec, rings))
    return fields, feats

def load_geojson(path):
    import json
    obj = json.load(open(path, encoding='utf-8'))
    feats = []
    for f in obj['features']:
        props = f.get('properties') or {}
        geom = f['geometry']
        if geom['type'] == 'MultiPolygon':
            polys = geom['coordinates']
        else:
            polys = [geom['coordinates']]
        rings = []
        for poly in polys:
            for ring in poly:
                rings.append([(p[0], p[1]) for p in ring])
        feats.append((props, rings))
    return feats

def selected_indices(n):
    idxs = [0, n // 2, n - 1]
    seen, out = set(), []
    for i in idxs:
        if i not in seen and i < n:
            seen.add(i)
            out.append(i)
    return out

def run(ds):
    key = ds['key']
    tr = None
    if key in PROJ4:
        tr = Transformer.from_crs(PROJ4[key], WGS84, always_xy=True)
        tfwd = Transformer.from_crs(WGS84, PROJ4[key], always_xy=True)
    is_gj = ds['path'].endswith('.geojson')
    fields, feats = (None, None) if is_gj else load_shapefile(ds['path'], tr)
    if is_gj:
        feats = load_geojson(ds['path'])
    recs = [f[0] for f in feats]
    if recs and not isinstance(recs[0], dict):
        recs = [r.as_dict() for r in recs]
        feats = [(recs[i], f[1]) for i, f in enumerate(feats)]

    def val(rec, f):
        v = rec.get(f)
        return v if v is not None else ''

    groups = {}
    for i, rec in enumerate(recs):
        name = str(val(rec, ds['name_en'])).strip()
        gid = key_of(ds, ds['id_field'], name, val(rec, ds['id_field']) if ds['id_field'] else '')
        g = groups.setdefault(gid, {'name': name, 'feats': []})
        g['feats'].append(i)

    sel = selected_indices(len(feats))
    if key == 'ns':
        for i, rec in enumerate(recs):
            if str(val(rec, 'ED_NO')).strip() == '29-':
                sel.append(i)

    outdir = os.path.join(OUT, key)
    os.makedirs(outdir, exist_ok=True)
    geo_rows, count_rows, pip_rows = [], [], []
    for i in sel:
        rec, rings = feats[i]
        name = str(val(rec, ds['name_en'])).strip()
        gid = key_of(ds, ds['id_field'], name, val(rec, ds['id_field']) if ds['id_field'] else '')
        gname = groups[gid]['name']
        for v in range(min(20, len(rings[0]))):
            geo_rows.append([i, 0, v, rings[0][v][0], rings[0][v][1]])
        count_rows.append([i, 0, len(rings[0])])
        pt = pip_pt(rings)
        if pt:
            pip_rows.append([pt[1], pt[0], gid, gname])

    with open(os.path.join(outdir, 'geo.csv'), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['feature_index', 'ring_index', 'vertex_index', 'lon', 'lat'])
        w.writerows(geo_rows)
    with open(os.path.join(outdir, 'ring_counts.csv'), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['feature_index', 'ring_index', 'vertex_count'])
        w.writerows(count_rows)
    with open(os.path.join(outdir, 'pip.csv'), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['lat', 'lon', 'id', 'name'])
        w.writerows(pip_rows)

    if tr is not None:
        lons, lats = [], []
        for _, rings in feats:
            for r in rings:
                lons.extend(p[0] for p in r)
                lats.extend(p[1] for p in r)
            if len(lons) > 200000:
                break
        minlon, maxlon, minlat, maxlat = min(lons), max(lons), min(lats), max(lats)
        rows = []
        for i in range(12):
            for j in range(12):
                lat = minlat + (maxlat - minlat) * (i + 0.5) / 12
                lon = minlon + (maxlon - minlon) * (j + 0.5) / 12
                x, y = tfwd.transform(lon, lat)
                rows.append([lat, lon, x, y])
        with open(os.path.join(outdir, 'proj.csv'), 'w', encoding='utf-8', newline='') as f:
            w = csv.writer(f)
            w.writerow(['lat', 'lon', 'x', 'y'])
            for r in rows:
                w.writerow([repr(a) for a in r])

    meta = {
        'kind': 'geojson' if is_gj else 'shapefile',
        'fields': fields if fields else '',
        'records': len(feats),
        'districts': len(groups),
        'projected': key in PROJ4,
    }
    with open(os.path.join(outdir, 'meta.txt'), 'w', encoding='utf-8') as f:
        for k, v in meta.items():
            f.write(f'{k}={v}\n')
    print(key, 'records:', len(feats), 'districts:', len(groups),
          'pip:', len(pip_rows), 'projected:', key in PROJ4)

print('field check:')
for ds in DATASETS:
    if ds['path'].endswith('.geojson'):
        continue
    sr = shapefile.Reader(ds['path'], encoding='UTF-8')
    recs = sr.records()
    print(ds['key'], [f[0] for f in sr.fields[1:]], 'n:', len(recs), ':', recs[0])
for ds in DATASETS:
    run(ds)
print('done')
