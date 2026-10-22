"""Validate canonical JSON: python tools/validate_data.py [--source PATH]."""

import argparse
import sys
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from tools.content import ContentError, DEFAULT_SOURCE, load_content


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    args = parser.parse_args()
    try:
        content = load_content(args.source)
    except (ContentError, OSError, UnicodeError) as error:
        print(f"Content validation failed: {error}", file=sys.stderr)
        return 1
    print(
        f"Validated {len(content['goods'])} goods, "
        f"{len(content['buildings'])} buildings, {len(content['recipes'])} recipes."
    )
    if "expandingEconomy" in content:
        extension = content["expandingEconomy"]
        print(f"Validated v3 extension: {len(extension['goods'])} good, "
              f"{len(extension['buildings'])} buildings, {len(extension['recipes'])} recipe.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
