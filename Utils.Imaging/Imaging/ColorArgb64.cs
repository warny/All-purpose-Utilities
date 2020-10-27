﻿using System;
using System.Runtime.InteropServices;
using Utils.Mathematics;

namespace Utils.Imaging
{

	[StructLayout(LayoutKind.Explicit)]
	public struct ColorArgb64 : IColorArgb<ushort>
	{
		[FieldOffset(0)]
		ulong value;

		[FieldOffset(8)]
		ushort alpha;
		[FieldOffset(4)]
		ushort red;
		[FieldOffset(2)]
		ushort green;
		[FieldOffset(0)]
		ushort blue;

		public ulong Value
		{
			get { return value; }
			set { this.value=value; }
		}

		public ushort Alpha
		{
			get { return alpha; }
			set { this.alpha=value; }
		}

		public ushort Red
		{
			get { return red; }
			set { this.red=value; }
		}

		public ushort Green
		{
			get { return green; }
			set { this.green=value; }
		}

		public ushort Blue
		{
			get { return blue; }
			set { this.blue=value; }
		}

		public ColorArgb64( ulong color ) : this()
		{
			this.value = color;
		}

		public ColorArgb64( uint color ) : this()
		{
			this.alpha = (ushort)(0xFF00 & color >> 16);
			this.red = (ushort)(0xFF00 & color >> 8);
			this.green = (ushort)(0xFF00 & color);
			this.blue = (ushort)(0xFF00 & color << 8);
		}

		public ColorArgb64( ColorArgb colorArgb ) : this()
		{
			this.alpha = (ushort)(colorArgb.Alpha * 65535);
			this.red =   (ushort)(colorArgb.Red * 65535);
			this.green = (ushort)(colorArgb.Green * 65535);
			this.blue =  (ushort)(colorArgb.Blue * 65535);
		}

		public ColorArgb64( ColorArgb32 colorArgb32 ) : this()
		{
			this.alpha = (ushort)(colorArgb32.Alpha << 8);
			this.red =   (ushort)(colorArgb32.Red << 8);
			this.green = (ushort)(colorArgb32.Green << 8);
			this.blue =  (ushort)(colorArgb32.Blue << 8);
		}

		public ColorArgb64( System.Drawing.Color color ) : this()
		{
			this.alpha = (ushort)(color.A << 8);
			this.red =   (ushort)(color.R << 8);
			this.green = (ushort)(color.G << 8);
			this.blue =  (ushort)(color.B << 8);
		}

		public ColorArgb64(ushort red, ushort green, ushort blue) : this(ushort.MaxValue, red, green, blue) { }
		public ColorArgb64( ushort alpha, ushort red, ushort green, ushort blue ) : this()
		{
			this.alpha = alpha;
			this.red= red;
			this.green= green;
			this.blue= blue;
		}

		public ColorArgb64( ushort[] array, int index ) : this()
		{
			this.alpha = array[index];
			this.red = array[index+1];
			this.green = array[index+2];
			this.blue = array[index+3];
		}

		public ColorArgb64( ColorAhsv64 colorAHSV ) : this()
		{
			alpha = colorAHSV.Alpha;
			ushort region, remainder, p, q, t;

			if (colorAHSV.Saturation == 0) {
				red = colorAHSV.Value;
				green = colorAHSV.Value;
				blue = colorAHSV.Value;
				return;
			}

			region = (ushort)(colorAHSV.Hue / 10923);
			remainder = (ushort)((colorAHSV.Hue - (region * 10923)) * 6);

			p = (ushort)((colorAHSV.Value * (65535 - colorAHSV.Saturation)) >> 16);
			q = (ushort)((colorAHSV.Value * (65535 - ((colorAHSV.Saturation * remainder) >> 16))) >> 16);
			t = (ushort)((colorAHSV.Value * (65535 - ((colorAHSV.Saturation * (65535 - remainder)) >> 16))) >> 16);

			switch (region) {
				case 0:
					red = colorAHSV.Value; green = t; blue = p;
					break;
				case 1:
					red = q; green = colorAHSV.Value; blue = p;
					break;
				case 2:
					red = p; green = colorAHSV.Value; blue = t;
					break;
				case 3:
					red = p; green = q; blue = colorAHSV.Value;
					break;
				case 4:					
					red = t; green = p; blue = colorAHSV.Value;
					break;
				default:
					red = colorAHSV.Value; green = p; blue = q;
					break;
			}
		}

		public override bool Equals( object obj )
		{
			return obj is ColorArgb64 && Value == ((ColorArgb64)obj).Value;
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public static implicit operator ColorArgb64(ColorAhsv32 color) => new ColorArgb64(color);
		public static implicit operator ColorArgb64(ColorArgb color) => new ColorArgb64(color);
		public static implicit operator ColorArgb64(ColorArgb32 color) => new ColorArgb64(color);
		public static implicit operator ColorArgb64(System.Drawing.Color color) => new ColorArgb64(color);

		public override string ToString() => $"a:{alpha} R:{red} G:{green} B:{blue}";

		private ColorArgb64 BuildColor(
			IColorArgb<ushort> other,
			Func<ushort, ushort, ushort> alphaFunction,
			Func<ushort, ushort, ushort, ushort, ushort, ushort> colorFunction)
		{
			ushort computedAlpha = alphaFunction(this.Alpha, other.Alpha);
			if (computedAlpha == 0) return new ColorArgb64(0);
			return new ColorArgb64(
					alpha,
					colorFunction(computedAlpha, this.Alpha, other.Alpha, this.Red, other.Alpha),
					colorFunction(computedAlpha, this.Alpha, other.Alpha, this.Green, other.Red),
					colorFunction(computedAlpha, this.Alpha, other.Alpha, this.Blue, other.Blue)
				);
		}

		public IColorArgb<ushort> Over(IColorArgb<ushort> other)
		{
			return BuildColor(
				other,
				(thisAlpha, otherAlpha) => (ushort)(thisAlpha + (ushort.MaxValue - thisAlpha) * otherAlpha / ushort.MaxValue),
				(alpha, thisAlpha, otherAlpha, thisComponent, otherComponent) => (ushort)(thisComponent * thisAlpha + (ushort.MaxValue - thisAlpha) * otherComponent / ushort.MaxValue));
		}

		public IColorArgb<ushort> Add(IColorArgb<ushort> other)
		{
			return BuildColor(
				other,
				(thisAlpha, otherAlpha) => (ushort)Math.Sqrt(thisAlpha * otherAlpha),
				(alpha, thisAlpha, otherAlpha, thisComponent, otherComponent) => (ushort)((thisComponent * thisAlpha + otherComponent * otherAlpha) / (thisAlpha + otherAlpha))
			);
		}

		public IColorArgb<ushort> Substract(IColorArgb<ushort> other)
		{
			return new ColorArgb64(
					MathEx.Min(this.Alpha, other.Alpha),
					MathEx.Min(this.Red, other.Red),
					MathEx.Min(this.Green, other.Green),
					MathEx.Min(this.Blue, other.Blue)
				);
		}

		public void Deconstruct(out ushort alpha, out ushort red, out ushort green, out ushort blue)
		{
			alpha = Alpha;
			red = Red;
			green = Green;
			blue = Blue;
		}

		public void Deconstruct(out ushort red, out ushort green, out ushort blue)
		{
			red = Red;
			green = Green;
			blue = Blue;
		}
	}
}
