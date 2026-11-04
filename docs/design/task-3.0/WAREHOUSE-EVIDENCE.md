# Warehouse V2: actual Editor evidence

These unmodified screenshots show the running Unity Editor at 1920×1080, Default text size, in a disposable seed 99 Industrial Sandbox. They are actual UI interactions and authoritative game values, not Figma renders or prototype quantities. The five current Figma Warehouse V2 frames were inspected separately for comparison.

| Actual capture | Approved Figma frame | Observed state |
| --- | --- | --- |
| [01: Buy targets](evidence/editor-warehouse-01.png) | `193:1057` | Flour and Logs enabled, both Buy to 4; Flour selected with actual marginal quote and current possible quantity. |
| [02: Sell reserves](evidence/editor-warehouse-02.png) | `193:1360` | Bread enabled, Keep 2; current stock 0 correctly reports retained stock protected. |
| [03: Buy reserve popup](evidence/editor-warehouse-03.png) | `215:1340` | Common Buy reserve 100; company cash 1195.5467 and available above reserve 1095.5467. |
| [04: Sell reserve popup](evidence/editor-warehouse-04.png) | `215:1797` | The same100-coin Buy reserve remains editable from Sell; scope explicitly excludes factory wages and ship trades. |
| [05: Invalid rule](evidence/editor-warehouse-05.png) | `216:1513` | Bread Buy to 10 conflicts with enabled Sell Keep 2. Draft remains visible, with an inline reason and an opposite-rule repair link. |

The implemented view retains the inset parchment workspace, persistent selected-good inspector, seven commodity rows, stock/target/price columns, ON/OFF controls, Buy/Sell switching and reserve popup. All seven rows are visible in these Default-size captures. Native UI Toolkit checkbox switches and numeric 0 for unlimited prices replace the prototype pill switches and “ANY” display. The popup is larger for readability; the invalid row uses a rust outline, while the summary status card retains its normal pale-green background. These are visual adaptations, not different rule semantics.

The UI-event journey configured the rules without advancing day 0 or changing cash 1195.5467, Flour 2, Logs 2, Bread 0, or factory batch/wage counters. Valid edits applied to the active warehouse policy immediately; saving the run to a local slot is a separate action. The invalid Bread 10 edit preserved the complete serialized state exactly. Repairing the target to 2 applied successfully; pausing Bread Buy then retained its limit. Final active policy was Flour Buy to 4, Logs Buy to 4, Bread Sell Keep 2 and Buy cash reserve 100.

Subsequent explicit Next day interactions exercised that policy: by day 4, automatic procurement had paid 68.6666 coins and the Bakery had completed one batch, paid 6 coins in wages and retained 2 Bread. By day 8, a second batch had completed; automatic sales had received 78 gross coins, paid 1.56 in fees and still retained 2 Bread. These are observed outcomes for this run, not future-price promises.

Raw snapshot comparisons and journey metrics remain in ignored local diagnostic files. No private raw save is included here. These five screenshots establish Editor 1920 Default evidence only; other resolutions, scales, player interaction and exact disk save/load are recorded separately in the [Task 3.0 verification report](../../TASK-3.0-VERIFICATION.md).
