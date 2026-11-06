"""Validate distribution JSON against the schema and canonical recipe graph.

Seed-specific bounded placement and assignment are verified by the .NET generator;
this validator checks configuration structure and seed-independent contradictions.
"""

import json
from pathlib import Path

from jsonschema import Draft202012Validator, validators

from tools.content import ContentError, ROOT, load_content, parse_json

DEFAULT_WORLD = ROOT / "data/world-generation.default.json"
WORLD_SCHEMA = ROOT / "data/world-generation.schema.json"
# Json.NET's authoritative configuration loader requires integer JSON tokens,
# not numerically integral floating-point values or Python booleans.
WorldSchemaValidator = validators.extend(Draft202012Validator, type_checker=
    Draft202012Validator.TYPE_CHECKER.redefine("integer", lambda checker, instance: type(instance) is int))


def validate_world_generation(value: dict, content: dict | None = None) -> dict:
    schema = json.loads(WORLD_SCHEMA.read_text(encoding="utf-8"))
    errors = sorted(WorldSchemaValidator(schema).iter_errors(value), key=lambda e: str(list(e.path)))
    if errors:
        error = errors[0]
        path = ".".join(map(str, error.path)) or "configuration"
        raise ContentError(f"{path}: {error.message}")

    def fail(message):
        raise ContentError(message)

    def bounds(item, path):
        if item["min"] > item["max"]:
            fail(f"{path}: min must not exceed max.")

    for key in ("townCount", "population", "prosperity", "marketCash", "householdCashPerResident"):
        bounds(value[key], key)
    default = json.loads(DEFAULT_WORLD.read_text(encoding="utf-8"))
    geo = default["geography"] | value["geography"]
    count = value["townCount"]["min"]
    if 2 * geo["margin"] >= min(geo["width"], geo["height"]):
        fail("geography.margin leaves no placement area.")
    if geo["maximumLaneDistance"] < max(geo["minimumLaneDistance"], geo["minimumTownSpacing"]):
        fail("geography.maximumLaneDistance must cover minimum lane distance and town spacing.")
    if geo["extraLaneCount"] > count * (count - 1) // 2 - (count - 1):
        fail("geography.extraLaneCount exceeds available links for townCount.min.")
    npc = default["npc"] | value["npc"]
    if npc["minimumCount"] > npc["maximumCount"]:
        fail("npc.minimumCount must not exceed maximumCount.")
    if npc["maximumShips"] < npc["initialShips"] or npc["maximumCount"] * npc["maximumShips"] + value.get("fleetLimit", default["fleetLimit"]) > 256:
        fail("NPC initial/max fleet bounds require initialShips <= maximumShips and total fleet <= 256.")
    if npc["merchantWeight"] + npc["industrialWeight"] + npc["diversifiedWeight"] == 0:
        fail("At least one NPC strategy weight must be positive.")
    bounds(npc["startingCash"], "npc.startingCash")
    bounds(npc["cargoCapacity"], "npc.cargoCapacity")
    institutions = value["institutions"]
    for key, index in institutions.items():
        if index is not None and index >= count:
            fail(f"institutions.{key} must be below townCount.min.")
    if institutions.get("mintPortIndex") is not None and institutions.get("mintPortIndex") == institutions.get("silverPortIndex"):
        fail("Mint and Silver source require separate towns.")

    content = content or load_content()
    extension = content.get("expandingEconomy", {})
    goods = {g["id"] for g in content["goods"] + extension.get("goods", [])}
    recipes = content["recipes"] + extension.get("recipes", [])
    enabled = set(value["enabledGoods"])
    if enabled - goods:
        fail("Unknown enabled good: " + ", ".join(sorted(enabled - goods)))
    if "bread" not in enabled:
        fail("enabledGoods must include Bread for the current household economy.")
    distributions = value["industries"]
    ids = [d["industryId"] for d in distributions]
    if len(ids) != len(set(ids)):
        fail("industries must contain unique industry IDs.")
    for industry in distributions:
        key = industry["industryId"]
        for field in ("publicTowns", "privateTowns"):
            bounds(industry[field], key + "." + field)
        if max(industry["publicTowns"]["min"], industry["privateTowns"]["min"]) > count:
            fail(f"Industry {key} minimum availability exceeds townCount.min.")
        if max(industry["publicTowns"]["max"], industry["privateTowns"]["max"]) == 0:
            fail(f"Enabled industry {key} must permit a public or private town.")
        local = [r for r in recipes if r["buildingId"] == key]
        if not local:
            fail(f"Unknown producing industry: {key}.")
        if (value["enforceProductionSeparation"] and any(q["goodId"] == "planks" for r in local for q in r["outputs"]) and
                max(industry["publicTowns"]["min"], industry["privateTowns"]["min"]) >= count):
            fail(f"Industry {key} requires every town at townCount.min, leaving no shipyard without local hull-material production.")
        for recipe in local:
            for quantity in recipe["inputs"] + recipe["outputs"]:
                if quantity["goodId"] not in enabled:
                    fail(f"Industry {key} requires enabled good {quantity['goodId']}.")
    for good, stock in value["startingInventories"].items():
        if good not in enabled:
            fail(f"startingInventories references disabled/unknown good {good}.")
        bounds(stock, "startingInventories." + good)
    selected = [r for r in recipes if r["buildingId"] in ids]
    if value["requireSupplyChains"]:
        obtainable = set()
        for _ in selected:
            for recipe in selected:
                if all(q["goodId"] in obtainable for q in recipe["inputs"]):
                    obtainable.update(q["goodId"] for q in recipe["outputs"])
        missing = {q["goodId"] for r in selected for q in r["inputs"] if q["goodId"] not in obtainable}
        if "bread" not in obtainable:
            missing.add("bread")
        if missing:
            fail("No renewable source for enabled supply chain: " + ", ".join(sorted(missing)))
    if value["enforceProductionSeparation"]:
        for first in selected:
            for second in selected:
                if first["id"] >= second["id"] or first["buildingId"] != second["buildingId"]:
                    continue
                if ({q["goodId"] for q in first["outputs"]} & {q["goodId"] for q in second["inputs"]} or
                        {q["goodId"] for q in second["outputs"]} & {q["goodId"] for q in first["inputs"]}):
                    fail(f"Industry {first['buildingId']} contains directly incompatible recipes.")
        inputs = {key: {q["goodId"] for r in selected if r["buildingId"] == key for q in r["inputs"]} for key in ids}
        outputs = {key: {q["goodId"] for r in selected if r["buildingId"] == key for q in r["outputs"]} for key in ids}
        for first in distributions:
            a = first["industryId"]
            for second in distributions:
                b = second["industryId"]
                if a >= b or not (outputs[a] & inputs[b] or outputs[b] & inputs[a]):
                    continue
                if max(first["publicTowns"]["min"], first["privateTowns"]["min"]) + max(second["publicTowns"]["min"], second["privateTowns"]["min"]) > count:
                    fail(f"Incompatible industries {a} and {b} require more separate towns than townCount.min.")
    return value


def load_world_generation(path: Path = DEFAULT_WORLD, content: dict | None = None) -> dict:
    raw = path.read_text(encoding="utf-8")
    if len(raw) > 131072:
        raise ContentError("World generation JSON must be at most 128 KiB.")
    return validate_world_generation(parse_json(raw), content)
