using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Utils.Drawing;
using Path = Utils.Drawing.Path;
using Utils.Mathematics.LinearAlgebra;

namespace Utils.Fonts;

/// <summary>
/// Collects drawing paths generated while converting glyph outlines.
/// </summary>
/// <typeparam name="T">Unused generic parameter kept for backward compatibility.</typeparam>
public class Paths<T> : IReadOnlyList<Path>, IGraphicConverter
{
    private readonly List<Path> paths;
    private Path path = null;
    private readonly Matrix<double> transformation;
    private Matrix<double> currentTransformation;

    /// <summary>
    /// Initializes a new <see cref="Paths{T}"/> with an identity transformation.
    /// </summary>
    public Paths()
    {
        this.transformation = MatrixTransformations.Identity<double>(3);
        this.currentTransformation = this.transformation;
        this.paths = new List<Path>();
    }

    /// <summary>
    /// Initializes a new <see cref="Paths{T}"/> with the provided transformation matrix.
    /// </summary>
    /// <param name="transformation">Homogeneous transformation applied to incoming coordinates.</param>
    public Paths(Matrix<double> transformation)
    {
        this.transformation = transformation;
        this.currentTransformation = transformation;
        this.paths = [];
    }

    /// <summary>
    /// Gets the number of generated paths.
    /// </summary>
    public int Count => paths.Count;

    /// <summary>
    /// Gets the path at the specified index.
    /// </summary>
    /// <param name="index">Index of the desired path.</param>
    public Path this[int index] => paths[index];

    /// <inheritdoc/>
    public void StartAt(float x, float y)
    {
        var p = new Vector<double>(x, y, 1);
        p = currentTransformation * p;

        path = new Path(new PointF((short)p[0], (short)p[1]));
        paths.Add(path);
    }

    /// <inheritdoc/>
    public void LineTo(float x, float y)
    {
        var p = new Vector<double>(x, y, 1);
        p = currentTransformation * p;
        path.LineTo(new PointF((short)p[0], (short)p[1]));
    }

    /// <inheritdoc/>
    public void BezierTo(params (float x, float y)[] points)
    {
        var tPoints = points
                .Select(p => currentTransformation * new Vector<double>(p.x, p.y, 1))
                .Select(p => new PointF((short)p[0], (short)p[1]));

        path.BezierTo(tPoints.ToArray());
    }

    /// <inheritdoc/>
    public void ClosePath() => path?.Close();

    /// <inheritdoc/>
    public void BeginDrawGlyph(float x, float y, System.Numerics.Matrix3x2 transform)
    {
        var glyphMatrix = new Matrix<double>(new double[,] {
            { transform.M11, transform.M21, transform.M31 + x },
            { transform.M12, transform.M22, transform.M32 + y },
            {             0,             0,                 1  }
        });
        currentTransformation = transformation * glyphMatrix;
    }

    /// <inheritdoc/>
    public void EndDrawGlyph() => currentTransformation = transformation;

    /// <inheritdoc/>
    public IEnumerator<Path> GetEnumerator() => paths.GetEnumerator();

    /// <inheritdoc/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
