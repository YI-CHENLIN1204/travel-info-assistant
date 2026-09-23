#!/usr/bin/env python3
"""Build the compact runtime airport catalogue from an OurAirports CSV snapshot."""

from __future__ import annotations

import argparse
import csv
import json
from datetime import UTC, datetime
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--source-commit", default="unknown")
    parser.add_argument("--generated-at")
    args = parser.parse_args()

    airports: list[dict[str, object]] = []
    with args.source.open(encoding="utf-8", newline="") as source:
        for row in csv.DictReader(source):
            iata = row["iata_code"].strip().upper()
            if (
                not iata
                or row["scheduled_service"] != "yes"
                or row["type"] == "closed"
            ):
                continue

            airports.append(
                {
                    "iata": iata,
                    "icao": row["icao_code"].strip().upper() or None,
                    "name": row["name"].strip(),
                    "municipality": row["municipality"].strip() or None,
                    "countryCode": row["iso_country"].strip().upper() or None,
                    "latitude": float(row["latitude_deg"]),
                    "longitude": float(row["longitude_deg"]),
                    "keywords": row["keywords"].strip() or None,
                }
            )

    airports.sort(key=lambda airport: (str(airport["iata"]), str(airport["name"])))
    generated_at = args.generated_at or datetime.now(UTC).isoformat().replace("+00:00", "Z")
    payload = {
        "source": "OurAirports",
        "sourceUrl": "https://ourairports.com/data/",
        "sourceCommit": args.source_commit,
        "generatedAt": generated_at,
        "airports": airports,
    }

    args.destination.parent.mkdir(parents=True, exist_ok=True)
    args.destination.write_text(
        json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
