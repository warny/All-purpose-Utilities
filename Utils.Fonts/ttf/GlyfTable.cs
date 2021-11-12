using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF
{
		public class GlyfTable : TrueTypeTable
	{
		private Glyf[] glyfs;
		private LocaTable loca;
		private MaxpTable maxp;

		public virtual Glyf GetGlyph(int i) => glyfs[i];

		protected internal GlyfTable() : base(TrueTypeTableTypes.glyf) { }

		public override TrueTypeFont TrueTypeFont { 
			get => base.TrueTypeFont;
			protected set
			{
				base.TrueTypeFont = value;
				loca = TrueTypeFont.GetTable<LocaTable>(TrueTypeTableTypes.loca);
				maxp = TrueTypeFont.GetTable<MaxpTable>(TrueTypeTableTypes.maxp);
				int numGlyphs = maxp.NumGlyphs;
				glyfs = new Glyf[numGlyphs];
			}
		}

		public override int Length => glyfs.Sum(g => g.Length);

		public override void WriteData(Writer data)
		{
			foreach (var glyf in glyfs)
			{
				glyf?.WriteData(data);
			}
		}

		public override void ReadData(Reader data)
		{
			for (int i = 0; i < glyfs.Length; i++)
			{
				int offset = loca.GetOffset(i);
				int size = loca.GetSize(i);
				if (size != 0)
				{
					glyfs[i] = Glyf.CreateGlyf(data.Slice(offset, size));
				}
			}
		}

		public override string ToString()
		{
			StringBuilder val = new StringBuilder();
			val.AppendLine($"    Glyf Table: ({glyfs.Length} glyphs)");
			val.AppendLine($"      Glyf 0: {(object)GetGlyph(0)}");
			return val.ToString();
		}
	}
}
