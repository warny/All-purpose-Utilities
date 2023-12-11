using System;
using System.Numerics;

namespace Utils.Mathematics
{
	public interface IAngleCalculator<T>
		where T : struct, IFloatingPointIeee754<T>
	{
        T Graduation { get; }
        T StraightAngle { get; }
        T Perigon { get; }
		T RightAngle { get; }

        T Sin(T angle);
		T Cos(T angle);
		T Tan(T angle);
		T Cot(T angle);
		T Asin(T value);
		T Acos(T value);
		T Atan(T value);
		T Acot(T value);
		T Tanh(T angle);
		T Sinh(T angle);
		T Cosh(T angle);
		T Atan2(T x, T y);
		T Acot2(T x, T y);
		T NormalizeMinToMax(T angle);
		T Normalize0To2Max(T angle);

		T FromRadian(T angle);
		T ToRadian(T angle);
	}

	public class Trigonometry<T> : IAngleCalculator<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        public T Graduation { get; }
		public T StraightAngle { get; }
		public T Perigon { get; }
		public T RightAngle { get; }

		public Trigonometry(T numberOfGraduation)
		{
			this.StraightAngle = numberOfGraduation / (T.One + T.One);
			this.Perigon = numberOfGraduation;
			this.RightAngle = StraightAngle / (T.One + T.One);
            this.Graduation = T.Pi / StraightAngle;
		}

		public virtual T Acos(T value) => MathEx.Round(T.Acos(value) / Graduation, -10);
		public virtual T Asin(T value) => MathEx.Round(T.Asin(value) / Graduation, -10);
		public virtual T Atan(T value) => MathEx.Round(T.Atan(value) / Graduation, -10);
		public virtual T Acot(T value) => T.Atan2(T.One, value);
		public virtual T Cos(T angle) => T.Cos(angle * Graduation);
		public virtual T Sin(T angle) => T.Sin(angle * Graduation);
		public virtual T Tan(T angle) => T.Tan(angle * Graduation);
		public virtual T Cot(T angle) => T.One / T.Tan(angle * Graduation);
		public virtual T Tanh(T angle) => T.Tanh(angle * Graduation);
		public virtual T Sinh(T angle) => T.Sinh(angle * Graduation);
		public virtual T Cosh(T angle) => T.Cosh(angle * Graduation);
		public virtual T Atan2(T x, T y) => MathEx.Round(T.Atan2(x, y) / Graduation, -10);
		public virtual T Acot2(T x, T y) => MathEx.Round(T.Atan2(y, x) / Graduation, -10);

		public virtual T NormalizeMinToMax(T angle) => MathEx.Mod(angle + StraightAngle + Perigon, Perigon) - StraightAngle;
		public virtual T Normalize0To2Max(T angle) => MathEx.Mod(angle, Perigon);

        public T FromRadian(T angle) => angle / Graduation;

		public T ToRadian(T angle) => angle * Graduation; 

        public static IAngleCalculator<T> Radian => new Radian<T>();
		public static IAngleCalculator<T> Degree => new Degree<T>();
		public static IAngleCalculator<T> Grade => new Grade<T>();
	}

	public sealed class Degree<T> : Trigonometry<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        public Degree() : base((T)Convert.ChangeType(360, typeof(T))) { }
	}

	public sealed class Grade<T> : Trigonometry<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        public Grade() : base((T)Convert.ChangeType(400, typeof(T))) { }
	}

	public sealed class Radian<T> : Trigonometry<T>
        where T : struct, IFloatingPointIeee754<T>
    {
        public Radian() : base(T.Pi) { }

		public override T Acos(T value) => T.Acos(value);
		public override T Asin(T value) => T.Asin(value);
		public override T Atan(T value) => T.Atan(value);
		public override T Acot(T value) => T.Atan2(T.One, value);
		public override T Cos(T angle) => T.Cos(angle);
		public override T Sin(T angle) => T.Sin(angle);
		public override T Tan(T angle) => T.Tan(angle);
		public override T Cot(T angle) => T.One / T.Tan(angle);
		public override T Tanh(T angle) => T.Tanh(angle);
		public override T Sinh(T angle) => T.Sinh(angle);
		public override T Cosh(T angle) => T.Cosh(angle);
		public override T Atan2(T x, T y) => T.Atan2(x, y);
		public override T Acot2(T x, T y) => T.Atan2(y, x);

		public override T NormalizeMinToMax(T angle) => MathEx.Mod(angle + T.Pi + T.Pi + T.Pi, T.Pi + T.Pi) - T.Pi;
		public override T Normalize0To2Max(T angle) => MathEx.Mod(angle, (T.One + T.One) * T.Pi);
	}
}
