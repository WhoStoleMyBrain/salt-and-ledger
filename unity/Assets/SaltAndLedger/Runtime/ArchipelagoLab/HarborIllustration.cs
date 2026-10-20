using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Original, static chart-and-sail artwork; it contains no economic or navigational state.</summary>
    public sealed class HarborIllustration : VisualElement
    {
        public HarborIllustration()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;
            if (width < 1 || height < 1) return;
            var p = context.painter2D;
            Vector2 Point(float x, float y) => new Vector2(x * width, y * height);
            p.strokeColor = new Color(.18f, .34f, .39f); p.lineWidth = 1;
            for (var line = 1; line < 9; line++)
            {
                p.BeginPath(); p.MoveTo(Point(.05f, line / 10f));
                p.LineTo(Point(.95f, line / 10f)); p.Stroke();
                p.BeginPath(); p.MoveTo(Point(line / 10f, .08f));
                p.LineTo(Point(line / 10f, .9f)); p.Stroke();
            }
            p.strokeColor = new Color(.43f, .59f, .57f); p.lineWidth = 2;
            p.BeginPath(); p.MoveTo(Point(.12f, .72f));
            p.BezierCurveTo(Point(.1f, .25f), Point(.92f, .86f), Point(.86f, .2f)); p.Stroke();
            p.fillColor = new Color(.72f, .62f, .39f);
            Polygon(p, Point(.24f, .63f), Point(.79f, .63f), Point(.67f, .75f), Point(.35f, .75f));
            p.strokeColor = new Color(.9f, .8f, .55f); p.lineWidth = 4;
            p.BeginPath(); p.MoveTo(Point(.5f, .2f)); p.LineTo(Point(.5f, .64f)); p.Stroke();
            p.fillColor = new Color(.83f, .79f, .61f);
            Polygon(p, Point(.47f, .23f), Point(.47f, .58f), Point(.24f, .58f));
            p.fillColor = new Color(.69f, .72f, .62f);
            Polygon(p, Point(.53f, .29f), Point(.73f, .57f), Point(.53f, .57f));
            p.strokeColor = new Color(.38f, .57f, .61f); p.lineWidth = 2;
            for (var line = 0; line < 3; line++)
            {
                var y = .8f + line * .045f;
                p.BeginPath(); p.MoveTo(Point(.2f + line * .08f, y));
                p.BezierCurveTo(Point(.38f, y - .025f), Point(.61f, y + .025f), Point(.83f - line * .08f, y));
                p.Stroke();
            }
        }
        private static void Polygon(Painter2D painter, params Vector2[] points)
        {
            painter.BeginPath(); painter.MoveTo(points[0]);
            for (var index = 1; index < points.Length; index++) painter.LineTo(points[index]);
            painter.ClosePath(); painter.Fill();
        }
    }
}
