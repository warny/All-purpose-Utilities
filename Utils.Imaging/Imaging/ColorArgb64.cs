using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Utils.Mathematics;

namespace Utils.Imaging;


[StructLayout(LayoutKind.Explicit)]
/// <summary>
/// Represents a 64-bit ARGB color using 16-bit components.
/// </summary>
public struct ColorArgb64 : IColorArgb<ushort>, IEquatable<ColorArgb64>, IEqualityOperators<ColorArgb64, ColorArgb64, bool>
{
	public static ushort MinValue { get; } = 0;
	public static ushort MaxValue { get; } = ushort.MaxValue;

	[FieldOffset(0)]
	ulong value;

	[FieldOffset(8)]
	ushort alpha;
	[FieldOffset(4)]
	ushort red;
	[FieldOffset(2)]
	ushort green;
	[FieldOffset(0)]
	ushort blue;

	public ulong Value
	{
		get { return value; }
		set { this.value = value; }
	}

	public ushort Alpha
	{
		get { return alpha; }
		set { this.alpha = value; }
	}

	public ushort Red
	{
		get { return red; }
		set { this.red = value; }
	}

	public ushort Green
	{
		get { return green; }
		set { this.green = value; }
	}

	public ushort Blue
	{
		get { return blue; }
		set { this.blue = value; }
	}

	public ColorArgb64(ulong color) : this()
	{
		this.value = color;
	}

	public ColorArgb64(uint color) : this()
	{
		this.alpha = (ushort)(0xFF00 & color >> 16);
		this.red = (ushort)(0xFF00 & color >> 8);
		this.green = (ushort)(0xFF00 & color);
		this.blue = (ushort)(0xFF00 & color << 8);
	}

	public ColorArgb64(ColorArgb colorArgb) : this()
	{
		this.alpha = (ushort)(colorArgb.Alpha * 65535);
		this.red = (ushort)(colorArgb.Red * 65535);
		this.green = (ushort)(colorArgb.Green * 65535);
		this.blue = (ushort)(colorArgb.Blue * 65535);
	}

	public ColorArgb64(ColorArgb32 colorArgb32) : this()
	{
		this.alpha = (ushort)(colorArgb32.Alpha << 8);
		this.red = (ushort)(colorArgb32.Red << 8);
		this.green = (ushort)(colorArgb32.Green << 8);
		this.blue = (ushort)(colorArgb32.Blue << 8);
	}

	public ColorArgb64(System.Drawing.Color color) : this()
	{
		this.alpha = (ushort)(color.A << 8);
		this.red = (ushort)(color.R << 8);
		this.green = (ushort)(color.G << 8);
		this.blue = (ushort)(color.B << 8);
	}

	public ColorArgb64(ushort red, ushort green, ushort blue) : this(ushort.MaxValue, red, green, blue) { }
	public ColorArgb64(ushort alpha, ushort red, ushort green, ushort blue) : this()
	{
		this.alpha = alpha;
		this.red = red;
		this.green = green;
		this.blue = blue;
	}

	public ColorArgb64(ushort[] array, int index) : this()
	{
		this.alpha = array[index];
		this.red = array[index + 1];
		this.green = array[index + 2];
		this.blue = array[index + 3];
	}

        public ColorArgb64(ColorAhsv64 colorAHSV) : this()
        {
                this = colorAHSV.ToArgbColor();
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ColorArgb64 other && Equals(other);

        /// <inheritdoc/>
        public bool Equals(ColorArgb64 other) => Value == other.Value;

        /// <inheritdoc/>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(ColorArgb64 left, ColorArgb64 right) => left.Equals(right);

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(ColorArgb64 left, ColorArgb64 right) => !left.Equals(right);

	public static implicit operator ColorArgb64(ColorArgb color) => new ColorArgb64(color);
	public static implicit operator ColorArgb64(ColorArgb32 color) => new ColorArgb64(color);
	public static implicit operator ColorArgb64(System.Drawing.Color color) => new ColorArgb64(color);

	public override string ToString() => $"a:{alpha} R:{red} G:{green} B:{blue}";

	private ColorArgb64 BuildColor(
		IColorArgb<ushort> other,
		Func<ushort, ushort, ushort> alphaFunction,
		Func<ushort, ushort, ushort, ushort, ushort, ushort> colorFunction)
	{
		ushort computedAlpha = alphaFunction(this.Alpha, other.Alpha);
		if (computedAlpha == 0) return new ColorArgb64(0);
		return new ColorArgb64(
				alpha,
				colorFunction(computedAlpha, this.Alpha, other.Alpha, this.Red, other.Alpha),
				colorFunction(computedAlpha, this.Alpha, other.Alpha, this.Green, other.Red),
				colorFunction(computedAlpha, this.Alpha, other.Alpha, this.Blue, other.Blue)
			);
	}

	public IColorArgb<ushort> Over(IColorArgb<ushort> other)
	{
		return BuildColor(
			other,
			(thisAlpha, otherAlpha) => (ushort)(thisAlpha + (ushort.MaxValue - thisAlpha) * otherAlpha / ushort.MaxValue),
			(alpha, thisAlpha, otherAlpha, thisComponent, otherComponent) => (ushort)(thisComponent * thisAlpha + (ushort.MaxValue - thisAlpha) * otherComponent / ushort.MaxValue));
	}

	public IColorArgb<ushort> Add(IColorArgb<ushort> other)
	{
		return BuildColor(
			other,
			(thisAlpha, otherAlpha) => (ushort)Math.Sqrt(thisAlpha * otherAlpha),
			(alpha, thisAlpha, otherAlpha, thisComponent, otherComponent) => (ushort)((thisComponent * thisAlpha + otherComponent * otherAlpha) / (thisAlpha + otherAlpha))
		);
	}

	public IColorArgb<ushort> Substract(IColorArgb<ushort> other)
	{
		return new ColorArgb64(
						MathEx.Min(this.Alpha, other.Alpha),
						MathEx.Min(this.Red, other.Red),
						MathEx.Min(this.Green, other.Green),
						MathEx.Min(this.Blue, other.Blue)
				);
	}

}
