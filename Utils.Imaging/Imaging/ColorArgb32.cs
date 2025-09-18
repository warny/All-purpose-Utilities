using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Utils.Mathematics;

namespace Utils.Imaging;

/// <summary>
/// Represents a 32-bit ARGB color using byte components.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct ColorArgb32 : IColorArgb<byte>, IEquatable<ColorArgb32>, IEqualityOperators<ColorArgb32, ColorArgb32, bool>
{
        /// <summary>
        /// Lowest component value available for the 8-bit representation.
        /// </summary>
        public static byte MinValue { get; } = 0;

        /// <summary>
        /// Highest component value available for the 8-bit representation.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the packed ARGB value.
        /// </summary>
        public uint Value
        {
                get { return value; }
                set { this.value = value; }
        }

        /// <summary>
        /// Gets or sets the alpha channel.
        /// </summary>
        public byte Alpha
        {
                get { return alpha; }
                set { this.alpha = value; }
        }

        /// <summary>
        /// Gets or sets the red channel.
        /// </summary>
        public byte Red
        {
                get { return red; }
                set { this.red = value; }
        }

        /// <summary>
        /// Gets or sets the green channel.
        /// </summary>
        public byte Green
        {
                get { return green; }
                set { this.green = value; }
        }

        /// <summary>
        /// Gets or sets the blue channel.
        /// </summary>
        public byte Blue
        {
                get { return blue; }
                set { this.blue = value; }
        }

        /// <summary>
        /// Initializes a color from a packed 32-bit ARGB value.
        /// </summary>
        /// <param name="color">The packed ARGB value.</param>
        public ColorArgb32(uint color) : this()
        {
                this.alpha = (byte)(0xFF & color >> 24);
                this.red = (byte)(0xFF & color >> 16);
                this.green = (byte)(0xFF & color >> 8);
                this.blue = (byte)(0xFF & color);
        }

        /// <summary>
        /// Initializes an opaque color from explicit RGB components.
        /// </summary>
        /// <param name="red">Red component.</param>
        /// <param name="green">Green component.</param>
        /// <param name="blue">Blue component.</param>
        public ColorArgb32(byte red, byte green, byte blue) : this(byte.MaxValue, red, green, blue) { }

        /// <summary>
        /// Initializes a color from explicit ARGB components.
        /// </summary>
        /// <param name="alpha">Alpha component.</param>
        /// <param name="red">Red component.</param>
        /// <param name="green">Green component.</param>
        /// <param name="blue">Blue component.</param>
        public ColorArgb32(byte alpha, byte red, byte green, byte blue) : this()
        {
                this.alpha = alpha;
                this.red = red;
                this.green = green;
                this.blue = blue;
        }

        /// <summary>
        /// Initializes a color by reading components from an array.
        /// </summary>
        /// <param name="array">The array containing ARGB values.</param>
        /// <param name="index">The starting index for the alpha component.</param>
        public ColorArgb32(byte[] array, int index) : this()
        {
                this.alpha = array[index];
                this.red = array[index + 1];
                this.green = array[index + 2];
                this.blue = array[index + 3];
        }

        /// <summary>
        /// Initializes a color by converting from another 8-bit color representation.
        /// </summary>
        /// <param name="colorArgb64">The source color.</param>
        public ColorArgb32(IColorArgb<byte> colorArgb64) : this()
        {
                this.alpha = (byte)(colorArgb64.Alpha * 255);
                this.red = (byte)(colorArgb64.Red * 255);
                this.green = (byte)(colorArgb64.Green * 255);
                this.blue = (byte)(colorArgb64.Blue * 255);
        }

        /// <summary>
        /// Initializes a color by converting from a floating-point representation.
        /// </summary>
        /// <param name="colorArgb64">The source color.</param>
        public ColorArgb32(ColorArgb colorArgb64) : this()
        {
                this.alpha = (byte)(colorArgb64.Alpha * 255);
                this.red = (byte)(colorArgb64.Red * 255);
                this.green = (byte)(colorArgb64.Green * 255);
                this.blue = (byte)(colorArgb64.Blue * 255);
        }

        /// <summary>
        /// Initializes a color by converting from a 16-bit representation.
        /// </summary>
        /// <param name="colorArgb64">The source color.</param>
        public ColorArgb32(ColorArgb64 colorArgb64) : this()
        {
                this.alpha = (byte)(colorArgb64.Alpha >> 8);
                this.red = (byte)(colorArgb64.Red >> 8);
                this.green = (byte)(colorArgb64.Green >> 8);
                this.blue = (byte)(colorArgb64.Blue >> 8);
        }

        /// <summary>
        /// Initializes a color from a <see cref="System.Drawing.Color"/> value.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        public ColorArgb32(System.Drawing.Color color) : this()
        {
                Alpha = color.A;
                Red = color.R;
                Green = color.G;
                Blue = color.B;
        }

        /// <summary>
        /// Initializes a color from an HSV representation.
        /// </summary>
        /// <param name="colorAHSV">The HSV color to convert.</param>
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

        /// <summary>
        /// Converts an HSV color to the 8-bit ARGB representation.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The converted color.</returns>
        public static implicit operator ColorArgb32(ColorAhsv32 color)
        {
                return new ColorArgb32(color);
        }

        /// <summary>
        /// Converts a floating-point ARGB color to the 8-bit representation.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The converted color.</returns>
        public static implicit operator ColorArgb32(ColorArgb color)
        {
                return new ColorArgb32(color);
        }

        /// <summary>
        /// Converts a 16-bit ARGB color to the 8-bit representation.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The converted color.</returns>
        public static implicit operator ColorArgb32(ColorArgb64 color)
        {
                return new ColorArgb32(color);
        }

        /// <summary>
        /// Converts a <see cref="System.Drawing.Color"/> value to the 8-bit representation.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The converted color.</returns>
        public static implicit operator ColorArgb32(System.Drawing.Color color)
        {
                return new ColorArgb32(color);
        }

        /// <summary>
        /// Returns a textual representation of the ARGB components.
        /// </summary>
        /// <returns>A string describing the ARGB values.</returns>
        public override string ToString() => $"a:{alpha} R:{red} G:{green} B:{blue}";

        /// <summary>
        /// Applies the Porter-Duff over operator using the current color as the foreground.
        /// </summary>
        /// <param name="other">The background color.</param>
        /// <returns>The composited color.</returns>
        public IColorArgb<byte> Over(IColorArgb<byte> other)
        {
                return new ColorArgb32(
                                (byte)(this.Alpha + (byte.MaxValue - this.Alpha) * other.Alpha / byte.MaxValue),
                                (byte)(this.Red * this.Alpha + (byte.MaxValue - this.Alpha) * other.Red / byte.MaxValue),
                                (byte)(this.Green * this.Alpha + (byte.MaxValue - this.Alpha) * other.Green / byte.MaxValue),
                                (byte)(this.Blue * this.Alpha + (byte.MaxValue - this.Alpha) * other.Blue / byte.MaxValue)
                        );
        }

        /// <summary>
        /// Adds two colors while clamping each component to the valid range.
        /// </summary>
        /// <param name="other">The color to add.</param>
        /// <returns>The resulting color.</returns>
        public IColorArgb<byte> Add(IColorArgb<byte> other)
        {
                return new ColorArgb32(
                                (byte)MathEx.Min(byte.MaxValue, this.Alpha + other.Alpha),
                                (byte)MathEx.Min(byte.MaxValue, this.Red + other.Red),
                                (byte)MathEx.Min(byte.MaxValue, this.Green + other.Green),
                                (byte)MathEx.Min(byte.MaxValue, this.Blue + other.Blue)
                        );
        }

        /// <summary>
        /// Produces a color using the component-wise minimum of the operands.
        /// </summary>
        /// <param name="other">The color compared with the current instance.</param>
        /// <returns>The resulting color.</returns>
        public IColorArgb<byte> Substract(IColorArgb<byte> other)
        {
                return new ColorArgb32(
                                                MathEx.Min(this.Alpha, other.Alpha),
                                                MathEx.Min(this.Red, other.Red),
                                                MathEx.Min(this.Green, other.Green),
                                                MathEx.Min(this.Blue, other.Blue)
                                );
        }

        /// <summary>
        /// Computes a linear gradient between two colors.
        /// </summary>
        /// <param name="color1">The starting color.</param>
        /// <param name="color2">The ending color.</param>
        /// <param name="position">Interpolation factor between 0 and 1.</param>
        /// <returns>The interpolated color.</returns>
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
