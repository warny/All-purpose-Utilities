using System.Runtime.InteropServices;

namespace Utils.Imaging
{
	[StructLayout(LayoutKind.Explicit)]
	public struct ColorArgb32 : IColorArgb<byte>
	{
		[FieldOffset(0)]
		uint value;

		[FieldOffset(3)]
		byte alpha;
		[FieldOffset(2)]
		byte red;
		[FieldOffset(1)]
		byte green;
		[FieldOffset(0)]
		byte blue;

		public uint Value
		{
			get { return value; }
			set { this.value=value; }
		}

		public byte Alpha
		{
			get { return alpha; }
			set { this.alpha=value; }
		}

		public byte Red
		{
			get { return red; }
			set { this.red=value; }
		}

		public byte Green
		{
			get { return green; }
			set { this.green=value; }
		}

		public byte Blue
		{
			get { return blue; }
			set { this.blue=value; }
		}


		public ColorArgb32( uint color ) : this()
		{
			this.alpha = (byte)(0xFF & color >> 24);
			this.red = (byte)(0xFF & color >> 16);
			this.green = (byte)(0xFF & color >> 8);
			this.blue = (byte)(0xFF & color);
		}

		public ColorArgb32(byte red, byte green, byte blue) : this(byte.MaxValue, red, green, blue) { }
		public ColorArgb32( byte alpha, byte red, byte green, byte blue ) : this()
		{
			this.alpha = alpha;
			this.red = red;
			this.green = green;
			this.blue = blue;
		}

		public ColorArgb32( byte[] array, int index ) : this()
		{
			this.alpha = array[index];
			this.red = array[index+1];
			this.green = array[index+2];
			this.blue = array[index+3];
		}

		public ColorArgb32( ColorArgb colorArgb64 ) : this()
		{
			this.alpha = (byte)(colorArgb64.Alpha * 255);
			this.red = (byte)(colorArgb64.Red * 255);
			this.green = (byte)(colorArgb64.Green * 255);
			this.blue = (byte)(colorArgb64.Blue * 255);
		}

		public ColorArgb32( ColorArgb64 colorArgb64 ) : this()
		{
			this.alpha = (byte)(colorArgb64.Alpha >> 8);
			this.red = (byte)(colorArgb64.Red >> 8);
			this.green = (byte)(colorArgb64.Green >> 8);
			this.blue = (byte)(colorArgb64.Blue >> 8);
		}

		public ColorArgb32( System.Drawing.Color color ) : this()
		{
			Alpha = color.A;
			Red = color.R;
			Green = color.G;
			Blue = color.B;
		}

		public ColorArgb32( ColorAhsv32 colorAHSV )	: this()
		{
			this.alpha = colorAHSV.Alpha;
			byte region, remainder, p, q, t;

			if (colorAHSV.Saturation == 0) {
				this.red = colorAHSV.Value;
				this.green = colorAHSV.Value;
				this.blue = colorAHSV.Value;
				return;
			}

			region = (byte)(colorAHSV.Hue / 43);
			remainder = (byte)((colorAHSV.Hue - (region * 43)) * 6);

			p = (byte)((colorAHSV.Value * (255 - colorAHSV.Saturation)) >> 8);
			q = (byte)((colorAHSV.Value * (255 - ((colorAHSV.Saturation * remainder) >> 8))) >> 8);
			t = (byte)((colorAHSV.Value * (255 - ((colorAHSV.Saturation * (255 - remainder)) >> 8))) >> 8);

			switch (region) {
				case 0:
					this.red = colorAHSV.Value; this.green = t; this.blue = p;
					break;
				case 1:
					this.red = q; this.green = colorAHSV.Value; this.blue = p;
					break;
				case 2:
					this.red = p; this.green = colorAHSV.Value; this.blue = t;
					break;
				case 3:
					this.red = p; this.green = q; this.blue = colorAHSV.Value;
					break;
				case 4:
					this.red = t; this.green = p; this.blue = colorAHSV.Value;
					break;
				default:
					this.red = colorAHSV.Value; this.green = p; this.blue = q;
					break;
			}
		}

		public override bool Equals( object obj )
		{
			return obj is ColorArgb32 && Value == ((ColorArgb32)obj).Value;
		}

		public override int GetHashCode()
		{
			return (int)Value;
		}

		public static implicit operator ColorArgb32( ColorAhsv32 color )
		{
			return new ColorArgb32(color);
		}

		public static implicit operator ColorArgb32( ColorArgb color )
		{
			return new ColorArgb32(color);
		}

		public static implicit operator ColorArgb32( ColorArgb64 color )
		{
			return new ColorArgb32(color);
		}

		public static implicit operator ColorArgb32( System.Drawing.Color color )
		{
			return new ColorArgb32(color);
		}

		public override string ToString() => $"a:{alpha} R:{red} G:{green} B:{blue}";
	}
}
