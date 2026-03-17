using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utils.Drawing;
using Utils.Fonts;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.Imaging;

namespace DrawTest
{
    public partial class TestForm : Form
    {
        public TestForm()
        {
            InitializeComponent();
            Draw();
        }

        private void Draw()
        {
            Bitmap image = new Bitmap(pictureBox1.Width, pictureBox1.Height);
            using (var a = new BitmapArgb32Accessor(image))
            {
                var d = new DrawI<ColorArgb32>(a);
                /*
				d.FillPolygon1(
					new ColorArgb32(0, 0, 0),
					new Point(0, 0),
					new Point(500, 500),
					new Point(500, 0),
					new Point(0, 500)
					);

				d.FillPolygon2(
					new ColorArgb32(255, 0, 0),
					new Point(500, 0),
					new Point(0, 1000),
					new Point(1000, 250),
					new Point(0, 250),
					new Point(1000, 1000)
					);
				*/
                d.FillRectangle(
                    new Point(0, 0),
                    new Point(500, 500),
                    new ColorArgb32(0, 0, 0)
                );

                d.FillBezier(new ColorArgb32(0, 255, 0),
                    new Point(0, 0),
                    new Point(500, 0),
                    new Point(500, 500),
                    new Point(0, 500),
                    new Point(0, 0)
                );
                d.DrawBezier(new ColorArgb32(255, 255, 255),
                    new Point(0, 0),
                    new Point(500, 0),
                    new Point(500, 500),
                    new Point(0, 500),
                    new Point(0, 0)
                );
                d.DrawLine(new Point(200, 0), new Point(0, 200), new ColorArgb32(255, 0, 0));
                d.DrawLine(new Point(100, 0), new Point(0, 200), new ColorArgb32(0, 255, 0));
                d.DrawLine(new Point(200, 0), new Point(0, 100), new ColorArgb32(0, 0, 255));
                d.DrawLine(new Point(-200, -200), new Point(200, 200), new ColorArgb32(255, 0, 0));
                d.DrawLine(new Point(-200, -200), new Point(400, 200), new ColorArgb32(255, 0, 0));
                d.DrawLine(new Point(-200, -200), new Point(200, 400), new ColorArgb32(255, 0, 0));
                d.DrawBezier(new ColorArgb32(255, 0, 0),
                    new Point(0, 0),
                    new Point(500, 0),
                    new Point(500, 500),
                    new Point(0, 500)
                );
                d.DrawBezier(new ColorArgb32(255, 0, 0),
                    new Point(0, 0),
                    new Point(250, 0),
                    new Point(250, 500),
                    new Point(0, 500)
                );
                d.FillCircle(new Point(1000, 300), 250, new ColorArgb32(255, 255, 0));
                d.DrawEllipse(new Point(1000, 300), 250, 100, new ColorArgb32(0, 255, 255), 0);
                d.FillCircle(new Point(1000, 300), 250, new ColorArgb32(0, 0, 0), Math.PI / 2, Math.PI);
                MapBrush<ColorArgb32> c1 = new MapBrush<ColorArgb32>((p, s) => ColorArgb32.LinearGrandient(new ColorArgb32(255, 0, 255), new ColorArgb32(255, 255, 0), p));
                d.DrawCircle(new Point(1000, 300), 250, c1, Math.PI / 2, Math.PI);

                var s1 = new Pathes(
                    new Circle(new PointF(300, 300), 100),
                    new Circle(new PointF(350, 300), 75, Math.PI * 2, 0)
                );

                d.FillShape1((x, y) => new ColorArgb32(128, 128, 0), s1);

                var s2 = new Pathes(
                    new Circle(new PointF(500, 500), 100),
                    new Circle(new PointF(550, 500), 75)
                );

                d.FillShape2((x, y) => new ColorArgb32(128, 0, 128), s2);

                DrawHelloWorld(d);
            }
            pictureBox1.Image = image;
        }

        private void DrawHelloWorld(DrawI<ColorArgb32> draw)
        {
            const string fontPath = @"C:\Windows\Fonts\arial.ttf";
            if (!File.Exists(fontPath)) return;

            using var stream = File.OpenRead(fontPath);
            var font = TrueTypeFont.ParseFont(stream);

            var head = font.GetTable<HeadTable>(TableTypes.HEAD);
            var hhea = font.GetTable<HheaTable>(TableTypes.HHEA);

            // Scale factor: map font units to 20 px cap height.
            float scale = 20f / head.UnitsPerEm;

            // TrueType Y axis points upward; screen Y points downward — flip Y.
            var glyphTransform = Matrix3x2.CreateScale(scale, -scale);

            // Baseline sits below the requested top y=70 by the ascender distance.
            float baselineY = 70f + hhea.Ascent * scale;

            var fillColor = new ColorArgb32(255, 255, 255);

            float x = 80f;
            char prev = '\0';
            foreach (char c in "Hello world")
            {
                if (prev != '\0')
                    x += font.GetSpacingCorrection(prev, c) * scale;

                var glyph = font.GetGlyph(c);
                if (glyph != null)
                {
                    // Collect glyph outlines into paths, then fill with non-zero winding rule
                    // (TrueType convention: outer contours CCW, inner contours CW in font space;
                    // after Y-flip they reverse, but FillShape2 still handles holes correctly).
                    var glyphPaths = new Paths<object>();
                    glyphPaths.BeginDrawGlyph(x, baselineY, glyphTransform);
                    glyph.ToGraphic(glyphPaths);
                    glyphPaths.EndDrawGlyph();

                    draw.FillShape2((px, py) => fillColor, glyphPaths.Cast<IDrawable>());
                }

                // Advance the pen by the hmtx advance width (correct typographic advance).
                // Fall back to a fixed width for whitespace glyphs that have no outline.
                float advance = glyph != null ? glyph.Width * scale : 0f;
                x += advance > 0f ? advance : 0.4f * 20f;
                prev = c;
            }
        }

    }
}
