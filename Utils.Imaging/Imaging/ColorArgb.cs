using System;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Imaging;

public struct ColorArgb : IColorArgb<double>
{
	private double alpha;
	private double red;
	private double green;
	private double blue;

	public double Alpha
	{
		get => alpha;

		set
		{
			if (!value.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(Alpha));
			this.alpha=value;
		}
	}

	public double Red
	{
		get => red;

		set
		{
			if (!value.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(Red));
			this.red=value;
		}
	}

	public double Green
	{
		get => green;

		set
		{
			if (!value.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(Green));
			this.green=value;
		}
	}

	public double Blue
	{
		get => blue;

		set
		{
			if (!value.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(Blue));
			this.blue=value;
		}
	}

	public ColorArgb(double red, double green, double blue) : this(1D, red, green, blue) { }

	public ColorArgb( double alpha, double red, double green, double blue )
	{
		alpha.ArgMustBeBetween(0.0, 1.0);
		red.ArgMustBeBetween(0.0, 1.0);
		green.ArgMustBeBetween(0.0, 1.0);
		blue.ArgMustBeBetween(0.0, 1.0);

		this.alpha = alpha;
		this.red = red;
		this.green = green;
		this.blue = blue;
	}

	public ColorArgb(ColorArgb32 color) : this(color.Alpha / 255.0, color.Red / 255.0, color.Green / 255.0, color.Blue / 255.0) { }

	public ColorArgb( ColorArgb64 color ) : this(color.Alpha / 255.0, color.Red / 255.0, color.Green / 255.0, color.Blue / 255.0) { }

        public ColorArgb( ColorAhsv color )
        {
		this.alpha = color.Alpha;

		double hh, p, q, t, ff;
		long i;

		if (color.Saturation <= 0.0) {       // < is bogus, just shuts up warnings
			this.red = color.Value;
			this.green = color.Value;
			this.blue = color.Value;
			return;
		}
		hh = color.Hue;
		if (hh >= 360.0) hh = 0.0;
		hh /= 60.0;
		i = (long)hh;
		ff = hh - i;
		p = color.Value * (1.0 - color.Saturation);
		q = color.Value * (1.0 - (color.Saturation* ff));
		t = color.Value * (1.0 - (color.Saturation * (1.0 - ff)));

                switch (i) {
                        case 0:
                                this.red = color.Value;
                                this.green = t;
                                this.blue = p;
                                break;
			case 1:
				this.red = q;
				this.green = color.Value;
				this.blue = p;
				break;
			case 2:
				this.red = p;
				this.green = color.Value;
				this.blue = t;
				break;

			case 3:
				this.red = p;
				this.green = q;
				this.blue = color.Value;
				break;
			case 4:
				this.red = t;
				this.green = p;
				this.blue = color.Value;
				break;
			default:
                                this.red = color.Value;
                                this.green = p;
                                this.blue = q;
                                break;
                }
        }

        /// <summary>
        /// Initializes a new instance from an <see cref="ColorAlab"/> value.
        /// </summary>
        /// <param name="color">Color in the ALab space.</param>
        public ColorArgb(ColorAlab color)
            : this()
        {
            this = color.ToArgb();
        }

        /// <summary>
        /// Initializes a new instance from an <see cref="ColorAcym"/> value.
        /// </summary>
        /// <param name="color">Color in the ACYM space.</param>
        public ColorArgb(ColorAcym color)
            : this()
        {
            this = color.ToArgb();
        }

        /// <summary>
        /// Initializes a new instance from an <see cref="ColorAcmyk"/> value.
        /// </summary>
        /// <param name="color">Color in the ACMYK space.</param>
        public ColorArgb(ColorAcmyk color)
            : this()
        {
            this = color.ToArgb();
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


	public void Deconstruct(out double alpha, out double red, out double green, out double blue)
	{
		alpha = Alpha;
		red = Red;
		green = Green;
		blue = Blue;
	}

	public void Deconstruct(out double red, out double green, out double blue)
	{
		red = Red;
		green = Green;
		blue = Blue;
	}
}
