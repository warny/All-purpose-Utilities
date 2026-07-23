using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Utils.Mathematics;

namespace Utils.Imaging;

/// <summary>
/// Represents a 32-bit ARGB color using byte components.
/// </summary>
/// <remarks>
/// <para>
/// The overlapping <see cref="Value"/> field uses a <c>[StructLayout(LayoutKind.Explicit)]</c>
/// layout that assumes <b>little-endian byte order</b>.  On a little-endian host the packed value
/// is <c>(Alpha &lt;&lt; 24) | (Red &lt;&lt; 16) | (Green &lt;&lt; 8) | Blue</c>.  Since this library
/// depends on <c>System.Drawing</c> (Windows/GDI+) it only runs on little-endian platforms,
/// so the canonical packing and the overlapping layout are always consistent.
/// </para>
/// </remarks>
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
    /// Gets or sets the packed ARGB value in host byte order.
    /// </summary>
    /// <remarks>
    /// On a little-endian host the value equals
    /// <c>(Alpha &lt;&lt; 24) | (Red &lt;&lt; 16) | (Green &lt;&lt; 8) | Blue</c>.
    /// </remarks>
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
    public ColorArgb32(uint color) : this() => this.value = color;

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
    /// Initializes a color by copying byte components from another 8-bit color representation.
    /// </summary>
    /// <param name="colorArgb64">The source color.</param>
    public ColorArgb32(IColorArgb<byte> colorArgb64) : this()
    {
        this.alpha = colorArgb64.Alpha;
        this.red = colorArgb64.Red;
        this.green = colorArgb64.Green;
        this.blue = colorArgb64.Blue;
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
    /// Straight-alpha formula: α_out = α_src + α_dst×(1−α_src);
    /// C_out = (C_src×α_src + C_dst×α_dst×(1−α_src)) / α_out.
    /// All intermediate arithmetic is performed in double precision to avoid overflow.
    /// </summary>
    /// <param name="other">The background color.</param>
    /// <returns>The composited color.</returns>
    public IColorArgb<byte> Over(IColorArgb<byte> other)
    {
        const double max = 255.0;
        double aS = this.Alpha / max;
        double aD = other.Alpha / max;
        double aOut = aS + aD * (1.0 - aS);
        if (aOut <= 0.0) return new ColorArgb32(0, 0, 0, 0);
        double kSrc = aS / aOut;
        double kDst = aD * (1.0 - aS) / aOut;
        return new ColorArgb32(
            (byte)Math.Round(aOut * max),
            (byte)Math.Round(Math.Clamp(this.Red   / max * kSrc + other.Red   / max * kDst, 0.0, 1.0) * max),
            (byte)Math.Round(Math.Clamp(this.Green / max * kSrc + other.Green / max * kDst, 0.0, 1.0) * max),
            (byte)Math.Round(Math.Clamp(this.Blue  / max * kSrc + other.Blue  / max * kDst, 0.0, 1.0) * max)
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
    /// Subtracts each channel of <paramref name="other"/> from the corresponding channel of this
    /// color, clamping results to [0, 255].
    /// </summary>
    /// <param name="other">The color to subtract.</param>
    /// <returns>The channel-wise clamped difference.</returns>
    public IColorArgb<byte> Subtract(IColorArgb<byte> other)
    {
        return new ColorArgb32(
            (byte)Math.Max(0, this.Alpha - other.Alpha),
            (byte)Math.Max(0, this.Red   - other.Red),
            (byte)Math.Max(0, this.Green - other.Green),
            (byte)Math.Max(0, this.Blue  - other.Blue)
        );
    }

    /// <summary>
    /// Computes a linear gradient between two colors.
    /// </summary>
    /// <param name="color1">The starting color.</param>
    /// <param name="color2">The ending color.</param>
    /// <param name="position">Interpolation factor between 0 and 1.</param>
    /// <returns>The interpolated color.</returns>
    public static ColorArgb32 LinearGradient(ColorArgb32 color1, ColorArgb32 color2, float position)
    {
        return new ColorArgb32(
                (byte)(color1.alpha * (1 - position) + color2.alpha * position),
                (byte)(color1.red * (1 - position) + color2.red * position),
                (byte)(color1.green * (1 - position) + color2.green * position),
    (byte)(color1.blue * (1 - position) + color2.blue * position)
);
    }
}
