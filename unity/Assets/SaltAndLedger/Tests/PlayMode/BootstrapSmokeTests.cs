using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class BootstrapSmokeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneRunsSmokeOnStart()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;

            var smoke = Object.FindAnyObjectByType<ProductionSmokeTest>();
            Assert.That(smoke, Is.Not.Null);
            Assert.That(smoke.Completed, Is.True);
            Assert.That(smoke.FinalInventory.GetQuantity("logs"), Is.Zero);
            Assert.That(smoke.FinalInventory.GetQuantity("planks"), Is.EqualTo(3));
        }
    }
}
