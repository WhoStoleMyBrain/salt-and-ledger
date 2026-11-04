# Approved Task 3.0 symbols

These local assets come from the user-supplied [Salt & Ledger UX Redesign Figma file](https://www.figma.com/design/YhPqWIwZSE2JW7fAttB8Ac/Salt---Ledger---UX-Redesign). The component and vector layers below were read through Figma MCP. Their individual vector assets were downloaded from the corresponding design-context responses; no screen, panel, mockup or Figma screenshot is used as a runtime texture.

The original SVG exports are retained unchanged in `assets/`. Unity uses transparent PNG renditions under `unity/Assets/SaltAndLedger/Art/Resources/ApprovedMaritime/`. Goods retain their original 47×47 vector canvas at four times resolution (188×188 pixels); industry symbols retain 54×54 at four times resolution (216×216 pixels). There is no recoloring, redrawing, stretching or removal of source padding. Rounded colored tiles are UI components, not baked into the icons.

| Runtime key | Component | Isolated vector layer | Original export |
| --- | --- | --- | --- |
| `Good("bread")` | `32:2` | `32:3` | [Bread](assets/goods/bread.svg) |
| `Good("flour")` | `32:6` | `32:7` | [Flour](assets/goods/flour.svg) |
| `Good("grain")` | `32:9` | `32:10` | [Grain](assets/goods/grain.svg) |
| `Good("logs")` | `32:12` | `32:13` | [Logs](assets/goods/logs.svg) |
| `Good("planks")` | `32:18` | `32:19` | [Planks](assets/goods/planks.svg) |
| `Good("furniture")` | `32:22` | `32:23` | [Furniture](assets/goods/furniture.svg) |
| `Good("silver")` | `32:25` | `32:26` | [Silver](assets/goods/silver.svg) |
| `Industry("farm")` | `49:553` | `49:554` | [Farm](assets/industry/farm.svg) |
| `Industry("bakery")` | `49:556` | `49:557` | [Bakery](assets/industry/bakery.svg) |
| `Industry("lumber_camp")` | `49:559` | `49:560` | [Lumber Camp](assets/industry/lumber-camp.svg) |
| `Industry("mill")` | `49:562` | `49:563` | [Mill](assets/industry/mill.svg) |
| `Industry("sawmill")` | `49:565` | `49:566` | [Sawmill](assets/industry/sawmill.svg) |
| `Industry("furniture_workshop")` | `49:568` | `49:569` | [Furniture Workshop](assets/industry/furniture-workshop.svg) |
| `Industry("natural")` | `49:571` | `52:708` | [Natural growth and sunlight](assets/industry/natural.svg) |

`ApprovedMaritimeAssets` caches local `Texture2D` resources. Its lookup keys are canonical good/building IDs, plus the approved `natural` presentation symbol. That symbol adds no building or production rule. Unknown keys return `null`; in particular, the approved industry component set contains no Silver Mine symbol. The caller can retain an accessible text label for that building without inventing an approved image. The seven-good set is complete.

## Import and reproduction

The PNGs were rendered from each standalone SVG in headless Chrome with a transparent background and device scale factor 4. The SVG root width and height were preserved. Only unused browser viewport outside that exact source canvas was removed. A square viewport does not change these square source assets. To regenerate, render the retained SVG, crop to `(0, 0, source_width * 4, source_height * 4)`, and save losslessly as RGBA PNG. No Figma access or temporary asset URL is needed for that step.

Unity metadata disables mipmaps and texture compression, retains non-power-of-two dimensions and alpha, uses bilinear filtering and clamp wrapping, and bounds the texture size at 256. Runtime code contains no network URLs and requires no SVG importer or new package. The 14 PNG files total 74,667 bytes before Unity import.

## Provenance and verification

These are supplied project-design assets used under the user's instruction to implement the approved Figma file. The Figma response does not include a separate third-party license or transfer of copyright; this record makes no public-domain or unrestricted-redistribution claim. Existing project artwork and its provenance remain separate.

All 14 component screenshots returned with the design contexts were inspected, then compared with a contact sheet rendered from the actual PNGs. The seven goods are distinct: loaf, sack, grain stalk, logs, stacked boards, chair and silver bars. Industry symbols match the approved farm, oven, axe, windmill, saw, workshop and growth illustrations. File checks confirmed nonempty transparent RGBA images, the expected dimensions and one metadata file per imported asset. This source/export verification is not evidence of Unity runtime layout or import success; the Task 3.0 verification record covers the integrated Editor/player checks.
