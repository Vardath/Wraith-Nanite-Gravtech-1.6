#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
path = root / "Source" / "WNGR2" / "Wraith" / "WraithFactionPolitics.cs"
errors = []

if not path.exists():
    errors.append("missing WraithFactionPolitics.cs")
else:
    text = path.read_text(encoding="utf-8", errors="replace")
    required = {
        "CinderVeiledGoodwill = -35": "Cinder/Veiled starting relation",
        "CinderPaleGoodwill = -65": "Cinder/Pale starting relation",
        "VeiledPaleGoodwill = 25": "Veiled/Pale starting relation",
        "if (initialized ||": "one-time initialization guard",
        "initialized = a && b && c": "successful initialization latch",
        "wngWraithLineageRelationsInitialized": "save-persistent initialization state",
        "WNG_WraithCinderCourt": "Cinder lineage resolution",
        "WNG_WraithVeiledHive": "Veiled lineage resolution",
        "WNG_WraithExiles": "Pale lineage resolution",
    }
    for needle, label in required.items():
        if needle not in text:
            errors.append("missing " + label)
    if "WNG_WraithBrood" in text:
        errors.append("Sable Brood must remain governed by its permanentEnemy FactionDef, not mutable goodwill initialization")

if errors:
    for error in errors:
        print("ERROR:", error)
    sys.exit(1)

print("Wraith lineage politics audit: OK")
