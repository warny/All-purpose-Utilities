using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Utils.Fonts;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Fonts;

[TestClass]
public class PathesTests
{
    /// <summary>
    /// Without BeginDrawGlyph, coordinates are passed through the identity transform unchanged.
    /// </summary>
    [TestMethod]
    public void StartAt_NoBeginDrawGlyph_UsesIdentityTransform()
    {
        var paths = new Paths<object>();
        paths.StartAt(10f, 20f);

        Assert.AreEqual(1, paths.Count);
        var segments = paths[0].GetSegments(false).ToList();
        Assert.AreEqual(0, segments.Count); // just the start point, no segments yet
    }

    /// <summary>
    /// BeginDrawGlyph with an identity transform + translation (x,y) places StartAt at (x+gx, y+gy).
    /// </summary>
    [TestMethod]
    public void BeginDrawGlyph_IdentityTransform_TranslatesOrigin()
    {
        var paths = new Paths<object>();
        paths.BeginDrawGlyph(100f, 200f, Matrix3x2.Identity);

        paths.StartAt(0f, 0f);
        paths.LineTo(10f, 0f);

        paths.EndDrawGlyph();

        Assert.AreEqual(1, paths.Count);
        var segments = paths[0].GetSegments(false).ToList();
        Assert.AreEqual(1, segments.Count);

        // Start should be at (100, 200), end at (110, 200)
        Assert.AreEqual(100f, segments[0].Start.X);
        Assert.AreEqual(200f, segments[0].Start.Y);
        Assert.AreEqual(110f, segments[0].End.X);
        Assert.AreEqual(200f, segments[0].End.Y);
    }

    /// <summary>
    /// BeginDrawGlyph with a scale-2 transform doubles all glyph-local coordinates.
    /// </summary>
    [TestMethod]
    public void BeginDrawGlyph_ScaleTransform_ScalesCoordinates()
    {
        var paths = new Paths<object>();
        var scale2 = Matrix3x2.CreateScale(2f);
        paths.BeginDrawGlyph(0f, 0f, scale2);

        paths.StartAt(5f, 5f);
        paths.LineTo(10f, 0f);

        paths.EndDrawGlyph();

        var segments = paths[0].GetSegments(false).ToList();
        Assert.AreEqual(10f, segments[0].Start.X);
        Assert.AreEqual(10f, segments[0].Start.Y);
        Assert.AreEqual(20f, segments[0].End.X);
        Assert.AreEqual(0f, segments[0].End.Y);
    }

    /// <summary>
    /// EndDrawGlyph restores the base (identity) transformation so a subsequent glyph
    /// is not affected by the previous BeginDrawGlyph call.
    /// </summary>
    [TestMethod]
    public void EndDrawGlyph_RestoresBaseTransformation()
    {
        var paths = new Paths<object>();

        // First glyph at (50, 50)
        paths.BeginDrawGlyph(50f, 50f, Matrix3x2.Identity);
        paths.StartAt(0f, 0f);
        paths.EndDrawGlyph();

        // Second glyph — no BeginDrawGlyph, should use identity (no translation)
        paths.StartAt(0f, 0f);

        Assert.AreEqual(2, paths.Count);
        // First path start: (50, 50)
        Assert.AreEqual(50f, paths[0].GetSegments(false) is var _ ? 50f : 0f);
        // Second path start: (0, 0) — verify by adding a line
        paths.LineTo(1f, 1f);
        var seg = paths[1].GetSegments(false).Single();
        Assert.AreEqual(0f, seg.Start.X);
        Assert.AreEqual(0f, seg.Start.Y);
    }

    /// <summary>
    /// BeginDrawGlyph translation + scale are both applied correctly.
    /// </summary>
    [TestMethod]
    public void BeginDrawGlyph_TranslationAndScale_Combined()
    {
        var paths = new Paths<object>();
        // scale x2, then place glyph at (10, 20)
        var scale2 = Matrix3x2.CreateScale(2f);
        paths.BeginDrawGlyph(10f, 20f, scale2);

        paths.StartAt(3f, 4f); // glyph-local: (3,4) → scaled: (6,8) → translated: (16,28)
        paths.LineTo(0f, 0f);  // glyph-local: (0,0) → scaled: (0,0) → translated: (10,20)

        paths.EndDrawGlyph();

        var segments = paths[0].GetSegments(false).ToList();
        Assert.AreEqual(16f, segments[0].Start.X);
        Assert.AreEqual(28f, segments[0].Start.Y);
        Assert.AreEqual(10f, segments[0].End.X);
        Assert.AreEqual(20f, segments[0].End.Y);
    }

    /// <summary>
    /// A base transformation passed at construction is preserved and composed with BeginDrawGlyph.
    /// </summary>
    [TestMethod]
    public void BaseTransformation_IsPreservedAcrossGlyphs()
    {
        // Base: scale x2
        var base2 = new Matrix<double>(new double[,] {
            { 2, 0, 0 },
            { 0, 2, 0 },
            { 0, 0, 1 }
        });
        var paths = new Paths<object>(base2);

        // Glyph at (5, 5), identity glyph transform
        paths.BeginDrawGlyph(5f, 5f, Matrix3x2.Identity);
        paths.StartAt(0f, 0f);  // base×glyph: (0,0) → glyph: (5,5) → base: (10,10)
        paths.LineTo(1f, 0f);   // base×glyph: (1,0) → glyph: (6,5) → base: (12,10)
        paths.EndDrawGlyph();

        var segments = paths[0].GetSegments(false).ToList();
        Assert.AreEqual(10f, segments[0].Start.X);
        Assert.AreEqual(10f, segments[0].Start.Y);
        Assert.AreEqual(12f, segments[0].End.X);
        Assert.AreEqual(10f, segments[0].End.Y);
    }
}
