# Towns, households and welfare

Living-city systems are part of the current Expanding Economy v3 game; the separate v2 player mode is retired. Market cash, household cash, each company and public treasury are distinct real accounts. A town can have wealthy households and an empty public Bread shelf, or stocked inputs with no employer payroll.

## Real wages and paid demand

Successful public batches pay households from market cash; private batches pay from company cash. Short payroll blocks the entire batch. Wages are expensed separately from FIFO material basis. OperatingSurplusAfterWages subtracts all paid wages from realized material margin, including wages for held output; it is not full net profit. Service fees are separately disclosed.

Essential Bread retail uses exact proportional demand with retained fractional numerator residuals. It buys the bounded affordable quantity from actual public stock using the shared marginal-price sum. Missing-stock and affordability shortfalls are separate. Luxury Furniture retains the exact current hypothetical next-food affordability reserve. Municipal Planks are unpaid own-use. No demand creates currency or hidden stock.

## Bounded observations

Each town retains six essential observations. Fulfillment F is the integer percent of desired food actually paid and consumed. Purchasing power P is capped 100 and compares remaining household cash with the current hypothetical next-food cost. The prosperity target is floor((3F+P)/4); movement is min(3,ceil(abs(target-current)/8)), bounded0–100. This indicator does not multiply prices or money.

Three consecutive fully fed, funded observations with prosperity at least 65 enable Furniture demand. Three observations with prosperity at most40 or purchasing power below 25 disable it. Population reviews occur every 12 days after six food observations, changing at most one resident within generated bounds10–80. Sustained healthy observations permit growth; severe deprivation and prosperity at most35 permit decline. Growth changes future demand, not earlier history or cash.

Grand Commission welfare cards use actual recent paid Bread and the greater current/initial-population denominator. Shrinking a town cannot erase its baseline. Deliveries alone do not count as feeding; NPC/public contributions can help regional welfare without personal attribution.

## Generation and circulation

Generator4 distributes geographically correlated capabilities across the configured town count. Public production, private eligibility and starter grants share a recipe-derived direct dependency exclusion: a town producing an industry input cannot also host that consuming industry. Supply-chain validation requires potential sources in the connected world, while finite inventories and accounts preserve ordinary shortages. See [world generation](WORLD_GENERATION.md).

Current circulation1 applies bounded real commercial fees and a four-day levy/relief review. Fee revenue splits between local market and households; fiscal transfers protect actual next food and ready payroll reserves. Modest initial role imports apply at creation only and never refill. Coverage quote parameters are fixed for the day and rederived from finite purchasing power at the next boundary. See [measured circulation](ECONOMIC_CIRCULATION_REVIEW.md) for exact rules, matched controls and counterexamples.
