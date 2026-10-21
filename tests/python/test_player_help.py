import copy
import json
import unittest
from pathlib import Path

from tools.content import ContentError, load_content
from tools.generate_docs import ENCYCLOPEDIA_PATH, render_outputs
from tools.player_help import load_player_help, validate_player_help


class PlayerHelpTests(unittest.TestCase):
    def setUp(self):
        self.content = load_content()
        self.help = load_player_help(self.content)

    def test_all_required_player_topics_exist_and_share_the_same_prose(self):
        expected = {"help_trading", "help_prices", "help_liquidity", "help_capacity",
                    "help_shipping", "help_ownership", "help_production", "help_factories",
                    "help_procurement", "help_industrial_routes", "help_cost_basis",
                    "help_finances", "help_charters", "help_challenges", "help_scoring",
                    "help_navigation", "help_seeded_worlds", "help_ascension", "help_readability",
                    "help_city_accounts", "help_city_needs", "help_city_welfare"}
        outputs = render_outputs(self.content, self.help)
        reference = json.loads(outputs[ENCYCLOPEDIA_PATH])
        entries = {entry["id"]: entry for entry in reference["mechanics"]}
        self.assertEqual(expected, set(entries))
        for authored in self.help["entries"]:
            generated = entries[authored["id"]]
            self.assertEqual(authored["paragraphs"], generated["paragraphs"])
            self.assertEqual(authored["description"], generated["description"])
            self.assertIn(Path("docs") / generated["wikiPath"], outputs)

    def test_references_resolve_across_help_and_canonical_categories(self):
        self.help["entries"][0]["relatedIds"].append("bread")
        self.assertIs(self.help, validate_player_help(self.help, self.content))
        reference = json.loads(render_outputs(self.content, self.help)[ENCYCLOPEDIA_PATH])
        entries = [item for kind in ("goods", "buildings", "recipes", "mechanics")
                   for item in reference[kind]]
        ids = {entry["id"] for entry in entries}
        for entry in entries:
            for related in entry["relatedIds"]:
                self.assertIn(related, ids)
            self.assertNotIn(entry["id"], entry["relatedIds"])
            self.assertIsInstance(entry["paragraphs"], list)

    def test_invalid_help_references_and_ids_are_rejected(self):
        for change, message in (
            (lambda value: value["entries"][0]["relatedIds"].append("absent"), "unknown reference"),
            (lambda value: value["entries"][0].update(id="bread"), "duplicate"),
            (lambda value: value["entries"].append(copy.deepcopy(value["entries"][0])), "duplicate"),
            (lambda value: value["entries"][0]["relatedIds"].append(value["entries"][0]["id"]), "self reference"),
        ):
            with self.subTest(message=message):
                value = copy.deepcopy(self.help)
                change(value)
                with self.assertRaisesRegex(ContentError, message):
                    validate_player_help(value, self.content)

    def test_structural_errors_and_unsupported_versions_are_rejected(self):
        for change in (
            lambda value: value.update(schemaVersion=2),
            lambda value: value.update(schemaVersion=1.0),
            lambda value: value.update(extra=True),
            lambda value: value["entries"][0].update(name="  "),
            lambda value: value["entries"][0].update(paragraphs=[]),
            lambda value: value["entries"][0].update(relatedIds=["bread", "bread"]),
            lambda value: value["entries"][0].update(id="bad\n"),
        ):
            value = copy.deepcopy(self.help)
            change(value)
            with self.assertRaises(ContentError):
                validate_player_help(value, self.content)

    def test_help_edits_change_both_machine_and_web_outputs(self):
        before = render_outputs(self.content, self.help)
        self.help["entries"][0]["paragraphs"][0] = "A changed player explanation."
        after = render_outputs(self.content, self.help)
        self.assertNotEqual(before[ENCYCLOPEDIA_PATH], after[ENCYCLOPEDIA_PATH])
        page = Path("docs/reference/mechanics/help_trading.md")
        self.assertNotEqual(before[page], after[page])
        self.assertIn(b"A changed player explanation", after[page])

    def test_help_order_and_link_order_do_not_change_generated_bytes(self):
        original = render_outputs(self.content, self.help)
        self.help["entries"].reverse()
        for entry in self.help["entries"]:
            entry["relatedIds"].reverse()
        self.assertEqual(original, render_outputs(self.content, self.help))

    def test_canonical_relations_are_derived_without_copied_help_recipe_facts(self):
        outputs = render_outputs(self.content, self.help)
        reference = json.loads(outputs[ENCYCLOPEDIA_PATH])
        bread = next(entry for entry in reference["goods"] if entry["id"] == "bread")
        bakery = next(entry for entry in reference["buildings"] if entry["id"] == "bakery")
        self.assertEqual(["bakery"], bread["relatedBuildingIds"])
        self.assertEqual(["flour", "logs"], bread["relatedGoodIds"])
        self.assertEqual(["flour", "logs"], bakery["inputGoodIds"])
        self.assertEqual(["bread"], bakery["outputGoodIds"])
        self.assertIn(b"1 [Flour](goods/flour.md) + 1 [Logs](goods/logs.md)", outputs[Path("docs/reference/chains.md")])
        recipe = next(entry for entry in self.content["recipes"] if entry["id"] == "bake_bread")
        recipe["outputs"][0]["quantity"] = 3
        changed = render_outputs(self.content, self.help)
        self.assertIn(b"3 [Bread](goods/bread.md)", changed[Path("docs/reference/chains.md")])
        changed_bread = next(entry for entry in json.loads(changed[ENCYCLOPEDIA_PATH])["goods"] if entry["id"] == "bread")
        self.assertIn("3 Bread", changed_bread["paragraphs"][0])


if __name__ == "__main__":
    unittest.main()
