using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics;

namespace Utils.Imaging
{
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

		public ColorArgb( double alpha, double red, double green, double blue )
		{
			if (!alpha.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(alpha));
			if (!red.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(red));
			if (!green.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(green));
			if (!blue.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(blue));

			this.alpha = alpha;
			this.red = red;
			this.green = green;
			this.blue = blue;
		}

		public ColorArgb( double red, double green, double blue )
		{
			if (!red.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(red));
			if (!green.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(green));
			if (!blue.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(blue));

			this.alpha = 1;
			this.red = red;
			this.green = green;
			this.blue = blue;
		}

		public ColorArgb( byte alpha, byte red, byte green, byte blue )
		{
			this.alpha = (double)alpha / 255;
			this.red = (double)red / 255;
			this.green = (double)green / 255;
			this.blue = (double)blue / 255;
		}

		public ColorArgb( ColorArgb32 color ) : this(color.Alpha, color.Red, color.Green, color.Blue) { }

		public ColorArgb( ushort alpha, ushort red, ushort green, ushort blue )
		{
			this.alpha = (double)alpha / 65535;
			this.red = (double)red / 65535;
			this.green = (double)green / 65535;
			this.blue = (double)blue / 65535;
		}
		public ColorArgb( ColorArgb64 color ) : this(color.Alpha, color.Red, color.Green, color.Blue) { }

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
				case 5:
				default:
					this.red = color.Value;
					this.green = p;
					this.blue = q;
					break;
			}
		}

		public static implicit operator ColorArgb( ColorAhsv color )
		{
			return new ColorArgb(color);
		}

		public static implicit operator ColorArgb( ColorArgb32 color )
		{
			return new ColorArgb(color);
		}

		public static implicit operator ColorArgb( ColorArgb64 color )
		{
			return new ColorArgb(color);
		}

		public static implicit operator ColorArgb( System.Drawing.Color color )
		{
			return new ColorArgb((double)color.A / 255, (double)color.R / 255, (double) color.G / 255, (double)color.B / 255);
		}

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
	}
}
