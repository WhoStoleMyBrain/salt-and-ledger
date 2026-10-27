using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    /// <summary>A bounded maritime illustration, not a census of a town's actual buildings.</summary>
    public sealed class MaritimeIllustration : VisualElement
    {
        private string kind;
        public string Kind
        {
            get => kind;
            set
            {
                if (kind == value) return;
                kind = value ?? "harbor";
                MarkDirtyRepaint();
            }
        }

        public MaritimeIllustration(string kind = "harbor")
        {
            AddToClassList("sl-illustration");
            pickingMode = PickingMode.Ignore;
            Kind = kind;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (contentRect.width < 1 || contentRect.height < 1) return;
            if (MaritimeArtwork.Draw(context, "Illustrations/" + Kind, contentRect)) return;
            var c = new IllustrationCanvas(context.painter2D, contentRect);
            var navy = new Color32(13, 38, 49, 255);
            var sea = new Color32(47, 108, 115, 255);
            var paper = new Color32(244, 236, 216, 255);
            var gold = new Color32(196, 163, 106, 255);
            var sand = new Color32(216, 199, 152, 255);
            var green = new Color32(57, 108, 96, 255);
            var rust = new Color32(164, 81, 57, 255);
            var wood = new Color32(100, 65, 44, 255);
            c.Polygon(navy, 0, 0, 240, 0, 240, 140, 0, 140);
            c.Polygon(sea, 0, 72, 240, 72, 240, 140, 0, 140);
            c.Polygon(new Color32(83, 126, 119, 255), 0, 68, 24, 52, 43, 62, 68, 45, 98, 68,
                130, 53, 157, 68, 191, 47, 219, 61, 240, 54, 240, 76, 0, 76);
            c.Line(new Color32(108, 151, 149, 255), 1, 12, 30, 45, 30, 54, 27, 75, 27);
            c.Line(new Color32(108, 151, 149, 255), 1, 154, 24, 176, 24, 187, 28, 215, 28);
            for (var line = 0; line < 5; line++)
            {
                var y = 85 + line * 11;
                c.Line(new Color32(110, 162, 158, 255), 1, 8 + line * 5, y, 32 + line * 4, y - 2, 50 + line * 5, y);
                c.Line(new Color32(110, 162, 158, 255), 1, 150 + line * 4, y + 1, 173 + line * 3, y - 1, 220, y + 1);
            }

            if (Kind == "ship")
            {
                c.Line(gold, 2.2f, 126, 20, 126, 99);
                c.Line(gold, 1.8f, 87, 43, 87, 99);
                c.Polygon(wood, 53, 98, 186, 98, 166, 119, 78, 119);
                c.Polygon(gold, 59, 98, 181, 98, 176, 105, 67, 105);
                c.Line(sand, 1, 73, 112, 165, 112);
                c.Polygon(paper, 121, 29, 121, 87, 84, 87, 97, 68, 102, 48);
                c.Polygon(sand, 132, 34, 169, 85, 132, 85);
                c.Polygon(paper, 83, 47, 83, 89, 59, 89);
                c.Polygon(rust, 126, 19, 149, 25, 126, 30);
                c.Line(gold, 1, 126, 24, 181, 98);
                c.Line(gold, 1, 126, 24, 60, 98);
                c.Line(wood, 1.1f, 121, 52, 103, 52);
                c.Line(wood, 1.1f, 121, 71, 94, 71);
                c.Line(paper, 1.8f, 60, 124, 82, 126, 103, 123, 124, 126, 149, 123, 173, 125);
            }
            else
            {
                c.Polygon(green, 13, 101, 38, 83, 82, 79, 113, 85, 158, 80, 194, 89, 224, 106, 203, 119, 46, 119);
                c.Polygon(sand, 23, 103, 52, 92, 95, 91, 123, 95, 157, 90, 195, 99, 218, 109, 198, 116, 49, 116);
                c.Line(gold, 1.5f, 33, 119, 78, 125, 143, 124, 201, 119);
                c.Polygon(wood, 138, 107, 213, 107, 213, 113, 138, 113);
                c.Line(wood, 3, 151, 111, 151, 123);
                c.Line(wood, 3, 199, 111, 199, 122);

                // One representative timber-and-stucco workshop, reused for all town headers.
                c.Polygon(paper, 53, 57, 114, 57, 114, 104, 53, 104);
                c.Polygon(gold, 114, 57, 139, 69, 139, 104, 114, 104);
                c.Polygon(rust, 44, 59, 80, 29, 118, 59);
                c.Polygon(new Color32(126, 63, 47, 255), 80, 29, 109, 39, 147, 72, 118, 59);
                c.Line(gold, 1.4f, 52, 57, 81, 33, 111, 58);
                c.Polygon(wood, 102, 25, 113, 25, 113, 47, 102, 38);
                c.Line(gold, 2, 100, 25, 115, 25);
                c.Line(wood, 3, 53, 61, 114, 61);
                c.Line(wood, 3, 54, 82, 113, 82);
                c.Line(wood, 3, 57, 59, 57, 104);
                c.Line(wood, 3, 110, 60, 110, 104);
                c.Line(wood, 2, 58, 63, 75, 80);
                c.Line(wood, 2, 108, 64, 92, 80);
                c.Polygon(wood, 76, 83, 94, 83, 94, 104, 76, 104);
                c.Polygon(sea, 64, 66, 74, 66, 74, 77, 64, 77);
                c.Polygon(sea, 92, 66, 103, 66, 103, 77, 92, 77);
                c.Line(gold, 1, 69, 67, 69, 76);
                c.Line(gold, 1, 97, 67, 97, 76);
                c.Line(wood, 1.5f, 123, 78, 130, 81, 130, 91, 123, 88, 123, 78);
                c.Line(gold, 1.5f, 61, 108, 99, 108);

                if (Kind == "bakery")
                {
                    c.Polygon(gold, 99, 83, 118, 83, 118, 99, 99, 99);
                    MaritimeGlyph.Draw(context.painter2D, "bread", c.Rect(99, 82, 19, 19));
                }
                else
                {
                    MaritimeGlyph.Draw(context.painter2D, "lighthouse", c.Rect(160, 37, 47, 65));
                    MaritimeGlyph.Draw(context.painter2D, "forest", c.Rect(10, 53, 40, 49));
                    MaritimeGlyph.Draw(context.painter2D, "ship", c.Rect(173, 101, 44, 34));
                }
            }
            c.Line(gold, 1, 2, 2, 238, 2, 238, 138, 2, 138, 2, 2);
            c.Line(new Color32(79, 119, 120, 255), .8f, 6, 6, 234, 6);
        }

        private readonly struct IllustrationCanvas
        {
            private readonly Painter2D painter;
            private readonly Rect bounds;
            private readonly float scale;
            public IllustrationCanvas(Painter2D painter, Rect bounds)
            {
                this.painter = painter;
                this.bounds = bounds;
                scale = Mathf.Min(bounds.width / 240, bounds.height / 140);
            }
            private Vector2 Point(float x, float y) => new Vector2(bounds.x + x * bounds.width / 240,
                bounds.y + y * bounds.height / 140);
            public Rect Rect(float x, float y, float width, float height) => new Rect(Point(x, y),
                new Vector2(width * bounds.width / 240, height * bounds.height / 140));
            public void Polygon(Color color, params float[] points)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(Point(points[0], points[1]));
                for (var i = 2; i < points.Length; i += 2) painter.LineTo(Point(points[i], points[i + 1]));
                painter.ClosePath();
                painter.Fill();
            }
            public void Line(Color color, float width, params float[] points)
            {
                painter.strokeColor = color;
                painter.lineWidth = Mathf.Max(.7f, width * scale);
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(Point(points[0], points[1]));
                for (var i = 2; i < points.Length; i += 2) painter.LineTo(Point(points[i], points[i + 1]));
                painter.Stroke();
            }
        }
    }
}
