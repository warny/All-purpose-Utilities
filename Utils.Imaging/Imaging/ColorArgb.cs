using System;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Imaging;

public struct ColorArgb : IColorArgb<double>
{

	public static double MinValue { get; } = 0.0;
	public static double MaxValue { get; } = 1.0;

	private double alpha;
	private double red;
	private double green;
	private double blue;

	public double Alpha
	{
		get => alpha;

		set
		{
			value.ArgMustBeBetween(MinValue, MaxValue);
			this.alpha=value;
		}
	}

	public double Red
	{
		get => red;

		set
		{
			value.ArgMustBeBetween(MinValue, MaxValue);
			this.red=value;
		}
	}

	public double Green
	{
		get => green;

		set
		{
			value.ArgMustBeBetween(MinValue, MaxValue);
			this.green=value;
		}
	}

	public double Blue
	{
		get => blue;

		set
		{
			value.ArgMustBeBetween(MinValue, MaxValue);
			this.blue=value;
		}
	}

	public ColorArgb(double red, double green, double blue) : this(1D, red, green, blue) { }

	public ColorArgb( double alpha, double red, double green, double blue )
	{
		alpha.ArgMustBeBetween(MinValue, MaxValue);
		red.ArgMustBeBetween(MinValue, MaxValue);
		green.ArgMustBeBetween(MinValue, MaxValue);
		blue.ArgMustBeBetween(MinValue, MaxValue);

		this.alpha = alpha;
		this.red = red;
		this.green = green;
		this.blue = blue;
	}

	public ColorArgb(ColorArgb32 color) : this(color.Alpha / 255.0, color.Red / 255.0, color.Green / 255.0, color.Blue / 255.0) { }

	public ColorArgb( ColorArgb64 color ) : this(color.Alpha / 255.0, color.Red / 255.0, color.Green / 255.0, color.Blue / 255.0) { }

        public ColorArgb(ColorAhsv color)
        {
                ColorArgb tmp = color.ToArgbColor();
                alpha = tmp.alpha;
                red = tmp.red;
                green = tmp.green;
                blue = tmp.blue;
        }

	public static implicit operator ColorArgb(ColorAhsv color) => new (color);

	public static implicit operator ColorArgb(ColorArgb32 color) => new (color);

	public static implicit operator ColorArgb(ColorArgb64 color) => new (color);

	public static implicit operator ColorArgb(System.Drawing.Color color) => new (color.A / 255.0, color.R / 255.0, color.G / 255.0, color.B / 255.0);

	public static ColorArgb Gradient( ColorArgb color1, ColorArgb color2, double percent )
	{
		if (percent < 0) percent = 0;
		else if (percent > 1) percent = 1;
		return new ColorArgb(
			color1.alpha * (1-percent) + color2.alpha * percent,
			Math.Sqrt(Math.Pow(color1.Red, 2) * (1-percent) + Math.Pow(color2.Red , 2) * percent),
			Math.Sqrt(Math.Pow(color1.green, 2) * (1-percent) + Math.Pow(color2.green, 2) * percent),
			Math.Sqrt(Math.Pow(color1.blue, 2) * (1-percent) + Math.Pow(color2.blue, 2) * percent)
		);
	}
	public override readonly string ToString() => $"a:{alpha} R:{red} G:{green} B:{blue}";

	public IColorArgb<double> Over(IColorArgb<double> other) => new ColorArgb(
		this.Alpha + (1.0 - this.Alpha) * other.Alpha,
		this.Red * this.Alpha + (1.0 - this.Alpha) * other.Red,
		this.Green * this.Alpha + (1.0 - this.Alpha) * other.Green,
		this.Blue * this.Alpha + (1.0 - this.Alpha) * other.Blue
	);

	public IColorArgb<double> Add(IColorArgb<double> other) => new ColorArgb(
		MathEx.Min(1.0, this.Alpha + other.Alpha),
		MathEx.Min(1.0, this.Red + other.Red),
		MathEx.Min(1.0, this.Green + other.Green),
		MathEx.Min(1.0, this.Blue + other.Blue)
	);

	public IColorArgb<double> Substract(IColorArgb<double> other) => new ColorArgb(
			MathEx.Min(this.Alpha, other.Alpha),
			MathEx.Min(this.Red, other.Red),
			MathEx.Min(this.Green, other.Green),
			MathEx.Min(this.Blue, other.Blue)
	);

}
