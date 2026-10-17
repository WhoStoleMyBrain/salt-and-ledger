"""Load the shared content format and check structural and semantic rules."""

import json
import re
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = ROOT / "data" / "content.json"
DEFAULT_SCHEMA = ROOT / "data" / "content.schema.json"
ID_PATTERN = re.compile(r"[a-z][a-z0-9_]*")


class ContentError(ValueError):
    """An invalid content document, with a useful author-facing message."""


def _object_without_duplicates(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        if key in result:
            raise ContentError(f"Duplicate JSON property: {key!r}")
        result[key] = value
    return result


def _reject_constant(value: str) -> None:
    raise ContentError(f"Nonstandard JSON constant: {value}")


def parse_json(text: str) -> object:
    """Reject duplicate keys and nonstandard constants accepted by json.loads."""
    try:
        return json.loads(
            text,
            object_pairs_hook=_object_without_duplicates,
            parse_constant=_reject_constant,
        )
    except json.JSONDecodeError as error:
        raise ContentError(f"Invalid JSON: {error}") from error


def validate_content(content: object, schema_path: Path = DEFAULT_SCHEMA) -> dict:
    """Validate schema, references, unique IDs, and recipe ingredient semantics."""
    schema = parse_json(schema_path.read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)
    validator = Draft202012Validator(schema)
    errors = sorted(validator.iter_errors(content), key=lambda error: str(error.json_path))
    if errors:
        raise ContentError("\n".join(f"{error.json_path}: {error.message}" for error in errors))

    # JSON Schema treats 1.0 as an integer. The runtime contract uses integer
    # tokens so neither floating-point notation nor booleans can enter counts.
    if type(content["schemaVersion"]) is not int:
        raise ContentError("schemaVersion must be an integer JSON token")

    ids = set()
    for kind in ("goods", "buildings", "recipes"):
        for entity in content[kind]:
            entity_id = entity["id"]
            if ID_PATTERN.fullmatch(entity_id) is None:
                raise ContentError(f"Invalid {kind} ID: {entity_id!r}")
            if entity_id in ids:
                raise ContentError(f"Duplicate global ID: {entity_id!r}")
            ids.add(entity_id)

    goods = {good["id"] for good in content["goods"]}
    buildings = {building["id"] for building in content["buildings"]}
    for recipe in content["recipes"]:
        if recipe["buildingId"] not in buildings:
            raise ContentError(f"Recipe {recipe['id']!r}: unknown building {recipe['buildingId']!r}")
        for side in ("inputs", "outputs"):
            seen = set()
            for ingredient in recipe[side]:
                good_id = ingredient["goodId"]
                if good_id not in goods:
                    raise ContentError(f"Recipe {recipe['id']!r} {side}: unknown good {good_id!r}")
                if good_id in seen:
                    raise ContentError(f"Recipe {recipe['id']!r} {side}: duplicate good {good_id!r}")
                if type(ingredient["quantity"]) is not int:
                    raise ContentError(f"Recipe {recipe['id']!r} {side}: quantity must be an integer JSON token")
                seen.add(good_id)
    return content


def load_content(path: Path = DEFAULT_SOURCE) -> dict:
    return validate_content(parse_json(path.read_text(encoding="utf-8")))
