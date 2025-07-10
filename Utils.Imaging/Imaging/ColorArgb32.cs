using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Utils.Mathematics;

namespace Utils.Imaging;

[StructLayout(LayoutKind.Explicit)]
/// <summary>
/// Represents a 32-bit ARGB color using byte components.
/// </summary>
public struct ColorArgb32 : IColorArgb<byte>, IEquatable<ColorArgb32>, IEqualityOperators<ColorArgb32, ColorArgb32, bool>
{
	public static byte MinValue { get; } = 0;
	public static byte MaxValue { get; } = byte.MaxValue;

	[FieldOffset(0)]
	uint value;

	[FieldOffset(3)]
	byte alpha;
	[FieldOffset(2)]
	byte red;
	[FieldOffset(1)]
	byte green;
	[FieldOffset(0)]
	byte blue;

	public uint Value
	{
		get { return value; }
		set { this.value = value; }
	}

	public byte Alpha
	{
		get { return alpha; }
		set { this.alpha = value; }
	}

	public byte Red
	{
		get { return red; }
		set { this.red = value; }
	}

	public byte Green
	{
		get { return green; }
		set { this.green = value; }
	}

	public byte Blue
	{
		get { return blue; }
		set { this.blue = value; }
	}


	public ColorArgb32(uint color) : this()
	{
		this.alpha = (byte)(0xFF & color >> 24);
		this.red = (byte)(0xFF & color >> 16);
		this.green = (byte)(0xFF & color >> 8);
		this.blue = (byte)(0xFF & color);
	}

	public ColorArgb32(byte red, byte green, byte blue) : this(byte.MaxValue, red, green, blue) { }
	public ColorArgb32(byte alpha, byte red, byte green, byte blue) : this()
	{
		this.alpha = alpha;
		this.red = red;
		this.green = green;
		this.blue = blue;
	}

	public ColorArgb32(byte[] array, int index) : this()
	{
		this.alpha = array[index];
		this.red = array[index + 1];
		this.green = array[index + 2];
		this.blue = array[index + 3];
	}

	public ColorArgb32(IColorArgb<byte> colorArgb64) : this()
	{
		this.alpha = (byte)(colorArgb64.Alpha * 255);
		this.red = (byte)(colorArgb64.Red * 255);
		this.green = (byte)(colorArgb64.Green * 255);
		this.blue = (byte)(colorArgb64.Blue * 255);
	}

	public ColorArgb32(ColorArgb colorArgb64) : this()
	{
		this.alpha = (byte)(colorArgb64.Alpha * 255);
		this.red = (byte)(colorArgb64.Red * 255);
		this.green = (byte)(colorArgb64.Green * 255);
		this.blue = (byte)(colorArgb64.Blue * 255);
	}

	public ColorArgb32(ColorArgb64 colorArgb64) : this()
	{
		this.alpha = (byte)(colorArgb64.Alpha >> 8);
		this.red = (byte)(colorArgb64.Red >> 8);
		this.green = (byte)(colorArgb64.Green >> 8);
		this.blue = (byte)(colorArgb64.Blue >> 8);
	}

	public ColorArgb32(System.Drawing.Color color) : this()
	{
		Alpha = color.A;
		Red = color.R;
		Green = color.G;
		Blue = color.B;
	}

        public ColorArgb32(ColorAhsv32 colorAHSV) : this()
        {
                this = colorAHSV.ToArgbColor();
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ColorArgb32 other && Equals(other);

        /// <inheritdoc/>
        public bool Equals(ColorArgb32 other) => Value == other.Value;

        /// <inheritdoc/>
        public override int GetHashCode() => (int)Value;

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(ColorArgb32 left, ColorArgb32 right) => left.Equals(right);

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(ColorArgb32 left, ColorArgb32 right) => !left.Equals(right);

	public static implicit operator ColorArgb32(ColorAhsv32 color)
	{
		return new ColorArgb32(color);
	}

	public static implicit operator ColorArgb32(ColorArgb color)
	{
		return new ColorArgb32(color);
	}

	public static implicit operator ColorArgb32(ColorArgb64 color)
	{
		return new ColorArgb32(color);
	}

	public static implicit operator ColorArgb32(System.Drawing.Color color)
	{
		return new ColorArgb32(color);
	}

	public override string ToString() => $"a:{alpha} R:{red} G:{green} B:{blue}";

	public IColorArgb<byte> Over(IColorArgb<byte> other)
	{
		return new ColorArgb32(
				(byte)(this.Alpha + (byte.MaxValue - this.Alpha) * other.Alpha / byte.MaxValue),
				(byte)(this.Red * this.Alpha + (byte.MaxValue - this.Alpha) * other.Red / byte.MaxValue),
				(byte)(this.Green * this.Alpha + (byte.MaxValue - this.Alpha) * other.Green / byte.MaxValue),
				(byte)(this.Blue * this.Alpha + (byte.MaxValue - this.Alpha) * other.Blue / byte.MaxValue)
			);
	}

	public IColorArgb<byte> Add(IColorArgb<byte> other)
	{
		return new ColorArgb32(
				(byte)MathEx.Min(byte.MaxValue, this.Alpha + other.Alpha),
				(byte)MathEx.Min(byte.MaxValue, this.Red + other.Red),
				(byte)MathEx.Min(byte.MaxValue, this.Green + other.Green),
				(byte)MathEx.Min(byte.MaxValue, this.Blue + other.Blue)
			);
	}

	public IColorArgb<byte> Substract(IColorArgb<byte> other)
	{
		return new ColorArgb32(
						MathEx.Min(this.Alpha, other.Alpha),
						MathEx.Min(this.Red, other.Red),
						MathEx.Min(this.Green, other.Green),
						MathEx.Min(this.Blue, other.Blue)
				);
	}


	public static ColorArgb32 LinearGrandient(ColorArgb32 color1, ColorArgb32 color2, float position)
	{
		return new ColorArgb32(
			(byte)(color1.alpha * (1 - position) + color2.alpha * position),
			(byte)(color1.red * (1 - position) + color2.red * position),
			(byte)(color1.green * (1 - position) + color2.green * position),
			(byte)(color1.blue * (1 - position) + color2.blue * position)
		);
	}
}
