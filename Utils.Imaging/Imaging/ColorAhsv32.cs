namespace Utils.Imaging;

public class ColorAhsv32 : IColorAhsv<byte>, IColorArgbConvertible<ColorAhsv32, ColorArgb32, byte>
{
	public static byte MinValue { get; } = 0;
	public static byte MaxValue { get; } = byte.MaxValue;

	public byte Alpha { get; set; }
	public byte Hue { get; set; }
	public byte Saturation { get; set; }
	public byte Value { get; set; }

	public ColorAhsv32( byte alpha, byte hue, byte saturation, byte value )
	{
		this.Alpha = alpha;
		this.Hue = hue;
		this.Saturation = saturation;
		this.Value = value;
	}
	public ColorAhsv32( ColorAhsv64 color )
	{
		this.Alpha = (byte)(color.Alpha >> 8);
		this.Hue = (byte)(color.Hue >> 8);
		this.Saturation = (byte)(color.Saturation >> 8);
		this.Value = (byte)(color.Value >> 8);
	}

	public ColorAhsv32( ColorAhsv color )
	{
		this.Alpha = (byte)(color.Alpha * 255);
		this.Hue = (byte)(color.Hue / 360 * 255);
		this.Saturation = (byte)(color.Saturation * 255);
		this.Value = (byte)(color.Value * 255);
	}

	public ColorAhsv32(System.Drawing.Color colorArgb) { FromArgbColor(colorArgb.A, colorArgb.R, colorArgb.G, colorArgb.B); }

	public static ColorAhsv32 FromArgbColor(ColorArgb32 colorArgb) => FromArgbColor(colorArgb.Alpha, colorArgb.Red, colorArgb.Green, colorArgb.Blue);

	public static ColorAhsv32 FromArgbColor(byte alpha, byte red, byte green, byte blue)
	{
		byte hue;
		byte saturation;
		byte value;


		byte rgbMin, rgbMax;

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
			hue = (byte)(0 + 43 * (green - blue) / delta);
		else if (rgbMax == green)
			hue = (byte)(85 + 43 * (blue - red) / delta);
		else
			hue = (byte)(171 + 43 * (red - green) / delta);

		return new (alpha, hue, saturation, value);
	}

	public ColorArgb32 ToArgbColor()
	{
		double hh, p, q, t, ff;
		long i;

		if (Saturation <= 0.0)
		{
			return new ColorArgb32(Alpha, Value, Value, Value);
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
				return new ColorArgb32(Alpha, Value, (byte)t, (byte)p);
			case 1:
				return new ColorArgb32(Alpha, (byte)q, Value, (byte)p);
			case 2:
				return new ColorArgb32(Alpha, (byte)p, Value, (byte)t);
			case 3:
				return new ColorArgb32(Alpha, (byte)p, (byte)q, Value);
			case 4:
				return new ColorArgb32(Alpha, (byte)t, (byte)p, Value);
			default:
				return new ColorArgb32(Alpha, Value, (byte)p, (byte)q);
		}
	}


	public static implicit operator ColorAhsv32(ColorAhsv color) => new ColorAhsv32(color);
	public static implicit operator ColorAhsv32(ColorAhsv64 color) => new ColorAhsv32(color);
	public static implicit operator ColorAhsv32(System.Drawing.Color color) => new ColorAhsv32(color);

	public override string ToString() => $"a:{Alpha} h:{Hue} s:{Saturation} v:{Value}";
}
