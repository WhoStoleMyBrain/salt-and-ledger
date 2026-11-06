import copy
import json
import unittest

from tools.content import ContentError
from tools.world_generation import DEFAULT_WORLD, load_world_generation, validate_world_generation


class WorldGenerationValidationTests(unittest.TestCase):
    def setUp(self):
        self.config = json.loads(DEFAULT_WORLD.read_text())

    def test_defaults_and_examples_validate(self):
        self.assertEqual({"min": 8, "max": 12}, load_world_generation()["townCount"])
        for example in sorted((DEFAULT_WORLD.parent / "world-generation-examples").glob("*.json")):
            with self.subTest(example=example.name):
                load_world_generation(example)

    def test_unknown_fields_ids_and_reversed_bounds_fail(self):
        cases = [lambda c: c.update(unknown=True), lambda c: c["townCount"].update(min=13),
                 lambda c: c["industries"][0].update(industryId="missing"),
                 lambda c: c["enabledGoods"].append("unknown"),
                 lambda c: c["npc"].update(minimumCount=25, maximumCount=24)]
        for change in cases:
            config = copy.deepcopy(self.config)
            change(config)
            with self.assertRaises(ContentError):
                validate_world_generation(config)

    def test_npc_investment_and_strategy_bounds(self):
        cases = [dict(initialShips=4, maximumShips=3), dict(maximumShips=9),
                 dict(maximumCount=128, maximumShips=3), dict(maximumIndustries=-1),
                 dict(investmentIntervalDays=0), dict(cashReserve=-1),
                 dict(merchantWeight=0, industrialWeight=0, diversifiedWeight=0),
                 dict(industrialWeight=1.5)]
        for fields in cases:
            config = copy.deepcopy(self.config)
            config["npc"].update(fields)
            with self.subTest(fields=fields), self.assertRaises(ContentError):
                validate_world_generation(config)

    def test_integer_tokens_and_case_sensitive_properties_match_csharp(self):
        cases = [lambda c: c.update(schemaVersion=1.0), lambda c: c.update(DeadlineDays=720),
                 lambda c: c["townCount"].update(min=8.0), lambda c: c["townCount"].update(Min=8),
                 lambda c: c["geography"].update(margin=2147483647),
                 lambda c: c["npc"].update(speed=True)]
        for change in cases:
            config = copy.deepcopy(self.config)
            change(config)
            with self.assertRaises(ContentError):
                validate_world_generation(config)

    def test_missing_upstream_and_impossible_conflicting_availability_fail(self):
        self.config["industries"] = [x for x in self.config["industries"] if x["industryId"] != "farm"]
        with self.assertRaisesRegex(ContentError, "grain"):
            validate_world_generation(self.config)
        self.config["requireSupplyChains"] = False
        validate_world_generation(self.config)
        for industry in self.config["industries"]:
            if industry["industryId"] in ("mill", "bakery"):
                industry["privateTowns"] = {"min": 8, "max": 8}
        with self.assertRaisesRegex(ContentError, "Incompatible"):
            validate_world_generation(self.config)

    def test_shipyard_requires_a_town_without_local_hull_material_output(self):
        sawmill = next(i for i in self.config["industries"] if i["industryId"] == "sawmill")
        sawmill["privateTowns"] = {"min": 8, "max": 8}
        with self.assertRaisesRegex(ContentError, "shipyard"):
            validate_world_generation(self.config)

    def test_lane_and_institution_contradictions_fail(self):
        self.config["geography"]["maximumLaneDistance"] = 100
        with self.assertRaisesRegex(ContentError, "maximumLaneDistance"):
            validate_world_generation(self.config)
        self.config["geography"]["maximumLaneDistance"] = 450
        self.config["institutions"].update(mintPortIndex=1, silverPortIndex=1)
        with self.assertRaisesRegex(ContentError, "separate"):
            validate_world_generation(self.config)


    def test_opportunity_limits_are_bounded_and_require_integer_tokens(self):
        for key, invalid in (("activeContractLimit", 0), ("activeContractLimit", 9),
                             ("maximumAvailableContracts", 17), ("generationIntervalDays", 0),
                             ("offerLifetimeDays", 366), ("historyLimit", 129),
                             ("activeContractLimit", 3.0), ("historyLimit", True),
                             ("unknown", 1)):
            with self.subTest(key=key, invalid=invalid):
                config = copy.deepcopy(self.config)
                config["opportunities"][key] = invalid
                with self.assertRaises(ContentError):
                    validate_world_generation(config)
        self.config.pop("opportunities")
        validate_world_generation(self.config)
        self.config["opportunities"] = {}
        validate_world_generation(self.config)
        self.config["opportunities"] = {"activeContractLimit": 1}
        validate_world_generation(self.config)


if __name__ == "__main__":
    unittest.main()
