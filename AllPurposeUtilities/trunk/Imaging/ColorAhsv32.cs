using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Imaging
{
	public class ColorAhsv32 : IColorAhsv<byte>
	{
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

		public ColorAhsv32( ColorArgb32 colorArgb )
		{
			Alpha = colorArgb.Alpha;

			byte rgbMin, rgbMax;

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

			Saturation = (byte)(255 * delta / Value);
			if (Saturation == 0) {
				Hue = 0;
				return;
			}

			if (rgbMax == colorArgb.Red)
				Hue = (byte)(0 + 43 * (colorArgb.Green - colorArgb.Blue) / delta);
			else if (rgbMax == colorArgb.Green)
				Hue = (byte)(85 + 43 * (colorArgb.Blue - colorArgb.Red) / delta);
			else
				Hue = (byte)(171 + 43 * (colorArgb.Red - colorArgb.Green) / delta);

			return;
		}


		public static implicit operator ColorAhsv32( ColorArgb32 color )
		{
			return new ColorAhsv32(color);
		}

		public static implicit operator ColorAhsv32( ColorAhsv color )
		{
			return new ColorAhsv32(color);
		}

		public static implicit operator ColorAhsv32( ColorAhsv64 color )
		{
			return new ColorAhsv32(color);
		}

	}
}
