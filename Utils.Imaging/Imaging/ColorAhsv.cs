using System;
using System.Numerics;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Imaging
{
        /// <summary>
        /// HSV color representation using <see cref="double"/> components.
        /// </summary>
        public class ColorAhsv : IColorAhsv<double>, IColorArgbConvertible<ColorAhsv, ColorArgb, double>
	{
		public static double MinValue { get; } = 0.0;
		public static double MaxValue { get; } = 1.0;

		private double alpha;
		private double hue;
		private double saturation;
		private double value;

                /// <summary>Alpha channel in the [0,1] range.</summary>
                public double Alpha
                {
                        get => alpha;
                        set
                        {
                                value.ArgMustBeBetween(MinValue, MaxValue);
                                alpha = value;
                        }
                }

                /// <summary>Hue component in degrees.</summary>
                public double Hue
                {
                        get => hue;
                        set => this.hue = MathEx.Mod(value, 360.0);
                }

                /// <summary>Saturation value in the [0,1] range.</summary>
                public double Saturation
                {
                        get => saturation;

                        set
                        {
                                value.ArgMustBeBetween(MinValue, MaxValue);
                                this.saturation = value;
                        }
                }

                /// <summary>Value (brightness) in the [0,1] range.</summary>
                public double Value
                {
                        get => value;

                        set
                        {
                                value.ArgMustBeBetween(MinValue, MaxValue);
                                this.value = value;
                        }
                }

                /// <summary>
                /// Initializes a new <see cref="ColorAhsv"/> with explicit components.
                /// </summary>
                /// <param name="alpha">Alpha in the [0,1] range.</param>
                /// <param name="hue">Hue expressed in degrees.</param>
                /// <param name="saturation">Saturation in the [0,1] range.</param>
                /// <param name="value">Value in the [0,1] range.</param>
                public ColorAhsv(double alpha, double hue, double saturation, double value)
                {
                        alpha.ArgMustBeBetween(MinValue, MaxValue);
                        saturation.ArgMustBeBetween(MinValue, MaxValue);
                        value.ArgMustBeBetween(MinValue, MaxValue);

                        this.alpha = alpha;
                        this.Hue = MathEx.Mod(hue, 360.0);
                        this.saturation = saturation;
                        this.value = value;
                }


                /// <summary>
                /// Initializes an opaque color using the specified HSV components.
                /// </summary>
                /// <param name="hue">Hue in degrees.</param>
                /// <param name="saturation">Saturation in the [0,1] range.</param>
                /// <param name="value">Value in the [0,1] range.</param>
                public ColorAhsv(double hue, double saturation, double value) : this(MaxValue, hue, saturation, value) { }

                /// <summary>
                /// Converts any implementation of <see cref="IColorAhsv{T}"/> into <see cref="ColorAhsv"/>.
                /// </summary>
                /// <typeparam name="TColorAshv">Source color type.</typeparam>
                /// <typeparam name="T">Component type.</typeparam>
                /// <param name="color">Color to convert.</param>
                /// <returns>A new <see cref="ColorAhsv"/> instance.</returns>
                public static ColorAhsv FromColorAshv<TColorAshv, T>(TColorAshv color)
                        where TColorAshv : IColorAhsv<T>
                        where T : struct, INumber<T>
                {
			double maxValue = double.CreateChecked(TColorAshv.MaxValue);
			double alpha = double.CreateChecked(color.Alpha) / maxValue;
			double hue = double.CreateChecked(color.Hue) / maxValue * 360;
			double saturation = double.CreateChecked(color.Saturation) / maxValue;
			double value = double.CreateChecked(color.Value) / maxValue;
			return new (alpha, hue, saturation, value);
		}

                /// <summary>
                /// Initializes a new instance from an ARGB color.
                /// </summary>
                /// <param name="color">Source color.</param>
                public ColorAhsv(ColorArgb color)
                {
                        ColorAhsv tmp = FromArgbColor(color);
                        alpha = tmp.alpha;
                        hue = tmp.hue;
                        saturation = tmp.saturation;
                        value = tmp.value;
                }

		public static implicit operator ColorAhsv(ColorArgb color) => new ColorAhsv(color);
		public static implicit operator ColorAhsv(ColorAhsv32 color) => FromColorAshv<ColorAhsv32, byte>(color);
		public static implicit operator ColorAhsv(ColorAhsv64 color) => FromColorAshv<ColorAhsv64, ushort>(color);

		public override string ToString() => $"a:{alpha} h:{hue} s:{saturation} v:{value}";
                public static ColorAhsv FromArgbColor(ColorArgb color)
                {
                        double min = Math.Min(color.Red, Math.Min(color.Green, color.Blue));
                        double max = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
                        double v = max;
                        double delta = max - min;
                        if (delta <= 0.0)
                        {
                                return new(color.Alpha, 0, 0, v);
                        }
                        double s = delta / max;
                        double h;
                        if (color.Red == max)
                                h = (color.Green - color.Blue) / delta;
                        else if (color.Green == max)
                                h = 2.0 + (color.Blue - color.Red) / delta;
                        else
                                h = 4.0 + (color.Red - color.Green) / delta;
                        h *= 60.0;
                        if (h < 0.0)
                                h += 360.0;
                        return new(color.Alpha, h, s, v);
                }
                /// <summary>
                /// Converts this color to an ARGB representation.
                /// </summary>
                public ColorArgb ToArgbColor()
                {
                        double hh, p, q, t, ff;
                        long i;

                        if (Saturation <= 0.0)
                        {
                                return new ColorArgb(alpha, value, value, value);
                        }

                        hh = hue;
                        if (hh >= 360.0)
                        {
                                hh = 0.0;
                        }
                        hh /= 60.0;
                        i = (long)hh;
                        ff = hh - i;
                        p = value * (1.0 - saturation);
                        q = value * (1.0 - saturation * ff);
                        t = value * (1.0 - saturation * (1.0 - ff));

                        return i switch
                        {
                                0 => new ColorArgb(alpha, value, t, p),
                                1 => new ColorArgb(alpha, q, value, p),
                                2 => new ColorArgb(alpha, p, value, t),
                                3 => new ColorArgb(alpha, p, q, value),
                                4 => new ColorArgb(alpha, t, p, value),
                                _ => new ColorArgb(alpha, value, p, q)
                        };
                }
        }

}
