"""One-off: fetch StatCan 2021 CFSA geometries from the Esri Canada ArcGIS
Online rehost (different server than the StatCan geo_wa service, which 500s
on some large polygons), compute WGS84 centroids, and store them in the
build_fsa_centroids.py cache (tools/.fsa_centroid_cache.json).

Source: https://services.arcgis.com/wjcPoefzjpzCgffS/arcgis/rest/services/
Forward_Sortation_Area_(FSA)_Boundaries_2021/FeatureServer/0
(Esri Canada's rehost of Statistics Canada's 2021 Census Forward Sortation
Area boundary file, product 92-179-X; field CFSAUID is the FSA code.)

Throwaway script (see THIRD_PARTY.md, "Dev-only tools").
"""

import json
import sys
import time
import urllib.parse
import urllib.request

from shapely.geometry import shape

QUERY = (
    "https://services.arcgis.com/wjcPoefzjpzCgffS/arcgis/rest/services/"
    "Forward_Sortation_Area_(FSA)_Boundaries_2021/FeatureServer/0/query"
)
CACHE = "tools/.fsa_centroid_cache.json"


def fetch_geojson(uid: str):
    url = QUERY + "?" + urllib.parse.urlencode(
        {
            "where": f"CFSAUID = '{uid}'",
            "returnGeometry": "true",
            "outSR": "4326",
            "f": "geojson",
        }
    )
    for attempt in range(5):
        try:
            with urllib.request.urlopen(url, timeout=300) as r:
                data = json.loads(r.read().decode())
            features = data.get("features") or []
            if not features:
                raise RuntimeError(f"no geometry for {uid}")
            return shape(features[0]["geometry"])
        except Exception as e:  # noqa: BLE001
            if attempt == 4:
                raise
            print(f"  {uid} attempt {attempt}: {e!r}", flush=True)
            time.sleep(3 * (attempt + 1))
    raise AssertionError("unreachable")


def main() -> None:
    try:
        with open(CACHE, encoding="utf-8") as f:
            cache = {k: tuple(v) for k, v in json.load(f).items()}
    except FileNotFoundError:
        cache = {}
    for uid in sys.argv[1:]:
        if uid in cache:
            continue
        geom = fetch_geojson(uid)
        c = geom.centroid
        cache[uid] = (c.y, c.x)
        print(f"{uid}: centroid=({c.y:.6f}, {c.x:.6f})", flush=True)
        with open(CACHE, "w", encoding="utf-8") as f:
            json.dump({k: list(v) for k, v in cache.items()}, f, indent=0)
        time.sleep(0.5)
    print(f"cache now has {len(cache)} uids")


if __name__ == "__main__":
    main()
