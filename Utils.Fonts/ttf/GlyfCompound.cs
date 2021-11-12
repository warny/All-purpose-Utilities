using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF
{
	public class GlyfCompound : Glyf
	{
		internal class GlyfComponent
		{
			public CompoundGlyfFlags flags;
			public short glyphIndex;
			public int compoundPoint;
			public int componentPoint;
			public float a = 1f;
			public float b = 0f;
			public float c = 0f;
			public float d = 1f;
			public float e = 0f;
			public float f = 0f;
		}

		private GlyfComponent[] Components { get; set; }

		private byte[] Instructions { get; set; }

		public virtual int NumComponents => Components.Length;

		public virtual short getGlyphIndex(int i) => Components[i].glyphIndex;

		public virtual double[] getTransform(int i)
		{
			const float limit = (33f / 65535f);
			GlyfComponent gc = Components[i];
			float m = Math.Max(Math.Abs(gc.a), Math.Abs(gc.b));
			if (Math.Abs(Math.Abs(gc.a) - Math.Abs(gc.c)) < limit)
			{
				m *= 2f;
			}
			float n = Math.Max(Math.Abs(gc.c), Math.Abs(gc.d));
			if (Math.Abs(Math.Abs(gc.c) - Math.Abs(gc.d)) < limit)
			{
				n *= 2f;
			}
			float e = m * gc.e;
			float f = n * gc.f;
			return new double[6] { gc.a, gc.b, gc.c, gc.d, e, f };
		}

		protected internal GlyfCompound() { }

		public virtual CompoundGlyfFlags getFlag(int i)
		{
			return Components[i].flags;
		}

		public override void ReadData(Reader data)
		{
			List<GlyfComponent> comps = new List<GlyfComponent>();
			bool hasInstructions = false;
			GlyfComponent current;
			do
			{
				current = new GlyfComponent();
				current.flags = (CompoundGlyfFlags)data.ReadInt16(true);
				current.glyphIndex = data.ReadInt16(true);
				if ((current.flags & CompoundGlyfFlags.ARG_1_AND_2_ARE_WORDS) != 0 && (current.flags & CompoundGlyfFlags.ARGS_ARE_XY_VALUES) != 0)
				{
					current.e = data.ReadInt16(true);
					current.f = data.ReadInt16(true);
				}
				else if ((current.flags & CompoundGlyfFlags.ARG_1_AND_2_ARE_WORDS) == 0 && (current.flags & CompoundGlyfFlags.ARGS_ARE_XY_VALUES) != 0)
				{
					current.e = data.ReadByte();
					current.f = data.ReadByte();
				}
				else if ((current.flags & CompoundGlyfFlags.ARG_1_AND_2_ARE_WORDS) != 0 && (current.flags & CompoundGlyfFlags.ARGS_ARE_XY_VALUES) == 0)
				{
					current.compoundPoint = data.ReadInt16(true);
					current.componentPoint = data.ReadInt16(true);
				}
				else
				{
					current.compoundPoint = data.ReadByte();
					current.componentPoint = data.ReadByte();
				}
				if ((current.flags & CompoundGlyfFlags.WE_HAVE_A_SCALE) != 0)
				{
					current.a = data.ReadInt16(true) / 16384f;
					current.d = current.a;
				}
				else if ((current.flags & CompoundGlyfFlags.WE_HAVE_AN_X_AND_Y_SCALE) != 0)
				{
					current.a = data.ReadInt16(true) / 16384f;
					current.d = data.ReadInt16(true) / 16384f;
				}
				else if ((current.flags & CompoundGlyfFlags.WE_HAVE_A_TWO_BY_TWO) != 0)
				{
					current.a = data.ReadInt16(true) / 16384f;
					current.b = data.ReadInt16(true) / 16384f;
					current.c = data.ReadInt16(true) / 16384f;
					current.d = data.ReadInt16(true) / 16384f;
				}
				if ((current.flags & CompoundGlyfFlags.WE_HAVE_INSTRUCTIONS) != 0)
				{
					hasInstructions = true;
				}
				comps.Add(current);
			}
			while ((current.flags & CompoundGlyfFlags.MORE_COMPONENTS) != 0);
			Components = comps.ToArray();
			byte[] instructions;
			if (hasInstructions)
			{
				int @short = data.ReadInt16(true);
				instructions = new byte[@short];
				for (int i = 0; i < instructions.Length; i++)
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

		public virtual int GetCompoundPoint(int i) => Components[i].compoundPoint;
		public virtual int GetComponentPoint(int i) => Components[i].componentPoint;
		public virtual bool ArgsAreWords(int i) => ((getFlag(i) & CompoundGlyfFlags.ARG_1_AND_2_ARE_WORDS) != 0);
		public virtual bool ArgsAreXYValues(int i) => ((getFlag(i) & CompoundGlyfFlags.ARGS_ARE_XY_VALUES) != 0);
		public virtual bool RoundXYToGrid(int i) => ((getFlag(i) & CompoundGlyfFlags.ROUND_XY_TO_GRID) != 0);
		public virtual bool HasAScale(int i) => ((getFlag(i) & CompoundGlyfFlags.WE_HAVE_A_SCALE) != 0);
		protected internal virtual bool MoreComponents(int i) => ((getFlag(i) & CompoundGlyfFlags.MORE_COMPONENTS) != 0);
		protected internal virtual bool HasXYScale(int i) => ((getFlag(i) & CompoundGlyfFlags.WE_HAVE_AN_X_AND_Y_SCALE) != 0);
		protected internal virtual bool HasTwoByTwo(int i) => ((getFlag(i) & CompoundGlyfFlags.WE_HAVE_A_TWO_BY_TWO) != 0);
		protected internal virtual bool HasInstructions(int i) => ((getFlag(i) & CompoundGlyfFlags.WE_HAVE_INSTRUCTIONS) != 0);
		public virtual bool UseMetrics(int i) => ((getFlag(i) & CompoundGlyfFlags.USE_MY_METRICS) != 0);
		public virtual bool OverlapCompound(int i) => ((getFlag(i) & CompoundGlyfFlags.OVERLAP_COMPOUND) != 0);
		public virtual short NumInstructions => (short)Instructions.Length;
		public virtual byte GetInstruction(int i) => Instructions[i];
	}

	[Flags]
	public enum CompoundGlyfFlags : short
	{
		ARG_1_AND_2_ARE_WORDS = 0x0001,
		ARGS_ARE_XY_VALUES = 0x0002,
		ROUND_XY_TO_GRID = 0x0004,
		WE_HAVE_A_SCALE = 0x0008,
		MORE_COMPONENTS = 0x0020,
		WE_HAVE_AN_X_AND_Y_SCALE = 0x0040,
		WE_HAVE_A_TWO_BY_TWO = 0x0080,
		WE_HAVE_INSTRUCTIONS = 0x0100,
		USE_MY_METRICS = 0x0200,
		OVERLAP_COMPOUND = 0x0400
	}
}
