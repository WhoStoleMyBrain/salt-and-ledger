using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class ArchipelagoTextScaleTests
    {
        private bool hadPreference;
        private int originalPreference;

        [SetUp]
        public void PreservePreference()
        {
            hadPreference = PlayerPrefs.HasKey(ArchipelagoTextScale.PreferenceKey);
            originalPreference = PlayerPrefs.GetInt(ArchipelagoTextScale.PreferenceKey);
            PlayerPrefs.DeleteKey(ArchipelagoTextScale.PreferenceKey);
        }

        [TearDown]
        public void RestorePreference()
        {
            if (hadPreference) PlayerPrefs.SetInt(ArchipelagoTextScale.PreferenceKey, originalPreference);
            else PlayerPrefs.DeleteKey(ArchipelagoTextScale.PreferenceKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void NewOrInvalidPreferenceUsesReadableDefault()
        {
            Assert.That(new ArchipelagoTextScale(new VisualElement()).Current, Is.EqualTo(ArchipelagoUiScale.Default));
            foreach (var invalid in new[] { -1, 4, int.MinValue, int.MaxValue })
            {
                PlayerPrefs.SetInt(ArchipelagoTextScale.PreferenceKey, invalid);
                Assert.That(new ArchipelagoTextScale(new VisualElement()).Current, Is.EqualTo(ArchipelagoUiScale.Default));
            }
        }

        [Test]
        public void ScaleIsExclusiveAndInvalidSelectionLeavesPresentationUnchanged()
        {
            var root = new VisualElement();
            var setting = new ArchipelagoTextScale(root, false);
            foreach (ArchipelagoUiScale scale in Enum.GetValues(typeof(ArchipelagoUiScale)))
            {
                setting.Apply(scale, false);
                Assert.That(setting.Current, Is.EqualTo(scale));
                Assert.That(root.GetClasses().Count(name => name.StartsWith("ui-text-", StringComparison.Ordinal)), Is.EqualTo(1));
                Assert.That(root.ClassListContains("ui-large-layout"), Is.EqualTo((int)scale >= (int)ArchipelagoUiScale.Large));
            }
            var before = root.GetClasses().ToArray();
            Assert.Throws<ArgumentOutOfRangeException>(() => setting.Apply((ArchipelagoUiScale)4));
            Assert.That(root.GetClasses(), Is.EquivalentTo(before));
            Assert.That(setting.Current, Is.EqualTo(ArchipelagoUiScale.ExtraLarge));
            Assert.That(PlayerPrefs.HasKey(ArchipelagoTextScale.PreferenceKey), Is.False);
        }

        [Test]
        public void OnlyExplicitPersistenceChangesTheSavedPreference()
        {
            var setting = new ArchipelagoTextScale(new VisualElement(), false);
            setting.Apply(ArchipelagoUiScale.Large);
            Assert.That(new ArchipelagoTextScale(new VisualElement()).Current, Is.EqualTo(ArchipelagoUiScale.Large));
            setting.Apply(ArchipelagoUiScale.Compact, false);
            Assert.That(new ArchipelagoTextScale(new VisualElement()).Current, Is.EqualTo(ArchipelagoUiScale.Large));
            Assert.That(new ArchipelagoTextScale(new VisualElement(), false).Current, Is.EqualTo(ArchipelagoUiScale.Default));
        }
    }
}
