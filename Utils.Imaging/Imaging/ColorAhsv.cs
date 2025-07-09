using System;
using System.Numerics;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Imaging
{
	public class ColorAhsv : IColorAhsv<double>, IColorArgbConvertible<ColorAhsv, ColorArgb, double>
	{
		public static double MinValue { get; } = 0.0;
		public static double MaxValue { get; } = 1.0;

		private double alpha;
		private double hue;
		private double saturation;
		private double value;

		public double Alpha
		{
			get => alpha;
			set
			{
				value.ArgMustBeBetween(MinValue, MaxValue);
				alpha = value;
			}
		}

		public double Hue
		{
			get => hue;
			set => this.hue = MathEx.Mod(value, 360.0);
		}

		public double Saturation
		{
			get => saturation; 

			set
			{
				value.ArgMustBeBetween(MinValue, MaxValue);
				this.saturation=value;
			}
		}

		public double Value
		{
			get => value;

			set
			{
				value.ArgMustBeBetween(MinValue, MaxValue);
				this.value=value;
			}
		}

		public ColorAhsv( double alpha, double hue, double saturation, double value )
		{
			alpha.ArgMustBeBetween(MinValue, MaxValue);
			saturation.ArgMustBeBetween(MinValue, MaxValue);
			value.ArgMustBeBetween(MinValue, MaxValue);

			this.alpha = alpha;
			this.Hue = MathEx.Mod(hue, 360.0);
			this.saturation = saturation;
			this.value = value;
		}


		public ColorAhsv(double hue, double saturation, double value) : this(MaxValue, hue, saturation, value) { }

		public static ColorAhsv FromColorAshv<TColorAshv, T>(TColorAshv color)
			where TColorAshv : IColorAhsv<T>
			where T : struct, INumber<T> 
		{
			double maxValue = double.CreateChecked(TColorAshv.MaxValue);
			double alpha = double.CreateChecked(color.Alpha) / maxValue;
			double hue = double.CreateChecked(color.Hue) / maxValue * 360;
			double saturation = double.CreateChecked(color.Saturation) / maxValue;
			double value = double.CreateChecked(color.Value) / maxValue;
			return new (alpha, hue, saturation, value);
		}

		public ColorAhsv( ColorArgb color )
		{

		}

		public static implicit operator ColorAhsv(ColorArgb color) => new ColorAhsv(color);
		public static implicit operator ColorAhsv(ColorAhsv32 color) => FromColorAshv<ColorAhsv32, byte>(color);
		public static implicit operator ColorAhsv(ColorAhsv64 color) => FromColorAshv<ColorAhsv64, ushort>(color);

		public override string ToString() => $"a:{alpha} h:{hue} s:{saturation} v:{value}";
		public static ColorAhsv FromArgbColor(ColorArgb color)
		{
			double min, max, delta;

			min = color.Red < color.Green ? color.Red : color.Green;
			min = min < color.Blue ? min : color.Blue;

			max = color.Red > color.Green ? color.Red : color.Green;
			max = max > color.Blue ? max : color.Blue;

			double value = max;                                // v
			delta = max - min;
			if (delta < 0.00001)
			{
				return new(color.Alpha, 0, 0, 0);
			}

			double saturation, hue;
			if (max > 0.0)
			{ // NOTE: if Max is == 0, this divide would cause a crash
				saturation = (delta / max);                  // s
			}
			else
			{
				// if max is 0, then r = g = b = 0              
				// s = 0, v is undefined
				saturation = 0.0;
				hue = 0.0;                            // its now undefined
				return new(color.Alpha, hue, saturation, value);
			}
			if (color.Red >= max)                           // > is bogus, just keeps compilor happy
				hue = (color.Green - color.Blue) / delta;        // between yellow & magenta
			else if (color.Green >= max)
				hue = 2.0 + (color.Blue - color.Red) / delta;  // between cyan & yellow
			else
				hue = 4.0 + (color.Red - color.Green) / delta;  // between magenta & cyan

			hue *= 60.0;                              // degrees

			if (hue < 0.0)
				hue += 360.0;

			return new (color.Alpha, hue, saturation, value);
		}
		public ColorArgb ToArgbColor() => throw new NotImplementedException();
	}

}
