using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Utils.Imaging;

public interface IImageAccessor<A, T> : IImageAccessor<A>
	where T : struct, INumber<T>
	where A : IColorArgb<T>
{ }

public interface IImageAccessor<T>
{
	/// <summary>
	/// Image width
	/// </summary>
	int Width { get; }
	/// <summary>
	/// Image height
	/// </summary>
	int Height { get; }

	/// <summary>
	/// Pixel accessor
	/// </summary>
	/// <param name="x">x coordinate</param>
	/// <param name="y">y coordinate</param>
	/// <returns></returns>
	T this[int x, int y] { get; set; }
	/// <summary>
	/// Pixel accessor
	/// </summary>
	/// <param name="p">pixel coordinates</param>
	/// <returns></returns>
	T this[Point p] {
		get => this[p.X, p.Y];
		set => this[p.X, p.Y] = value;
	}
}

public interface IColorArgb<T> where T : struct, INumber<T>
{
	virtual static T MinValue { get; } = T.CreateChecked(0);
	abstract static T MaxValue { get; }

	T Alpha { get; set; }
	T Red { get; set; }
	T Green { get; set; }
	T Blue { get; set; }

	IColorArgb<T> Over(IColorArgb<T> other);
	IColorArgb<T> Add(IColorArgb<T> other);
	IColorArgb<T> Substract(IColorArgb<T> other);
	void Deconstruct(out T alpha, out T red, out T green, out T blue)
	{
		alpha = Alpha;
		red = Red;
		green = Green;
		blue = Blue;
	}
	void Deconstruct(out T red, out T green, out T blue)
	{
		red = Red;
		green = Green;
		blue = Blue;
	}

}

/// <summary>
/// Define the color system to be convertible with <typeparamref name="TArgb"/>
/// </summary>
/// <typeparam name="TSelf"></typeparam>
/// <typeparam name="TArgb"></typeparam>
/// <typeparam name="T"></typeparam>
public interface IColorArgbConvertible<TSelf, TArgb, T>
	where TSelf : IColorArgbConvertible<TSelf, TArgb, T>
	where TArgb : IColorArgb<T>
	where T : struct, INumber<T>
{
	/// <summary>
	/// Convert from ArgbColor
	/// </summary>
	/// <param name="color"><typeparamref name="TSelf"/> to convert from</param>
	/// <returns><typeparamref name="TSelf"/></returns>
	static abstract TSelf FromArgbColor(TArgb color);

	/// <summary>
	/// Implicitly convert from <typeparamref name="TArgb"/>
	/// </summary>
	/// <param name="color"></param>
	static virtual implicit operator TSelf(TArgb color) => TSelf.FromArgbColor(color);

	/// <summary>
	/// Converts to <typeparamref name="TArgb"/>
	/// </summary>
	/// <returns><typeparamref name="TArgb"/></returns>
	TArgb ToArgbColor();

	/// <summary>
	/// Implicitly convert to <typeparamref name="TArgb"/>
	/// </summary>
	/// <param name="color"></param>
	static virtual implicit operator TArgb(TSelf color) => color.ToArgbColor();

}

public interface IColorAhsv<T> where T : struct, INumber<T>
{
	virtual static T MinValue { get; } = T.CreateChecked(0);
	abstract static T MaxValue { get; }

	/// <summary>
	/// Alpha component
	/// </summary>
	T Alpha { get; set; }
	/// <summary>
	/// Hue component
	/// </summary>
	T Hue { get; set; }
	/// <summary>
	/// Saturation component
	/// </summary>
	T Saturation { get; set; }
	/// <summary>
	/// Value component
	/// </summary>
	T Value { get; set; }
	void Deconstruct(out T alpha, out T hue, out T saturation, out T value)
	{
		alpha = Alpha;
		hue = Hue;
		saturation = Saturation;
		value = Value;
	}

	void Deconstruct(out T hue, out T saturation, out T value)
	{
		hue = Hue;
		saturation = Saturation;
		value = Value;
	}

}
