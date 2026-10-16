"""One-off build: fetch geolocator FSA centroids for every StatCan 2021 CFSA
plus any active-domain FSA not in the StatCan list. Writes tools/geolocator_hits.csv.

StatCan source: https://geo.statcan.gc.ca/geo_wa/rest/services/2021/
Cartographic_boundary_files/MapServer/14 (CFSAUIDs listed in tools/statcan_cfsaus.txt).

The geolocator API is flaky under parallel load (transient 404 {"message":"Forbidden"}),
so probe with low concurrency and retry 404s.

Throwaway script (see THIRD_PARTY.md, "Dev-only tools").
"""

import json
import sys
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor

BASE = "https://geolocator.api.geo.ca/?q={q}&keys=fsa"
FIRST = [c for c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ" if c not in "DFIOWZ"]
MIDDLE = "0123456789"
LAST = [c for c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ" if c not in "DFIOWZ"]
DOMAIN = {a + b + c for a in FIRST for b in MIDDLE for c in LAST}


def probe(code: str):
    """Return (code, status, lat, lng). status: OK | MISS | EXC:<detail>."""
    last = None
    for attempt in range(6):
        try:
            with urllib.request.urlopen(BASE.format(q=code), timeout=30) as r:
                data = json.loads(r.read().decode())
            break
        except urllib.error.HTTPError as e:
            if e.code == 404:
                last = "HTTP404"
                time.sleep(0.5 + attempt * 0.5)
                continue
            last = f"HTTP{e.code}"
            break
        except Exception as e:  # noqa: BLE001 - transient network errors, retried
            last = repr(e)
            time.sleep(0.5)
    else:
        return code, f"EXC:{last}", None, None
    for item in data:
        if isinstance(item, dict) and item.get("key") == "fsa" and item.get("name") == code:
            return code, "OK", item["lat"], item["lng"]
    return code, "MISS", None, None


def main() -> None:
    statcan = set(
        line.strip()
        for line in open("tools/statcan_cfsaus.txt", encoding="utf-8")
        if line.strip()
    )
    targets = sorted(statcan | (DOMAIN - statcan))
    print(f"targets={len(targets)} statcan={len(statcan)} domain-extra={len(DOMAIN - statcan)}")

    results: dict[str, tuple[str, object, object]] = {}
    done = 0
    with ThreadPoolExecutor(max_workers=2) as pool:
        for code, status, lat, lng in pool.map(probe, targets):
            done += 1
            if done % 100 == 0:
                print(f"{done}/{len(targets)}", flush=True)
            results[code] = (status, lat, lng)

    from collections import Counter

    counts = Counter(r[0].split(":", 1)[0] for r in results.values())
    print("statuses:", dict(counts))

    with open("tools/geolocator_hits.csv", "w", encoding="utf-8") as f:
        f.write("fsa,statcan,latitude,longitude\n")
        for code in sorted(results):
            status, lat, lng = results[code]
            if status == "OK":
                f.write(f"{code},{'1' if code in statcan else '0'},{lat},{lng}\n")

    in_statcan = [c for c in statcan if results.get(c, ("MISS",))[0] != "OK"]
    domain_extra = sorted(DOMAIN - statcan)
    domain_extra_hits = [c for c in domain_extra if results.get(c, ("MISS",))[0] == "OK"]
    print(f"statcan fsas without geolocator data: {len(in_statcan)}")
    print(f"  sample: {in_statcan[:20]}")
    print(f"domain codes not in statcan but with geolocator data: {len(domain_extra_hits)}")
    print(f"  list: {domain_extra_hits}")
    # Cross-checks from the live service (verbatim 2026-10-16).
    for code, lat, lng in [("K1S", 45.401193, -75.687205), ("V5K", 49.280764, -123.039539)]:
        print(f"check {code}: expected=({lat},{lng}) got={results.get(code)}")


if __name__ == "__main__":
    main()
