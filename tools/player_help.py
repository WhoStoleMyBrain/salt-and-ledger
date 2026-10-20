"""Validate shared player explanations and their canonical/reference links."""

from pathlib import Path

from jsonschema import Draft202012Validator

from tools.content import ContentError, ID_PATTERN, ROOT, parse_json

DEFAULT_HELP = ROOT / "data/player-help.json"
DEFAULT_HELP_SCHEMA = ROOT / "data/player-help.schema.json"


def validate_player_help(value: object, content: dict,
                         schema_path: Path = DEFAULT_HELP_SCHEMA) -> dict:
    schema = parse_json(schema_path.read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)
    errors = sorted(Draft202012Validator(schema).iter_errors(value),
                    key=lambda error: str(error.json_path))
    if errors:
        raise ContentError("\n".join(f"Player help {error.json_path}: {error.message}"
                                     for error in errors))
    if type(value["schemaVersion"]) is not int:
        raise ContentError("Player help schemaVersion must be an integer JSON token")
    ids = {item["id"] for kind in ("goods", "buildings", "recipes") for item in content[kind]}
    for entry in value["entries"]:
        entry_id = entry["id"]
        if ID_PATTERN.fullmatch(entry_id) is None or entry_id in ids:
            raise ContentError(f"Invalid or duplicate player-help ID: {entry_id!r}")
        ids.add(entry_id)
    for entry in value["entries"]:
        for related_id in entry["relatedIds"]:
            if related_id not in ids:
                raise ContentError(f"Player help {entry['id']!r}: unknown reference {related_id!r}")
            if related_id == entry["id"]:
                raise ContentError(f"Player help {entry['id']!r}: self reference")
    return value


def load_player_help(content: dict, path: Path = DEFAULT_HELP,
                     schema_path: Path = DEFAULT_HELP_SCHEMA) -> dict:
    return validate_player_help(parse_json(path.read_text(encoding="utf-8")), content, schema_path)
