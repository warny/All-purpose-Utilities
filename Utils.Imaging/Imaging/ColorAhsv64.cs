namespace Utils.Imaging
{
        /// <summary>
        /// HSV color representation using 16-bit components.
        /// </summary>
	public class ColorAhsv64 : IColorAhsv<ushort>, IColorArgbConvertible<ColorAhsv64, ColorArgb64, ushort>
	{
		public static ushort MinValue { get; } = 0;
		public static ushort MaxValue { get; } = ushort.MaxValue;

                /// <summary>Alpha component.</summary>
                public ushort Alpha { get; set; }
                /// <summary>Hue component.</summary>
                public ushort Hue { get; set; }
                /// <summary>Saturation component.</summary>
                public ushort Saturation { get; set; }
                /// <summary>Value component.</summary>
                public ushort Value { get; set; }

                /// <summary>
                /// Initializes a new instance with explicit 16-bit components.
                /// </summary>
                public ColorAhsv64(ushort alpha, ushort hue, ushort saturation, ushort value)
                {
                        Alpha = alpha;
                        Hue = hue;
                        Saturation = saturation;
                        Value = value;
                }

                /// <summary>
                /// Creates a HSV color from 16-bit ARGB components.
                /// </summary>
                public static ColorAhsv64 FromArgbColor(ushort alpha, ushort red, ushort green, ushort blue)
		{
			ushort hue;
			ushort saturation;
			ushort value;


			ushort rgbMin, rgbMax;

			rgbMin = Mathematics.MathEx.Min(red, green, blue);
			rgbMax = Mathematics.MathEx.Max(red, green, blue);

                        // gray case
                        if (rgbMin == rgbMax)
                        {
                                return new(alpha, 0, 0, rgbMax);
                        }
                        value = rgbMax;

			int delta = rgbMax - rgbMin;

                        saturation = (ushort)(65535 * delta / value);
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

                /// <summary>
                /// Converts this color to <see cref="ColorArgb64"/>.
                /// </summary>
                public ColorArgb64 ToArgbColor()
                {
                        ushort a = Alpha;
                        if (Saturation == 0)
                        {
                                return new ColorArgb64(a, Value, Value, Value);
                        }

                        ushort region = (ushort)(Hue / 10923);
                        ushort remainder = (ushort)((Hue - region * 10923) * 6);

                        ushort p = (ushort)((Value * (65535 - Saturation)) >> 16);
                        ushort q = (ushort)((Value * (65535 - ((Saturation * remainder) >> 16))) >> 16);
                        ushort t = (ushort)((Value * (65535 - ((Saturation * (65535 - remainder)) >> 16))) >> 16);

                        return region switch
                        {
                                0 => new ColorArgb64(a, Value, t, p),
                                1 => new ColorArgb64(a, q, Value, p),
                                2 => new ColorArgb64(a, p, Value, t),
                                3 => new ColorArgb64(a, p, q, Value),
                                4 => new ColorArgb64(a, t, p, Value),
                                _ => new ColorArgb64(a, Value, p, q)
                        };
                }

		public static implicit operator ColorAhsv64(ColorAhsv color) => new ColorAhsv64((ushort)(color.Alpha * 65535), (ushort)(color.Hue * 65535), (ushort)(color.Saturation * 65535), (ushort)(color.Value * 65535));
		public override string ToString() => $"a:{Alpha} h:{Hue} s:{Saturation} v:{Value}";
                /// <summary>
                /// Converts from a <see cref="ColorArgb64"/> value.
                /// </summary>
                public static ColorAhsv64 FromArgbColor(ColorArgb64 color) => FromArgbColor(color.Alpha, color.Red, color.Green, color.Blue);
	}
}
