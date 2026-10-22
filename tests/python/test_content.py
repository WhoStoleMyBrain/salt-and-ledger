import copy
import json
import unittest

from jsonschema import Draft202012Validator

from tools.content import ContentError, DEFAULT_SCHEMA, DEFAULT_SOURCE, expanding_content, load_content, parse_json, validate_content


class ContentValidationTests(unittest.TestCase):
    def setUp(self):
        self.content = json.loads(DEFAULT_SOURCE.read_text(encoding="utf-8"))

    def assert_invalid(self, change):
        content = copy.deepcopy(self.content)
        change(content)
        with self.assertRaises(ContentError):
            validate_content(content)

    def test_schema_is_draft_2020_12_and_valid(self):
        schema = json.loads(DEFAULT_SCHEMA.read_text(encoding="utf-8"))
        self.assertEqual("https://json-schema.org/draft/2020-12/schema", schema["$schema"])
        Draft202012Validator.check_schema(schema)
        Draft202012Validator(schema).validate(self.content)

    def test_sample_loads_with_consistent_recipe(self):
        content = load_content()
        self.assertEqual({"logs", "planks", "grain", "flour", "bread", "furniture"}, {item["id"] for item in content["goods"]})
        recipe = content["recipes"][0]
        self.assertEqual("sawmill", recipe["buildingId"])
        self.assertEqual([{"goodId": "logs", "quantity": 2}], recipe["inputs"])
        self.assertEqual([{"goodId": "planks", "quantity": 1}], recipe["outputs"])

    def test_v3_catalog_is_explicit_and_preserves_original_definitions(self):
        content = load_content()
        expanded = expanding_content(content)
        self.assertEqual(6, len(content["goods"]))
        self.assertEqual(7, len(expanded["goods"]))
        self.assertEqual(8, len(expanded["buildings"]))
        self.assertIs(content["goods"][0], expanded["goods"][0])
        self.assertEqual("silver", expanded["goods"][-1]["id"])
        self.assertEqual("extraction", expanded["recipes"][-1]["kind"])
        self.assertNotIn("royal_mint", {recipe["buildingId"] for recipe in expanded["recipes"]})

    def test_extension_is_fully_validated_and_cannot_change_base_references(self):
        for version in (2, 4, 3.0, True, None):
            self.assert_invalid(lambda data: data["expandingEconomy"].__setitem__("version", version))
        self.assert_invalid(lambda data: data["expandingEconomy"]["goods"][0].__setitem__("id", "logs"))
        self.assert_invalid(lambda data: data["expandingEconomy"]["recipes"][0]["outputs"][0].__setitem__("quantity", 1.0))
        self.assert_invalid(lambda data: data["expandingEconomy"]["recipes"][0]["outputs"][0].__setitem__("goodId", "missing"))
        self.assert_invalid(lambda data: data["expandingEconomy"].__setitem__("typo", 1))
        self.assert_invalid(lambda data: data["recipes"][0]["inputs"][0].__setitem__("goodId", "silver"))

    def test_legacy_document_without_extension_remains_valid(self):
        self.content.pop("expandingEconomy")
        self.assertIs(self.content, expanding_content(validate_content(self.content)))

    def test_required_collections_are_nonempty(self):
        for kind in ("goods", "buildings", "recipes"):
            with self.subTest(kind=kind):
                self.assert_invalid(lambda data: data.__setitem__(kind, []))
                self.assert_invalid(lambda data: data.pop(kind))

    def test_extraction_is_explicit_and_has_only_outputs(self):
        content = load_content()
        extraction = next(recipe for recipe in content["recipes"] if recipe["id"] == "harvest_logs")
        self.assertEqual("extraction", extraction["kind"])
        self.assertEqual("lumber_camp", extraction["buildingId"])
        self.assertEqual([], extraction["inputs"])
        self.assertEqual([{"goodId": "logs", "quantity": 2}], extraction["outputs"])
        self.assert_invalid(lambda data: data["recipes"][1].pop("kind"))
        self.assert_invalid(lambda data: data["recipes"][1].__setitem__("inputs", [{"goodId": "logs", "quantity": 1}]))
        self.assert_invalid(lambda data: data["recipes"][1].__setitem__("outputs", []))
        self.assert_invalid(lambda data: data["recipes"][1].__setitem__("inputs", None))
        self.assert_invalid(lambda data: data["recipes"][1].pop("inputs"))

    def test_recipe_kinds_are_closed_and_case_sensitive(self):
        for value in ("renewable", "Extraction", "", None, True, 1, []):
            with self.subTest(value=value):
                self.assert_invalid(lambda data: data["recipes"][0].__setitem__("kind", value))
        self.assert_invalid(lambda data: data["recipes"][0].__setitem__("kind", "extraction"))
        self.content["recipes"][0]["kind"] = "transformation"
        validate_content(self.content)
        self.content["recipes"][0]["inputs"] = []
        with self.assertRaises(ContentError):
            validate_content(self.content)

    def test_original_version_one_document_remains_valid(self):
        self.content["buildings"] = self.content["buildings"][:1]
        self.content["recipes"] = self.content["recipes"][:1]
        validate_content(self.content)
        self.assertNotIn("kind", self.content["recipes"][0])
        self.assertEqual(1, self.content["schemaVersion"])

    def test_schema_version_is_exact_integer_one(self):
        for version in (0, 2, "1", True, None, 1.0):
            with self.subTest(version=version):
                self.assert_invalid(lambda data: data.__setitem__("schemaVersion", version))

    def test_unknown_properties_rejected_at_every_level(self):
        for path in ((), ("goods", 0), ("buildings", 0), ("recipes", 0), ("recipes", 0, "inputs", 0)):
            with self.subTest(path=path):
                content = copy.deepcopy(self.content)
                node = content
                for part in path:
                    node = node[part]
                node["typo"] = 1
                with self.assertRaises(ContentError):
                    validate_content(content)

    def test_names_and_descriptions_are_nonblank_strings(self):
        for field in ("name", "description"):
            for value in ("", " \t\n", None, 1):
                with self.subTest(field=field, value=value):
                    self.assert_invalid(lambda data: data["goods"][0].__setitem__(field, value))

    def test_ids_reject_invalid_characters_and_trailing_newlines(self):
        for value in ("Logs", "1logs", "log-s", "../logs", "logs\n", "", "é", "logs space"):
            with self.subTest(value=value):
                self.assert_invalid(lambda data: data["goods"][0].__setitem__("id", value))

    def test_ids_are_globally_unique(self):
        self.assert_invalid(lambda data: data["goods"].append(copy.deepcopy(data["goods"][0])))
        self.assert_invalid(lambda data: data["buildings"][0].__setitem__("id", "logs"))
        self.assert_invalid(lambda data: data["recipes"][0].__setitem__("id", "sawmill"))

    def test_references_must_target_the_correct_entity_kind(self):
        for value in ("missing", "logs", "sawmill\n"):
            with self.subTest(building=value):
                self.assert_invalid(lambda data: data["recipes"][0].__setitem__("buildingId", value))
        for side in ("inputs", "outputs"):
            for value in ("missing", "sawmill", "logs\n"):
                with self.subTest(side=side, good=value):
                    self.assert_invalid(lambda data: data["recipes"][0][side][0].__setitem__("goodId", value))

    def test_recipe_sides_are_nonempty_and_have_unique_goods(self):
        for side in ("inputs", "outputs"):
            with self.subTest(side=side):
                self.assert_invalid(lambda data: data["recipes"][0].__setitem__(side, []))
                self.assert_invalid(lambda data: data["recipes"][0][side].append(copy.deepcopy(data["recipes"][0][side][0])))

    def test_quantities_are_positive_int32_integer_tokens(self):
        for quantity in (0, -1, 2147483648, 1.5, 1.0, True, "2", None):
            with self.subTest(quantity=quantity):
                self.assert_invalid(lambda data: data["recipes"][0]["inputs"][0].__setitem__("quantity", quantity))
        self.content["recipes"][0]["inputs"][0]["quantity"] = 2147483647
        validate_content(self.content)

    def test_json_parser_rejects_duplicate_keys_and_nonstandard_json(self):
        for text in (
            '{"schemaVersion": 1, "schemaVersion": 1}',
            '{"inputs": [{"goodId": "logs", "goodId": "planks"}]}',
            '{"value": NaN}', '{"value": Infinity}', '{"value": -Infinity}',
            '{"value": 1,}', '{/* comment */ "value": 1}',
            "{'value': 1}", '{value: 1}', '{"value": 0x10}',
            'leading {"value": 1}', '{"value": 1} trailing',
            '{"value": 1} {"value": 2}',
        ):
            with self.subTest(text=text), self.assertRaises(ContentError):
                parse_json(text)

    def test_float_and_exponent_integer_notation_rejected_after_parsing(self):
        text = DEFAULT_SOURCE.read_text(encoding="utf-8")
        for invalid in (
            text.replace('"schemaVersion": 1', '"schemaVersion": 1.0'),
            text.replace('"quantity": 2', '"quantity": 2e0'),
            text.replace('"quantity": 2', '"quantity": 1e999'),
        ):
            with self.subTest(invalid=invalid), self.assertRaises(ContentError):
                validate_content(parse_json(invalid))


if __name__ == "__main__":
    unittest.main()
