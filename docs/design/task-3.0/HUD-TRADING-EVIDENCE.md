# HUD, trading and text-scale evidence

These unmodified captures show the running Unity Editor in the seed 99 Industrial Sandbox on day 8. Controls were operated through actual UI events; observations came from read-only serialization. They contain real inventory, quotes and company accounts rather than the Figma prototype's illustrative values.

| View | Actual screenshot | Observed behavior |
| --- | --- | --- |
| No selection | [1920 Default](evidence/editor-hud-none.png) | Full-width chart with no stale inspector. |
| Selected town | [1920 Default](evidence/editor-hud-town.png) | Northport production, population, prosperity and household needs; connected distances on the chart. |
| Selected ship | [1920 Default](evidence/editor-hud-ship.png) | Ledger's actual dock, cargo capacity, manual-control state and navigation actions. |
| Trading A | [1920 Default](evidence/editor-trading-a.png) | Market → Ship purchased 1 Grain for exactly 16 coins. The reverse sale restored cargo and received 13.72 net coins, without advancing time. |
| Trading B | [1920 Default](evidence/editor-trading-b.png) | Market ↔ Warehouse shows public stock and the separate company pool. A genuine 2-Flour purchase for 26.6666 coins is documented in the Bakery journey. |
| Trading C | [1920 Default](evidence/editor-trading-c.png) | Warehouse → Ship loaded 1 Flour; unloading restored the original pool inventory. Company cash remained unchanged throughout. |

The captures preserve the approved shared HUD, left navigation, inset trading workspace, commodity-centered transfer arrows and right contextual inspector. The no-selection chart uses available width; selecting a town or ship restores the appropriate inspector. Trading pairs explicitly identify physical owners and distinguish paid trades from free dockside transfers. The screenshot of Trading A is the purchased state; Trading C is the loaded state.

## Smaller viewport and larger text

| Actual capture at 1366×768 | Visible result |
| --- | --- |
| [Trading, Default](evidence/editor-trading-1366-default.png) | Compact transfer table; lower goods and advanced actions use the workspace scroller. |
| [Trading, Large](evidence/editor-trading-1366-large.png) | Larger text with two complete commodity rows initially visible and a separate context scroller. |
| [Trading, Extra Large](evidence/editor-trading-1366-extralarge.png) | One complete commodity row initially visible; additional rows require scrolling. This remains a density limitation, not an all-rows-at-once layout. |
| [Factory, Large](evidence/editor-factory-1366-large.png) | Actual recipe, available inputs, output and applied 50% rate are readable; settings continue below in the workspace scroller. |
| [Warehouse, Large](evidence/editor-warehouse-1366-large.png) | Responsive heading/actions, summary and first complete commodity row; additional rules scroll. |

The actual Editor journey switched through Compact, Default, Large and Extra Large at both 1366×768 and 1920×1080. Every size/scale combination preserved the complete serialized simulation state exactly. These comparisons verify passive presentation changes; they do not imply that every row fits simultaneously at larger text sizes. UI reachability tests and other platform evidence are recorded in the [Task 3.0 verification report](../../TASK-3.0-VERIFICATION.md).

Raw snapshots and exact journey metrics remain in ignored local diagnostic files. This evidence package includes screenshots only, with no private raw saves.
