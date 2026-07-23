using System;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class BitmapAccessorTests
{
    // ── BitmapAccessor ────────────────────────────────────────────────────────

    [TestMethod]
    public void BitmapAccessor_NullBitmap_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new BitmapAccessor(null!));
    }

    [TestMethod]
    public void BitmapAccessor_WriteThenRead_RoundTrips()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        using var acc = new BitmapAccessor(bmp, PixelFormat.Format32bppArgb);

        acc[1, 2, 0] = 42;
        Assert.AreEqual(42, acc[1, 2, 0]);
    }

    [TestMethod]
    public void BitmapAccessor_NegativeX_ThrowsArgumentOutOfRangeException()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        using var acc = new BitmapAccessor(bmp, PixelFormat.Format32bppArgb);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[-1, 0, 0]; });
    }

    [TestMethod]
    public void BitmapAccessor_XEqualToWidth_ThrowsArgumentOutOfRangeException()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        using var acc = new BitmapAccessor(bmp, PixelFormat.Format32bppArgb);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[4, 0, 0]; });
    }

    [TestMethod]
    public void BitmapAccessor_NegativeY_ThrowsArgumentOutOfRangeException()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        using var acc = new BitmapAccessor(bmp, PixelFormat.Format32bppArgb);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[0, -1, 0]; });
    }

    [TestMethod]
    public void BitmapAccessor_YEqualToHeight_ThrowsArgumentOutOfRangeException()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        using var acc = new BitmapAccessor(bmp, PixelFormat.Format32bppArgb);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[0, 4, 0]; });
    }

    [TestMethod]
    public void BitmapAccessor_ComponentIndexTooLarge_ThrowsArgumentOutOfRangeException()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        using var acc = new BitmapAccessor(bmp, PixelFormat.Format32bppArgb);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[0, 0, 4]; });
    }

    [TestMethod]
    public void BitmapAccessor_AfterDispose_ThrowsObjectDisposedException()
    {
        var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        var acc = new BitmapAccessor(bmp, PixelFormat.Format32bppArgb);
        acc.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => { _ = acc[0, 0, 0]; });
    }

    [TestMethod]
    public void BitmapAccessor_DoubleDispose_DoesNotThrow()
    {
        var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        var acc = new BitmapAccessor(bmp, PixelFormat.Format32bppArgb);
        acc.Dispose();
        // Second disposal must be a no-op.
        acc.Dispose();
    }

    [TestMethod]
    public void BitmapAccessor_InvalidRegionNegativeX_ThrowsArgumentOutOfRangeException()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        var region = new Rectangle(-1, 0, 2, 2);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BitmapAccessor(bmp, PixelFormat.Format32bppArgb, region));
    }

    [TestMethod]
    public void BitmapAccessor_InvalidRegionZeroWidth_ThrowsArgumentOutOfRangeException()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        var region = new Rectangle(0, 0, 0, 2);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BitmapAccessor(bmp, PixelFormat.Format32bppArgb, region));
    }

    // ── BitmapIndexed8Accessor ────────────────────────────────────────────────

    [TestMethod]
    public void BitmapIndexed8Accessor_NullBitmap_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new BitmapIndexed8Accessor(null!));
    }

    [TestMethod]
    public void BitmapIndexed8Accessor_WriteThenRead_RoundTrips()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
        using var acc = new BitmapIndexed8Accessor(bmp);

        acc[1, 2] = 77;
        Assert.AreEqual(77, acc[1, 2]);
    }

    [TestMethod]
    public void BitmapIndexed8Accessor_OutOfBounds_ThrowsArgumentOutOfRangeException()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
        using var acc = new BitmapIndexed8Accessor(bmp);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[4, 0]; });
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[0, 4]; });
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[-1, 0]; });
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[0, -1]; });
    }

    [TestMethod]
    public void BitmapIndexed8Accessor_AfterDispose_ThrowsObjectDisposedException()
    {
        var bmp = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
        var acc = new BitmapIndexed8Accessor(bmp);
        acc.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => { _ = acc[0, 0]; });
    }

    [TestMethod]
    public void BitmapIndexed8Accessor_DoubleDispose_DoesNotThrow()
    {
        var bmp = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
        var acc = new BitmapIndexed8Accessor(bmp);
        acc.Dispose();
        acc.Dispose();
    }

    // ── BitmapArgb64Accessor ──────────────────────────────────────────────────

    [TestMethod]
    public void BitmapArgb64Accessor_NullBitmap_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new BitmapArgb64Accessor(null!));
    }

    [TestMethod]
    public void BitmapArgb64Accessor_WriteThenRead_RoundTrips()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format64bppArgb);
        using var acc = new BitmapArgb64Accessor(bmp);

        var color = new ColorArgb64(1000, 2000, 3000, 4000);
        acc[2, 3] = color;

        var read = acc[2, 3];
        Assert.AreEqual(color.Alpha, read.Alpha);
        Assert.AreEqual(color.Red, read.Red);
        Assert.AreEqual(color.Green, read.Green);
        Assert.AreEqual(color.Blue, read.Blue);
    }

    [TestMethod]
    public void BitmapArgb64Accessor_MultipleRows_UsesStrideCorrectly()
    {
        // Write to multiple pixels in different rows and verify no aliasing.
        using var bmp = new Bitmap(4, 4, PixelFormat.Format64bppArgb);
        using var acc = new BitmapArgb64Accessor(bmp);

        var red = new ColorArgb64(ushort.MaxValue, ushort.MaxValue, 0, 0);
        var blue = new ColorArgb64(ushort.MaxValue, 0, 0, ushort.MaxValue);

        acc[0, 0] = red;
        acc[0, 1] = blue;

        var r0 = acc[0, 0];
        var r1 = acc[0, 1];

        Assert.AreEqual(red.Red, r0.Red, "Row 0 red component");
        Assert.AreEqual(red.Blue, r0.Blue, "Row 0 blue component");
        Assert.AreEqual(blue.Red, r1.Red, "Row 1 red component");
        Assert.AreEqual(blue.Blue, r1.Blue, "Row 1 blue component");
    }

    [TestMethod]
    public void BitmapArgb64Accessor_OutOfBounds_ThrowsArgumentOutOfRangeException()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format64bppArgb);
        using var acc = new BitmapArgb64Accessor(bmp);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[4, 0]; });
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[0, 4]; });
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[-1, 0]; });
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => { _ = acc[0, -1]; });
    }

    [TestMethod]
    public void BitmapArgb64Accessor_AfterDispose_ThrowsObjectDisposedException()
    {
        var bmp = new Bitmap(4, 4, PixelFormat.Format64bppArgb);
        var acc = new BitmapArgb64Accessor(bmp);
        acc.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => { _ = acc[0, 0]; });
    }

    [TestMethod]
    public void BitmapArgb64Accessor_DoubleDispose_DoesNotThrow()
    {
        var bmp = new Bitmap(4, 4, PixelFormat.Format64bppArgb);
        var acc = new BitmapArgb64Accessor(bmp);
        acc.Dispose();
        acc.Dispose();
    }

    [TestMethod]
    public void BitmapArgb64Accessor_UlongInterface_WriteThenRead_RoundTrips()
    {
        using var bmp = new Bitmap(4, 4, PixelFormat.Format64bppArgb);
        using var acc = new BitmapArgb64Accessor(bmp);
        IImageAccessor<ulong> ulongAcc = acc;

        ulong expected = 0xAAAABBBBCCCCDDDDUL;
        ulongAcc[1, 2] = expected;
        Assert.AreEqual(expected, ulongAcc[1, 2]);
    }
}
