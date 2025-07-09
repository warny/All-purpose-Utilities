namespace Utils.Imaging
{
	public class ColorAhsv64 : IColorAhsv<ushort>, IColorArgbConvertible<ColorAhsv64, ColorArgb64, ushort>
	{
		public static ushort MinValue { get; } = 0;
		public static ushort MaxValue { get; } = ushort.MaxValue;

		public ushort Alpha { get; set; }
		public ushort Hue { get; set; }
		public ushort Saturation { get; set; }
		public ushort Value { get; set; }

		public ColorAhsv64( ushort alpha, ushort hue, ushort Saturation, ushort value )
		{
			this.Alpha = alpha;
			this.Hue = hue;
			this.Saturation = Saturation;
			this.Value = value;
		}

		public static ColorAhsv64 FromArgbColor(ushort alpha, ushort red, ushort green, ushort blue)
		{
			ushort hue;
			ushort saturation;
			ushort value;


			ushort rgbMin, rgbMax;

			rgbMin = Mathematics.MathEx.Min(red, green, blue);
			rgbMax = Mathematics.MathEx.Max(red, green, blue);

			//cas du gris
			if (rgbMin == rgbMax)
			{
				return new(alpha, rgbMin, rgbMin, rgbMin);
			}
			value = rgbMax;

			int delta = rgbMax - rgbMin;

			saturation = (byte)(255 * delta / value);
			if (saturation == 0)
			{
				return new(alpha, 0, saturation, value);
			}

			if (rgbMax == red)
				hue = (ushort)(0 + 10923 * (green - blue) / delta);
			else if (rgbMax == green)
				hue = (ushort)(21845 + 10923 * (blue - red) / delta);
			else
				hue = (ushort)(43690 + 10923 * (red - green) / delta);

			return new(alpha, hue, saturation, value);
		}

		public ColorArgb64 ToArgbColor()
		{
			double hh, p, q, t, ff;
			long i;

			if (Saturation <= 0.0)
			{
				return new ColorArgb64(Alpha, Value, Value, Value);
			}

			hh = Hue;
			if (hh >= 360.0) hh = 0.0;
			hh /= 60.0;
			i = (long)hh;
			ff = hh - i;
			p = Value * (1.0 - Saturation);
			q = Value * (1.0 - (Saturation * ff));
			t = Value * (1.0 - (Saturation * (1.0 - ff)));

			switch (i)
			{
				case 0:
					return new ColorArgb64(Alpha, Value, (ushort)t, (ushort)p);
				case 1:
					return new ColorArgb64(Alpha, (ushort)q, Value, (ushort)p);
				case 2:
					return new ColorArgb64(Alpha, (ushort)p, Value, (ushort)t);
				case 3:
					return new ColorArgb64(Alpha, (ushort)p, (ushort)q, Value);
				case 4:
					return new ColorArgb64(Alpha, (ushort)t, (ushort)p, Value);
				default:
					return new ColorArgb64(Alpha, Value, (ushort)p, (ushort)q);
			}
		}

		public static implicit operator ColorAhsv64(ColorAhsv color) => new ColorAhsv64((ushort)(color.Alpha * 65535), (ushort)(color.Hue * 65535), (ushort)(color.Saturation * 65535), (ushort)(color.Value * 65535));
		public override string ToString() => $"a:{Alpha} h:{Hue} s:{Saturation} v:{Value}";
		public static ColorAhsv64 FromArgbColor(ColorArgb64 color) => FromArgbColor(color.Alpha, color.Red, color.Green, color.Blue);
	}
}
