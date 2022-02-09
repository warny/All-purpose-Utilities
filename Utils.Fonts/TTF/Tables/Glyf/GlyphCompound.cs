using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Glyph;

public class GlyphCompound : GlyphBase
{
	internal class GlyfComponent
	{
		public CompoundGlyfFlags flags;
		public short GlyphIndex { get; internal set; }
		public int CompoundPoint { get; internal set; }
		public int ComponentPoint { get; internal set; }
		public float a { get; internal set; } = 1f;
		public float b { get; internal set; } = 0f;
		public float c { get; internal set; } = 0f;
		public float d { get; internal set; } = 1f;
		public float e { get; internal set; } = 0f;
		public float f { get; internal set;} = 0f;

		public float m { get; private set; } = 0f;
		public float n { get; private set; } = 0f;

		public virtual void ComputeTransform()
		{
			const float limit = (33f / 65535f);
			m = Math.Max(Math.Abs(a), Math.Abs(b));
			if (Math.Abs(Math.Abs(a) - Math.Abs(c)) < limit)
			{
				m *= 2f;
			}
			n = Math.Max(Math.Abs(c), Math.Abs(d));
			if (Math.Abs(Math.Abs(c) - Math.Abs(d)) < limit)
			{
				n *= 2f;
			}
		}

		public (float x, float y, bool onCurve) Transform((float x, float y, bool onCurve) point) => Transform(point.x, point.y, point.onCurve);

		public (float x, float y, bool onCurve) Transform(float x, float y, bool onCurve)
			=> (
				a * x + c * y + m * e,
				b * x + d * y + n * f,
				onCurve
			);

	}

	public override bool IsCompound => true;

	private GlyfComponent[] Components { get; set; }

	public byte[] Instructions { get; private set; }

	public virtual int ComponentsCount => Components.Length;

	public virtual short getGlyphIndex(int i) => Components[i].GlyphIndex;

	protected internal GlyphCompound() { }

	public override void ReadData(Reader data)
	{
		List<GlyfComponent> comps = new List<GlyfComponent>();
		bool hasInstructions = false;
		GlyfComponent current;
		do
		{
			current = new GlyfComponent();
			current.flags = (CompoundGlyfFlags)data.ReadInt16(true);
			current.GlyphIndex = data.ReadInt16(true);
			switch ((current.flags.HasFlag(CompoundGlyfFlags.ARG_1_AND_2_ARE_WORDS), current.flags.HasFlag(CompoundGlyfFlags.ARGS_ARE_XY_VALUES)))
			{
				case (true, true):
					current.e = data.ReadInt16(true);
					current.f = data.ReadInt16(true);
					break;
				case (false, true):
					current.e = data.ReadInt16(true);
					current.f = data.ReadInt16(true);
					break;
				case (true, false):
					current.CompoundPoint = data.ReadInt16(true);
					current.ComponentPoint = data.ReadInt16(true);
					break;
				case (false, false):
					current.CompoundPoint = data.ReadInt16(true);
					current.ComponentPoint = data.ReadInt16(true);
					break;
			}


			if (current.flags.HasFlag(CompoundGlyfFlags.WE_HAVE_A_SCALE))
			{
				current.a = data.ReadInt16(true) / 16384f;
				current.d = current.a;
			}
			else if (current.flags.HasFlag(CompoundGlyfFlags.WE_HAVE_AN_X_AND_Y_SCALE))
			{
				current.a = data.ReadInt16(true) / 16384f;
				current.d = data.ReadInt16(true) / 16384f;
			}
			else if (current.flags.HasFlag(CompoundGlyfFlags.WE_HAVE_A_TWO_BY_TWO))
			{
				current.a = data.ReadInt16(true) / 16384f;
				current.b = data.ReadInt16(true) / 16384f;
				current.c = data.ReadInt16(true) / 16384f;
				current.d = data.ReadInt16(true) / 16384f;
			}
			if (current.flags.HasFlag(CompoundGlyfFlags.WE_HAVE_INSTRUCTIONS))
			{
				hasInstructions = true;
			}
			current.ComputeTransform();
			comps.Add(current);
		}
		while ((current.flags & CompoundGlyfFlags.MORE_COMPONENTS) != 0);
		Components = comps.ToArray();
		byte[] instructions;
		if (hasInstructions)
		{
			int instructionsCount = data.ReadInt16(true);
			instructions = new byte[instructionsCount];
			for (int i = 0; i < instructionsCount; i++)
			{
				instructions[i] = data.ReadByte();
			}
		}
		else
		{
			instructions = new byte[0];
		}
		Instructions = instructions;
	}

	public override IEnumerable<IEnumerable<(float X, float Y, bool onCurve)>> Contours
	{
		get
		{
			return Components.SelectMany(
				component=>
				{
					var glyph = GlyfTable.GetGlyph(component.GlyphIndex);
					return glyph.Contours.Select(cs => cs.Select(c => component.Transform(c)));
				}
			);
		}
	}
}
