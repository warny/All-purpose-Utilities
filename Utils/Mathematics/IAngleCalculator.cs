using System;

namespace Utils.Mathematics
{
	public interface IAngleCalculator
	{
		double Sin(double angle);
		double Cos(double angle);
		double Tan(double angle);
		double Cot(double angle);
		double Asin(double value);
		double Acos(double value);
		double Atan(double value);
		double Acot(double value);
		double Tanh(double angle);
		double Sinh(double angle);
		double Cosh(double angle);
		double Atan2(double x, double y);
		double Acot2(double x, double y);
		double NormalizeMinToMax(double angle);
		double Normalize0To2Max(double angle);
	}

	public class Trigonometry : IAngleCalculator
	{
		public double Graduation { get; }
		public double StraightAngle { get; }
		public double Perigon { get; }

		public Trigonometry(double numberOfGraduation)
		{
			this.StraightAngle = numberOfGraduation / 2;
			this.Perigon = numberOfGraduation;
			this.Graduation = Math.PI / StraightAngle;
		}

		public virtual double Acos(double value) => MathEx.Round(Math.Acos(value) / Graduation, -10);
		public virtual double Asin(double value) => MathEx.Round(Math.Asin(value) / Graduation, -10);
		public virtual double Atan(double value) => MathEx.Round(Math.Atan(value) / Graduation, -10);
		public virtual double Acot(double value) => Math.Atan2(1, value);
		public virtual double Cos(double angle) => Math.Cos(angle * Graduation);
		public virtual double Sin(double angle) => Math.Sin(angle * Graduation);
		public virtual double Tan(double angle) => Math.Tan(angle * Graduation);
		public virtual double Cot(double angle) => 1 / Math.Tan(angle * Graduation);
		public virtual double Tanh(double angle) => Math.Tanh(angle * Graduation);
		public virtual double Sinh(double angle) => Math.Sinh(angle * Graduation);
		public virtual double Cosh(double angle) => Math.Cosh(angle * Graduation);
		public virtual double Atan2(double x, double y) => MathEx.Round(Math.Atan2(x, y) / Graduation, -10);
		public virtual double Acot2(double x, double y) => MathEx.Round(Math.Atan2(y, x) / Graduation, -10);

		public virtual double NormalizeMinToMax(double angle) => MathEx.Mod(angle + StraightAngle + Perigon, Perigon) - StraightAngle;
		public virtual double Normalize0To2Max(double angle) => MathEx.Mod(angle, Perigon);

		public static IAngleCalculator Radian => new Radian();
		public static IAngleCalculator Degree => new Degree();
		public static IAngleCalculator Grade => new Grade();
	}

	public sealed class Degree : Trigonometry
	{
		public Degree() : base(360) { }
	}

	public sealed class Grade : Trigonometry
	{
		public Grade() : base(400) { }
	}

	public sealed class Radian : Trigonometry
	{
		public Radian() : base(Math.PI) { }

		public override double Acos(double value) => Math.Acos(value);
		public override double Asin(double value) => Math.Asin(value);
		public override double Atan(double value) => Math.Atan(value);
		public override double Acot(double value) => Math.Atan2(1, value);
		public override double Cos(double angle) => Math.Cos(angle);
		public override double Sin(double angle) => Math.Sin(angle);
		public override double Tan(double angle) => Math.Tan(angle);
		public override double Cot(double angle) => 1 / Math.Tan(angle);
		public override double Tanh(double angle) => Math.Tanh(angle);
		public override double Sinh(double angle) => Math.Sinh(angle);
		public override double Cosh(double angle) => Math.Cosh(angle);
		public override double Atan2(double x, double y) => Math.Atan2(x, y);
		public override double Acot2(double x, double y) => Math.Atan2(y, x);

		public override double NormalizeMinToMax(double angle) => MathEx.Mod(angle + 3 * Math.PI, 2 * Math.PI) - Math.PI;
		public override double Normalize0To2Max(double angle) => MathEx.Mod(angle, 2 * Math.PI);
	}
}
