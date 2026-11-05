# Contracts and development projects

Open **Contracts & Projects** from Company or Economy, or inspect a town's local opportunities. The board separates available contracts, active commitments, development projects and resolved history. Inspect the destination, cargo, local stock, travel time, payment, delivery schedule and reward before committing. Offers use the current generated world; they are not promises that a route will remain profitable or that stock will remain available.

Choose **All towns** or a specific town and select the owned **Delivery ship**. **Accept contract** moves an offer into **Active commitments**; **Deliver cargo** previews and executes the entered whole quantity. **Maximum now** fills the amount the selected ship can currently deliver. The board displays each recurring window's own closing day. Generated offers show their duration from acceptance; royal deadlines remain fixed.

## Choose a commitment

The default active limit is **three contracts**. Projects do not consume a contract slot. Declining an available opportunity costs nothing. Accepting a contract reserves its full remaining payment and completion reward from the **finite external trading authority**, so another purchase cannot spend its promised funds. Towns receive domestic deliveries; royal deliveries leave the region as exports. Neither route creates money. A normal market sale does not count as a contract delivery.

| Contract | What must happen | Completion evidence |
| --- | --- | --- |
| Bulk procurement | Deliver the stated quantity to the actual destination before its deadline | Real cargo transferred, including partial shipments |
| Recurring supply | Deliver each window's quantity within that window | Each individual window is checked; missing one fails the remaining agreement |
| Industrial supply | Deliver the named input to an existing public factory town | The named factory must actually complete the funded input batches through normal production |
| Emergency relief | Deliver food to a town with actual scarcity or unmet consumption | Sufficient paid household food consumption across multiple opportunities, backed by player contribution credit |

Deliveries pay the contracted unit price from the reserved account. The completion reward is paid once when the entire obligation and evidence are satisfied. A missed deadline forfeits the remaining opportunity and its unearned bonus; unspent reservation returns to the authority. Previous valid delivery payments are retained. No hidden reputation or arbitrary cash penalty applies.

Recurring windows cannot be supplied early. Finishing one window does not let the ship pre-deliver all later windows in the same visit. The deadline is inclusive; the next day resolves a missed window. Industrial input arrival alone cannot finish the agreement: ordinary inputs, wages, output limits and production timing still apply. Relief stock alone cannot finish its agreement either: households must actually buy and consume the food. NPC activity cannot supply the required player contribution.

Some ordinary offers share a resource-competition group: they compete for the same cargo stream and overlapping shipping time, but a capable company may complete both. An **exclusive tender** names its mutually exclusive alternatives; accepting one withdraws the other. These restrictions are shown before acceptance.

## Rewards with different uses

Cash is a finite account transfer. A commercial privilege temporarily lowers ordinary sale handling fees at the issuing town from **2% to 1%**, including local warehouse and ship sales. Separate cumulative discounted-sales prefixes preserve exact rounding across split orders. Gross market affordability, FIFO costs and local fee recipients still use the normal transaction path. The displayed privilege expiration is inclusive and does not stack into an unbounded discount.

Infrastructure access permits additional public industry commissions at the rewarded town. A permit does not authorize a conflicting producer/consumer pair, a conflicting Mint/shipyard capability or an unavailable canonical industry. It grants access; the subsequent project still requires its full real investment.

## Invest in permanent infrastructure

Projects accept partial cash and material contributions. Cash pays the destination town; delivered materials are consumed as construction inputs with their original FIFO basis released explicitly. Contributions are permanent investments. Completion creates the infrastructure once, with no extra money, inputs or inventory.

| Project | Initial terms | Permanent effect |
| --- | --- | --- |
| Public Industry Commission | Canonical blueprint construction price plus six construction materials | A public facility using the canonical recipe, interval, output limit and market-funded wages |
| Trading Depot | 120 coins plus four construction materials | 40 whole goods of company storage at the town, without requiring a factory |

The board names the actual construction material, normally Planks with a reachable enabled alternative where necessary. Ship contributions require real cargo at the project dock. Existing local company stock may also contribute; remote ship cargo cannot. Money may be contributed separately.

On **Projects**, choose **Start project**. Select **Selected ship cargo** or **Local company warehouse**, enter material units and coins, then review and choose **Contribute**. **Fill available contribution** proposes the remaining amounts limited to current holdings and cash; it spends nothing until contribution. Set material units to zero to invest money separately. Finished projects appear in **History**, with their actual facility or warehouse details.

A commissioned public factory starts empty of private inventory and first becomes due one complete recipe interval after completion. Its inputs and outputs use the ordinary public market, and successful batches pay real local wages. It is public property and does not count as a company factory or private Company Value.

A depot joins the company's one physical/FIFO store at that town. Its capacity adds to actual local facility capacity; it adds no production or recipe output ceiling. Harbor market trading, loading/unloading and warehouse automation use the same store. Depot investment counts once as private capital while pending or completed, and its held goods are counted separately once. Public commissions remain excluded from private assets.

## Automate and continue

Add **Deliver contract** to an existing ship itinerary, select the contract and its named good, and set the usual maximum/minimum quantity and Skip/Wait behavior. It executes through the same physical delivery command as the board. The ship must be at the named town, the contract must be active and its current window must have remaining demand. Resolved or pruned contracts automatically skip, allowing the route to continue. Unaccepted offers and active cargo/window shortages retain the configured Skip/Wait behavior. The normal eight-operation budget and saved route cursor prevent repeated work after loading or repeated dockside execution.

Royal export contracts appear on the same board and use the same delivery lifecycle. Existing royal API callers retain an atomic accept-and-deliver convenience path; it does not create a second entitlement or bypass active limits and finite funds. The Grand Commission's existing export objective remains functional.

Schema8 saves retain the complete bounded board, deterministic generation sequence, funding reservations, current windows, production/consumption contribution evidence, partial project investments, permanent facilities/depots and privileges. Loading restores exact state without running generation, settling rewards or advancing a day. Older schemas are rejected safely.

The world-generation configuration controls the active limit, offer-pool limit, review interval, offer lifetime and retained ordinary history. Defaults are three active contracts, eight available ordinary contracts (plus up to three authored royal exports), a 16-day review, 24-day ordinary offer lifetime and 64 ordinary history entries. Infrastructure records and still-effective privileges remain authoritative after ordinary history pruning. See [world configuration](WORLD_GENERATION.md), [automation](AUTOMATION.md), and [verification](TASK-3.2-VERIFICATION.md).
