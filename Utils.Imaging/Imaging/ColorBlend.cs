using System;
using Utils.Mathematics;

namespace Utils.Imaging
{
    /// <summary>
    /// Provides color blending operations similar to those found in image editing software.
    /// </summary>
    public static class ColorBlend
    {
        /// <summary>
        /// Multiplies the components of two colors.
        /// </summary>
        public static ColorArgb Multiply(ColorArgb a, ColorArgb b)
        {
            return new ColorArgb(
                MathEx.Min(1.0, a.Alpha * b.Alpha),
                MathEx.Min(1.0, a.Red * b.Red),
                MathEx.Min(1.0, a.Green * b.Green),
                MathEx.Min(1.0, a.Blue * b.Blue));
        }

        /// <summary>
        /// Uses <paramref name="mask"/> to weight each component of <paramref name="color"/>.
        /// </summary>
        public static ColorArgb Mask(ColorArgb mask, ColorArgb color)
        {
            return new ColorArgb(
                color.Alpha * mask.Alpha,
                color.Red * mask.Red,
                color.Green * mask.Green,
                color.Blue * mask.Blue);
        }

        /// <summary>
        /// Divides the components of two colors.
        /// </summary>
        public static ColorArgb Divide(ColorArgb a, ColorArgb b)
        {
            return new ColorArgb(
                b.Alpha == 0 ? 1.0 : MathEx.Min(1.0, a.Alpha / b.Alpha),
                b.Red == 0 ? 1.0 : MathEx.Min(1.0, a.Red / b.Red),
                b.Green == 0 ? 1.0 : MathEx.Min(1.0, a.Green / b.Green),
                b.Blue == 0 ? 1.0 : MathEx.Min(1.0, a.Blue / b.Blue));
        }

        /// <summary>
        /// Computes the average of two colors.
        /// </summary>
        public static ColorArgb Average(ColorArgb a, ColorArgb b)
        {
            return new ColorArgb(
                (a.Alpha + b.Alpha) / 2,
                (a.Red + b.Red) / 2,
                (a.Green + b.Green) / 2,
                (a.Blue + b.Blue) / 2);
        }

        /// <summary>
        /// Chooses the color closer to the specified intensity.
        /// </summary>
        /// <param name="a">First color.</param>
        /// <param name="b">Second color.</param>
        /// <param name="target">Target intensity in the [0,1] range.</param>
        /// <returns>The color with intensity closest to <paramref name="target"/>.</returns>
        public static ColorArgb ClosestToIntensity(ColorArgb a, ColorArgb b, double target)
        {
            static double Intensity(ColorArgb c) => 0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue;

            double ia = Math.Abs(Intensity(a) - target);
            double ib = Math.Abs(Intensity(b) - target);
            return ia <= ib ? a : b;
        }

        /// <summary>
        /// Multiplies the components of two colors.
        /// </summary>
        public static ColorArgb32 Multiply(ColorArgb32 a, ColorArgb32 b)
        {
            static byte Mul(byte x, byte y) => (byte)(x * y / byte.MaxValue);
            return new ColorArgb32(
                Mul(a.Alpha, b.Alpha),
                Mul(a.Red, b.Red),
                Mul(a.Green, b.Green),
                Mul(a.Blue, b.Blue));
        }

        /// <summary>
        /// Applies a per-channel mask to <paramref name="color"/>.
        /// </summary>
        public static ColorArgb32 Mask(ColorArgb32 mask, ColorArgb32 color)
        {
            static byte MaskComp(byte m, byte c) => (byte)(c * m / byte.MaxValue);
            return new ColorArgb32(
                MaskComp(mask.Alpha, color.Alpha),
                MaskComp(mask.Red, color.Red),
                MaskComp(mask.Green, color.Green),
                MaskComp(mask.Blue, color.Blue));
        }

        /// <summary>
        /// Divides the components of two colors.
        /// </summary>
        public static ColorArgb32 Divide(ColorArgb32 a, ColorArgb32 b)
        {
            static byte Div(byte x, byte y) => y == 0 ? byte.MaxValue : (byte)Math.Min(byte.MaxValue, x * byte.MaxValue / y);
            return new ColorArgb32(
                Div(a.Alpha, b.Alpha),
                Div(a.Red, b.Red),
                Div(a.Green, b.Green),
                Div(a.Blue, b.Blue));
        }

