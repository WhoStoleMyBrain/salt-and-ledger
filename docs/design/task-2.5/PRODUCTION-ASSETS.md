# Task 2.5 production art sources

The runtime kit is original project-authored vector artwork, implemented as small UI Toolkit `Painter2D` elements. It is **not** an extraction from the supplied concept sheets, a screenshot skin, or artwork copied from another game. The concept sheets remain inspiration only and are not runtime resources.

## Source inventory and provenance

| Source | Runtime use | Provenance / dependency |
| --- | --- | --- |
| `unity/Assets/SaltAndLedger/Runtime/ArchipelagoLab/MaritimeGlyph.cs` | Commodity, account, action and map landmark symbols | Original authored geometric paths created for Salt & Ledger Task 2.5; no third-party image or font material |
| `unity/Assets/SaltAndLedger/Runtime/ArchipelagoLab/MaritimeIllustration.cs` | Small harbor, representative bakery and ship vignettes | Original authored woodcut-style composition, sharing the glyph palette; not a factual town building census |
| `unity/Assets/SaltAndLedger/Runtime/ArchipelagoLab/IllustratedMaritime.uss` | Shared parchment/sea materials, semantic states, goods and recipe rows | Original project styling; inherits the existing text-scale contract |
| `unity/Assets/SaltAndLedger/Runtime/ArchipelagoLab/ArchipelagoMap.cs` | Coast, chart and marker artwork at actual generated coordinates | Existing project map evolved for this milestone; geometry remains presentation-only |

No third-party source material was incorporated into these new drawings, so no third-party asset license or attribution dependency is introduced. The drawings are maintained as project source; this change does not add an open-source license or grant rights to third-party inspiration images. No downloaded stock art, commercial font, external runtime fetch, icon package, SVG importer or raster atlas is required. Existing Unity runtime text rendering remains unchanged.

The small illustration is deliberately schematic. A decorative workshop is a visual sign for industry, not a claim that an unbuilt facility exists, and the illustrated ship is not a new purchasable hull. All names, ownership, quantities, recipes and statuses beside the artwork must bind to actual game state.

## Symbol vocabulary

| Key | Visible form | Text equivalent |
| --- | --- | --- |
| `logs` | Round-cut bark logs with growth rings | Logs |
| `planks` | Straight milled boards with parallel grain | Planks |
| `grain` | Bound wheat sheaf | Grain |
| `flour` | Tied flour sack with grain seal | Flour |
| `bread` | Scored oval loaf | Bread |
| `furniture` | Timber chair with back and legs | Furniture |
| `silver` | Faceted silver ingot | Silver |
| `ship` | Cream sails, mast and hull | Ship |
| `industry` | Sawtooth workshop and chimney | Industry |
| `market` | Balance scales | Market |
| `cash` | Coin stack and upright coin | Coins |
| `warehouse` | Roofed storehouse with braced doors | Company stock |
| `attention` | Exclamation triangle | Attention, plus the actual reason |
| `info` | Letter i in a roundel | Information |
| `farm`, `forest`, `mill`, `lighthouse` | Sheaf, firs, windmill and coastal tower | Food source, Forest, Processing, Coastal town |

Every functional use keeps a short text name, tooltip or explicit status sentence. The color is supplementary: Logs and Planks have different silhouettes, Flour and Grain have different containers, and warning states retain words or `!`. The host must not infer status from the icon.

## Import and reproduction

The production source **is** the C# path geometry. Unity compiles it with the ordinary host assembly; each file has a stable checked-in `.meta`. `generateVisualContent` issues bounded paths through the engine's supported Painter2D API at the actual element bounds. There is no image conversion step and no editor-only asset generator to run. The same `MaritimeGlyph.Draw` renders chart landmarks, while `new MaritimeGlyph(key)` works in tables and recipes.

The element ignores picking so the parent button remains the keyboard/pointer target. Each icon uses a 64-unit source canvas fit uniformly into its bounds. Shared size classes provide 24/30/48px small/normal/large defaults and 28/34/56px at Large/Extra Large text. Map markers use a narrower dedicated glyph class beside the short owned/NPC label. Artwork is repainted on layout/key changes, not recomputed as an economic update. The separate map can interpolate committed ship positions without changing icon meaning.

`MaritimeIllustration(kind)` supports `harbor`, `bakery` and `ship`. Its small variant is 112×66px, normal 156×92px, with a 140px-high banner variant for a dedicated card. The content is static vector decoration; no stock, route, population or forecast exists inside it.

## Palette, typography and review boundary

The source of truth for final component colors is the `--sl-*` token block in `IllustratedMaritime.uss`. The production palette keeps the required deep sea, navy, parchment, ink, gold, green and rust values. Secondary paper text uses **`#536067`** rather than the moodboard's lighter `#64727A` to improve numerical/readability contrast; dark gold **`#775527`** is used for small category labels on parchment. Small warning text uses **`#93452F`**, while the original rust remains in artwork, trim and filled buttons. Light antique gold remains trim rather than small vital text.

No new font is distributed. The existing crisp runtime face provides body, exact quantities, controls and headings, with hierarchy from size, weight and spacing. This is a deliberate accessibility-first deviation from the illustrated serif titles. Text keeps the original 14/17/21/25px body scale; artwork never shrinks text to make room.

The source palette's calculated sRGB contrast ratios are ink/parchment **9.67:1**, muted/parchment **4.89:1**, dark gold/parchment **5.09:1**, warning ink/parchment **5.05:1**, green/parchment **4.54:1**, cream/primary green **5.11:1**, and cream/navy **13.32:1**. These measure solid swatches, not actual screen composition. Source C# compilation against the installed Unity 6000.6.6f1 assemblies passed with C# 8 / `netstandard2.1`, zero warnings/errors; that does not replace actual Unity import and runtime drawing checks.

Actual rendering was reviewed in the running Editor and in the rebuilt Windows Mono player's **24 final frames** covering town/deck, Economy, Fleet, Harbor table/order and Industry at 1366×768 / 1920×1080, Default/Large. The player frames show the actual continued day-4 world. The seven good symbols and shared recipe shapes remain recognizable beside their text, and the Harbor/Industry workshop plus Fleet ship vignettes render as real UI elements. The paper text, calm/warning distinction and dark ship-label correction are visible in those frames. Dense details intentionally scroll at the smaller resolution; no texture import fallback was needed.

The final **24-frame Editor matrix** and the rebuilt player matrix both confirm separate ship markers/town callouts at 1366×768 Large. Four supplemental player Harbor frames select actual available Bread: the complete **39.5 gross / 0.79 fee / 38.71 net** preview and Buy/Sell actions fit the initial Default viewport, with Large using its intentional internal reflow. The player's day-4 Grain frames separately demonstrate correct disabled Sell styling for empty local stock. Compact/Extra Large Fleet frames were reviewed in both hosts, and the player's day-16 itinerary frame shows the same artwork with real changed cargo and voyage state. Artwork, text and control sizes remain intact. Native Windows input remains unavailable through the computer-use pipe; actual UI Toolkit event-driven interactions are verified separately. The lead's [verification record](../../TASK-2.5-VERIFICATION.md) owns the committed final player evidence, actual compilation/tests, focus and acceptance limits. Concept images remain separately identified and are never presented as live captures.
