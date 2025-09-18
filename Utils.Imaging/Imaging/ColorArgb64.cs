using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Utils.Mathematics;

namespace Utils.Imaging;


/// <summary>
/// Represents a 64-bit ARGB color using 16-bit components.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct ColorArgb64 : IColorArgb<ushort>, IEquatable<ColorArgb64>, IEqualityOperators<ColorArgb64, ColorArgb64, bool>
{
        /// <summary>
        /// Lowest component value available for the 16-bit representation.
        /// </summary>
        public static ushort MinValue { get; } = 0;

        /// <summary>
        /// Highest component value available for the 16-bit representation.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the packed ARGB value.
        /// </summary>
        public ulong Value
        {
                get { return value; }
                set { this.value = value; }
        }

        /// <summary>
        /// Gets or sets the alpha channel.
        /// </summary>
        public ushort Alpha
        {
                get { return alpha; }
                set { this.alpha = value; }
        }

        /// <summary>
        /// Gets or sets the red channel.
        /// </summary>
        public ushort Red
        {
                get { return red; }
                set { this.red = value; }
        }

        /// <summary>
        /// Gets or sets the green channel.
        /// </summary>
        public ushort Green
        {
                get { return green; }
                set { this.green = value; }
        }

        /// <summary>
        /// Gets or sets the blue channel.
        /// </summary>
        public ushort Blue
        {
                get { return blue; }
                set { this.blue = value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorArgb64"/> struct from a 64-bit packed ARGB value.
        /// </summary>
        /// <param name="color">The packed ARGB value containing four 16-bit components.</param>
        public ColorArgb64(ulong color) : this()
        {
                this.value = color;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorArgb64"/> struct from a 32-bit packed ARGB value.
        /// </summary>
        /// <param name="color">The packed ARGB value containing four 8-bit components.</param>
        public ColorArgb64(uint color) : this()
        {
                this.alpha = (ushort)(0xFF00 & color >> 16);
                this.red = (ushort)(0xFF00 & color >> 8);
                this.green = (ushort)(0xFF00 & color);
                this.blue = (ushort)(0xFF00 & color << 8);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorArgb64"/> struct from a floating-point color.
        /// </summary>
        /// <param name="colorArgb">The floating-point color whose channels are scaled to 16 bits.</param>
        public ColorArgb64(ColorArgb colorArgb) : this()
        {
                this.alpha = (ushort)(colorArgb.Alpha * 65535);
                this.red = (ushort)(colorArgb.Red * 65535);
                this.green = (ushort)(colorArgb.Green * 65535);
                this.blue = (ushort)(colorArgb.Blue * 65535);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorArgb64"/> struct from an 8-bit color.
        /// </summary>
        /// <param name="colorArgb32">The 8-bit color whose channels are expanded to 16 bits.</param>
        public ColorArgb64(ColorArgb32 colorArgb32) : this()
        {
                this.alpha = (ushort)(colorArgb32.Alpha << 8);
                this.red = (ushort)(colorArgb32.Red << 8);
                this.green = (ushort)(colorArgb32.Green << 8);
                this.blue = (ushort)(colorArgb32.Blue << 8);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorArgb64"/> struct from a <see cref="System.Drawing.Color"/> value.
        /// </summary>
        /// <param name="color">The color whose channels are expanded to 16 bits.</param>
        public ColorArgb64(System.Drawing.Color color) : this()
        {
                this.alpha = (ushort)(color.A << 8);
                this.red = (ushort)(color.R << 8);
                this.green = (ushort)(color.G << 8);
                this.blue = (ushort)(color.B << 8);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorArgb64"/> struct using explicit RGB components.
        /// </summary>
        /// <param name="red">The red component of the color.</param>
        /// <param name="green">The green component of the color.</param>
        /// <param name="blue">The blue component of the color.</param>
        public ColorArgb64(ushort red, ushort green, ushort blue) : this(ushort.MaxValue, red, green, blue) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="ColorArgb64"/> struct using explicit ARGB components.
        /// </summary>
        /// <param name="alpha">The alpha component of the color.</param>
        /// <param name="red">The red component of the color.</param>
        /// <param name="green">The green component of the color.</param>
        /// <param name="blue">The blue component of the color.</param>
        public ColorArgb64(ushort alpha, ushort red, ushort green, ushort blue) : this()
        {
                this.alpha = alpha;
                this.red = red;
                this.green = green;
                this.blue = blue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorArgb64"/> struct from an array of channel values.
        /// </summary>
        /// <param name="array">The array containing ARGB components.</param>
        /// <param name="index">The starting index of the alpha component in the array.</param>
        public ColorArgb64(ushort[] array, int index) : this()
        {
                this.alpha = array[index];
                this.red = array[index + 1];
                this.green = array[index + 2];
                this.blue = array[index + 3];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorArgb64"/> struct from an HSV color representation.
        /// </summary>
        /// <param name="colorAHSV">The HSV color converted to the ARGB representation.</param>
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

        /// <summary>
        /// Creates a <see cref="ColorArgb64"/> instance from a floating-point color.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>A <see cref="ColorArgb64"/> with 16-bit components.</returns>
        public static implicit operator ColorArgb64(ColorArgb color) => new ColorArgb64(color);

        /// <summary>
        /// Creates a <see cref="ColorArgb64"/> instance from an 8-bit per channel color.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>A <see cref="ColorArgb64"/> with 16-bit components.</returns>
        public static implicit operator ColorArgb64(ColorArgb32 color) => new ColorArgb64(color);

        /// <summary>
        /// Creates a <see cref="ColorArgb64"/> instance from a <see cref="System.Drawing.Color"/> value.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>A <see cref="ColorArgb64"/> with 16-bit components.</returns>
        public static implicit operator ColorArgb64(System.Drawing.Color color) => new ColorArgb64(color);

        /// <summary>
        /// Returns a textual representation of the color components.
        /// </summary>
        /// <returns>A string describing the ARGB components.</returns>
        public override string ToString() => $"a:{alpha} R:{red} G:{green} B:{blue}";

        /// <summary>
        /// Creates a new color by combining this color with another one using provided blending functions.
        /// </summary>
        /// <param name="other">The color blended with the current instance.</param>
        /// <param name="alphaFunction">Function computing the resulting alpha component.</param>
        /// <param name="colorFunction">Function computing a resulting color component.</param>
        /// <returns>The blended color produced by the supplied delegates.</returns>
        private ColorArgb64 BuildColor(
                IColorArgb<ushort> other,
                Func<ushort, ushort, ushort> alphaFunction,
                Func<ushort, ushort, ushort, ushort, ushort, ushort> colorFunction)
        {
                ushort computedAlpha = alphaFunction(this.Alpha, other.Alpha);
                if (computedAlpha == 0) return new ColorArgb64(0);
                return new ColorArgb64(
                                computedAlpha,
                                colorFunction(computedAlpha, this.Alpha, other.Alpha, this.Red, other.Red),
                                colorFunction(computedAlpha, this.Alpha, other.Alpha, this.Green, other.Green),
                                colorFunction(computedAlpha, this.Alpha, other.Alpha, this.Blue, other.Blue)
                        );
        }

        /// <summary>
        /// Applies standard alpha compositing with another color placed underneath the current one.
        /// </summary>
        /// <param name="other">The background color.</param>
        /// <returns>The result of the over compositing operation.</returns>
        public IColorArgb<ushort> Over(IColorArgb<ushort> other)
        {
                return BuildColor(
                        other,
                        (thisAlpha, otherAlpha) => (ushort)(thisAlpha + (ushort.MaxValue - thisAlpha) * otherAlpha / ushort.MaxValue),
                        (alpha, thisAlpha, otherAlpha, thisComponent, otherComponent) =>
                        {
                                double numerator = (double)thisComponent * thisAlpha + (double)(ushort.MaxValue - thisAlpha) * otherComponent;
                                return (ushort)(numerator / ushort.MaxValue);
                        });
        }

        /// <summary>
        /// Combines this color with another one by averaging the components using the alphas as weights.
        /// </summary>
        /// <param name="other">The color blended with the current instance.</param>
        /// <returns>The blended color.</returns>
        public IColorArgb<ushort> Add(IColorArgb<ushort> other)
        {
                return BuildColor(
                        other,
                        (thisAlpha, otherAlpha) => (ushort)Math.Sqrt((double)thisAlpha * otherAlpha),
                        (alpha, thisAlpha, otherAlpha, thisComponent, otherComponent) =>
                        {
                                double numerator = (double)thisComponent * thisAlpha + (double)otherComponent * otherAlpha;
                                double denominator = thisAlpha + otherAlpha;
                                return denominator == 0 ? (ushort)0 : (ushort)(numerator / denominator);
                        }
                );
        }

        /// <summary>
        /// Subtracts each component of another color from the current one using a minimum operator.
        /// </summary>
        /// <param name="other">The color to subtract.</param>
        /// <returns>The resulting color containing the minimum components.</returns>
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
