using System.Numerics;

namespace Utils.Mathematics;

/// <summary>
/// Provides an interface for angle-based calculations in various units (Radians, Degrees, Grades).
/// Includes standard and extended trigonometric, hyperbolic, and inverse functions.
/// </summary>
/// <typeparam name="T">A floating-point numeric type (e.g., float, double) implementing <see cref="IFloatingPointIeee754{T}"/>.</typeparam>
public interface IAngleCalculator<T>
	where T : struct, IFloatingPointIeee754<T>
{
	/// <summary>
	/// Represents the full rotation (perigon) in this angle system. 
	/// For example, 360 in degrees, 2π in radians, 400 in grads.
	/// </summary>
	T Perigon { get; }

	/// <summary>
	/// Represents a straight angle (half of <see cref="Perigon"/>). 
	/// For example, 180 in degrees, π in radians, 200 in grads.
	/// </summary>
	T StraightAngle { get; }

	/// <summary>
	/// Represents a right angle (one-quarter of <see cref="Perigon"/>). 
	/// For example, 90 in degrees, π/2 in radians, 100 in grads.
	/// </summary>
	T RightAngle { get; }

	/// <summary>
	/// Represents the conversion factor to radians. For non-radian systems, 
	/// use this to convert angles to radians and call standard .NET trig methods.
	/// </summary>
	T Graduation { get; }

	#region Basic Trigonometric
	T Sin(T angle);
	T Cos(T angle);
	T Tan(T angle);
	T Cot(T angle);
	T Sec(T angle);
	T Csc(T angle);
	#endregion

	#region Inverse Trigonometric
	T Asin(T value);
	T Acos(T value);
	T Atan(T value);
	T Acot(T value);
	T Asec(T value);
	T Acsc(T value);
	#endregion

	#region Hyperbolic
	T Sinh(T angle);
	T Cosh(T angle);
	T Tanh(T angle);
	T Csch(T angle);
	T Sech(T angle);
	T Coth(T angle);
	#endregion

	#region Inverse Hyperbolic
	T Asinh(T value);
	T Acosh(T value);
	T Atanh(T value);
	T Acoth(T value);
	T Asech(T value);
	T Acsch(T value);
	#endregion

	#region Angle Arithmetic and Normalization
	/// <summary>
	/// Computes the four-quadrant inverse tangent of x/y, returning an angle in the range 
	/// [-<see cref="StraightAngle"/>, <see cref="StraightAngle"/>].
	/// This is analogous to <c>atan2(x, y)</c> in standard math libraries.
	/// </summary>
	T Atan2(T x, T y);

	/// <summary>
	/// Analogous to <see cref="Atan2(T,T)"/> but for the cotangent. 
	/// Internally computed as <c>atan2(y, x)</c>.
	/// </summary>
	T Acot2(T x, T y);

	/// <summary>
	/// Normalizes an angle into the range [ -StraightAngle, +StraightAngle ), 
	/// i.e., -180..180 for degrees, -π..π for radians, -200..200 for grads.
	/// </summary>
	T NormalizeMinToMax(T angle);

	/// <summary>
	/// Normalizes an angle into the range [0, <see cref="Perigon"/>), 
	/// i.e., 0..360 for degrees, 0..2π for radians, 0..400 for grads.
	/// </summary>
	T Normalize0To2Max(T angle);

	/// <summary>
	/// Adds two angles in this system, wrapping around if the sum exceeds <see cref="Perigon"/>.
	/// </summary>
	T AddAngles(T angle1, T angle2);

	/// <summary>
	/// Subtracts angle2 from angle1 in this system, wrapping around if the result is negative or above <see cref="Perigon"/>.
	/// </summary>
	T SubtractAngles(T angle1, T angle2);
	#endregion

	#region Radian Conversions
	/// <summary>
	/// Converts an angle from radians into the current measurement system.
	/// </summary>
	T FromRadian(T angle);

	/// <summary>
	/// Converts an angle to radians from the current measurement system.
	/// </summary>
	T ToRadian(T angle);
	#endregion
}