        /// <summary>
        /// Computes the average of two colors.
        /// </summary>
        public static ColorArgb32 Average(ColorArgb32 a, ColorArgb32 b)
        {
            static byte Avg(byte x, byte y) => (byte)((x + y) / 2);
            return new ColorArgb32(
                Avg(a.Alpha, b.Alpha),
                Avg(a.Red, b.Red),
                Avg(a.Green, b.Green),
                Avg(a.Blue, b.Blue));
        }

        /// <summary>
        /// Chooses between two colors based on which one has an intensity closer to a target value.
        /// </summary>
        /// <param name="a">First color to compare.</param>
        /// <param name="b">Second color to compare.</param>
        /// <param name="target">Target intensity in the [0,1] range.</param>
        /// <returns>The color whose intensity is closest to <paramref name="target"/>.</returns>
        public static ColorArgb32 ClosestToIntensity(ColorArgb32 a, ColorArgb32 b, double target)
        {
            static double Intensity(ColorArgb32 c) =>
                (0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue) / byte.MaxValue;

            double ia = Math.Abs(Intensity(a) - target);
            double ib = Math.Abs(Intensity(b) - target);
            return ia <= ib ? a : b;
        }

        /// <summary>
        /// Multiplies the components of two colors.
        /// </summary>
        public static ColorArgb64 Multiply(ColorArgb64 a, ColorArgb64 b)
        {
            static ushort Mul(ushort x, ushort y) => (ushort)((uint)x * y / ushort.MaxValue);
            return new ColorArgb64(
                Mul(a.Alpha, b.Alpha),
                Mul(a.Red, b.Red),
                Mul(a.Green, b.Green),
                Mul(a.Blue, b.Blue));
        }

        /// <summary>
        /// Masks <paramref name="color"/> using per-channel factors from <paramref name="mask"/>.
        /// </summary>
        public static ColorArgb64 Mask(ColorArgb64 mask, ColorArgb64 color)
        {
            static ushort MaskComp(ushort m, ushort c) => (ushort)((uint)c * m / ushort.MaxValue);
            return new ColorArgb64(
                MaskComp(mask.Alpha, color.Alpha),
                MaskComp(mask.Red, color.Red),
                MaskComp(mask.Green, color.Green),
                MaskComp(mask.Blue, color.Blue));
        }

        /// <summary>
        /// Divides the components of two colors.
        /// </summary>
        public static ColorArgb64 Divide(ColorArgb64 a, ColorArgb64 b)
        {
            static ushort Div(ushort x, ushort y) => y == 0 ? ushort.MaxValue : (ushort)Math.Min(ushort.MaxValue, (uint)x * ushort.MaxValue / y);
            return new ColorArgb64(
                Div(a.Alpha, b.Alpha),
                Div(a.Red, b.Red),
                Div(a.Green, b.Green),
                Div(a.Blue, b.Blue));
        }

        /// <summary>
        /// Computes the average of two colors.
        /// </summary>
        public static ColorArgb64 Average(ColorArgb64 a, ColorArgb64 b)
        {
            static ushort Avg(ushort x, ushort y) => (ushort)((x + y) / 2);
            return new ColorArgb64(
                Avg(a.Alpha, b.Alpha),
                Avg(a.Red, b.Red),
                Avg(a.Green, b.Green),
                Avg(a.Blue, b.Blue));
        }

        /// <summary>
        /// Chooses between two 64-bit colors based on intensity proximity.
        /// </summary>
        /// <param name="a">First color.</param>
        /// <param name="b">Second color.</param>
        /// <param name="target">Target intensity in the [0,1] range.</param>
        /// <returns>The color with intensity closest to <paramref name="target"/>.</returns>
        public static ColorArgb64 ClosestToIntensity(ColorArgb64 a, ColorArgb64 b, double target)
        {
            static double Intensity(ColorArgb64 c) =>
                (0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue) / ushort.MaxValue;

            double ia = Math.Abs(Intensity(a) - target);
            double ib = Math.Abs(Intensity(b) - target);
            return ia <= ib ? a : b;
        }
    }
}
