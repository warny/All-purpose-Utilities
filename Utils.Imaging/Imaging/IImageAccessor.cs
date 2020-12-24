using System;
using System.Drawing;

namespace Utils.Imaging
{
	public interface IImageAccessor<A, T> : IImageAccessor<A> 
		where T : struct
		where A : IColorArgb<T>
	{ }

	public interface IImageAccessor<T>
	{
		int Width { get; }
		int Height { get; }

		T this[int x, int y] { get; set; }
		T this[Point p] { get; set; }
	}

	public interface IColorArgb<T> where T : struct
	{
		T Alpha { get; set; }
		T Red { get; set; }
		T Green { get; set; }
		T Blue { get; set; }

		IColorArgb<T> Over(IColorArgb<T> other);
		IColorArgb<T> Add(IColorArgb<T> other);
		IColorArgb<T> Substract(IColorArgb<T> other);
		void Deconstruct(out T alpha, out T red, out T green, out T blue);
		void Deconstruct(out T red, out T green, out T blue);
	}

	public interface IColorAhsv<T> where T : struct
	{
		T Alpha { get; set; }
		T Hue { get; set; }
		T Saturation { get; set; }
		T Value { get; set; }
		void Deconstruct(out T alpha, out T hue, out T saturation, out T value);
		void Deconstruct(out T hue, out T saturation, out T value);
	}

}
									