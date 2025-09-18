using System;
using System.Drawing;
using System.Numerics;

namespace Utils.Imaging;

/// <summary>
/// Provides strongly typed access to an image whose pixels are represented by an ARGB color structure.
/// </summary>
/// <typeparam name="A">ARGB color representation used for each pixel.</typeparam>
/// <typeparam name="T">Numeric type describing the channel precision.</typeparam>
public interface IImageAccessor<A, T> : IImageAccessor<A>
    where T : struct, INumber<T>
    where A : IColorArgb<T>
{ }

/// <summary>
/// Describes an abstraction capable of exposing read/write access to a two-dimensional image resource.
/// </summary>
/// <typeparam name="T">Pixel value representation.</typeparam>
public interface IImageAccessor<T>
{
    /// <summary>
    /// Gets the width of the region exposed by the accessor, in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the height of the region exposed by the accessor, in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Gets or sets a pixel at the provided zero-based coordinates.
    /// </summary>
    /// <param name="x">Horizontal coordinate of the pixel to access.</param>
    /// <param name="y">Vertical coordinate of the pixel to access.</param>
    T this[int x, int y] { get; set; }

    /// <summary>
    /// Gets or sets a pixel at the provided zero-based coordinates.
    /// </summary>
    /// <param name="p">Location of the pixel to access.</param>
    T this[Point p]
    {
        get => this[p.X, p.Y];
        set => this[p.X, p.Y] = value;
    }
}

/// <summary>
/// Represents a color value expressed through alpha, red, green, and blue channels.
/// </summary>
/// <typeparam name="T">Numeric type describing the channel precision.</typeparam>
public interface IColorArgb<T>
    where T : struct, INumber<T>
{
    /// <summary>
    /// Gets the minimum representable value for each channel.
    /// </summary>
    static virtual T MinValue { get; } = T.CreateChecked(0);

    /// <summary>
    /// Gets the maximum representable value for each channel.
    /// </summary>
    static abstract T MaxValue { get; }

    /// <summary>
    /// Gets or sets the alpha channel component.
    /// </summary>
    T Alpha { get; set; }

    /// <summary>
    /// Gets or sets the red channel component.
    /// </summary>
    T Red { get; set; }

    /// <summary>
    /// Gets or sets the green channel component.
    /// </summary>
    T Green { get; set; }

    /// <summary>
    /// Gets or sets the blue channel component.
    /// </summary>
    T Blue { get; set; }

    /// <summary>
    /// Blends this color over <paramref name="other"/> using standard alpha compositing.
    /// </summary>
    /// <param name="other">Underlying color to blend onto.</param>
    /// <returns>The composited color.</returns>
    IColorArgb<T> Over(IColorArgb<T> other);

    /// <summary>
    /// Adds the channels of <paramref name="other"/> to this color and returns the result.
    /// </summary>
    /// <param name="other">Color to add channel values from.</param>
    /// <returns>The channel-wise sum.</returns>
    IColorArgb<T> Add(IColorArgb<T> other);

    /// <summary>
    /// Subtracts the channels of <paramref name="other"/> from this color and returns the result.
    /// </summary>
    /// <param name="other">Color providing channel values to subtract.</param>
    /// <returns>The channel-wise difference.</returns>
    IColorArgb<T> Substract(IColorArgb<T> other);

    /// <summary>
    /// Deconstructs the color into its alpha and RGB components.
    /// </summary>
    /// <param name="alpha">Receives the alpha component.</param>
    /// <param name="red">Receives the red component.</param>
    /// <param name="green">Receives the green component.</param>
    /// <param name="blue">Receives the blue component.</param>
    void Deconstruct(out T alpha, out T red, out T green, out T blue)
    {
        alpha = Alpha;
        red = Red;
        green = Green;
        blue = Blue;
    }

    /// <summary>
    /// Deconstructs the color into its RGB components, discarding alpha.
    /// </summary>
    /// <param name="red">Receives the red component.</param>
    /// <param name="green">Receives the green component.</param>
    /// <param name="blue">Receives the blue component.</param>
    void Deconstruct(out T red, out T green, out T blue)
    {
        red = Red;
        green = Green;
        blue = Blue;
    }
}

/// <summary>
/// Defines a color system that can convert to and from an ARGB representation.
/// </summary>
/// <typeparam name="TSelf">Implementing color type.</typeparam>
/// <typeparam name="TArgb">ARGB representation compatible with <typeparamref name="TSelf"/>.</typeparam>
/// <typeparam name="T">Numeric type describing the channel precision.</typeparam>
public interface IColorArgbConvertible<TSelf, TArgb, T>
    where TSelf : IColorArgbConvertible<TSelf, TArgb, T>
    where TArgb : IColorArgb<T>
    where T : struct, INumber<T>
{
    /// <summary>
    /// Converts an ARGB color into the implementing type.
    /// </summary>
    /// <param name="color">ARGB source color.</param>
    /// <returns>Equivalent color expressed as <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromArgbColor(TArgb color);

    /// <summary>
    /// Implicitly convert from <typeparamref name="TArgb"/>.
    /// </summary>
    /// <param name="color">ARGB color to convert.</param>
    static virtual implicit operator TSelf(TArgb color) => TSelf.FromArgbColor(color);

    /// <summary>
    /// Converts this color to its ARGB representation.
    /// </summary>
    /// <returns>Equivalent color expressed as <typeparamref name="TArgb"/>.</returns>
    TArgb ToArgbColor();

    /// <summary>
    /// Implicitly convert to <typeparamref name="TArgb"/>.
    /// </summary>
    /// <param name="color">Color to convert.</param>
    static virtual implicit operator TArgb(TSelf color) => color.ToArgbColor();
}

/// <summary>
/// Represents a color value expressed through alpha, hue, saturation, and value channels.
/// </summary>
/// <typeparam name="T">Numeric type describing the channel precision.</typeparam>
public interface IColorAhsv<T>
    where T : struct, INumber<T>
{
    /// <summary>
    /// Gets the minimum representable value for each channel.
    /// </summary>
    static virtual T MinValue { get; } = T.CreateChecked(0);

    /// <summary>
    /// Gets the maximum representable value for each channel.
    /// </summary>
    static abstract T MaxValue { get; }

    /// <summary>
    /// Gets or sets the alpha channel component.
    /// </summary>
    T Alpha { get; set; }

    /// <summary>
    /// Gets or sets the hue channel component.
    /// </summary>
    T Hue { get; set; }

    /// <summary>
    /// Gets or sets the saturation channel component.
    /// </summary>
    T Saturation { get; set; }

    /// <summary>
    /// Gets or sets the value (brightness) channel component.
    /// </summary>
    T Value { get; set; }

    /// <summary>
    /// Deconstructs the color into alpha, hue, saturation, and value components.
    /// </summary>
    /// <param name="alpha">Receives the alpha component.</param>
    /// <param name="hue">Receives the hue component.</param>
    /// <param name="saturation">Receives the saturation component.</param>
    /// <param name="value">Receives the value component.</param>
    void Deconstruct(out T alpha, out T hue, out T saturation, out T value)
    {
        alpha = Alpha;
        hue = Hue;
        saturation = Saturation;
        value = Value;
    }

    /// <summary>
    /// Deconstructs the color into hue, saturation, and value components, discarding alpha.
    /// </summary>
    /// <param name="hue">Receives the hue component.</param>
    /// <param name="saturation">Receives the saturation component.</param>
    /// <param name="value">Receives the value component.</param>
    void Deconstruct(out T hue, out T saturation, out T value)
    {
        hue = Hue;
        saturation = Saturation;
        value = Value;
    }
}
