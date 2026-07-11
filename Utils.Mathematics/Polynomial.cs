using System.Numerics;

namespace Utils.Mathematics;

/// <summary>
/// Represents a polynomial with coefficients of type <typeparamref name="T"/>.
/// Coefficients are stored as <c>[a₀, a₁, …, aₙ]</c> representing <c>a₀ + a₁x + … + aₙxⁿ</c>.
/// </summary>
/// <typeparam name="T">Floating-point scalar type.</typeparam>
public sealed class Polynomial<T> : IEquatable<Polynomial<T>>
    where T : struct, IFloatingPoint<T>
{
    private static readonly T Epsilon = T.CreateChecked(1e-10);

    /// <summary>Coefficients in ascending power order: index i is the coefficient of xⁱ.</summary>
    private readonly T[] _coefficients;

    /// <summary>Gets the degree of the polynomial (highest power with a non-zero coefficient).</summary>
    public int Degree => _coefficients.Length - 1;

    /// <summary>Returns the coefficient of xⁱ.</summary>
    /// <param name="i">Power index.</param>
    public T this[int i] => _coefficients[i];

    /// <summary>
    /// Initializes a polynomial from its coefficients in ascending power order.
    /// Trailing exact-zero coefficients are trimmed so the stored degree is minimal.
    /// </summary>
    /// <param name="coefficients">Coefficients [a₀, a₁, …, aₙ]. At least one value is required.</param>
    /// <exception cref="ArgumentException">Thrown when no coefficients are provided.</exception>
    public Polynomial(params IEnumerable<T> coefficients)
        : this(Canonicalize(coefficients.ToArray()))
    {
    }

    /// <summary>
    /// Creates a polynomial directly from an already-canonical coefficient array (no trailing exact
    /// zero unless the polynomial is the zero polynomial), without copying or re-trimming it. Every
    /// call site owning a freshly built array must route it through <see cref="Canonicalize"/> first
    /// so canonical form - and therefore <see cref="Equals(Polynomial{T})"/>/<see cref="GetHashCode"/>
    /// consistency - holds regardless of which constructor or operator produced the instance.
    /// </summary>
    private Polynomial(T[] canonicalCoefficients) => _coefficients = canonicalCoefficients;

    /// <summary>
    /// Trims trailing exact-zero coefficients so the stored array has minimal length (degree zero
    /// for the zero polynomial). Uses exact zero rather than a tolerance: an absolute epsilon has no
    /// scale-independent meaning across arbitrary <typeparamref name="T"/> and coefficient
    /// magnitudes, and would make canonical form - and therefore equality and hashing - depend on
    /// where a caller's values happen to fall relative to a fixed constant.
    /// </summary>
    private static T[] Canonicalize(T[] coefficients)
    {
        if (coefficients.Length == 0)
            throw new ArgumentException("At least one coefficient is required.", nameof(coefficients));
        int last = coefficients.Length - 1;
        while (last > 0 && coefficients[last] == T.Zero) last--;
        return last == coefficients.Length - 1 ? coefficients : coefficients[..(last + 1)];
    }

    // -------------------------------------------------------------------------
    // Evaluation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Evaluates the polynomial at <paramref name="x"/> using Horner's method.
    /// </summary>
    /// <param name="x">The point at which to evaluate.</param>
    /// <returns>The value of the polynomial at <paramref name="x"/>.</returns>
    public T Evaluate(T x)
    {
        T result = _coefficients[Degree];
        for (int i = Degree - 1; i >= 0; i--)
            result = result * x + _coefficients[i];
        return result;
    }

    // -------------------------------------------------------------------------
    // Calculus
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the formal derivative of this polynomial.
    /// </summary>
    /// <returns>A new polynomial representing the derivative.</returns>
    public Polynomial<T> Derive()
    {
        if (Degree == 0) return new Polynomial<T>([T.Zero]);
        T[] d = new T[Degree];
        for (int i = 1; i <= Degree; i++)
            d[i - 1] = T.CreateChecked(i) * _coefficients[i];
        return new Polynomial<T>(Canonicalize(d));
    }

    /// <summary>
    /// Returns an antiderivative of this polynomial with the given integration constant.
    /// </summary>
    /// <param name="constant">Constant of integration (default: zero).</param>
    /// <returns>A new polynomial representing the antiderivative.</returns>
    public Polynomial<T> Integrate(T constant = default)
    {
        T[] result = new T[Degree + 2];
        result[0] = constant;
        for (int i = 0; i <= Degree; i++)
            result[i + 1] = _coefficients[i] / T.CreateChecked(i + 1);
        return new Polynomial<T>(Canonicalize(result));
    }

    // -------------------------------------------------------------------------
    // Root finding
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to find a real root near <paramref name="initialGuess"/> using Newton–Raphson iteration.
    /// </summary>
    /// <param name="initialGuess">Starting point for the iteration. Must be finite.</param>
    /// <param name="maxIterations">Maximum number of iterations. Must be greater than zero.</param>
    /// <param name="tolerance">Convergence tolerance; defaults to 1e-10. Must be finite and positive.</param>
    /// <returns>The found root, or <see langword="null"/> if the method did not converge.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxIterations"/> is not positive, <paramref name="initialGuess"/>
    /// is not finite, or <paramref name="tolerance"/> is not finite and positive.
    /// </exception>
    public T? FindRoot(T initialGuess, int maxIterations = 100, T? tolerance = null)
    {
        if (maxIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxIterations), maxIterations, "Must be greater than zero.");
        if (!T.IsFinite(initialGuess))
            throw new ArgumentOutOfRangeException(nameof(initialGuess), initialGuess, "Must be a finite value.");

        T tol = tolerance ?? Epsilon;
        if (!T.IsFinite(tol) || tol <= T.Zero)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Must be a finite, positive value.");

        var derivative = Derive();
        T x = initialGuess;
        for (int i = 0; i < maxIterations; i++)
        {
            T fx = Evaluate(x);
            if (T.Abs(fx) <= tol) return x;
            // A derivative merely close to zero (not just exactly zero) already makes the Newton
            // step numerically unstable; reuse the convergence tolerance as that threshold rather
            // than comparing to exact zero.
            T fpx = derivative.Evaluate(x);
            if (T.Abs(fpx) <= tol) return null;
            T next = x - fx / fpx;
            if (!T.IsFinite(next)) return null;
            if (T.Abs(next - x) <= tol) return next;
            x = next;
        }
        return null;
    }

    // -------------------------------------------------------------------------
    // Operators
    // -------------------------------------------------------------------------

    /// <summary>Returns the sum of two polynomials.</summary>
    public static Polynomial<T> operator +(Polynomial<T> a, Polynomial<T> b)
    {
        int len = Math.Max(a._coefficients.Length, b._coefficients.Length);
        T[] result = new T[len];
        for (int i = 0; i < len; i++)
        {
            T ca = i < a._coefficients.Length ? a._coefficients[i] : T.Zero;
            T cb = i < b._coefficients.Length ? b._coefficients[i] : T.Zero;
            result[i] = ca + cb;
        }
        return new Polynomial<T>(Canonicalize(result));
    }

    /// <summary>Returns the difference of two polynomials.</summary>
    public static Polynomial<T> operator -(Polynomial<T> a, Polynomial<T> b)
    {
        int len = Math.Max(a._coefficients.Length, b._coefficients.Length);
        T[] result = new T[len];
        for (int i = 0; i < len; i++)
        {
            T ca = i < a._coefficients.Length ? a._coefficients[i] : T.Zero;
            T cb = i < b._coefficients.Length ? b._coefficients[i] : T.Zero;
            result[i] = ca - cb;
        }
        return new Polynomial<T>(Canonicalize(result));
    }

    /// <summary>Returns the product of two polynomials.</summary>
    public static Polynomial<T> operator *(Polynomial<T> a, Polynomial<T> b)
    {
        T[] result = new T[a._coefficients.Length + b._coefficients.Length - 1];
        for (int i = 0; i < a._coefficients.Length; i++)
            for (int j = 0; j < b._coefficients.Length; j++)
                result[i + j] += a._coefficients[i] * b._coefficients[j];
        return new Polynomial<T>(Canonicalize(result));
    }

    /// <summary>Returns the polynomial scaled by a scalar.</summary>
    public static Polynomial<T> operator *(T scalar, Polynomial<T> p)
    {
        T[] result = new T[p._coefficients.Length];
        for (int i = 0; i < result.Length; i++) result[i] = scalar * p._coefficients[i];
        return new Polynomial<T>(Canonicalize(result));
    }

    /// <summary>Returns the polynomial scaled by a scalar.</summary>
    public static Polynomial<T> operator *(Polynomial<T> p, T scalar) => scalar * p;

    // -------------------------------------------------------------------------
    // Equality
    // -------------------------------------------------------------------------

    /// <summary>
    /// Determines whether this polynomial is exactly equal to <paramref name="other"/>: same degree
    /// and identical coefficients. Consistent with <see cref="GetHashCode"/> by construction, unlike
    /// a tolerance-based comparison would be. Use <see cref="ApproximatelyEquals"/> for a
    /// tolerance-aware comparison; it must not be used to implement this member or
    /// <see cref="GetHashCode"/>; doing so would let two polynomials compare equal while hashing
    /// differently, breaking the contract required by <see cref="Dictionary{TKey, TValue}"/>,
    /// <see cref="HashSet{T}"/>, and LINQ set operations.
    /// </summary>
    public bool Equals(Polynomial<T>? other)
    {
        if (other is null) return false;
        if (Degree != other.Degree) return false;
        for (int i = 0; i <= Degree; i++)
            if (_coefficients[i] != other._coefficients[i]) return false;
        return true;
    }

    /// <summary>
    /// Determines whether this polynomial is equal to <paramref name="other"/> within
    /// <paramref name="tolerance"/> on every coefficient. This is a separate, explicitly opt-in
    /// comparison: it is not used by <see cref="Equals(Polynomial{T})"/> or
    /// <see cref="GetHashCode"/> and must not be relied upon for hashed-collection membership, since
    /// approximate equality is not transitive in general and would violate the hash contract.
    /// </summary>
    /// <param name="other">The polynomial to compare with.</param>
    /// <param name="tolerance">Maximum allowed absolute difference per coefficient.</param>
    public bool ApproximatelyEquals(Polynomial<T>? other, T tolerance)
    {
        if (other is null) return false;
        if (Degree != other.Degree) return false;
        for (int i = 0; i <= Degree; i++)
            if (T.Abs(_coefficients[i] - other._coefficients[i]) > tolerance) return false;
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Polynomial<T> p && Equals(p);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Degree);
        foreach (T coefficient in _coefficients)
            hash.Add(coefficient);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (Degree == 0) return _coefficients[0].ToString() ?? "0";
        var parts = new List<string>();
        for (int i = Degree; i >= 0; i--)
        {
            if (T.Abs(_coefficients[i]) <= Epsilon) continue;
            string coef = _coefficients[i].ToString() ?? "0";
            parts.Add(i == 0 ? coef : i == 1 ? $"{coef}x" : $"{coef}x^{i}");
        }
        return parts.Count == 0 ? "0" : string.Join(" + ", parts);
    }
}
