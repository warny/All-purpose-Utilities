using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Utils.Drawing;
using Utils.Mathematics.LinearAlgebra;

namespace Utils.Fonts;

public class Paths<T> : IReadOnlyList<Path>, IGraphicConverter
{
	private readonly List<Path> paths;
	private Path path = null;
	private Matrix<double> transformation;

	public Paths()
	{
		this.transformation = MatrixTransformations.Identity<double>(3);
		this.paths = new List<Path>();
	}

	public Paths(Matrix<double> transformation)
	{
		this.transformation = transformation;
		this.paths = [];
	}

	public int Count => paths.Count;

	public Path this[int index] => paths[index];

	public void StartAt(float x, float y)
	{
		var p = new Vector<double>(x, y, 1);
		p = transformation * p;

		path = new Path(new PointF((short)p[0], (short)p[1]));
		paths.Add(path);
	}

	public void LineTo(float x, float y)
	{
		var p = new Vector<double>(x, y, 1);
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

	public IEnumerator<Path> GetEnumerator() => paths.GetEnumerator();
	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
