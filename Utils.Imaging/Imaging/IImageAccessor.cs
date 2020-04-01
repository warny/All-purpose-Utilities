using System.Drawing;

namespace Utils.Imaging
{
	public interface IImageAccessor<A, T> 
		where T : struct
		where A : IColorArgb<T>
	{
		int Width { get; }
		int Height { get; }

		A this[int x, int y] { get; set; }
		A this[Point p] { get; set; }

		void Rectangle(Rectangle rectangle, A color);
		void Rectangle(int left, int top, int width, int height, A color);
	}

	public interface IImageAccessor<T>
	{
		int Width { get; }
		int Height { get; }

		T this[int x, int y] { get; set; }
		T this[Point p] { get; set; }

		void Rectangle(Rectangle rectangle, T color);
		void Rectangle(int left, int top, int width, int height, T color);
	}

	public interface IColorArgb<T> where T : struct
	{
		T Alpha { get; set; }
		T Red { get; set; }
		T Green { get; set; }
		T Blue { get; set; }
	}

	public interface IColorAhsv<T> where T : struct
	{
		T Alpha { get; set; }
		T Hue { get; set; }
		T Saturation { get; set; }
		T Value { get; set; }
	}

}
