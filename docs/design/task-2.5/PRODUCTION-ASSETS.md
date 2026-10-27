# Production art sources

The runtime kit combines user-supplied generated raster artwork with project-authored UI Toolkit `Painter2D` symbols and fallback illustrations. Concept sheets remain inspiration only and are not runtime resources. Artwork does not encode economic state.

## Source inventory and provenance

| Source | Runtime use | Provenance / dependency |
| --- | --- | --- |
| `unity/Assets/SaltAndLedger/Art/Resources/Maritime/Goods/` | Seven commodity symbols; Planks and Silver use distinct UV regions of the supplied combined sheet | User-supplied generated assets, renamed and installed by the preceding asset-replacement task |
| `unity/Assets/SaltAndLedger/Art/Resources/Maritime/Illustrations/` | Harbor, forest harbor, bakery and ship images | User-supplied generated illustrations fitted within existing component bounds |
| `unity/Assets/SaltAndLedger/Runtime/ArchipelagoLab/MaritimeArtwork.cs` | Stable resource lookup, aspect fit and the two sheet regions | Project code; retains original supplied pixels |
| `unity/Assets/SaltAndLedger/Runtime/ArchipelagoLab/MaritimeGlyph.cs` | Commodity, account, action and map landmark symbols | Original authored geometric paths created for Salt & Ledger Task 2.5; no third-party image or font material |
| `unity/Assets/SaltAndLedger/Runtime/ArchipelagoLab/MaritimeIllustration.cs` | Small harbor, representative bakery and ship vignettes | Original authored woodcut-style composition, sharing the glyph palette; not a factual town building census |
| `unity/Assets/SaltAndLedger/Runtime/ArchipelagoLab/IllustratedMaritime.uss` | Shared parchment/sea materials, semantic states, goods and recipe rows | Original project styling; inherits the existing text-scale contract |
| `unity/Assets/SaltAndLedger/Runtime/ArchipelagoLab/ArchipelagoMap.cs` | Coast, chart and marker artwork at actual generated coordinates | Existing project map evolved for this milestone; geometry remains presentation-only |

The generated raster files were supplied by the repository owner; the repository records no separate third-party asset license for them. This cleanup does not invent a license or grant rights to reference images. Original vector paths remain project source. No downloaded stock-art package, commercial font, external runtime fetch or SVG importer is required. Existing Unity runtime text rendering remains unchanged. Unused supplied art remains under `art/backup/generated-maritime/` and is not imported as gameplay content.

Illustrations are decorative signs, not a claim that an unbuilt facility exists or a new hull is purchasable. Names, ownership, quantities, recipes and statuses beside them bind to actual game state.

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

Unity imports the supplied PNGs using checked-in metadata. `MaritimeArtwork` caches stable Resources lookups and aspect-fits them into the existing UI bounds; Planks and Silver select separate regions of their shared sheet. No regeneration step is required. Shared symbols and fallback illustrations remain C# geometry: `generateVisualContent` issues bounded Painter2D paths at the element bounds. `MaritimeGlyph.Draw` renders chart landmarks, while `new MaritimeGlyph(key)` works in tables and recipes.

The element ignores picking so the parent button remains the keyboard/pointer target. Each icon uses a 64-unit source canvas fit uniformly into its bounds. Shared size classes provide 24/30/48px small/normal/large defaults and 28/34/56px at Large/Extra Large text. Map markers use a narrower dedicated glyph class beside the short owned/NPC label. Artwork is repainted on layout/key changes, not recomputed as an economic update. The separate map can interpolate committed ship positions without changing icon meaning.

`MaritimeIllustration(kind)` supports `harbor`, `bakery` and `ship`. Its small variant is 112×66px, normal 156×92px, with a 140px-high banner variant for a dedicated card. It uses the installed raster illustration with vector fallback; no stock, route, population or forecast exists inside the artwork.

## Palette, typography and review boundary

The source of truth for final component colors is the `--sl-*` token block in `IllustratedMaritime.uss`. The production palette keeps the required deep sea, navy, parchment, ink, gold, green and rust values. Secondary paper text uses **`#536067`** rather than the moodboard's lighter `#64727A` to improve numerical/readability contrast; dark gold **`#775527`** is used for small category labels on parchment. Small warning text uses **`#93452F`**, while the original rust remains in artwork, trim and filled buttons. Light antique gold remains trim rather than small vital text.

No new font is distributed. The existing crisp runtime face provides body, exact quantities, controls and headings, with hierarchy from size, weight and spacing. This is a deliberate accessibility-first deviation from the illustrated serif titles. Text keeps the original 14/17/21/25px body scale; artwork never shrinks text to make room.

The source palette's calculated sRGB contrast ratios are ink/parchment **9.67:1**, muted/parchment **4.89:1**, dark gold/parchment **5.09:1**, warning ink/parchment **5.05:1**, green/parchment **4.54:1**, cream/primary green **5.11:1**, and cream/navy **13.32:1**. These measure solid swatches, not actual screen composition. Source C# compilation against the installed Unity 6000.6.6f1 assemblies passed with C# 8 / `netstandard2.1`, zero warnings/errors; that does not replace actual Unity import and runtime drawing checks.

Current rendered evidence and limitations are recorded in [cleanup verification](../../REPOSITORY-CLEANUP.md). Historical screenshot matrices were removed; concept sheets remain inspiration only.
