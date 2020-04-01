using System;
using Utils.Mathematics;

namespace Utils.Imaging
{
	public class ColorAhsv : IColorAhsv<double>
	{
		private double alpha;
		private double hue;
		private double saturation;
		private double value;

		public double Alpha
		{
			get => alpha;
			set
			{
				if (!value.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(Alpha));
				alpha = value;
			}
		}

		public double Hue
		{
			get => hue;

			set
			{
				this.hue = Math.IEEERemainder(value, 360.0);
			}
		}

		public double Saturation
		{
			get => saturation; 

			set
			{
				if (!value.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(Saturation));
				this.saturation=value;
			}
		}

		public double Value
		{
			get => value;

			set
			{
				if (!value.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(Value));
				this.value=value;
			}
		}

		public ColorAhsv( double alpha, double hue, double saturation, double value )
		{
			if (!alpha.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(alpha));
			if (!saturation.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(saturation));
			if (!value.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(value));

			this.alpha = alpha;
			this.Hue = Math.IEEERemainder(hue, 360.0);
			this.saturation = saturation;
			this.value = value;
		}


		public ColorAhsv( double hue, double saturation, double value )
		{
			if (!saturation.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(saturation));
			if (!value.Between(0.0, 1.0)) throw new ArgumentOutOfRangeException(nameof(value));

			this.alpha = 1;
			this.Hue = Math.IEEERemainder(hue, 360.0);
			this.saturation = saturation;
			this.value = value;
		}

		public ColorAhsv( ColorAhsv32 color ) {
			this.alpha = (double)color.Alpha / 255;
			this.Hue = (double)color.Alpha / 255 * 360;
			this.saturation = (double)color.Alpha / 255;
			this.value = (double)color.Alpha / 255;
		}

		public ColorAhsv( ColorAhsv64 color )
		{
			this.alpha = (double)color.Alpha / 65535;
			this.Hue = (double)color.Alpha / 65535 * 360;
			this.saturation = (double)color.Alpha / 65535;
			this.value = (double)color.Alpha / 65535;
		}

		public ColorAhsv( ColorArgb color )
		{
			this.alpha = color.Alpha;
			double min, max, delta;

			min = color.Red < color.Green ? color.Red : color.Green;
			min = min  < color.Blue ? min : color.Blue;

			max = color.Red > color.Green ? color.Red : color.Green;
			max = max  > color.Blue ? max : color.Blue;

			this.value = max;                                // v
			delta = max - min;
			if (delta < 0.00001) {
				this.saturation = 0;
				this.hue = 0; // undefined, maybe nan?
				return;
			}
			if (max > 0.0) { // NOTE: if Max is == 0, this divide would cause a crash
				Saturation = (delta / max);                  // s
			} else {
				// if max is 0, then r = g = b = 0              
				// s = 0, v is undefined
				Saturation = 0.0;
				Hue = 0.0;                            // its now undefined
				return;
			}
			if (color.Red >= max)                           // > is bogus, just keeps compilor happy
				this.hue = (color.Green - color.Blue) / delta;        // between yellow & magenta
			else if (color.Green >= max)
				this.hue = 2.0 + (color.Blue - color.Red) / delta;  // between cyan & yellow
			else
				this.hue = 4.0 + (color.Red - color.Green) / delta;  // between magenta & cyan

			this.hue *= 60.0;                              // degrees

			if (this.hue < 0.0)
				this.hue += 360.0;

		}

		public static implicit operator ColorAhsv( ColorArgb color )
		{
			return new ColorAhsv(color);
		}

		public static implicit operator ColorAhsv( ColorAhsv32 color )
		{
			return new ColorAhsv(color);
		}

		public static implicit operator ColorAhsv( ColorAhsv64 color )
		{
			return new ColorAhsv(color);
		}
		public override string ToString() => $"a:{alpha} h:{hue} s:{saturation} v:{value}";
	}
}
