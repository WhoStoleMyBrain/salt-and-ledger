import copy
import json
import re
import tempfile
import unittest
from pathlib import Path

from tools.content import ROOT, load_content, validate_content
from tools.generate_docs import ENCYCLOPEDIA_PATH, render_outputs, synchronize


class ReferenceGenerationTests(unittest.TestCase):
    def setUp(self):
        self.content = load_content()

    def test_checked_in_reference_is_fresh(self):
        self.assertEqual([], synchronize(render_outputs(self.content), ROOT, check=True))

    def test_reference_contains_forward_and_reverse_semantic_links(self):
        outputs = render_outputs(self.content)
        encyclopedia = json.loads(outputs[ENCYCLOPEDIA_PATH])
        self.assertEqual(1, encyclopedia["formatVersion"])
        self.assertEqual(1, encyclopedia["sourceSchemaVersion"])
        goods = {item["id"]: item for item in encyclopedia["goods"]}
        buildings = {item["id"]: item for item in encyclopedia["buildings"]}
        recipes = {item["id"]: item for item in encyclopedia["recipes"]}
        self.assertEqual(["bake_bread", "saw_planks"], goods["logs"]["consumedByRecipeIds"])
        self.assertEqual(["harvest_logs"], goods["logs"]["producedByRecipeIds"])
        self.assertEqual(["saw_planks"], goods["planks"]["producedByRecipeIds"])
        self.assertEqual(["saw_planks"], buildings["sawmill"]["recipeIds"])
        self.assertEqual("sawmill", recipes["saw_planks"]["buildingId"])
        self.assertEqual(self.content["recipes"][0]["inputs"], recipes["saw_planks"]["inputs"])
        self.assertEqual("transformation", recipes["saw_planks"]["kind"])
        self.assertEqual(["harvest_logs"], buildings["lumber_camp"]["recipeIds"])
        self.assertEqual("extraction", recipes["harvest_logs"]["kind"])
        self.assertEqual([], recipes["harvest_logs"]["inputs"])
        for kind in ("goods", "buildings", "recipes"):
            for item in encyclopedia[kind]:
                self.assertIn(Path("docs") / item["wikiPath"], outputs)

    def test_extraction_page_explains_resource_source(self):
        outputs = render_outputs(self.content)
        recipe = outputs[Path("docs/reference/recipes/harvest_logs.md")].decode()
        self.assertIn("Recipe kind: **extraction**.", recipe)
        self.assertIn("No tradeable inputs.", recipe)
        self.assertIn("| [Logs](../goods/logs.md) | 2 |", recipe)
        original = copy.deepcopy(self.content)
        original["recipes"] = original["recipes"][:1]
        original["buildings"] = original["buildings"][:1]
        legacy = json.loads(render_outputs(validate_content(original))[ENCYCLOPEDIA_PATH])
        self.assertEqual("transformation", legacy["recipes"][0]["kind"])

    def test_all_generated_markdown_links_resolve(self):
        outputs = render_outputs(self.content)
        for path, value in outputs.items():
            if path.suffix != ".md":
                continue
            for link in re.findall(r"\]\(([^)]+)\)", value.decode("utf-8")):
                target = (ROOT / path.parent / link).resolve().relative_to(ROOT)
                self.assertTrue(target in outputs or target == Path("docs/MECHANICS.md"), f"Broken link in {path}: {link}")

    def test_generation_is_byte_stable_and_uses_lf(self):
        first = render_outputs(self.content)
        self.assertEqual(first, render_outputs(self.content))
        for value in first.values():
            self.assertTrue(value.endswith(b"\n"))
            self.assertNotIn(b"\r", value)
            self.assertNotIn(str(ROOT).encode(), value)

    def test_reordered_definitions_and_recipe_sides_have_identical_output(self):
        expanded = copy.deepcopy(self.content)
        expanded["buildings"].append({"id": "test_building", "name": "Test", "description": "Test only."})
        recipe = copy.deepcopy(expanded["recipes"][0])
        recipe["id"] = "test_recipe"
        recipe["inputs"].append({"goodId": "planks", "quantity": 3})
        recipe["outputs"].append({"goodId": "logs", "quantity": 4})
        expanded["recipes"].append(recipe)
        validate_content(expanded)

        def reverse(value):
            if isinstance(value, dict):
                return {key: reverse(item) for key, item in reversed(list(value.items()))}
            if isinstance(value, list):
                return [reverse(item) for item in reversed(value)]
            return value

        self.assertEqual(render_outputs(expanded), render_outputs(reverse(expanded)))

    def test_source_changes_update_machine_reference_and_wiki(self):
        before = render_outputs(self.content)
        self.content["recipes"][0]["inputs"][0]["quantity"] = 3
        self.content["goods"][0]["description"] = "Changed source description."
        after = render_outputs(validate_content(self.content))
        recipe_path = Path("docs/reference/recipes/saw_planks.md")
        self.assertNotEqual(before[recipe_path], after[recipe_path])
        self.assertIn(b"| [Logs](../goods/logs.md) | 3 |", after[recipe_path])
        self.assertNotEqual(before[ENCYCLOPEDIA_PATH], after[ENCYCLOPEDIA_PATH])
        recipe = next(item for item in json.loads(after[ENCYCLOPEDIA_PATH])["recipes"] if item["id"] == "saw_planks")
        self.assertEqual(3, recipe["inputs"][0]["quantity"])
        self.assertIn(b"Changed source description", after[Path("docs/reference/goods/logs.md")])

    def test_changed_relationship_updates_reverse_references(self):
        self.content["recipes"][0]["inputs"][0]["goodId"] = "planks"
        outputs = render_outputs(validate_content(self.content))
        encyclopedia = json.loads(outputs[ENCYCLOPEDIA_PATH])
        goods = {item["id"]: item for item in encyclopedia["goods"]}
        self.assertEqual(["bake_bread"], goods["logs"]["consumedByRecipeIds"])
        self.assertEqual(["make_furniture", "saw_planks"], goods["planks"]["consumedByRecipeIds"])
        self.assertNotIn(b"../recipes/saw_planks.md", outputs[Path("docs/reference/goods/logs.md")])

    def test_authored_text_cannot_inject_markdown_or_html(self):
        name = "Logs | [link](evil) <b>bold</b> *star* & stuff"
        self.content["goods"][0]["name"] = name
        self.content["goods"][0]["description"] = "Text\n# injected heading"
        outputs = render_outputs(validate_content(self.content))
        recipe = outputs[Path("docs/reference/recipes/saw_planks.md")].decode()
        self.assertIn(r"Logs \| \[link\]\(evil\) &lt;b&gt;bold&lt;/b&gt; \*star\* &amp; stuff", recipe)
        self.assertNotIn("<b>", recipe)
        self.assertNotIn("[link](evil)", recipe)
        self.assertNotIn("\n# injected", outputs[Path("docs/reference/goods/logs.md")].decode())
        machine = json.loads(outputs[ENCYCLOPEDIA_PATH])
        self.assertEqual(name, next(good for good in machine["goods"] if good["id"] == "logs")["name"])

    def test_check_reports_missing_changed_and_unexpected_files_without_writing(self):
        outputs = render_outputs(self.content)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            missing = synchronize(outputs, root, check=True)
            self.assertEqual(len(outputs), len(missing))
            self.assertEqual([], list(root.iterdir()))
            synchronize(outputs, root, check=False)
            self.assertEqual([], synchronize(outputs, root, check=True))

            changed = root / "docs/reference/goods/logs.md"
            changed.write_text("stale", encoding="utf-8")
            absent = root / ENCYCLOPEDIA_PATH
            absent.unlink()
            unexpected = root / "docs/reference/obsolete.md"
            unexpected.write_text("obsolete", encoding="utf-8")
            differences = synchronize(outputs, root, check=True)
            self.assertEqual(3, len(differences))
            self.assertTrue(any(line.startswith("Changed") for line in differences))
            self.assertTrue(any(line.startswith("Missing") for line in differences))
            self.assertTrue(any(line.startswith("Unexpected") for line in differences))
            self.assertEqual("stale", changed.read_text(encoding="utf-8"))
            self.assertFalse(absent.exists())
            self.assertTrue(unexpected.exists())

            authored = root / "docs/MECHANICS.md"
            authored.write_text("Maintained specification.", encoding="utf-8")
            unrelated = root / "generated/unrelated.txt"
            unrelated.write_text("Not generator-owned.", encoding="utf-8")
            synchronize(outputs, root, check=False)
            self.assertFalse(unexpected.exists())
            self.assertEqual([], synchronize(outputs, root, check=True))
            self.assertEqual("Maintained specification.", authored.read_text(encoding="utf-8"))
            self.assertEqual("Not generator-owned.", unrelated.read_text(encoding="utf-8"))

    def test_removed_source_entity_removes_obsolete_generated_page(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            expanded = copy.deepcopy(self.content)
            expanded["goods"].append({"id": "test_good", "name": "Test good", "description": "Test only."})
            synchronize(render_outputs(validate_content(expanded)), root, check=False)
            obsolete = root / "docs/reference/goods/test_good.md"
            self.assertTrue(obsolete.exists())
            outputs = render_outputs(self.content)
            self.assertTrue(any("Unexpected" in line for line in synchronize(outputs, root, check=True)))
            synchronize(outputs, root, check=False)
            self.assertFalse(obsolete.exists())


if __name__ == "__main__":
    unittest.main()
