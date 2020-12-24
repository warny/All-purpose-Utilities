using System;
using System.Collections.Generic;
using System.Text;
using Utils.Imaging;
using Utils.Mathematics;

namespace Utils.Drawing
{
	public class Draw<T> 
	{
		public IImageAccessor<T> ImageAccessor { get; }
		public double Top { get; }
		public double Left { get; }
		public double Right { get; }
		public double Down { get; }

		private readonly double hRatio, vRatio;

		public Draw(IImageAccessor<T> imageAccessor) : this(imageAccessor, 0, 0, imageAccessor.Width - 1, imageAccessor.Height - 1) { }

		public Draw(IImageAccessor<T> imageAccessor, double top, double left, double right, double down)
		{
			ImageAccessor = imageAccessor;
			Top = top;
			Left = left;
			Right = right;
			Down = down;
			hRatio = ImageAccessor.Width / (Right - Left);
			vRatio = ImageAccessor.Height / (Down - Top);
		}

		public (int x, int y) ComputePixelPosition(double x, double y)
		{
			return (
				(int)((x - Left) * hRatio),
				(int)((y = Down) * vRatio)
			);
		}

		public (double x, double y) ComputePoint(int x, int y)
		{
			return (
				x / hRatio + Left,
				y / vRatio + Top
			);
		}

		public void Drawpoint(double x, double y, T color)
		{
			var p = ComputePixelPosition(x, y);
			DrawPoint(p.x, p.y, color);
		}

		public void DrawPoint(int x, int y, T color)
		{
			ImageAccessor[x, y] = color;
		}

		public void DrawLine(double x1, double y1, double x2, double y2, T color)
		{
			var p1 = ComputePixelPosition(x1, y1);
			var p2 = ComputePixelPosition(x2, y2);
			DrawLine(p1.x, p1.y, p2.x, p2.y, color);
		}

		private void DrawLine(int x1, int y1, int x2 , int y2, T color)
		{
			const int precision = 1024;
			if (Math.Abs(x1 - x2) > Math.Abs(y1 - y2))
			{
				if (x1 > x2) { (x1, y1, x2, y2) = (x2, y2, x1, y1); }
				int slope = precision * (y1 - y2) / (x1 - x2);
				int k = (y1 * precision - slope * x1);
				for (int x = x1; x <= x2; x++)
				{
					int y = (x * slope + k) / precision;
					ImageAccessor[x, y] = color;
				}
			}
			else
			{
				if (x1 > x2) { (x1, y1, x2, y2) = (x2, y2, x1, y1); }
				int slope = precision * (x1 - x2) / (y1 - y2);
				int k = x1 * precision - slope * y1;
				for (int y = y1; y <= y2; y++)
				{
					int x = (y * slope + k) / precision;
					ImageAccessor[x, y] = color;
				}
			}
		}

		public void DrawLine(double x1, double y1, double x2, double y2, Func<double, T> color)
		{
			var p1 = ComputePixelPosition(x1, y1);
			var p2 = ComputePixelPosition(x2, y2);
			DrawLine(p1.x, p1.y, p2.x, p2.y, color);
		}

		private void DrawLine(int x1, int y1, int x2 , int y2, Func<double, T> color)
		{
			const int precision = 1024;
			Func<int, double> colorGradient;

			if (Math.Abs(x1 - x2) > Math.Abs(y1 - y2))
			{
				if (x1 > x2)
				{
					(x1, y1, x2, y2) = (x2, y2, x1, y1);
					colorGradient = i => 1 - (double)(i - x1) / (double)(x2 - x1);
				}
				else
				{
					colorGradient = i => (double)(i - x1) / (double)(x2 - x1);
				}


				int slope = precision * (y1 - y2) / (x1 - x2);
				int k = (y1 * precision - slope * x1);
				for (int x = x1; x <= x2; x++)
				{
					int y = (x * slope + k) / precision;
					ImageAccessor[x, y] = color(colorGradient(x));
				}
			}
			else
			{
				if (x1 > x2) { 
					(x1, y1, x2, y2) = (x2, y2, x1, y1); 
					colorGradient = i => 1 - (double)(i - y1) / (double)(y2 - y1);
				}
				else
				{
					colorGradient = i => (double)(i - y1) / (double)(y2 - y1);
				}

				int slope = precision * (x1 - x2) / (y1 - y2);
				int k = x1 * precision - slope * y1;
				for (int y = y1; y <= y2; y++)
				{
					int x = (y * slope + k) / precision;
					ImageAccessor[x, y] = color(colorGradient(x));
				}
			}
		}

		public void FillRectangle(double x1, double y1, double x2, double y2, T color)
		{
			var p1 = ComputePixelPosition(x1, y1);
			var p2 = ComputePixelPosition(x2, y2);
			FillRectangle(p1.x, p1.y, p2.x, p2.y, color);
		}

		public void FillRectangle(int x1, int y1, int x2, int y2, T color)
		{
			if (x1 > x2) { (x1, x2) = (x2, x1); }
			if (y1 > y2) { (y1, y2) = (y2, y1); }

			for (int y = y1; y <= y2; y++)
			{
				for (int x = x1; x <= x2; x++)
				{
					ImageAccessor[x, y] = color;
				}
			}
		}


	}
}
