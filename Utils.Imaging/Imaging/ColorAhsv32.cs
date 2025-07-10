using System;
using System.Numerics;

namespace Utils.Imaging;
/// <summary>
/// HSV color stored with 8-bit components.
/// </summary>

public class ColorAhsv32 :
        IColorAhsv<byte>,
        IColorArgbConvertible<ColorAhsv32, ColorArgb32, byte>,
        IEquatable<ColorAhsv32>,
        IEqualityOperators<ColorAhsv32, ColorAhsv32, bool>
{
	public static byte MinValue { get; } = 0;
	public static byte MaxValue { get; } = byte.MaxValue;

/// <summary>Alpha channel.</summary>
public byte Alpha { get; set; }
/// <summary>Hue component.</summary>
public byte Hue { get; set; }
/// <summary>Saturation component.</summary>
public byte Saturation { get; set; }
/// <summary>Value component.</summary>
public byte Value { get; set; }

/// <summary>
/// Initializes a new instance with explicit component values.
/// </summary>
public ColorAhsv32(byte alpha, byte hue, byte saturation, byte value)
	{
		this.Alpha = alpha;
		this.Hue = hue;
		this.Saturation = saturation;
		this.Value = value;
	}
/// <summary>
/// Converts from a 64-bit HSV color.
/// </summary>
public ColorAhsv32(ColorAhsv64 color)
	{
		this.Alpha = (byte)(color.Alpha >> 8);
		this.Hue = (byte)(color.Hue >> 8);
		this.Saturation = (byte)(color.Saturation >> 8);
		this.Value = (byte)(color.Value >> 8);
	}

/// <summary>
/// Converts from a double precision HSV color.
/// </summary>
public ColorAhsv32(ColorAhsv color)
	{
		this.Alpha = (byte)(color.Alpha * 255);
		this.Hue = (byte)(color.Hue / 360 * 255);
		this.Saturation = (byte)(color.Saturation * 255);
		this.Value = (byte)(color.Value * 255);
	}

/// <summary>
/// Creates a new instance from a <see cref="System.Drawing.Color"/>.
/// </summary>
public ColorAhsv32(System.Drawing.Color colorArgb) { FromArgbColor(colorArgb.A, colorArgb.R, colorArgb.G, colorArgb.B); }

/// <summary>
/// Converts from a 32-bit ARGB color.
/// </summary>
public static ColorAhsv32 FromArgbColor(ColorArgb32 colorArgb) => FromArgbColor(colorArgb.Alpha, colorArgb.Red, colorArgb.Green, colorArgb.Blue);

/// <summary>
/// Creates a HSV color from 8-bit ARGB components.
/// </summary>
public static ColorAhsv32 FromArgbColor(byte alpha, byte red, byte green, byte blue)
	{
		byte hue;
		byte saturation;
		byte value;


		byte rgbMin, rgbMax;

		rgbMin = Mathematics.MathEx.Min(red, green, blue);
		rgbMax = Mathematics.MathEx.Max(red, green, blue);

                // gray case
                if (rgbMin == rgbMax)
                {
                        return new(alpha, 0, 0, rgbMax);
                }
                value = rgbMax;

		int delta = rgbMax - rgbMin;

                saturation = (byte)(255 * delta / value);
                if (saturation == 0)
                {
                        return new(alpha, 0, saturation, value);
                }

		if (rgbMax == red)
			hue = (byte)(0 + 43 * (green - blue) / delta);
		else if (rgbMax == green)
			hue = (byte)(85 + 43 * (blue - red) / delta);
		else
			hue = (byte)(171 + 43 * (red - green) / delta);

		return new (alpha, hue, saturation, value);
	}

        /// <summary>
        /// Converts this HSV color to <see cref="ColorArgb32"/>.
        /// </summary>
        public ColorArgb32 ToArgbColor()
        {
                if (Saturation == 0)
                {
                        return new ColorArgb32(Alpha, Value, Value, Value);
                }

                byte region = (byte)(Hue / 43);
                byte remainder = (byte)((Hue - (region * 43)) * 6);

                byte p = (byte)((Value * (255 - Saturation)) >> 8);
                byte q = (byte)((Value * (255 - ((Saturation * remainder) >> 8))) >> 8);
                byte t = (byte)((Value * (255 - ((Saturation * (255 - remainder)) >> 8))) >> 8);

                return region switch
                {
                        0 => new ColorArgb32(Alpha, Value, t, p),
                        1 => new ColorArgb32(Alpha, q, Value, p),
                        2 => new ColorArgb32(Alpha, p, Value, t),
                        3 => new ColorArgb32(Alpha, p, q, Value),
                        4 => new ColorArgb32(Alpha, t, p, Value),
                        _ => new ColorArgb32(Alpha, Value, p, q),
                };
        }


	public static implicit operator ColorAhsv32(ColorAhsv color) => new ColorAhsv32(color);
	public static implicit operator ColorAhsv32(ColorAhsv64 color) => new ColorAhsv32(color);
        public static implicit operator ColorAhsv32(System.Drawing.Color color) => new ColorAhsv32(color);

        public override string ToString() => $"a:{Alpha} h:{Hue} s:{Saturation} v:{Value}";

        /// <inheritdoc/>
        public bool Equals(ColorAhsv32? other) =>
                other is not null &&
                Alpha == other.Alpha &&
                Hue == other.Hue &&
                Saturation == other.Saturation &&
                Value == other.Value;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ColorAhsv32 other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Alpha, Hue, Saturation, Value);

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(ColorAhsv32? left, ColorAhsv32? right) =>
                left is null ? right is null : left.Equals(right);

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(ColorAhsv32? left, ColorAhsv32? right) => !(left == right);
}
