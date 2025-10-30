using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using Utils.Drawing;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class DrawMappingTests
{
    private sealed class ArrayImageAccessor<T> : IImageAccessor<T>
    {
        private readonly T[,] data;

        public ArrayImageAccessor(int width, int height)
        {
            data = new T[width, height];
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        public T this[int x, int y]
        {
            get => data[x, y];
            set => data[x, y] = value;
        }
    }

    [TestMethod]
    public void ComputePixelPosition_MapsViewportCoordinates()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(100, 50);
        var draw = new DrawF<ColorArgb32>(accessor, -10, -5, 5, 10);

        Point pixel = draw.ComputePixelPosition(0f, 0f);

        Assert.AreEqual(50, pixel.X);
        Assert.AreEqual(25, pixel.Y);
    }

    [TestMethod]
    public void ComputePoint_InvertsPixelMapping()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(80, 60);
        var draw = new DrawF<ColorArgb32>(accessor, 0, 0, accessor.Width, accessor.Height);
        PointF viewportPoint = new(12.5f, 30.5f);

        PointF pixel = draw.ComputePixelPositionF(viewportPoint);
        PointF mappedBack = draw.ComputePoint(pixel);

        Assert.AreEqual(viewportPoint.X, mappedBack.X, 1e-3);
        Assert.AreEqual(viewportPoint.Y, mappedBack.Y, 1e-3);
    }

    [TestMethod]
    public void DrawPoint_WritesPixelWhenInsideBounds()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        var draw = new DrawI<ColorArgb32>(accessor);
        ColorArgb32 color = new(255, 10, 20, 30);

        draw.DrawPoint(3, 4, color);

        Assert.AreEqual(color.Alpha, accessor[3, 4].Alpha);
        Assert.AreEqual(color.Red, accessor[3, 4].Red);
        Assert.AreEqual(color.Green, accessor[3, 4].Green);
        Assert.AreEqual(color.Blue, accessor[3, 4].Blue);
    }

    [TestMethod]
    public void DrawPoint_IgnoresOutOfBoundsCoordinates()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(5, 5);
        var draw = new DrawI<ColorArgb32>(accessor);
        ColorArgb32 color = new(255, 10, 20, 30);

        draw.DrawPoint(-1, 0, color);
        draw.DrawPoint(0, 5, color);

        for (int y = 0; y < accessor.Height; y++)
        {
            for (int x = 0; x < accessor.Width; x++)
            {
                Assert.AreEqual(0, accessor[x, y].Alpha);
                Assert.AreEqual(0, accessor[x, y].Red);
                Assert.AreEqual(0, accessor[x, y].Green);
                Assert.AreEqual(0, accessor[x, y].Blue);
            }
        }
    }

    [TestMethod]
    public void DrawFDrawPointUsesViewportMapping()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(16, 16);
        var draw = new DrawF<ColorArgb32>(accessor, 0, 0, 16, 16);
        ColorArgb32 color = new(255, 100, 110, 120);

        draw.DrawPoint(7.9f, 8.2f, color);

        Point expectedPixel = draw.ComputePixelPosition(7.9f, 8.2f);
        Assert.AreEqual(color.Alpha, accessor[expectedPixel.X, expectedPixel.Y].Alpha);
        Assert.AreEqual(color.Red, accessor[expectedPixel.X, expectedPixel.Y].Red);
        Assert.AreEqual(color.Green, accessor[expectedPixel.X, expectedPixel.Y].Green);
        Assert.AreEqual(color.Blue, accessor[expectedPixel.X, expectedPixel.Y].Blue);
    }
}
