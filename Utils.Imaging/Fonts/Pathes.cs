using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Drawing;

namespace Utils.Fonts
{
	public class Pathes<T> : IReadOnlyList<Path>, IGraphicConverter
	{
		private readonly List<Path> pathes;
		private Path path = null;
		private Mathematics.LinearAlgebra.Matrix<double> transformation;

		public Pathes()
		{
			this.transformation = Mathematics.LinearAlgebra.Matrix<double>.Identity(3);
			this.pathes = new List<Path>();
		}

		public Pathes(Mathematics.LinearAlgebra.Matrix<double> transformation)
		{
			this.transformation = transformation;
			this.pathes = new List<Path>();
		}

		public int Count => pathes.Count;

		public Path this[int index] => pathes[index];

		public void StartAt(float x, float y)
		{
			var p = new Mathematics.LinearAlgebra.Vector<double>(x, y, 1);
			p = transformation * p;

			path = new Path(new PointF((short)p[0], (short)p[1]));
			pathes.Add(path);
		}

		public void LineTo(float x, float y)
		{
			var p = new Mathematics.LinearAlgebra.Vector<double>(x, y, 1);
			p = transformation * p;
			path.LineTo(new PointF((short)p[0], (short)p[1])); ;
		}

		public void BezierTo(params (float x, float y)[] points)
		{
			var tPoints = points
				.Select(p => transformation * new Mathematics.LinearAlgebra.Vector<double>(p.x, p.y, 1))
				.Select(p => new PointF((short)p[0], (short)p[1]));

			path.BezierTo(tPoints.ToArray());
		}

		public IEnumerator<Path> GetEnumerator() => pathes.GetEnumerator();
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
