using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Utils.Imaging;

namespace UtilsTest.Imaging;

/// <summary>
/// Tests sprite blending through the Windows bitmap accessor.
/// </summary>
[TestClass]
public class SpriteBlendTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ApplySpriteBitmapAccessor()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("BitmapAccessor is only supported on Windows.");
        }
        using var destBmp = new Bitmap(3, 3, PixelFormat.Format32bppArgb);
        using var spriteBmp = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
        using var dest = new BitmapAccessor(destBmp);
        using var sprite = new BitmapAccessor(spriteBmp);

        for (int y = 0; y < dest.Height; y++)
        {
            for (int x = 0; x < dest.Width; x++)
            {
                dest[x, y, 0] = 128; // blue
                dest[x, y, 1] = 128; // green
                dest[x, y, 2] = 128; // red
                dest[x, y, 3] = 255; // alpha
            }
        }

        for (int y = 0; y < sprite.Height; y++)
        {
            for (int x = 0; x < sprite.Width; x++)
            {
                sprite[x, y, 0] = 255;
                sprite[x, y, 1] = 255;
                sprite[x, y, 2] = 255;
                sprite[x, y, 3] = 255;
            }
        }

        dest.ApplySprite(new Point(1, 1), sprite, ColorBlend.Multiply);

        ColorArgb32 res = new(
            dest[1, 1, 3],
            dest[1, 1, 2],
            dest[1, 1, 1],
            dest[1, 1, 0]);

        Assert.AreEqual(128, res.Red);
        Assert.AreEqual(128, res.Green);
        Assert.AreEqual(128, res.Blue);
    }
}
