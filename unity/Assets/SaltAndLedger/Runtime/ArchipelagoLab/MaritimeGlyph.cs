using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Illustrated goods and procedural utility symbols; these contain no game state.</summary>
    public sealed class MaritimeGlyph : VisualElement
    {
        private string key;
        public string Key
        {
            get => key;
            set
            {
                var next = value ?? "info";
                if (key == next) return;
                key = next;
                tooltip = LabelFor(key);
                MarkDirtyRepaint();
            }
        }

        public MaritimeGlyph(string key)
        {
            AddToClassList("sl-glyph");
            pickingMode = PickingMode.Ignore;
            Key = key;
            generateVisualContent += context =>
            {
                if (!MaritimeArtwork.DrawGlyph(context, Key, contentRect))
                    Draw(context.painter2D, Key, contentRect);
            };
        }

        public static string LabelFor(string key)
        {
            switch (key)
            {
                case "logs": return "Logs";
                case "planks": return "Planks";
                case "grain": return "Grain";
                case "flour": return "Flour";
                case "bread": return "Bread";
                case "furniture": return "Furniture";
                case "silver": return "Silver";
                case "ship": return "Ship";
                case "industry": return "Industry";
                case "market": return "Market";
                case "cash": return "Coins";
                case "attention": return "Attention";
                case "warehouse": return "Company stock";
                case "farm": return "Food source";
                case "forest": return "Forest";
                case "mill": return "Processing";
                case "lighthouse": return "Coastal town";
                default: return "Information";
            }
        }

        /// <summary>Draw the same authored paths in a marker or illustration without creating another element.</summary>
        public static void Draw(Painter2D painter, string key, Rect bounds)
        {
            if (bounds.width < 1 || bounds.height < 1) return;
            var c = new GlyphCanvas(painter, bounds);
            switch (key)
            {
                case "logs": Logs(c); break;
                case "planks": Planks(c); break;
                case "grain": case "farm": Grain(c); break;
                case "flour": Flour(c); break;
                case "bread": Bread(c); break;
                case "furniture": Furniture(c); break;
                case "silver": Silver(c); break;
                case "ship": Ship(c); break;
                case "industry": Industry(c); break;
                case "market": Market(c); break;
                case "cash": Cash(c); break;
                case "attention": Attention(c); break;
                case "warehouse": Warehouse(c); break;
                case "forest": Forest(c); break;
                case "mill": Mill(c); break;
                case "lighthouse": Lighthouse(c); break;
                default: Info(c); break;
            }
        }

        private static readonly Color Ink = new Color32(38, 52, 59, 255);
        private static readonly Color Paper = new Color32(244, 236, 216, 255);
        private static readonly Color Gold = new Color32(196, 163, 106, 255);
        private static readonly Color Honey = new Color32(226, 176, 83, 255);
        private static readonly Color Wood = new Color32(151, 91, 51, 255);
        private static readonly Color Brown = new Color32(100, 65, 44, 255);
        private static readonly Color Green = new Color32(57, 108, 96, 255);
        private static readonly Color Rust = new Color32(164, 81, 57, 255);

        private static void Logs(GlyphCanvas c)
        {
            c.Polygon(Wood, 13, 32, 40, 12, 51, 17, 26, 39);
            c.Polygon(Brown, 26, 39, 51, 17, 53, 25, 29, 48);
            c.Ellipse(20, 40, 10, 11, Honey);
            c.Ellipse(20, 40, 5, 6, Gold);
            c.Polygon(Wood, 28, 43, 51, 26, 59, 33, 36, 52);
            c.Ellipse(31, 49, 9, 10, Honey);
            c.Line(Brown, 1.7f, 30, 45, 34, 49, 30, 53);
            c.Line(Paper, 1.5f, 22, 29, 41, 15);
        }

        private static void Planks(GlyphCanvas c)
        {
            c.Polygon(Gold, 8, 38, 42, 11, 56, 18, 22, 45);
            c.Polygon(Wood, 22, 45, 56, 18, 56, 25, 22, 52);
            c.Polygon(Honey, 8, 46, 41, 21, 53, 28, 20, 55);
            c.Line(Brown, 1.7f, 17, 38, 42, 18);
            c.Line(Brown, 1.7f, 19, 46, 42, 28);
            c.Line(Paper, 1.5f, 10, 45, 20, 50, 52, 26);
        }

        private static void Grain(GlyphCanvas c)
        {
            c.Line(Brown, 2.5f, 29, 57, 32, 10);
            c.Line(Brown, 2.2f, 35, 57, 45, 21);
            c.Line(Brown, 2.2f, 27, 55, 16, 22);
            for (var n = 0; n < 4; n++)
            {
                var y = 16 + n * 7;
                c.Polygon(Honey, 31, y + 5, 21, y, 22, y + 7, 31, y + 11);
                c.Polygon(Gold, 33, y + 5, 43, y - 1, 42, y + 6, 32, y + 12);
            }
            c.Polygon(Honey, 31, 16, 27, 8, 32, 3, 36, 9);
            c.Polygon(Gold, 42, 35, 50, 26, 53, 33, 40, 44);
            c.Polygon(Honey, 22, 37, 11, 29, 10, 36, 25, 45);
            c.Line(Rust, 4, 23, 47, 37, 49);
        }

        private static void Flour(GlyphCanvas c)
        {
            c.Polygon(Paper, 22, 11, 42, 11, 39, 22, 48, 36, 51, 51,
                43, 57, 20, 57, 12, 51, 16, 35, 25, 22);
            c.Polygon(Gold, 22, 11, 26, 5, 31, 9, 36, 5, 42, 11);
            c.Line(Brown, 3, 23, 22, 40, 22);
            c.Line(Gold, 2, 21, 31, 18, 46);
            c.Line(Gold, 2, 40, 30, 45, 48);
            c.Ellipse(32, 41, 9, 10, Honey);
            c.Line(Brown, 1.7f, 32, 35, 32, 48);
            c.Line(Brown, 1.7f, 28, 38, 32, 41, 36, 37);
        }

        private static void Bread(GlyphCanvas c)
        {
            c.Ellipse(32, 37, 26, 18, Wood);
            c.Ellipse(31, 32, 25, 17, Honey);
            c.Line(Paper, 4, 19, 25, 24, 29, 25, 34);
            c.Line(Paper, 4, 30, 21, 35, 26, 36, 32);
            c.Line(Paper, 4, 41, 23, 46, 28, 46, 34);
            c.Line(Brown, 1.6f, 13, 44, 24, 48, 41, 47, 52, 41);
        }

        private static void Furniture(GlyphCanvas c)
        {
            c.Polygon(Wood, 15, 8, 44, 8, 44, 34, 15, 34);
            c.Polygon(Gold, 20, 13, 39, 13, 39, 28, 20, 28);
            c.Line(Brown, 2, 29, 13, 29, 28);
            c.Polygon(Honey, 12, 34, 44, 34, 53, 41, 21, 41);
            c.Polygon(Brown, 21, 41, 53, 41, 53, 47, 21, 47);
            c.Line(Brown, 5, 17, 37, 17, 58);
            c.Line(Brown, 5, 48, 45, 48, 59);
            c.Line(Brown, 4, 24, 46, 24, 56);
            c.Line(Paper, 1.4f, 16, 11, 16, 30);
        }

        private static void Silver(GlyphCanvas c)
        {
            c.Polygon(new Color32(161, 184, 188, 255), 8, 40, 47, 40, 56, 26, 17, 26);
            c.Polygon(new Color32(224, 234, 229, 255), 17, 26, 24, 15, 49, 15, 56, 26);
            c.Polygon(new Color32(98, 129, 142, 255), 47, 40, 56, 26, 54, 45, 45, 56);
            c.Polygon(new Color32(190, 210, 212, 255), 8, 40, 47, 40, 45, 56, 6, 56);
            c.Line(Paper, 2.2f, 21, 25, 48, 25);
            c.Line(Paper, 2, 12, 44, 37, 44);
        }

        private static void Ship(GlyphCanvas c)
        {
            c.Polygon(Wood, 7, 43, 57, 43, 47, 55, 18, 55);
            c.Line(Gold, 2, 12, 47, 53, 47);
            c.Line(Brown, 3, 31, 7, 31, 44);
            c.Polygon(Paper, 27, 11, 27, 38, 9, 38);
            c.Polygon(Gold, 35, 16, 53, 38, 35, 38);
            c.Polygon(Rust, 32, 6, 45, 10, 32, 14);
            c.Line(new Color32(118, 174, 172, 255), 2, 8, 59, 19, 57, 31, 60, 44, 57, 55, 59);
        }

        private static void Industry(GlyphCanvas c)
        {
            c.Polygon(Gold, 7, 55, 7, 28, 23, 18, 23, 29, 39, 19, 39, 31, 55, 31, 55, 55);
            c.Polygon(Rust, 43, 9, 51, 9, 51, 31, 43, 31);
            c.Polygon(Paper, 13, 37, 20, 37, 20, 44, 13, 44);
            c.Polygon(Paper, 27, 37, 34, 37, 34, 44, 27, 44);
            c.Polygon(Brown, 42, 42, 49, 42, 49, 55, 42, 55);
            c.Line(Paper, 1.5f, 43, 14, 50, 14);
        }

        private static void Market(GlyphCanvas c)
        {
            c.Line(Gold, 4, 32, 11, 32, 51);
            c.Line(Gold, 3, 12, 22, 52, 22);
            c.Ellipse(32, 16, 4, 4, Honey);
            c.Line(Ink, 2, 15, 23, 6, 40, 24, 40, 15, 23);
            c.Line(Ink, 2, 49, 23, 40, 40, 58, 40, 49, 23);
            c.Polygon(Honey, 5, 40, 25, 40, 21, 47, 10, 47);
            c.Polygon(Honey, 39, 40, 59, 40, 54, 47, 43, 47);
            c.Polygon(Brown, 20, 51, 44, 51, 48, 57, 16, 57);
        }

        private static void Cash(GlyphCanvas c)
        {
            c.Polygon(Wood, 8, 35, 37, 35, 37, 47, 8, 47);
            c.Ellipse(22, 46, 15, 7, Gold);
            c.Ellipse(22, 35, 15, 7, Honey);
            c.Line(Paper, 1.7f, 9, 40, 16, 43, 29, 43, 36, 40);
            c.Ellipse(42, 29, 15, 20, Gold);
            c.Ellipse(42, 27, 11, 15, Honey);
            c.Line(Brown, 2, 46, 18, 39, 18, 36, 24, 46, 29, 44, 36, 37, 36);
            c.Line(Brown, 1.7f, 42, 14, 42, 40);
        }

        private static void Attention(GlyphCanvas c)
        {
            c.Polygon(Rust, 32, 7, 59, 54, 5, 54);
            c.Line(Paper, 5, 32, 23, 32, 37);
            c.Ellipse(32, 46, 2.5f, 2.5f, Paper, false);
        }

        private static void Info(GlyphCanvas c)
        {
            c.Ellipse(32, 32, 24, 24, Green);
            c.Line(Paper, 5, 32, 29, 32, 46);
            c.Line(Paper, 4, 27, 45, 37, 45);
            c.Ellipse(32, 20, 3, 3, Paper, false);
        }

        private static void Warehouse(GlyphCanvas c)
        {
            c.Polygon(Gold, 9, 26, 32, 10, 55, 26, 55, 56, 9, 56);
            c.Polygon(Rust, 5, 25, 32, 5, 59, 25, 54, 30, 32, 14, 10, 30);
            c.Polygon(Brown, 19, 32, 45, 32, 45, 56, 19, 56);
            c.Line(Honey, 2, 21, 34, 43, 53);
            c.Line(Honey, 2, 43, 34, 21, 53);
        }

        private static void Forest(GlyphCanvas c)
        {
            c.Line(Brown, 4, 19, 39, 19, 59);
            c.Line(Brown, 4, 44, 36, 44, 57);
            c.Polygon(Green, 19, 8, 5, 31, 12, 29, 3, 45, 35, 45, 27, 29, 33, 31);
            c.Polygon(new Color32(85, 133, 99, 255), 44, 5, 32, 28, 38, 26, 29, 43, 60, 43, 51, 26, 57, 28);
            c.Line(Gold, 1.5f, 20, 15, 14, 27);
        }

        private static void Mill(GlyphCanvas c)
        {
            c.Polygon(Gold, 23, 20, 40, 20, 47, 58, 17, 58);
            c.Polygon(Rust, 20, 22, 31, 9, 44, 22);
            c.Line(Brown, 3, 31, 27, 31, 3);
            c.Line(Brown, 3, 31, 27, 56, 27);
            c.Line(Brown, 3, 31, 27, 31, 49);
            c.Line(Brown, 3, 31, 27, 8, 27);
            c.Polygon(Paper, 32, 5, 39, 5, 39, 20, 32, 20);
            c.Polygon(Paper, 39, 28, 55, 28, 55, 35, 39, 35);
            c.Polygon(Paper, 24, 34, 30, 34, 30, 48, 24, 48);
            c.Polygon(Paper, 9, 20, 24, 20, 24, 26, 9, 26);
            c.Ellipse(31, 27, 3, 3, Honey);
            c.Polygon(Brown, 28, 47, 36, 47, 36, 58, 28, 58);
        }

        private static void Lighthouse(GlyphCanvas c)
        {
            c.Polygon(Paper, 25, 19, 39, 19, 47, 57, 17, 57);
            c.Polygon(Rust, 23, 31, 41, 31, 43, 39, 21, 39);
            c.Polygon(Gold, 22, 11, 42, 11, 42, 21, 22, 21);
            c.Polygon(Rust, 18, 11, 32, 3, 46, 11);
            c.Line(Paper, 2, 10, 14, 4, 14);
            c.Line(Paper, 2, 54, 14, 60, 14);
            c.Polygon(Ink, 28, 47, 36, 47, 36, 57, 28, 57);
        }

        private readonly struct GlyphCanvas
        {
            private readonly Painter2D painter;
            private readonly float scale;
            private readonly Vector2 origin;

            public GlyphCanvas(Painter2D painter, Rect bounds)
            {
                this.painter = painter;
                scale = Mathf.Min(bounds.width, bounds.height) / 64f;
                origin = bounds.center - new Vector2(32 * scale, 32 * scale);
            }

            private Vector2 Point(float x, float y) => origin + new Vector2(x * scale, y * scale);

            public void Polygon(Color fill, params float[] points)
            {
                painter.fillColor = fill;
                painter.strokeColor = Ink;
                painter.lineWidth = Mathf.Max(0.85f, 1.8f * scale);
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(Point(points[0], points[1]));
                for (var i = 2; i < points.Length; i += 2) painter.LineTo(Point(points[i], points[i + 1]));
                painter.ClosePath();
                painter.Fill();
                painter.Stroke();
            }

            public void Line(Color color, float width, params float[] points)
            {
                painter.strokeColor = color;
                painter.lineWidth = Mathf.Max(.8f, width * scale);
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(Point(points[0], points[1]));
                for (var i = 2; i < points.Length; i += 2) painter.LineTo(Point(points[i], points[i + 1]));
                painter.Stroke();
            }

            public void Ellipse(float x, float y, float radiusX, float radiusY, Color fill, bool outline = true)
            {
                const float k = .5522848f;
                painter.fillColor = fill;
                painter.strokeColor = Ink;
                painter.lineWidth = Mathf.Max(.85f, 1.8f * scale);
                painter.BeginPath();
                painter.MoveTo(Point(x + radiusX, y));
                painter.BezierCurveTo(Point(x + radiusX, y + radiusY * k), Point(x + radiusX * k, y + radiusY), Point(x, y + radiusY));
                painter.BezierCurveTo(Point(x - radiusX * k, y + radiusY), Point(x - radiusX, y + radiusY * k), Point(x - radiusX, y));
                painter.BezierCurveTo(Point(x - radiusX, y - radiusY * k), Point(x - radiusX * k, y - radiusY), Point(x, y - radiusY));
                painter.BezierCurveTo(Point(x + radiusX * k, y - radiusY), Point(x + radiusX, y - radiusY * k), Point(x + radiusX, y));
                painter.ClosePath();
                painter.Fill();
                if (outline) painter.Stroke();
            }
        }
    }
}
