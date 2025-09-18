using System;
using System.Numerics;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Imaging;

/// <summary>
/// Represents an ARGB color using <see cref="double"/> components.
/// </summary>
public struct ColorArgb : IColorArgb<double>, IEquatable<ColorArgb>, IEqualityOperators<ColorArgb, ColorArgb, bool>
{

        /// <summary>
        /// Lowest component value accepted for <see cref="ColorArgb"/> instances.
        /// </summary>
        public static double MinValue { get; } = 0.0;

        /// <summary>
        /// Highest component value accepted for <see cref="ColorArgb"/> instances.
        /// </summary>
        public static double MaxValue { get; } = 1.0;

	private double alpha;
	private double red;
	private double green;
	private double blue;

        /// <summary>
        /// Gets or sets the alpha channel expressed in the [0,1] range.
        /// </summary>
        public double Alpha
        {
                get => alpha;

                set
                {
                        value.ArgMustBeBetween(MinValue, MaxValue);
                        this.alpha=value;
                }
        }

        /// <summary>
        /// Gets or sets the red channel expressed in the [0,1] range.
        /// </summary>
        public double Red
        {
                get => red;

                set
                {
			value.ArgMustBeBetween(MinValue, MaxValue);
			this.red=value;
		}
	}

        /// <summary>
        /// Gets or sets the green channel expressed in the [0,1] range.
        /// </summary>
        public double Green
        {
                get => green;

                set
                {
			value.ArgMustBeBetween(MinValue, MaxValue);
			this.green=value;
		}
	}

        /// <summary>
        /// Gets or sets the blue channel expressed in the [0,1] range.
        /// </summary>
        public double Blue
        {
                get => blue;

                set
                {
			value.ArgMustBeBetween(MinValue, MaxValue);
			this.blue=value;
		}
	}

        /// <summary>
        /// Initializes an opaque color with explicit RGB components.
        /// </summary>
        /// <param name="red">Red component in the [0,1] range.</param>
        /// <param name="green">Green component in the [0,1] range.</param>
        /// <param name="blue">Blue component in the [0,1] range.</param>
        public ColorArgb(double red, double green, double blue) : this(1D, red, green, blue) { }

        /// <summary>
        /// Initializes a color using explicit ARGB components.
        /// </summary>
        /// <param name="alpha">Alpha component in the [0,1] range.</param>
        /// <param name="red">Red component in the [0,1] range.</param>
        /// <param name="green">Green component in the [0,1] range.</param>
        /// <param name="blue">Blue component in the [0,1] range.</param>
        public ColorArgb(double alpha, double red, double green, double blue)
        {
                alpha.ArgMustBeBetween(MinValue, MaxValue);
                red.ArgMustBeBetween(MinValue, MaxValue);
                green.ArgMustBeBetween(MinValue, MaxValue);
                blue.ArgMustBeBetween(MinValue, MaxValue);

		this.alpha = alpha;
		this.red = red;
		this.green = green;
		this.blue = blue;
	}

        /// <summary>
        /// Initializes a color by converting from an 8-bit ARGB representation.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        public ColorArgb(ColorArgb32 color) : this(color.Alpha / 255.0, color.Red / 255.0, color.Green / 255.0, color.Blue / 255.0) { }

        /// <summary>
        /// Initializes a color by converting from a 16-bit ARGB representation.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        public ColorArgb(ColorArgb64 color) : this(color.Alpha / 255.0, color.Red / 255.0, color.Green / 255.0, color.Blue / 255.0) { }

        /// <summary>
        /// Initializes a color by converting from an HSV representation.
        /// </summary>
        /// <param name="color">The HSV color to convert.</param>
        public ColorArgb(ColorAhsv color) : this()
        {
                this = color.ToArgbColor();
        }

        /// <summary>
        /// Converts an HSV color to the floating-point ARGB representation.
        /// </summary>
        /// <param name="color">The HSV color to convert.</param>
        /// <returns>The converted color.</returns>
        public static implicit operator ColorArgb(ColorAhsv color) => new (color);

