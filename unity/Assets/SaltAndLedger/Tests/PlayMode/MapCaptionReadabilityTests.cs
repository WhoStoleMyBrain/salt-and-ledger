using System.Collections;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class MapCaptionReadabilityTests
    {
        [UnityTest]
        public IEnumerator RouteCaptionsAvoidTownsShipsAndEachOtherAcrossSeededLayouts()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            var controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            var root = controller.Root;
            root.style.flexGrow = 0;
            root.style.flexShrink = 0;
            foreach (var size in new[] { new Vector2(1366, 768), new Vector2(1920, 1080) })
            foreach (var scale in new[] { ArchipelagoUiScale.Compact, ArchipelagoUiScale.Default,
                ArchipelagoUiScale.Large, ArchipelagoUiScale.ExtraLarge })
            foreach (var seed in new int?[] { null, -42, 42, 99, int.MinValue, int.MaxValue })
            {
                root.style.width = size.x;
                root.style.height = size.y;
                controller.TextScale.Apply(scale, false);
                controller.BeginRun(new RunIdentity(null, RunScenario.IndustrialCharter,
                    RunWorldMode.ExpandingEconomy, seed ?? 42));
                Assert.That(controller.Map.VisibleRouteCount, Is.Zero, "Unselected map does not display six permanent distance captions.");
                controller.Map.ShowAllRoutes = true;
                yield return null;
                yield return null;
                var captions = controller.Map.Query<Label>(className: "route-distance").ToList();
                var markers = controller.Map.Query<Button>().ToList();
                Assert.That(captions.Count, Is.EqualTo(6));
                for (var index = 0; index < markers.Count; index++)
                {
                    var bounds = markers[index].worldBound;
                    var context = size + " / " + scale + " / seed " + seed + " / " + markers[index].name;
                    Assert.That(controller.Map.worldBound.Contains(bounds.min), Is.True, context);
                    Assert.That(controller.Map.worldBound.Contains(bounds.max), Is.True, context);
                    foreach (var previous in markers.Take(index))
                        Assert.That(bounds.Overlaps(previous.worldBound), Is.False, context + " / " + previous.name);
                }
                for (var index = 0; index < captions.Count; index++)
                {
                    var bounds = captions[index].worldBound;
                    var context = size + " / " + scale + " / seed " + seed + " / " + captions[index].text;
                    Assert.That(controller.Map.worldBound.Contains(bounds.min), Is.True, context);
                    Assert.That(controller.Map.worldBound.Contains(bounds.max), Is.True, context);
                    foreach (var marker in markers)
                        Assert.That(bounds.Overlaps(marker.worldBound), Is.False, context + " / " + marker.name);
                    foreach (var previous in captions.Take(index))
                        Assert.That(bounds.Overlaps(previous.worldBound), Is.False, context + " / another caption");
                }
                controller.Map.ShowAllRoutes = false;
            }
        }
    }
}
