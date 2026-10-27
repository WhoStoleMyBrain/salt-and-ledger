using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Supplied illustrations, fitted to existing UI bounds without changing layout or picking.</summary>
    internal static class MaritimeArtwork
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        public static bool DrawGlyph(MeshGenerationContext context, string key, Rect bounds)
        {
            switch (key)
            {
                case "logs": case "grain": case "flour": case "bread": case "furniture":
                    return Draw(context, "Goods/" + key, bounds);
                case "planks":
                    return Draw(context, "Goods/planks-silver", bounds,
                        new Rect(64f / 1448, 256f / 1086, 674f / 1448, 515f / 1086));
                case "silver":
                    return Draw(context, "Goods/planks-silver", bounds,
                        new Rect(776f / 1448, 263f / 1086, 629f / 1448, 496f / 1086));
                case "ship":
                    return Draw(context, "Illustrations/ship", bounds);
                default:
                    return false;
            }
        }

        public static bool Draw(MeshGenerationContext context, string path, Rect bounds, Rect? region = null)
        {
            if (!Textures.TryGetValue(path, out var texture) || texture == null)
            {
                texture = Resources.Load<Texture2D>("Maritime/" + path);
                if (texture == null) return false;
                Textures[path] = texture;
            }
            if (bounds.width < 1 || bounds.height < 1) return true;
            // UV regions isolate the two goods on the supplied sheet; its source pixels stay intact.
            var uv = region ?? new Rect(0, 0, 1, 1);
            var width = texture.width * uv.width;
            var height = texture.height * uv.height;
            var scale = Mathf.Min(bounds.width / width, bounds.height / height);
            var fitted = new Rect(bounds.center.x - width * scale / 2,
                bounds.center.y - height * scale / 2, width * scale, height * scale);
            var mesh = context.Allocate(4, 6, texture);
            mesh.SetNextVertex(VertexAt(fitted.xMin, fitted.yMin, uv.xMin, uv.yMax));
            mesh.SetNextVertex(VertexAt(fitted.xMax, fitted.yMin, uv.xMax, uv.yMax));
            mesh.SetNextVertex(VertexAt(fitted.xMax, fitted.yMax, uv.xMax, uv.yMin));
            mesh.SetNextVertex(VertexAt(fitted.xMin, fitted.yMax, uv.xMin, uv.yMin));
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(0);
            return true;
        }

        private static Vertex VertexAt(float x, float y, float u, float v) => new Vertex
        {
            position = new Vector3(x, y, Vertex.nearZ),
            uv = new Vector2(u, v),
            tint = Color.white
        };
    }
}