/// <summary>
/// Provides a base implementation of <see cref="IAngleCalculator{T}"/> for custom angle measures
/// such as Degrees or Grades, along with default trigonometric/hyperbolic implementations.
/// </summary>
/// <typeparam name="T">A floating-point numeric type (e.g., float, double) implementing <see cref="IFloatingPointIeee754{T}"/>.</typeparam>
public class Trigonometry<T> : IAngleCalculator<T>
	where T : struct, IFloatingPointIeee754<T>
{
	#region Properties

	/// <inheritdoc />
	public T Perigon { get; }

	/// <inheritdoc />
	public T StraightAngle { get; }

	/// <inheritdoc />
	public T RightAngle { get; }

	/// <inheritdoc />
	public T Graduation { get; }

	#endregion

	#region Constructors

	/// <summary>
	/// Creates a new <see cref="Trigonometry{T}"/> with the specified number of "units" 
	/// representing a full revolution (perigon).
	/// </summary>
	/// <param name="numberOfGraduation">
	/// Value denoting the full rotation in the chosen measurement system 
	/// (e.g., 360 for degrees, 400 for grads, π for radians).
	/// </param>
	protected Trigonometry(T numberOfGraduation)
	{
		// Full rotation
		Perigon = numberOfGraduation;

		// Straight angle is half a rotation
		StraightAngle = numberOfGraduation / (T.One + T.One);

		// Right angle is one quarter of a rotation
		RightAngle = StraightAngle / (T.One + T.One);

		// Graduation is the factor to convert from "this system's angle" to radians:
		//   rad = angle * Graduation
		// So if 'Perigon' = 360, then Graduation = π/180, etc.
		Graduation = T.Pi / StraightAngle;
	}

	#endregion

	#region Basic Trigonometric

	/// <inheritdoc />
	public virtual T Sin(T angle) => T.Sin(angle * Graduation);

	/// <inheritdoc />
	public virtual T Cos(T angle) => T.Cos(angle * Graduation);

	/// <inheritdoc />
	public virtual T Tan(T angle) => T.Tan(angle * Graduation);

	/// <inheritdoc />
	public virtual T Cot(T angle) => T.One / T.Tan(angle * Graduation);

	/// <inheritdoc />
	public virtual T Sec(T angle) => T.One / Cos(angle);

	/// <inheritdoc />
	public virtual T Csc(T angle) => T.One / Sin(angle);

	#endregion

	#region Inverse Trigonometric

	/// <inheritdoc />
	public virtual T Asin(T value)
		=> MathEx.Round(T.Asin(value) / Graduation, -10);

	/// <inheritdoc />
	public virtual T Acos(T value)
		=> MathEx.Round(T.Acos(value) / Graduation, -10);

	/// <inheritdoc />
	public virtual T Atan(T value)
		=> MathEx.Round(T.Atan(value) / Graduation, -10);

	/// <inheritdoc />
	public virtual T Acot(T value)
		=> Atan(T.One / value);

	/// <inheritdoc />
	public virtual T Asec(T value)
		=> Acos(T.One / value);

	/// <inheritdoc />
	public virtual T Acsc(T value)
		=> Asin(T.One / value);

	#endregion

	#region Hyperbolic

	/// <inheritdoc />
	public virtual T Sinh(T angle) => T.Sinh(angle * Graduation);

	/// <inheritdoc />
	public virtual T Cosh(T angle) => T.Cosh(angle * Graduation);

	/// <inheritdoc />
	public virtual T Tanh(T angle) => T.Tanh(angle * Graduation);

	/// <inheritdoc />
	public virtual T Csch(T angle) => T.One / T.Sinh(angle * Graduation);

	/// <inheritdoc />
	public virtual T Sech(T angle) => T.One / T.Cosh(angle * Graduation);

	/// <inheritdoc />
	public virtual T Coth(T angle) => T.One / T.Tanh(angle * Graduation);

	#endregion

	#region Inverse Hyperbolic

	/// <inheritdoc />
	public virtual T Asinh(T value)
		=> MathEx.Round(T.Asinh(value) / Graduation, -10);

	/// <inheritdoc />
	public virtual T Acosh(T value)
		=> MathEx.Round(T.Acosh(value) / Graduation, -10);

	/// <inheritdoc />
	public virtual T Atanh(T value)
		=> MathEx.Round(T.Atanh(value) / Graduation, -10);

	/// <inheritdoc />
	public virtual T Acoth(T value)
		=> Atanh(T.One / value);

	/// <inheritdoc />
	public virtual T Asech(T value)
		=> Acosh(T.One / value);

	/// <inheritdoc />
	public virtual T Acsch(T value)
		=> Asinh(T.One / value);

	#endregion

	#region Angle Arithmetic and Normalization

	/// <inheritdoc />
	public virtual T Atan2(T x, T y)
		=> MathEx.Round(T.Atan2(x, y) / Graduation, -10);

	/// <inheritdoc />
	public virtual T Acot2(T x, T y)
		=> MathEx.Round(T.Atan2(y, x) / Graduation, -10);

	/// <inheritdoc />
	public virtual T NormalizeMinToMax(T angle)
		=> MathEx.Mod(angle + StraightAngle + Perigon, Perigon) - StraightAngle;

	/// <inheritdoc />
	public virtual T Normalize0To2Max(T angle)
		=> MathEx.Mod(angle, Perigon);

	/// <inheritdoc />
	public virtual T AddAngles(T angle1, T angle2)
		=> Normalize0To2Max(angle1 + angle2);

	/// <inheritdoc />
	public virtual T SubtractAngles(T angle1, T angle2)
		=> Normalize0To2Max(angle1 - angle2);

	#endregion

	#region Radian Conversions

	/// <inheritdoc />
	public virtual T FromRadian(T angle) => angle / Graduation;

	/// <inheritdoc />
	public virtual T ToRadian(T angle) => angle * Graduation;

	#endregion

	#region Built-in Angle Systems

	/// <summary>
	/// A built-in calculator for Radians (Perigon = 2π).
	/// </summary>
	public static IAngleCalculator<T> Radian => new RadianCalculator();

	/// <summary>
	/// A built-in calculator for Degrees (Perigon = 360).
	/// </summary>
	public static IAngleCalculator<T> Degree { get; } = new Trigonometry<T>((T)Convert.ChangeType(360, typeof(T)));

	/// <summary>
	/// A built-in calculator for Grads (Perigon = 400).
	/// </summary>
	public static IAngleCalculator<T> Grade { get; } = new Trigonometry<T>((T)Convert.ChangeType(400, typeof(T)));

	private readonly static Dictionary<T, IAngleCalculator<T>> _angleCalculators = new() {
		{  Radian.Perigon, Radian },
		{  Degree.Perigon, Degree },
		{  Grade.Perigon, Grade }
	};

	/// <summary>
	/// Retrieves a cached calculator matching the specified perigon value or creates one on demand.
	/// </summary>
	/// <param name="perigon">The angle span representing a full rotation in the desired system.</param>
	/// <returns>An <see cref="IAngleCalculator{T}"/> configured for the requested perigon.</returns>
	public static IAngleCalculator<T> Get(T perigon)
			=> _angleCalculators.GetOrAdd(perigon, () => new Trigonometry<T>(perigon));

	#endregion

	/// <summary>
	/// Represents an angle calculator operating natively in radians.
	/// </summary>
	/// <typeparam name="T">A floating-point numeric type (e.g., float, double) implementing <see cref="IFloatingPointIeee754{T}"/>.</typeparam>
	private sealed class RadianCalculator : Trigonometry<T>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="RadianCalculator"/> class with a 2π perigon.
		/// </summary>
		internal RadianCalculator()
				: base((T.Pi * (T.One + T.One))) // 2π
		{
			// For a fully 'native' radian approach, Perigon = 2π
			// so that StraightAngle = π and RightAngle = π/2, etc.
		}

		#region Override Basic Trig (No Rounding Needed)

		/// <inheritdoc />
		public override T Sin(T angle) => T.Sin(angle);

		/// <inheritdoc />
		public override T Cos(T angle) => T.Cos(angle);

		/// <inheritdoc />
		public override T Tan(T angle) => T.Tan(angle);

		/// <inheritdoc />
		public override T Cot(T angle) => T.One / T.Tan(angle);

		/// <inheritdoc />
		public override T Sec(T angle) => T.One / T.Cos(angle);

		/// <inheritdoc />
		public override T Csc(T angle) => T.One / T.Sin(angle);

		#endregion

		#region Override Inverse Trig

		/// <inheritdoc />
		public override T Asin(T value) => T.Asin(value);

		/// <inheritdoc />
		public override T Acos(T value) => T.Acos(value);

		/// <inheritdoc />
		public override T Atan(T value) => T.Atan(value);

		/// <inheritdoc />
		public override T Acot(T value) => T.Atan2(T.One, value);

		/// <inheritdoc />
		public override T Asec(T value) => T.Acos(T.One / value);

		/// <inheritdoc />
		public override T Acsc(T value) => T.Asin(T.One / value);

		#endregion

		#region Override Hyperbolic

		/// <inheritdoc />
		public override T Sinh(T angle) => T.Sinh(angle);

		/// <inheritdoc />
		public override T Cosh(T angle) => T.Cosh(angle);

		/// <inheritdoc />
		public override T Tanh(T angle) => T.Tanh(angle);

		/// <inheritdoc />
		public override T Csch(T angle) => T.One / T.Sinh(angle);

		/// <inheritdoc />
		public override T Sech(T angle) => T.One / T.Cosh(angle);

		/// <inheritdoc />
		public override T Coth(T angle) => T.One / T.Tanh(angle);

		#endregion

		#region Override Inverse Hyperbolic

		/// <inheritdoc />
		public override T Asinh(T value) => T.Asinh(value);

		/// <inheritdoc />
		public override T Acosh(T value) => T.Acosh(value);

		/// <inheritdoc />
		public override T Atanh(T value) => T.Atanh(value);

		/// <inheritdoc />
		public override T Acoth(T value) => T.Atanh(T.One / value);

		/// <inheritdoc />
		public override T Asech(T value) => T.Acosh(T.One / value);

		/// <inheritdoc />
		public override T Acsch(T value) => T.Asinh(T.One / value);

		#endregion

		#region Override Angle Arithmetic

		/// <inheritdoc />
		public override T Atan2(T x, T y) => T.Atan2(x, y);

		/// <inheritdoc />
		public override T Acot2(T x, T y) => T.Atan2(y, x);

		/// <summary>
		/// Normalizes an angle into the range [-π, +π).
		/// </summary>
		public override T NormalizeMinToMax(T angle)
		{
			// Equivalent to angle mod 2π in [-π, π).
			return MathEx.Mod(angle + T.Pi + T.Pi, (T.Pi + T.Pi)) - T.Pi;
		}

		/// <summary>
		/// Normalizes an angle into the range [0, 2π).
		/// </summary>
		public override T Normalize0To2Max(T angle)
		{
			return MathEx.Mod(angle, (T.Pi + T.Pi));
		}

		#endregion

		#region Override Radian Conversions (No conversion needed natively)

		/// <inheritdoc />
		public override T FromRadian(T angle) => angle;

		/// <inheritdoc />
		public override T ToRadian(T angle) => angle;

		#endregion
	}
}
