using System.Collections.Generic;
using UnityEngine;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Local, transparent exports of the approved Figma commodity and industry symbols.</summary>
    public static class ApprovedMaritimeAssets
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        public static Texture2D Good(string goodId)
        {
            switch (goodId)
            {
                case "bread": case "flour": case "grain": case "logs":
                case "planks": case "furniture": case "silver":
                    return Load("goods/" + goodId);
                default:
                    return null;
            }
        }

        /// <summary>Uses canonical building IDs. Natural is the approved extraction/input illustration.</summary>
        public static Texture2D Industry(string buildingId)
        {
            switch (buildingId)
            {
                case "farm": case "bakery": case "mill": case "sawmill": case "natural":
                    return Load("industry/" + buildingId);
                case "lumber_camp":
                    return Load("industry/lumber-camp");
                case "furniture_workshop":
                    return Load("industry/furniture-workshop");
                default:
                    return null;
            }
        }

        private static Texture2D Load(string path)
        {
            if (Textures.TryGetValue(path, out var texture) && texture != null) return texture;
            texture = Resources.Load<Texture2D>("ApprovedMaritime/" + path);
            if (texture != null) Textures[path] = texture;
            return texture;
        }
    }
}
