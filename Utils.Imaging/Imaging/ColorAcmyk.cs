using System;
using Utils.Objects;

namespace Utils.Imaging;

/// <summary>
/// Represents a color using the ACMYK color model (Alpha, Cyan, Magenta, Yellow, Key).
/// </summary>
public struct ColorAcmyk : IColorAcmyk<double>
{
    private double alpha;
    private double cyan;
    private double magenta;
    private double yellow;
    private double key;

    /// <summary>Alpha component in the [0,1] range.</summary>
    public double Alpha
    {
        readonly get => alpha;
        set => alpha = value.ArgMustBeBetween(0.0, 1.0);
    }

    /// <summary>Cyan component in the [0,1] range.</summary>
    public double Cyan
    {
        readonly get => cyan;
        set => cyan = value.ArgMustBeBetween(0.0, 1.0);
    }

    /// <summary>Magenta component in the [0,1] range.</summary>
    public double Magenta
    {
        readonly get => magenta;
        set => magenta = value.ArgMustBeBetween(0.0, 1.0);
    }

    /// <summary>Yellow component in the [0,1] range.</summary>
    public double Yellow
    {
        readonly get => yellow;
        set => yellow = value.ArgMustBeBetween(0.0, 1.0);
    }

    /// <summary>Key/Black component in the [0,1] range.</summary>
    public double Key
    {
        readonly get => key;
        set => key = value.ArgMustBeBetween(0.0, 1.0);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ColorAcmyk"/>.
    /// </summary>
    public ColorAcmyk(double alpha, double cyan, double magenta, double yellow, double key)
    {
        this.alpha = 0;
        this.cyan = 0;
        this.magenta = 0;
        this.yellow = 0;
        this.key = 0;
        Alpha = alpha;
        Cyan = cyan;
        Magenta = magenta;
        Yellow = yellow;
        Key = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorAcmyk"/> struct
    /// from an <see cref="ColorArgb"/> value.
    /// </summary>
    /// <param name="color">Color in the ARGB space.</param>
    public ColorAcmyk(ColorArgb color)
        : this()
    {
        Alpha = color.Alpha;
        double k = 1.0 - Math.Max(color.Red, Math.Max(color.Green, color.Blue));
        Key = k;
        if (k >= 1.0)
        {
            Cyan = 0;
            Magenta = 0;
            Yellow = 0;
            return;
        }

        double denom = 1.0 - k;
        Cyan = (1.0 - color.Red - k) / denom;
        Magenta = (1.0 - color.Green - k) / denom;
        Yellow = (1.0 - color.Blue - k) / denom;
    }

    /// <summary>
    /// Converts this color to an <see cref="ColorArgb"/>.
    /// </summary>
    public readonly ColorArgb ToArgb()
    {
        double r = (1.0 - Cyan) * (1.0 - Key);
        double g = (1.0 - Magenta) * (1.0 - Key);
        double b = (1.0 - Yellow) * (1.0 - Key);
        return new ColorArgb(Alpha, r, g, b);
    }

    /// <summary>
    /// Implicit conversion from <see cref="ColorArgb"/>.
    /// </summary>
    public static implicit operator ColorAcmyk(ColorArgb color) => new(color);

    /// <summary>
    /// Implicit conversion to <see cref="ColorArgb"/>.
    /// </summary>
    public static implicit operator ColorArgb(ColorAcmyk color) => color.ToArgb();

    /// <summary>
    /// Deconstructs this color into its components.
    /// </summary>
    public void Deconstruct(out double alpha, out double cyan, out double magenta, out double yellow, out double key)
    {
        alpha = Alpha;
        cyan = Cyan;
        magenta = Magenta;
        yellow = Yellow;
        key = Key;
    }

    /// <summary>
    /// Returns a readable string representation of this color.
    /// </summary>
    public override readonly string ToString() => $"a:{alpha} c:{cyan} m:{magenta} y:{yellow} k:{key}";
}

