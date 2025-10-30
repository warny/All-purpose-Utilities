using System;
using System.Numerics;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Imaging
{
    /// <summary>
    /// HSV color representation using <see cref="double"/> components.
    /// </summary>
    public class ColorAhsv :
            IColorAhsv<double>,
            IColorArgbConvertible<ColorAhsv, ColorArgb, double>,
            IEquatable<ColorAhsv>,
            IEqualityOperators<ColorAhsv, ColorAhsv, bool>
    {
        /// <summary>
        /// Lower bound accepted for HSV components expressed as <see cref="double"/> values.
        /// </summary>
        public static double MinValue { get; } = 0.0;

        /// <summary>
        /// Upper bound accepted for HSV components expressed as <see cref="double"/> values.
        /// </summary>
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
            return new(alpha, hue, saturation, value);
        }

        /// <summary>
        /// Initializes a new instance from the specified ARGB color.
        /// </summary>
        /// <param name="color">Source color converted into HSV space.</param>
        public ColorAhsv(ColorArgb color)
        {
            ColorAhsv tmp = FromArgbColor(color);
            alpha = tmp.alpha;
            hue = tmp.hue;
            saturation = tmp.saturation;
            value = tmp.value;
        }

        /// <summary>
        /// Converts an ARGB color into an HSV representation.
        /// </summary>
        /// <param name="color">Source color expressed with floating-point components.</param>
        /// <returns>The corresponding HSV color.</returns>
        public static implicit operator ColorAhsv(ColorArgb color) => new ColorAhsv(color);

        /// <summary>
        /// Converts an 8-bit HSV color to the double-precision representation.
        /// </summary>
        /// <param name="color">Source HSV color.</param>
        /// <returns>The converted color.</returns>
        public static implicit operator ColorAhsv(ColorAhsv32 color) => FromColorAshv<ColorAhsv32, byte>(color);

        /// <summary>
        /// Converts a 16-bit HSV color to the double-precision representation.
        /// </summary>
        /// <param name="color">Source HSV color.</param>
        /// <returns>The converted color.</returns>
        public static implicit operator ColorAhsv(ColorAhsv64 color) => FromColorAshv<ColorAhsv64, ushort>(color);

        /// <summary>
        /// Returns a textual representation of the HSV components.
        /// </summary>
        /// <returns>A string describing the alpha, hue, saturation, and value.</returns>
        public override string ToString() => $"a:{alpha} h:{hue} s:{saturation} v:{value}";

        /// <summary>
        /// Converts an ARGB color into the HSV representation.
        /// </summary>
        /// <param name="color">Source color expressed with floating-point components.</param>
        /// <returns>A new HSV color containing the converted values.</returns>
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
        /// Converts this HSV color to its ARGB representation.
        /// </summary>
        public ColorArgb ToArgbColor()
        {
            if (Saturation <= 0.0)
            {
                return new ColorArgb(Alpha, Value, Value, Value);
            }

            double hh = Hue;
            if (hh >= 360.0)
                hh = 0.0;
            hh /= 60.0;

            long i = (long)hh;
            double ff = hh - i;
            double p = Value * (1.0 - Saturation);
            double q = Value * (1.0 - (Saturation * ff));
            double t = Value * (1.0 - (Saturation * (1.0 - ff)));

            return i switch
            {
                0 => new ColorArgb(Alpha, Value, t, p),
                1 => new ColorArgb(Alpha, q, Value, p),
                2 => new ColorArgb(Alpha, p, Value, t),
                3 => new ColorArgb(Alpha, p, q, Value),
                4 => new ColorArgb(Alpha, t, p, Value),
                _ => new ColorArgb(Alpha, Value, p, q),
            };
        }

        /// <inheritdoc/>
        public bool Equals(ColorAhsv? other) =>
                other is not null &&
                Alpha == other.Alpha &&
                Hue == other.Hue &&
                Saturation == other.Saturation &&
                Value == other.Value;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ColorAhsv other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Alpha, Hue, Saturation, Value);

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(ColorAhsv? left, ColorAhsv? right) =>
                left is null ? right is null : left.Equals(right);

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(ColorAhsv? left, ColorAhsv? right) => !(left == right);
    }

}
