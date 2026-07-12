using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Represents a k-dimensional affine subspace (flat) embedded in a d-dimensional ambient space.
/// </summary>
/// <remarks>
/// <para>
/// Construct via <see cref="FromSpan"/> (k spanning vectors → dimension k) or
/// <see cref="FromNormals"/> (k normal vectors → dimension d − k).
/// The internal representation is an orthonormal basis computed by Gram–Schmidt, so all
/// geometric operations are numerically stable.
/// </para>
/// <para>
/// Special cases: dimension 0 is a single point; dimension d is the full ambient space.
/// </para>
/// </remarks>
/// <typeparam name="T">Floating-point scalar type.</typeparam>
public sealed class AffineSubspace<T> : IEquatable<AffineSubspace<T>>, ICloneable, IFormattable
    where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, IRootFunctions<T>
{
    private static readonly T Epsilon = T.CreateChecked(1e-10);

    /// <summary>Orthonormal basis of the subspace direction space.</summary>
    private readonly Vector<T>[] _basis;

    /// <summary>Orthonormal normals (complement of <see cref="_basis"/>), computed lazily.</summary>
    private Vector<T>[]? _normals;

    /// <summary>Gets the anchor point that lies on the subspace.</summary>
    public Vector<T> Anchor { get; }

    /// <summary>Gets the dimension of this subspace.</summary>
    public int Dimension => _basis.Length;

    /// <summary>Gets the dimension of the ambient space.</summary>
    public int AmbientDimension => Anchor.Dimension;

    /// <summary>Gets the codimension (= AmbientDimension − Dimension).</summary>
    public int Codimension => AmbientDimension - Dimension;

    private AffineSubspace(Vector<T> anchor, Vector<T>[] orthonormalBasis)
    {
        Anchor = anchor;
        _basis = orthonormalBasis;
    }

    // -------------------------------------------------------------------------
    // Factories
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates an affine subspace of dimension <c>k</c> spanned by <c>k</c> direction vectors.
    /// Linearly dependent inputs are silently discarded; the resulting dimension equals the rank.
    /// </summary>
    /// <param name="anchor">A point on the subspace.</param>
    /// <param name="directions">Spanning directions. At least one must be non-zero.</param>
    /// <returns>The affine subspace through <paramref name="anchor"/> in the given directions.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no directions are supplied, a direction has the wrong ambient dimension,
    /// or all directions are zero / mutually collinear.
    /// </exception>
    public static AffineSubspace<T> FromSpan(Vector<T> anchor, params Vector<T>[] directions)
    {
        if (directions.Length == 0)
            throw new ArgumentException("At least one direction vector is required.", nameof(directions));

        var basis = GramSchmidt(anchor.Dimension, directions);
        if (basis.Length == 0)
            throw new ArgumentException("All direction vectors are linearly dependent (zero span).", nameof(directions));

        return new AffineSubspace<T>(anchor, basis);
    }

    /// <summary>
    /// Creates an affine subspace of dimension <c>d − k</c> defined by <c>k</c> normal vectors.
    /// The subspace is the set of points whose displacement from <paramref name="anchor"/>
    /// is orthogonal to every supplied normal.
    /// Linearly dependent normals are silently discarded.
    /// </summary>
    /// <param name="anchor">A point on the subspace.</param>
    /// <param name="normals">Normal vectors. Each independent normal reduces the dimension by one.</param>
    /// <returns>The affine subspace through <paramref name="anchor"/> orthogonal to the given normals.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no normals are supplied or a normal has the wrong ambient dimension.
    /// </exception>
    public static AffineSubspace<T> FromNormals(Vector<T> anchor, params Vector<T>[] normals)
    {
        if (normals.Length == 0)
            throw new ArgumentException("At least one normal vector is required.", nameof(normals));

        var orthonormals = GramSchmidt(anchor.Dimension, normals);
        var basis = ComputeComplement(anchor.Dimension, orthonormals);
        return new AffineSubspace<T>(anchor, basis);
    }

    // -------------------------------------------------------------------------
    // Geometric operations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the orthogonal projection of <paramref name="point"/> onto this subspace.
    /// </summary>
    /// <param name="point">Point to project. Must have the same ambient dimension.</param>
    /// <returns>The closest point on this subspace to <paramref name="point"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the point has a different ambient dimension.</exception>
    public Vector<T> Project(Vector<T> point)
    {
        ValidateDimension(point);
        var v = point - Anchor;
        var result = Anchor;
        foreach (var e in _basis)
            result = result + (v * e) * e;
        return result;
    }

    /// <summary>
    /// Computes the shortest distance from <paramref name="point"/> to this subspace.
    /// </summary>
    /// <param name="point">Point to measure from. Must have the same ambient dimension.</param>
    /// <returns>The perpendicular distance.</returns>
    /// <exception cref="ArgumentException">Thrown when the point has a different ambient dimension.</exception>
    public T DistanceTo(Vector<T> point)
    {
        ValidateDimension(point);
        return (point - Project(point)).Norm;
    }

    /// <summary>
    /// Determines whether <paramref name="point"/> lies on this subspace within the given tolerance.
    /// </summary>
    /// <param name="point">Point to test.</param>
    /// <param name="tolerance">Maximum allowable perpendicular distance. Must be finite and non-negative.</param>
    /// <returns><see langword="true"/> if the point is within <paramref name="tolerance"/> of the subspace.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="tolerance"/> is not finite or is negative: an unchecked negative
    /// tolerance would reject even an exact member, <see langword="NaN"/> would make every comparison
    /// false, and positive infinity would silently accept every finite-distance point as a member (see
    /// TODO-2026-07-11-pass4.md item #48).
    /// </exception>
    public bool Contains(Vector<T> point, T tolerance)
    {
        ValidateTolerance(tolerance, nameof(tolerance));
        return DistanceTo(point) <= tolerance;
    }

    /// <summary>
    /// Computes the intersection of this subspace with a line.
    /// </summary>
    /// <remarks>
    /// Uses the least-squares normal-equation formulation over the orthonormal normals of this
    /// subspace. The result is exact when the line is not parallel to the subspace.
    /// </remarks>
    /// <param name="line">Line to intersect with. Must share the same ambient dimension.</param>
    /// <returns>
    /// The unique intersection point, or <see langword="null"/> when the line is parallel to
    /// (or embedded in) the subspace.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the line has a different ambient dimension.</exception>
    public Vector<T>? IntersectWith(Line<T> line)
    {
        if (line.Dimension != AmbientDimension)
            throw new ArgumentException("The line must have the same ambient dimension as the subspace.", nameof(line));

        // Solve Σ_i (n̂_i·d) t = −Σ_i (n̂_i·d)(n̂_i·(p−anchor)) via normal equations.
        var normals = GetOrComputeNormals();
        T numerator = T.Zero;
        T denominator = T.Zero;
        var offset = line.Point - Anchor;
        foreach (var n in normals)
        {
            T nd = n * line.Direction;
            numerator -= nd * (n * offset);
            denominator += nd * nd;
        }

        if (T.Abs(denominator) <= Epsilon)
            return null;

        T t = numerator / denominator;
        var candidate = line.Point + t * line.Direction;

        // For codimension > 1 the normal-equation least-squares t minimises the
        // residual but may not satisfy all constraints simultaneously.  Verify
        // the candidate actually lies on the subspace before returning it.
        if (DistanceTo(candidate) > Epsilon)
            return null;

        return candidate;
    }

    /// <summary>
    /// Computes the intersection of this subspace with another affine subspace.
    /// </summary>
    /// <remarks>
    /// Combines the normal constraints of both subspaces using an augmented Gram–Schmidt that
    /// detects inconsistency (parallel subspaces with no common point).
    /// The anchor of the returned subspace is the minimum-norm solution of the combined constraint system.
    /// </remarks>
    /// <param name="other">The other affine subspace. Must share the same ambient dimension.</param>
    /// <returns>
    /// The intersection as a new <see cref="AffineSubspace{T}"/>, or <see langword="null"/>
    /// when the two subspaces are parallel and disjoint.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="other"/> has a different ambient dimension.</exception>
    public AffineSubspace<T>? IntersectWith(AffineSubspace<T> other)
    {
        if (other.AmbientDimension != AmbientDimension)
            throw new ArgumentException("Both subspaces must have the same ambient dimension.", nameof(other));

        int d = AmbientDimension;

        // Build augmented normal set [n̂ | c] via Gram–Schmidt, tracking offsets.
        // Inconsistency = a normal collapses to zero but its offset does not.
        var combinedNormals = new List<Vector<T>>();
        var combinedOffsets = new List<T>();

        // Seed with this subspace's normals.
        foreach (var n in GetOrComputeNormals())
        {
            combinedNormals.Add(n);
            combinedOffsets.Add(n * Anchor);
        }

        // Absorb other's normals with their respective anchors.
        foreach (var n in other.GetOrComputeNormals())
        {
            T[] u = new T[d];
            for (int i = 0; i < d; i++) u[i] = n[i];
            T offsetRemainder = n * other.Anchor;

            for (int j = 0; j < combinedNormals.Count; j++)
            {
                T dot = Dot(u, combinedNormals[j]);
                for (int k = 0; k < d; k++) u[k] -= dot * combinedNormals[j][k];
                offsetRemainder -= dot * combinedOffsets[j];
            }

            T norm = Norm(u);
            if (norm <= Epsilon)
            {
                // Normal already spanned — check offset consistency.
                if (T.Abs(offsetRemainder) > Epsilon)
                    return null;
                continue;
            }

            for (int k = 0; k < d; k++) u[k] /= norm;
            offsetRemainder /= norm;
            combinedNormals.Add(new Vector<T>(u));
            combinedOffsets.Add(offsetRemainder);
        }

        // Minimum-norm anchor: x = Σ c_i n̂_i  (valid because normals are orthonormal).
        T[] anchorComponents = new T[d];
        for (int i = 0; i < combinedNormals.Count; i++)
        {
            T c = combinedOffsets[i];
            var ni = combinedNormals[i];
            for (int k = 0; k < d; k++)
                anchorComponents[k] += c * ni[k];
        }

        var intersectionAnchor = new Vector<T>(anchorComponents);
        var intersectionBasis = ComputeComplement(d, [.. combinedNormals]);
        return new AffineSubspace<T>(intersectionAnchor, intersectionBasis);
    }

    /// <summary>
    /// Returns a <see cref="Line{T}"/> equivalent to this subspace.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this subspace is not one-dimensional.</exception>
    public Line<T> ToLine()
    {
        if (Dimension != 1)
            throw new InvalidOperationException(
                $"Only a one-dimensional subspace can be converted to a Line (this subspace has dimension {Dimension}).");
        return new Line<T>(Anchor, _basis[0]);
    }

    // -------------------------------------------------------------------------
    // Object overrides
    // -------------------------------------------------------------------------

    /// <summary>
    /// Determines whether <paramref name="other"/> represents the same affine subspace as this one.
    /// Two subspaces are equal when they have the same dimension and the same point set
    /// (i.e., <paramref name="other"/>'s anchor and all its direction vectors lie in this subspace).
    /// </summary>
    /// <param name="other">Subspace to compare.</param>
    /// <returns><see langword="true"/> if both subspaces are geometrically identical.</returns>
    public bool Equals(AffineSubspace<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Dimension != other.Dimension || AmbientDimension != other.AmbientDimension) return false;
        if (DistanceTo(other.Anchor) > Epsilon) return false;
        foreach (var e in other._basis)
            if (DistanceTo(Anchor + e) > Epsilon) return false;
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AffineSubspace<T> s && Equals(s);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Dimension, AmbientDimension);

    /// <inheritdoc/>
    public object Clone() => new AffineSubspace<T>(new Vector<T>(Anchor), (Vector<T>[])_basis.Clone());

    /// <inheritdoc/>
    public override string ToString() => ToString(null, null);

    /// <summary>
    /// Returns a string describing the anchor point and the subspace dimensions.
    /// </summary>
    /// <param name="format">Unused; reserved for future format specifiers.</param>
    /// <param name="formatProvider">Unused; reserved for future format providers.</param>
    /// <returns>A human-readable description of this subspace.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider)
        => $"Anchor: {Anchor}, Dimension: {Dimension}/{AmbientDimension}";

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>Returns the cached orthonormal normals, computing them on first access.</summary>
    private Vector<T>[] GetOrComputeNormals()
        => _normals ??= ComputeComplement(AmbientDimension, _basis);

    private void ValidateDimension(Vector<T> point)
    {
        if (point.Dimension != AmbientDimension)
            throw new ArgumentException(
                "The point must have the same ambient dimension as the subspace.", nameof(point));
    }

    /// <summary>
    /// Validates that a caller-supplied tolerance is usable as a comparison threshold: finite and
    /// non-negative, mirroring the same policy used elsewhere in this library (e.g.
    /// <see cref="Vector{T}.Normalize"/>, <see cref="Matrix{T}.Invert"/>) rather than accepting a raw,
    /// unchecked scalar (see TODO-2026-07-11-pass4.md item #48).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not finite or is negative.</exception>
    private static void ValidateTolerance(T tolerance, string parameterName)
    {
        if (!T.IsFinite(tolerance) || tolerance < T.Zero)
            throw new ArgumentOutOfRangeException(parameterName, tolerance, "Tolerance must be finite and non-negative.");
    }

    /// <summary>Dot product of a raw array against a <see cref="Vector{T}"/>.</summary>
    private static T Dot(T[] a, Vector<T> b)
    {
        T result = T.Zero;
        for (int i = 0; i < a.Length; i++) result += a[i] * b[i];
        return result;
    }

    /// <summary>Euclidean norm of a raw component array.</summary>
    private static T Norm(T[] v)
    {
        T sum = T.Zero;
        foreach (var x in v) sum += x * x;
        return T.Sqrt(sum);
    }

    /// <summary>
    /// Gram–Schmidt orthonormalization. Returns the maximal orthonormal subset of
    /// <paramref name="vectors"/> (linearly dependent inputs are discarded).
    /// </summary>
    /// <param name="ambientDimension">Expected dimension of each vector.</param>
    /// <param name="vectors">Input vectors to orthonormalize.</param>
    /// <returns>An array of mutually orthonormal vectors spanning the same subspace.</returns>
    /// <exception cref="ArgumentException">Thrown when a vector has the wrong ambient dimension.</exception>
    private static Vector<T>[] GramSchmidt(int ambientDimension, Vector<T>[] vectors)
    {
        var result = new List<Vector<T>>(vectors.Length);
        foreach (var v in vectors)
        {
            if (v.Dimension != ambientDimension)
                throw new ArgumentException($"All vectors must have ambient dimension {ambientDimension}.");

            T[] u = new T[ambientDimension];
            for (int i = 0; i < ambientDimension; i++) u[i] = v[i];

            foreach (var e in result)
            {
                T dot = Dot(u, e);
                for (int i = 0; i < ambientDimension; i++) u[i] -= dot * e[i];
            }

            T norm = Norm(u);
            if (norm <= Epsilon) continue;

            for (int i = 0; i < ambientDimension; i++) u[i] /= norm;
            result.Add(new Vector<T>(u));
        }
        return [.. result];
    }

    /// <summary>
    /// Computes the orthonormal complement of <paramref name="orthonormals"/> in R^d.
    /// The returned vectors span the subspace orthogonal to every vector in <paramref name="orthonormals"/>.
    /// </summary>
    /// <param name="d">Ambient space dimension.</param>
    /// <param name="orthonormals">Already-orthonormal vectors whose complement is sought.</param>
    /// <returns>An orthonormal basis of the complement subspace.</returns>
    private static Vector<T>[] ComputeComplement(int d, Vector<T>[] orthonormals)
    {
        // Project each standard basis vector out of the given orthonormal directions.
        T[][] candidates = new T[d][];
        for (int i = 0; i < d; i++)
        {
            candidates[i] = new T[d];
            candidates[i][i] = T.One;
            foreach (var n in orthonormals)
            {
                T dot = Dot(candidates[i], n);
                for (int k = 0; k < d; k++) candidates[i][k] -= dot * n[k];
            }
        }

        // Re-orthonormalize the remaining candidates among themselves.
        var result = new List<Vector<T>>(d - orthonormals.Length);
        foreach (var u in candidates)
        {
            foreach (var e in result)
            {
                T dot = Dot(u, e);
                for (int k = 0; k < d; k++) u[k] -= dot * e[k];
            }
            T norm = Norm(u);
            if (norm <= Epsilon) continue;
            for (int k = 0; k < d; k++) u[k] /= norm;
            result.Add(new Vector<T>(u));
        }
        return [.. result];
    }
}
