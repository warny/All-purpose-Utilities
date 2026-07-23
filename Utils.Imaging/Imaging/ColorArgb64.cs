using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Utils.Mathematics;

namespace Utils.Imaging;


/// <summary>
/// Represents a 64-bit ARGB color using 16-bit components.
/// </summary>
/// <remarks>
/// <para>
/// The overlapping <see cref="Value"/> field uses a <c>[StructLayout(LayoutKind.Explicit)]</c>
/// layout that assumes <b>little-endian byte order</b>.  On a little-endian host the packed value
/// is <c>((ulong)Alpha &lt;&lt; 48) | ((ulong)Red &lt;&lt; 32) | ((ulong)Green &lt;&lt; 16) | Blue</c>.
/// Since this library depends on <c>System.Drawing</c> (Windows/GDI+) it only runs on little-endian
/// platforms, so the canonical packing and the overlapping layout are always consistent.
/// </para>
/// </remarks>
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

    // little-endian layout: blue[0-1] green[2-3] red[4-5] alpha[6-7]
    [FieldOffset(6)]
    ushort alpha;
    [FieldOffset(4)]
    ushort red;
    [FieldOffset(2)]
    ushort green;
    [FieldOffset(0)]
    ushort blue;

    /// <summary>
    /// Gets or sets the packed ARGB value in host byte order.
    /// </summary>
    /// <remarks>
    /// On a little-endian host the value equals
    /// <c>((ulong)Alpha &lt;&lt; 48) | ((ulong)Red &lt;&lt; 32) | ((ulong)Green &lt;&lt; 16) | Blue</c>.
    /// </remarks>
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
    /// <param name="color">The packed ARGB value containing four 8-bit components in 0xAARRGGBB order.</param>
    public ColorArgb64(uint color) : this()
    {
        this.alpha = ExpandByte((byte)(color >> 24));
        this.red = ExpandByte((byte)(color >> 16));
        this.green = ExpandByte((byte)(color >> 8));
        this.blue = ExpandByte((byte)color);
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
        this.alpha = ExpandByte(colorArgb32.Alpha);
        this.red = ExpandByte(colorArgb32.Red);
        this.green = ExpandByte(colorArgb32.Green);
        this.blue = ExpandByte(colorArgb32.Blue);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorArgb64"/> struct from a <see cref="System.Drawing.Color"/> value.
    /// </summary>
    /// <param name="color">The color whose channels are expanded to 16 bits.</param>
    public ColorArgb64(System.Drawing.Color color) : this()
    {
        this.alpha = ExpandByte(color.A);
        this.red = ExpandByte(color.R);
        this.green = ExpandByte(color.G);
        this.blue = ExpandByte(color.B);
    }

    /// <summary>
    /// Expands an 8-bit channel value to 16 bits while preserving the full [0, 65535] range.
    /// Maps 0 to 0 and 255 to 65535 exactly via bit replication.
    /// </summary>
    private static ushort ExpandByte(byte value) => (ushort)((value << 8) | value);

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
    /// Straight-alpha formula: α_out = α_src + α_dst×(1−α_src);
    /// C_out = (C_src×α_src + C_dst×α_dst×(1−α_src)) / α_out.
    /// All intermediate arithmetic is performed in double precision.
    /// </summary>
    /// <param name="other">The background color.</param>
    /// <returns>The result of the over compositing operation.</returns>
    public IColorArgb<ushort> Over(IColorArgb<ushort> other)
    {
        const double max = 65535.0;
        double aS = this.alpha / max;
        double aD = other.Alpha / max;
        double aOut = aS + aD * (1.0 - aS);
        if (aOut <= 0.0) return new ColorArgb64(0);
        double kSrc = aS / aOut;
        double kDst = aD * (1.0 - aS) / aOut;
        return new ColorArgb64(
            (ushort)Math.Round(aOut * max),
            (ushort)Math.Round(Math.Clamp(this.red   / max * kSrc + other.Red   / max * kDst, 0.0, 1.0) * max),
            (ushort)Math.Round(Math.Clamp(this.green / max * kSrc + other.Green / max * kDst, 0.0, 1.0) * max),
            (ushort)Math.Round(Math.Clamp(this.blue  / max * kSrc + other.Blue  / max * kDst, 0.0, 1.0) * max)
        );
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
    /// Subtracts each channel of <paramref name="other"/> from the corresponding channel of this
    /// color, clamping results to [0, 65535].
    /// </summary>
    /// <param name="other">The color to subtract.</param>
    /// <returns>The channel-wise clamped difference.</returns>
    public IColorArgb<ushort> Subtract(IColorArgb<ushort> other)
    {
        return new ColorArgb64(
            (ushort)Math.Max(0, this.Alpha - other.Alpha),
            (ushort)Math.Max(0, this.Red   - other.Red),
            (ushort)Math.Max(0, this.Green - other.Green),
            (ushort)Math.Max(0, this.Blue  - other.Blue)
        );
    }

}
