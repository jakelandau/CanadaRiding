"""Offline ground truth for the vintage tests.

For each past-vintage KML, take each named district's representative point and
report which current-vintage SHP district contains it. Output is used to hard-code
expected names in VintageTests.cs.
"""
import xml.etree.ElementTree as ET

import shapefile
from shapely import wkt
from shapely.geometry import MultiPolygon, Polygon, Point
from shapely.geometry import shape

DATA = r"D:\Documents\GitHub\CanadaRiding\data\Shapefiles"


def kml_districts(path):
    """Parse a KML export into {name: shapely geometry}, cp1252-fallback decode."""
    raw = open(path, "rb").read()
    tree = ET.fromstring(mixed_decode(raw))
    out = {}
    for pm in tree.iter():
        if not pm.tag.endswith("Placemark"):
            continue
        name = None
        attrs = {}
        polys = []
        for el in pm.iter():
            if el.tag.endswith("SimpleData"):
                attrs[el.get("name")] = (el.text or "").strip()
            elif el.tag.endswith("name") and name is None and el.text:
                name = el.text.strip()
            if el.tag.endswith("coordinates"):
                pts = []
                for tok in (el.text or "").split():
                    parts = tok.split(",")
                    pts.append((float(parts[0]), float(parts[1])))
                if pts:
                    polys.append(Polygon(pts))
        if name is None:
            for key in ("ED_NAME", "ELECT_NAME"):
                if attrs.get(key):
                    name = attrs[key]
                    break
        if name and polys:
            out[name] = polys[0] if len(polys) == 1 else MultiPolygon(polys)
    return out


def mixed_decode(raw):
    """UTF-8 with per-byte cp1252 fallback, as the library's KML reader does."""
    out = []
    i = 0
    while i < len(raw):
        b = raw[i]
        if b < 0x80:
            out.append(chr(b))
            i += 1
        else:
            n = utf8_len(raw, i)
            if n:
                out.append(raw[i:i+n].decode("utf-8"))
                i += n
            else:
                out.append(bytes([b]).decode("cp1252"))
                i += 1
    return "".join(out)


def utf8_len(raw, i):
    b = raw[i]
    if 0xC2 <= b <= 0xDF:
        n = 2
    elif 0xE0 <= b <= 0xEF:
        n = 3
    elif 0xF0 <= b <= 0xF4:
        n = 4
    else:
        return 0
    if i + n > len(raw):
        return 0
    for j in range(i + 1, i + n):
        if raw[j] & 0xC0 != 0x80:
            return 0
    if b == 0xE0 and raw[i+1] < 0xA0:
        return 0
    return n


def parse_polygons(elem):
    """Yield list-of-ring coordinate lists from a <Polygon> element."""
    if elem is None:
        return []
    for inner in elem.iter():
        if inner.tag.endswith("}coordinates"):
            pts = []
            for tok in inner.text.split():
                parts = tok.split(",")
                pts.append((float(parts[0]), float(parts[1])))
            if pts:
                return [pts]
    return []


def rep_point(geom):
    p = geom.representative_point()
    return p.x, p.y  # lon, lat


def shp_districts(path, id_field, name_field):
    sf = shapefile.Reader(path)
    out = []
    for geom_shape, rec in zip(sf.shapes(), sf.records()):
        geom = shape(geom_shape.__geo_interface__)
        out.append((rec[name_field], rec[id_field] if id_field else None, geom))
    return out


def contains(districts, lon, lat):
    p = Point(lon, lat)
    return [name for (name, _id, geom) in districts if geom.contains(p)]


# --- QC: 2022 KML vs 2026 SHP -----------------------------------------
qc2022 = kml_districts(DATA + r"\2022Quebec.kml")
qc2026 = shp_districts(
    DATA + r"\Provincial\QC\circonscriptions_électorales_2026_shapefile\Circonscription_electorales_2026_shapefile.shp",
    "CO_CEP", "NM_CEP")

targets = ["Châteauguay", "Mégantic", "Prévost", "Mille-Îles", "Lotbinière-Frontenac", "Vachon"]
print("=== QC ===")
for t in targets:
    if t not in qc2022:
        print(f"{t}: NOT in 2022 KML")
        continue
    lon, lat = rep_point(qc2022[t])
    cur = contains(qc2026, lon, lat)
    print(f"{t}: point=({lat:.10f},{lon:.10f}) in2022={t!r} in2026={cur}")

# --- NS: 2021 KML vs 2026 SHP ------------------------------------------
ns2021 = kml_districts(DATA + r"\2019NovaScotia.kml")
ns2026 = shp_districts(DATA + r"\Provincial\NS\NS_2026ED_Bnds.shp", "ED_NO", "ED_NAME")
print("=== NS ===")
for t in ["Richmond"]:
    if t not in ns2021:
        print(f"{t}: NOT in 2021 KML")
        continue
    lon, lat = rep_point(ns2021[t])
    cur = contains(ns2026, lon, lat)
    print(f"{t}: point=({lat:.10f},{lon:.10f}) in2021={t!r} in2026={cur}")

# --- YT: 2021 KML vs 2025 SHP ------------------------------------------
yt2021 = kml_districts(DATA + r"\2021Yukon.kml")
yt2025 = shp_districts(DATA + r"\Territorial\YT\Yukon_Electoral_Districts.shp", None, "ELECT_NAME")
print("=== YT ===")
for t in ["Riverdale North", "Klondike", "Whitehorse Centre"]:
    if t not in yt2021:
        print(f"{t}: NOT in 2021 KML")
        continue
    lon, lat = rep_point(yt2021[t])
    cur = contains(yt2025, lon, lat)
    print(f"{t}: point=({lat:.10f},{lon:.10f}) in2021={t!r} in2025={cur}")
