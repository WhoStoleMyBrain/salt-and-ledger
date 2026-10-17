using System;
using System.Collections.Generic;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Unity host for the same four production commands as the headless demo.</summary>
    public sealed class ProductionSmokeTest : MonoBehaviour
    {
        public Inventory FinalInventory { get; private set; }
        public bool Completed { get; private set; }

        private void Start()
        {
            var exitAfterSmoke = !Application.isEditor &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-saltAndLedgerSmokeTest") >= 0;
            try
            {
                RunSmokeTest();
                if (exitAfterSmoke)
                {
                    Application.Quit(0);
                }
            }
            catch (Exception)
            {
                if (exitAfterSmoke)
                {
                    Application.Quit(1);
                }

                throw;
            }
        }

        public void RunSmokeTest()
        {
            Completed = false;
            FinalInventory = null;
            var contentAsset = Resources.Load<TextAsset>("CanonicalContent");
            if (contentAsset == null)
            {
                throw new InvalidOperationException(
                    "CanonicalContent is missing. Run python tools/prepare_unity.py from the repository root, then let Unity import the generated assets.");
            }

            var content = ContentLoader.Load(contentAsset.text);
            var inventory = new Inventory(content, new Dictionary<string, int> { ["logs"] = 6 });
            Debug.Log($"Loaded schema {content.SchemaVersion}: {content.Goods.Count} goods, {content.Buildings.Count} building, {content.Recipes.Count} recipe.");
            Debug.Log($"Initial: logs={inventory.GetQuantity("logs")}, planks={inventory.GetQuantity("planks")}");

            for (var batch = 1; batch <= 4; batch++)
            {
                var before = inventory.Snapshot();
                var produced = Production.TryRunBatch(inventory, "sawmill", "saw_planks");
                Debug.Log($"Batch {batch}: {(produced ? "produced" : "insufficient inputs")}; logs={inventory.GetQuantity("logs")}, planks={inventory.GetQuantity("planks")}");
                if (produced != (batch <= 3))
                {
                    throw new InvalidOperationException($"Unexpected production result for smoke batch {batch}.");
                }

                if (!produced)
                {
                    foreach (var quantity in before)
                    {
                        if (inventory.GetQuantity(quantity.Key) != quantity.Value)
                        {
                            throw new InvalidOperationException("Rejected smoke batch changed inventory.");
                        }
                    }
                }
            }

            if (inventory.GetQuantity("logs") != 0 || inventory.GetQuantity("planks") != 3)
            {
                throw new InvalidOperationException("Unexpected final smoke inventory.");
            }

            FinalInventory = inventory;
            Completed = true;
            Debug.Log("Salt & Ledger smoke passed: logs=0, planks=3; rejected batch left inventory unchanged.");
        }
    }
}