        /// <summary>
        /// Converts an 8-bit ARGB color to the floating-point representation.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The converted color.</returns>
        public static implicit operator ColorArgb(ColorArgb32 color) => new (color);

        /// <summary>
        /// Converts a 16-bit ARGB color to the floating-point representation.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The converted color.</returns>
        public static implicit operator ColorArgb(ColorArgb64 color) => new (color);

        /// <summary>
        /// Converts a <see cref="System.Drawing.Color"/> value to the floating-point representation.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The converted color.</returns>
        public static implicit operator ColorArgb(System.Drawing.Color color) => new (color.A / 255.0, color.R / 255.0, color.G / 255.0, color.B / 255.0);

        /// <summary>
        /// Blends two colors using a perceptual gradient.
        /// </summary>
        /// <param name="color1">The starting color.</param>
        /// <param name="color2">The ending color.</param>
        /// <param name="percent">Blend factor in the [0,1] range.</param>
        /// <returns>The interpolated color.</returns>
        public static ColorArgb Gradient( ColorArgb color1, ColorArgb color2, double percent )
        {
                if (percent < 0) percent = 0;
                else if (percent > 1) percent = 1;
                return new ColorArgb(
                        color1.alpha * (1-percent) + color2.alpha * percent,
			Math.Sqrt(Math.Pow(color1.Red, 2) * (1-percent) + Math.Pow(color2.Red , 2) * percent),
			Math.Sqrt(Math.Pow(color1.green, 2) * (1-percent) + Math.Pow(color2.green, 2) * percent),
			Math.Sqrt(Math.Pow(color1.blue, 2) * (1-percent) + Math.Pow(color2.blue, 2) * percent)
		);
	}
        /// <summary>
        /// Returns a textual representation of the ARGB components.
        /// </summary>
        /// <returns>A string describing the ARGB values.</returns>
        public override readonly string ToString() => $"a:{alpha} R:{red} G:{green} B:{blue}";

        /// <summary>
        /// Applies the Porter-Duff over operator using the current color as the foreground.
        /// </summary>
        /// <param name="other">The background color.</param>
        /// <returns>The composited color.</returns>
        public IColorArgb<double> Over(IColorArgb<double> other) => new ColorArgb(
                this.Alpha + (1.0 - this.Alpha) * other.Alpha,
                this.Red * this.Alpha + (1.0 - this.Alpha) * other.Red,
                this.Green * this.Alpha + (1.0 - this.Alpha) * other.Green,
                this.Blue * this.Alpha + (1.0 - this.Alpha) * other.Blue
        );

        /// <summary>
        /// Adds two colors while clamping each component to the [0,1] range.
        /// </summary>
        /// <param name="other">The color to add.</param>
        /// <returns>The resulting color.</returns>
        public IColorArgb<double> Add(IColorArgb<double> other) => new ColorArgb(
                MathEx.Min(1.0, this.Alpha + other.Alpha),
                MathEx.Min(1.0, this.Red + other.Red),
                MathEx.Min(1.0, this.Green + other.Green),
                MathEx.Min(1.0, this.Blue + other.Blue)
        );

        /// <summary>
        /// Produces a color using the component-wise minimum of the operands.
        /// </summary>
        /// <param name="other">The color compared with the current instance.</param>
        /// <returns>The resulting color.</returns>
        public IColorArgb<double> Substract(IColorArgb<double> other) => new ColorArgb(
                        MathEx.Min(this.Alpha, other.Alpha),
                        MathEx.Min(this.Red, other.Red),
                        MathEx.Min(this.Green, other.Green),
                        MathEx.Min(this.Blue, other.Blue)
        );

        /// <inheritdoc/>
        public bool Equals(ColorArgb other) =>
                Alpha == other.Alpha &&
                Red == other.Red &&
                Green == other.Green &&
                Blue == other.Blue;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ColorArgb other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Alpha, Red, Green, Blue);

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(ColorArgb left, ColorArgb right) => left.Equals(right);

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(ColorArgb left, ColorArgb right) => !left.Equals(right);

}
