using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Numerics;
using Utils.Imaging;

namespace UtilsTest.Imaging;

internal class ArrayImageAccessor<A, T> : IImageAccessor<A, T>
    where A : struct, IColorArgb<T>
    where T : struct, INumber<T>
{
    private readonly A[,] data;

    public ArrayImageAccessor(int width, int height)
    {
        data = new A[width, height];
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
    public A this[int x, int y]
    {
        get => data[x, y];
        set => data[x, y] = value;
    }
}
[TestClass]
public class SpriteBlendTests
{
    [TestMethod]
    public void ApplySpriteMultiply()
    {
        var dest = new ArrayImageAccessor<ColorArgb32, byte>(3, 3);
        var sprite = new ArrayImageAccessor<ColorArgb32, byte>(2, 2);

        for (int y = 0; y < dest.Height; y++)
        {
            for (int x = 0; x < dest.Width; x++)
            {
                dest[x, y] = new ColorArgb32(128, 128, 128, 128);
            }
        }

        for (int y = 0; y < sprite.Height; y++)
        {
            for (int x = 0; x < sprite.Width; x++)
            {
                sprite[x, y] = new ColorArgb32(255, 255, 255, 255);
            }
        }

        dest.ApplySprite(sprite, new Point(1, 1), ColorBlend.Multiply);

        ColorArgb32 result = dest[1, 1];
        Assert.AreEqual(128, result.Red);
        Assert.AreEqual(128, result.Green);
        Assert.AreEqual(128, result.Blue);
    }
}
