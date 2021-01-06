using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Lists;

namespace Utils.Drawing
{
	public class Circle : IDrawable
	{
		public float Length
		{
			get
			{
				ComputeLines();
				return polylines.Length;
			}
		}

		public Point Center { get; }
		public double Orientation { get; }
		public int Radius1 { get; }
		public int Radius2 { get; }
		public double StartAngle { get; }
		public double EndAngle { get; }

		private Polylines polylines;

		public Circle(Point center, int radius, double startAngle = 0, double endAngle = Math.PI * 2)
			: this(center, radius, radius, 0, startAngle, endAngle)
		{
		}
		
		public Circle(Point center, int radius1, int radius2, double orientation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
		{
			Center = center;
			Radius1 = radius1;
			Radius2 = radius2;
			Orientation = orientation;

			double angle = endAngle - startAngle;
			if (Math.Abs(angle) > Math.PI * 2)
			{
				startAngle = 0;
				endAngle = Math.PI * 2;
			}
			StartAngle = startAngle;
			EndAngle = endAngle;
		}

		private void ComputeLines()
		{
			IEnumerable<Point> ComputePoints()
			{
				double angle = EndAngle - StartAngle;
				var angularResolution = angle / (Math.Max(Radius1, Radius2) * Math.PI * 2);

				var cosR = Math.Cos(Orientation);
				var sinR = Math.Sin(Orientation);

				Func<double, bool> test;
				if (StartAngle > EndAngle)
				{
					test = alpha => alpha >= EndAngle;
				}
				else
				{
					test = alpha => alpha <= EndAngle;
				}

				double delta1 = Math.Sin(StartAngle) * Radius1;
				double delta2 = Math.Cos(StartAngle) * Radius2;

				int deltaX = (int)(cosR * delta1 + sinR * delta2);
				int deltaY = (int)(sinR * delta1 + cosR * delta2);

				Point lastPoint = new Point(Center.X + deltaX, Center.Y + deltaY);
				yield return lastPoint;

				for (double a = StartAngle + angularResolution; test(a); a += angularResolution)
				{
					delta1 = Math.Sin(a) * Radius1;
					delta2 = Math.Cos(a) * Radius2;

					deltaX = (int)(cosR * delta1 + sinR * delta2);
					deltaY = (int)(sinR * delta1 + cosR * delta2);

					var newPoint = new Point(Center.X + deltaX, Center.Y + deltaY);
					if (newPoint.X == lastPoint.X && newPoint.Y == lastPoint.Y) continue;
					lastPoint = newPoint;
					yield return newPoint;
				}
			}

			polylines ??= new Polylines(ComputePoints().ToArray());
		}

		public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
		{
			ComputeLines();
			return polylines.GetPoints(closed, position);
		}
	}
}
