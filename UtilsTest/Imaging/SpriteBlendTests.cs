using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Utils.Imaging;

namespace UtilsTest.Imaging
{
    internal class ArrayImageAccessor<A, T> : IImageAccessor<A, T>
        where A : struct, IColorArgb<T>
        where T : struct
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
        public A this[Point p]
        {
            get => data[p.X, p.Y];
            set => data[p.X, p.Y] = value;
        }
    }

    [TestClass]
    public class SpriteBlendTests
    {
        [TestMethod]
		[Ignore]
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

        [TestMethod]
		[Ignore]
		public void ApplySpriteBitmapAccessor()
        {
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
}
