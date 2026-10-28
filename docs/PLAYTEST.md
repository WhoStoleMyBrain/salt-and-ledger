# Current playtest checklist

Use ArchipelagoLab in the actual Unity Editor and Windows Mono player. Back up real local save slots before testing slot writes; test slots must not replace a player's only progress.

1. Main menu → New Run. Enter seed99, Merchant, StockOnly, Ascension0. Explore all four towns and return without changing the initial state. Draft All Harbors, A New Industry, Flour for the Bakers and Royal Institution, then Begin.
2. Inspect Map → town → Harbor → Industry → Fleet/Economy → Map. Confirm passive inspection retains day/revision/save fingerprint. Verify selected ship/town, keyboard focus, Escape/modal priority, internal scrolling and compact HUD.
3. Follow the [ordinary commission witness](GRAND_COMMISSION.md#follow-the-seed-99-witness-through-ordinary-controls). Buy actual inputs, fund distinct-town Mill/Bakery batches and Silver extraction; disable completed production where the witness says. Quotes, paid amounts and FIFO must agree.
4. Use the route planner for a Food-source→Recovery Bread buy/sell itinerary. Apply, enable, execute physical purchase/departure and save during the voyage. Load and check exact cargo, cursor, remaining days and daily budget. On arrival execute the sale, pause and continue the commission manually. Never edit a save or grant cash/materials.
5. Fulfill the finite Furniture contract and mint physically carried Silver at the public Mint. Import the four Flour to the named destination. The Journal must show real visits, funded distinct-town production, signed net sales and public issuance; all four plus solvency trigger victory immediately.
6. Inspect results/frozen Harbor and save/load the terminal world. Economic commands must remain blocked. Retry must restore the original seed/charter/draft/rules at day 0. An unsupported schema1–5 or damaged load must preserve the valid active session.
7. At1366×768 and 1920×1080 test Default/Large, then Compact/Extra Large when layout changes. Capture current map, Harbor, Industry, Fleet/route and Journal with readable text. Check actual icons and parchment contrast rather than comparing only source styles.

The headless `--grand-commission --check` runs two manual wins, one itinerary win with exact mid-voyage save restoration, and an idle deadline failure. It is an executable strategy reference, not evidence of native mouse/keyboard or platform rendering. Record automated UI events, native input and headless checks separately in [verification](REPOSITORY-CLEANUP.md).
