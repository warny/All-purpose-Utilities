using System;
using Utils.Objects;

namespace Utils.Imaging;

/// <summary>
/// Represents a color using the ALab color model (Alpha, Lightness, A, B).
/// </summary>
public struct ColorAlab : IColorAlab<double>
{
    private double alpha;
    private double l;
    private double a;
    private double b;

    /// <summary>Alpha component in the [0,1] range.</summary>
    public double Alpha
    {
        readonly get => alpha;
        set => alpha = value.ArgMustBeBetween(0.0, 1.0);
    }

    /// <summary>Lightness component in the [0,100] range.</summary>
    public double L
    {
        readonly get => l;
        set => l = value.ArgMustBeBetween(0.0, 100.0);
    }

    /// <summary>A component in the [-128,127] range.</summary>
    public double A
    {
        readonly get => a;
        set => a = value.ArgMustBeBetween(-128.0, 127.0);
    }

    /// <summary>B component in the [-128,127] range.</summary>
    public double B
    {
        readonly get => b;
        set => b = value.ArgMustBeBetween(-128.0, 127.0);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ColorAlab"/>.
    /// </summary>
    public ColorAlab(double alpha, double l, double a, double b)
    {
        this.alpha = 0;
        this.l = 0;
        this.a = 0;
        this.b = 0;
        Alpha = alpha;
        L = l;
        A = a;
        B = b;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorAlab"/> struct
    /// from an <see cref="ColorArgb"/> value.
    /// </summary>
    /// <param name="color">Color in the ARGB space.</param>
    public ColorAlab(ColorArgb color)
        : this()
    {
        Alpha = color.Alpha;

        static double ToLinear(double c) =>
            c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        double r = ToLinear(color.Red);
        double g = ToLinear(color.Green);
        double bl = ToLinear(color.Blue);

        double x = r * 0.4124 + g * 0.3576 + bl * 0.1805;
        double y = r * 0.2126 + g * 0.7152 + bl * 0.0722;
        double z = r * 0.0193 + g * 0.1192 + bl * 0.9505;

        x /= 0.95047;
        y /= 1.0;
        z /= 1.08883;

        static double F(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : (7.787 * t) + (16.0 / 116.0);

        double fx = F(x);
        double fy = F(y);
        double fz = F(z);

        L = 116 * fy - 16;
        A = 500 * (fx - fy);
        B = 200 * (fy - fz);
    }

    /// <summary>
    /// Converts this color to an <see cref="ColorArgb"/>.
    /// </summary>
    public readonly ColorArgb ToArgb()
    {
        double y = (L + 16) / 116.0;
        double x = A / 500.0 + y;
        double z = y - B / 200.0;

        static double FInv(double t) =>
            t > 0.206893034 ? t * t * t : (t - 16.0 / 116.0) / 7.787;

        x = FInv(x) * 0.95047;
        y = FInv(y);
        z = FInv(z) * 1.08883;

        double r = x * 3.2406 + y * -1.5372 + z * -0.4986;
        double g = x * -0.9689 + y * 1.8758 + z * 0.0415;
        double bl = x * 0.0557 + y * -0.2040 + z * 1.0570;

        static double ToSrgb(double c) =>
            c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;

        r = ToSrgb(r);
        g = ToSrgb(g);
        bl = ToSrgb(bl);

        static double Clamp(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        return new ColorArgb(Alpha, Clamp(r), Clamp(g), Clamp(bl));
    }

    /// <summary>
    /// Implicit conversion from <see cref="ColorArgb"/>.
    /// </summary>
    public static implicit operator ColorAlab(ColorArgb color) => new(color);

    /// <summary>
    /// Implicit conversion to <see cref="ColorArgb"/>.
    /// </summary>
    public static implicit operator ColorArgb(ColorAlab color) => color.ToArgb();

    /// <summary>
    /// Deconstructs this color into its components.
    /// </summary>
    public void Deconstruct(out double alpha, out double l, out double a, out double b)
    {
        alpha = Alpha;
        l = L;
        a = A;
        b = B;
    }

    /// <summary>
    /// Returns a readable string representation of this color.
    /// </summary>
    public override readonly string ToString() => $"a:{alpha} l:{l} a:{this.a} b:{this.b}";
}

