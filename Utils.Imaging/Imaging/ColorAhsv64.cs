namespace Utils.Imaging
{
	public class ColorAhsv64 : IColorAhsv<ushort>
	{
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

		public ColorAhsv64( ColorArgb64 colorArgb )
		{
			Alpha = colorArgb.Alpha;

			ushort rgbMin, rgbMax;

			rgbMin = Mathematics.MathEx.Min(colorArgb.Red, colorArgb.Green, colorArgb.Blue);
			rgbMax = Mathematics.MathEx.Max(colorArgb.Red, colorArgb.Green, colorArgb.Blue);

			//cas du noir
			if (Value == 0) {
				Value = rgbMax;
				Hue = 0;
				Saturation = 0;
				return;
			}

			int delta = rgbMax - rgbMin;

			Saturation = (ushort)(65535 * delta / Value);
			if (Saturation == 0) {
				Hue = 0;
				return;
			}

			if (rgbMax == colorArgb.Red)
				Hue = (ushort)(0 + 10923 * (colorArgb.Green - colorArgb.Blue) / delta);
			else if (rgbMax == colorArgb.Green)
				Hue = (ushort)(21845 + 10923 * (colorArgb.Blue - colorArgb.Red) / delta);
			else
				Hue = (ushort)(43690 + 10923 * (colorArgb.Red - colorArgb.Green) / delta);

		}
		
		public static implicit operator ColorAhsv64(ColorArgb64 color) => new ColorAhsv64(color);
		public static implicit operator ColorAhsv64(ColorAhsv color) => new ColorAhsv64((ushort)(color.Alpha * 65535), (ushort)(color.Hue * 65535), (ushort)(color.Saturation * 65535), (ushort)(color.Value * 65535));
		public static implicit operator ColorAhsv64(System.Drawing.Color color) => new ColorAhsv64(color);
		public override string ToString() => $"a:{Alpha} h:{Hue} s:{Saturation} v:{Value}";

		public void Deconstruct(out ushort alpha, out ushort hue, out ushort saturation, out ushort value)
		{
			alpha = Alpha;
			hue = Hue;
			saturation = Saturation;
			value = Value;
		}

		public void Deconstruct(out ushort hue, out ushort saturation, out ushort value)
		{
			hue = Hue;
			saturation = Saturation;
			value = Value;
		}
	}
}
