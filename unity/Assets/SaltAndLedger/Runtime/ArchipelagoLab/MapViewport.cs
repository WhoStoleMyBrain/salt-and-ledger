using System;
using UnityEngine;

namespace SaltAndLedger.UnityHost
{
    /// <summary>A presentation-only, aspect-preserving chart transform. No economic coordinates are changed.</summary>
    public readonly struct MapViewport
    {
        public Rect WorldBounds { get; }
        public Rect ViewportBounds { get; }
        public Rect ContentBounds { get; }
        public Vector2 WorldCenter => WorldBounds.center;
        public Vector2 ViewCenter => ContentBounds.center;
        public float Scale { get; }

        private MapViewport(Rect worldBounds, Rect viewportBounds, Rect contentBounds)
        {
            WorldBounds = worldBounds;
            ViewportBounds = viewportBounds;
            ContentBounds = contentBounds;
            Scale = Mathf.Min(contentBounds.width / worldBounds.width, contentBounds.height / worldBounds.height);
        }

        /// <summary>Fits world bounds with left/top/right/bottom padding, proportionally clamped on small panels.</summary>
        public static MapViewport Fit(Rect worldBounds, Rect viewportBounds, Vector4 padding)
        {
            if (!Finite(worldBounds) || worldBounds.width < 0 || worldBounds.height < 0)
                throw new ArgumentOutOfRangeException(nameof(worldBounds));
            if (!Finite(viewportBounds) || viewportBounds.width <= 0 || viewportBounds.height <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportBounds));
            if (!Finite(padding.x) || !Finite(padding.y) || !Finite(padding.z) || !Finite(padding.w))
                throw new ArgumentOutOfRangeException(nameof(padding));
            var center = worldBounds.center;
            worldBounds = new Rect(center.x - Mathf.Max(1, worldBounds.width) / 2,
                center.y - Mathf.Max(1, worldBounds.height) / 2,
                Mathf.Max(1, worldBounds.width), Mathf.Max(1, worldBounds.height));
            var left = Mathf.Max(0, padding.x);
            var top = Mathf.Max(0, padding.y);
            var right = Mathf.Max(0, padding.z);
            var bottom = Mathf.Max(0, padding.w);
            ClampPair(ref left, ref right, viewportBounds.width);
            ClampPair(ref top, ref bottom, viewportBounds.height);
            var content = new Rect(viewportBounds.x + left, viewportBounds.y + top,
                viewportBounds.width - left - right, viewportBounds.height - top - bottom);
            return new MapViewport(worldBounds, viewportBounds, content);
        }

        public Vector2 WorldToViewport(Vector2 world) => ViewCenter + (world - WorldCenter) * Scale;
        public Vector2 ViewportToWorld(Vector2 view) => WorldCenter + (view - ViewCenter) / Scale;

        private static void ClampPair(ref float first, ref float second, float extent)
        {
            var limit = extent * 0.8f;
            var sum = (double)first + second;
            if (sum <= limit) return;
            first = (float)(first / sum * limit);
            second = (float)(second / sum * limit);
        }

        private static bool Finite(Rect value) => Finite(value.x) && Finite(value.y) &&
            Finite(value.width) && Finite(value.height);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
