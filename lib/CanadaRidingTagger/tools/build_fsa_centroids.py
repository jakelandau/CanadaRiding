"""One-off build: compute WGS84 centroids of StatCan 2021 CFSA (forward sortation
area) polygons and merge with the geolocator live-index values.

Sources:
- StatCan 2021 CFSA boundaries, served by the ArcGIS REST service
  https://geo.statcan.gc.ca/geo_wa/rest/services/2021/Cartographic_boundary_files/MapServer/14
  (product 92-179-X). Native CRS is a NAD83 Lambert projection; the service
  reprojects to WGS84 on request (outSR=4326).
- geoview-api-geolocator live index (geolocator.api.geo.ca), swept verbatim by
  sweep_geolocator.py -> tools/geolocator_hits.csv. The geolocator ingested
  centroids of this same StatCan file; 6 CFSA areas are missing from its index.

This script (a) computes shapely centroids for the 6 missing areas plus a
verification sample of areas that are in the geolocator index, and (b) writes
the final merged data/FsaCentroids.csv: geolocator values verbatim where
present, StatCan-computed centroids for the missing areas. Any verification
sample point that disagrees with the geolocator value is reported.

Usage: python tools/build_fsa_centroids.py R0A G2A S9X L8H J3Y L8G
Throwaway script (see THIRD_PARTY.md, "Dev-only tools").
"""

import json
from pathlib import Path
import sys
import time
import urllib.parse
import urllib.request

from shapely.geometry import shape

QUERY = "https://geo.statcan.gc.ca/geo_wa/rest/services/2021/Cartographic_boundary_files/MapServer/14/query"
SCRIPT_DIR = Path(__file__).resolve().parent
ROOT = SCRIPT_DIR.parent.parent.parent
CACHE = SCRIPT_DIR / ".fsa_centroid_cache.json"


def load_cache() -> dict[str, tuple[float, float]]:
    try:
        with open(CACHE, encoding="utf-8") as f:
            raw = json.load(f)
        return {k: tuple(v) for k, v in raw.items()}
    except FileNotFoundError:
        return {}


def save_cache(cache: dict[str, tuple[float, float]]) -> None:
    with open(CACHE, "w", encoding="utf-8") as f:
        json.dump({k: list(v) for k, v in cache.items()}, f, indent=0)
# Areas in both the geolocator index and the 2021 CFSA file, for method
# verification (other geolocator-indexed areas appear to come from an older
# vintage and are absent from the 2021 service).
VERIFY = [
    "K1S", "V5K", "K1A", "A0A", "X0A", "E3A", "T2J",
    "N9A", "C1A", "M5H", "Y1A", "P4P", "J2N",
]


def fetch_geojson(uid: str):
    """Fetch one CFSA polygon in WGS84 as a shapely geometry."""
    url = QUERY + "?" + urllib.parse.urlencode(
        {
            "where": f"CFSAUID = '{uid}'",
            "outFields": "CFSAUID",
            "returnGeometry": "true",
            "outSR": "4326",
            "f": "geojson",
        }
    )
    for attempt in range(10):
        try:
            with urllib.request.urlopen(url, timeout=300) as r:
                data = json.loads(r.read().decode())
            features = data.get("features") or []
            if not features:
                raise RuntimeError(f"no geometry for {uid}")
            return shape(features[0]["geometry"])
        except Exception as e:  # noqa: BLE001 - ArcGIS service is flaky (500s)
            if attempt == 9:
                raise
            print(f"  {uid} attempt {attempt}: {e!r}", flush=True)
            time.sleep(3 * (attempt + 1))


def centroid_latlon(geom) -> tuple[float, float]:
    c = geom.centroid
    return c.y, c.x


def main() -> None:
    missing = sys.argv[1:]

    hits = {}
    with open(SCRIPT_DIR / "geolocator_hits.csv", encoding="utf-8") as f:
        for line in f.read().splitlines()[1:]:
            code, _statcan, lat, lng = line.split(",")
            hits[code] = (float(lat), float(lng))

    computed: dict[str, tuple[float, float]] = load_cache()
    for uid in list(missing) + VERIFY:
        if uid in computed:
            continue
        geom = fetch_geojson(uid)
        computed[uid] = centroid_latlon(geom)
        clat, clng = computed[uid]
        print(f"{uid}: centroid=({clat:.6f}, {clng:.6f})", flush=True)
        save_cache(computed)
        time.sleep(0.5)

    # Verification: computed centroid must match the geolocator value.
    mismatches = 0
    for uid in VERIFY:
        if uid not in hits:
            print(f"note: {uid} not in geolocator hits; skipped verification")
            continue
        clat, clng = computed[uid]
        glat, glng = hits[uid]
        if abs(clat - glat) > 0.001 or abs(clng - glng) > 0.001:
            mismatches += 1
            print(f"MISMATCH {uid}: computed=({clat:.6f},{clng:.6f}) geolocator=({glat:.6f},{glng:.6f})")
    print(f"verification: mismatches={mismatches}")

    # Final table: geolocator verbatim + computed centroids for the missing areas.
    rows = dict(hits)
    for uid in missing:
        rows[uid] = computed[uid]

    with open(ROOT / "data" / "FsaCentroids.csv", "w", encoding="utf-8", newline="\n") as f:
        f.write("fsa,latitude,longitude\n")
        for code in sorted(rows):
            lat, lng = rows[code]
            f.write(f"{code},{lat:.6f},{lng:.6f}\n")
    print(f"wrote data/FsaCentroids.csv with {len(rows)} rows")


if __name__ == "__main__":
    main()
