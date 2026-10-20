using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public enum ArchipelagoUiScale { Compact, Default, Large, ExtraLarge }

    /// <summary>Presentation preference only: applies inherited layout tokens, never world commands.</summary>
    public sealed class ArchipelagoTextScale
    {
        public const string PreferenceKey = "SaltAndLedger.UiTextScale";
        private static readonly string[] ScaleClasses =
        {
            "ui-text-compact", "ui-text-default", "ui-text-large", "ui-text-extra-large"
        };
        private readonly VisualElement root;

        public ArchipelagoUiScale Current { get; private set; }

        public ArchipelagoTextScale(VisualElement root, bool loadPreference = true)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            var saved = loadPreference ? PlayerPrefs.GetInt(PreferenceKey, (int)ArchipelagoUiScale.Default)
                : (int)ArchipelagoUiScale.Default;
            Apply(saved >= 0 && saved < ScaleClasses.Length ? (ArchipelagoUiScale)saved : ArchipelagoUiScale.Default, false);
            root.RegisterCallback<GeometryChangedEvent>(change => UpdateViewportClasses(change.newRect.width, change.newRect.height));
        }

        public void Apply(ArchipelagoUiScale scale, bool persist = true)
        {
            if ((int)scale < 0 || (int)scale >= ScaleClasses.Length)
                throw new ArgumentOutOfRangeException(nameof(scale), "Choose Compact, Default, Large or Extra Large.");
            Current = scale;
            for (var index = 0; index < ScaleClasses.Length; index++)
                root.EnableInClassList(ScaleClasses[index], index == (int)scale);
            root.EnableInClassList("ui-large-layout", scale >= ArchipelagoUiScale.Large);
            UpdateViewportClasses(root.layout.width, root.layout.height);
            if (!persist) return;
            PlayerPrefs.SetInt(PreferenceKey, (int)scale);
            PlayerPrefs.Save();
        }

        private void UpdateViewportClasses(float width, float height)
        {
            if (!float.IsNaN(width) && !float.IsInfinity(width) && width > 0)
                root.EnableInClassList("ui-narrow", width <= 1500);
            if (!float.IsNaN(height) && !float.IsInfinity(height) && height > 0)
                root.EnableInClassList("ui-short", height <= 850);
        }
    }
}
