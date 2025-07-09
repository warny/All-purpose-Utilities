using System;
using Utils.Objects;

namespace Utils.Imaging;

/// <summary>
/// Represents a color using the ACYM color model (Alpha, Cyan, Yellow, Magenta).
/// </summary>
public struct ColorAcym : IColorAcym<double>
{
    private double alpha;
    private double cyan;
    private double yellow;
    private double magenta;

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

    /// <summary>Yellow component in the [0,1] range.</summary>
    public double Yellow
    {
        readonly get => yellow;
        set => yellow = value.ArgMustBeBetween(0.0, 1.0);
    }

    /// <summary>Magenta component in the [0,1] range.</summary>
    public double Magenta
    {
        readonly get => magenta;
        set => magenta = value.ArgMustBeBetween(0.0, 1.0);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ColorAcym"/>.
    /// </summary>
    public ColorAcym(double alpha, double cyan, double yellow, double magenta)
    {
        this.alpha = 0;
        this.cyan = 0;
        this.yellow = 0;
        this.magenta = 0;
        Alpha = alpha;
        Cyan = cyan;
        Yellow = yellow;
        Magenta = magenta;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorAcym"/> struct
    /// from an <see cref="ColorArgb"/> value.
    /// </summary>
    /// <param name="color">Color in the ARGB color space.</param>
    public ColorAcym(ColorArgb color)
        : this()
    {
        Alpha = color.Alpha;
        Cyan = 1.0 - color.Red;
        Yellow = 1.0 - color.Blue;
        Magenta = 1.0 - color.Green;
    }

    /// <summary>
    /// Converts this color to an <see cref="ColorArgb"/>.
    /// </summary>
    public readonly ColorArgb ToArgb() => new(Alpha, 1.0 - Cyan, 1.0 - Magenta, 1.0 - Yellow);

    /// <summary>
    /// Implicit conversion from <see cref="ColorArgb"/>.
    /// </summary>
    public static implicit operator ColorAcym(ColorArgb color) => new(color);

    /// <summary>
    /// Implicit conversion to <see cref="ColorArgb"/>.
    /// </summary>
    public static implicit operator ColorArgb(ColorAcym color) => color.ToArgb();

    /// <summary>
    /// Deconstructs this color into its components.
    /// </summary>
    public void Deconstruct(out double alpha, out double cyan, out double yellow, out double magenta)
    {
        alpha = Alpha;
        cyan = Cyan;
        yellow = Yellow;
        magenta = Magenta;
    }

    /// <summary>
    /// Returns a readable string representation of this color.
    /// </summary>
    public override readonly string ToString() => $"a:{alpha} c:{cyan} y:{yellow} m:{magenta}";
}

