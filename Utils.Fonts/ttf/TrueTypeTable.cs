using System;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF
{
	public class TrueTypeTable
	{

		public Tag Tag { get; }
		public virtual TrueTypeFont TrueTypeFont { get; protected set; }

		private byte[] data;

		public static TrueTypeTable CreateTable(TrueTypeFont ttf, Tag tag, Reader data = null)
		{
			TrueTypeTable trueTypeTable = (TrueTypeTableTypes.Tags)tag.Value switch
			{
				TrueTypeTableTypes.Tags.cmap => new CmapTable(),
				TrueTypeTableTypes.Tags.glyf => new GlyfTable(),
				TrueTypeTableTypes.Tags.head => new HeadTable(),
				TrueTypeTableTypes.Tags.hhea => new HheaTable(),
				TrueTypeTableTypes.Tags.hmtx => new HmtxTable(),
				TrueTypeTableTypes.Tags.loca => new LocaTable(),
				TrueTypeTableTypes.Tags.maxp => new MaxpTable(),
				TrueTypeTableTypes.Tags.name => new NameTable(),
				TrueTypeTableTypes.Tags.post => new PostTable(),
				_ => new TrueTypeTable(tag),
			};
			if (data != null)
			{
				trueTypeTable.TrueTypeFont = ttf;
				trueTypeTable.ReadData(data);
			}
			return trueTypeTable;
		}

		protected internal TrueTypeTable(Tag i)
		{
			Tag = i;
		}

		public virtual void ReadData(Reader data)
		{
			this.data = data.ReadBytes((int)data.BytesLeft);
		}

		public virtual void WriteData(Writer data)
		{
			data.WriteBytes(this.data);
		}

		public virtual int Length => data.Length;

		public override string ToString()
		{
			string text = $"    {Tag} Table.  Data is: ";
			if (this.data == null)
			{
				return text + "not set";
			}
			return text + "set";
		}
	}
    public static class TrueTypeTableTypes
    {
        public enum Tags {
            /// <summary>
            /// accent attachment
            /// </summary>
            acnt = 0x61636E74,
            /// <summary>
            /// anchor point
            /// </summary>
            ankr = 0x616E6B72,
            /// <summary>
            /// axis variation
            /// </summary>
            avar = 0x61766172,
            /// <summary>
            /// bitmap data
            /// </summary>
            bdat = 0x62646174,
            /// <summary>
            /// bitmap font header
            /// </summary>
            bhed = 0x62686564,
            /// <summary>
            /// bitmap location
            /// </summary>
            bloc = 0x626C6F63,
            /// <summary>
            /// baseline
            /// </summary>
            bsln = 0x62736C6E,
            /// <summary>
            /// character code mapping
            /// </summary>
            cmap = 0x636D6170,
            /// <summary>
            /// CVT variation
            /// </summary>
            cvar = 0x63766172,
            /// <summary>
            /// control value
            /// </summary>
            cvt = 0x63767420,
            /// <summary>
            /// embedded bitmap scaling control
            /// </summary>
            EBSC = 0x45425343,
            /// <summary>
            /// font descriptor
            /// </summary>
            fdsc = 0x66647363,
            /// <summary>
            /// layout feature
            /// </summary>
            feat = 0x66656174,
            /// <summary>
            /// font metrics
            /// </summary>
            fmtx = 0x666D7478,
            /// <summary>
            /// font family compatibility
            /// </summary>
            fond = 0x666F6E64,
            /// <summary>
            /// font program
            /// </summary>
            fpgm = 0x6670676D,
            /// <summary>
            /// font variation
            /// </summary>
            fvar = 0x66766172,
            /// <summary>
            /// grid-fitting and scan-conversion procedure
            /// </summary>
            gasp = 0x67617370,
            /// <summary>
            /// glyph outline
            /// </summary>
            glyf = 0x676C7966,
            /// <summary>
            /// glyph variation
            /// </summary>
            gvar = 0x67766172,
            /// <summary>
            /// horizontal device metrics
            /// </summary>
            hdmx = 0x68646D78,
            /// <summary>
            /// font header
            /// </summary>
            head = 0x68656164,
            /// <summary>
            /// horizontal header
            /// </summary>
            hhea = 0x68686561,
            /// <summary>
            /// horizontal metrics
            /// </summary>
            hmtx = 0x686D7478,
            /// <summary>
            /// justification
            /// </summary>
            just = 0x6A757374,
            /// <summary>
            /// kerning
            /// </summary>
            kern = 0x6B65726E,
            /// <summary>
            /// extended kerning
            /// </summary>
            kerx = 0x6B657278,
            /// <summary>
            /// ligature caret
            /// </summary>
            lcar = 0x6C636172,
            /// <summary>
            /// glyph location
            /// </summary>
            loca = 0x6C6F6361,
            /// <summary>
            /// language tag
            /// </summary>
            ltag = 0x6C746167,
            /// <summary>
            /// maximum profile
            /// </summary>
            maxp = 0x6D617870,
            /// <summary>
            /// metadata
            /// </summary>
            meta = 0x6D657461,
            /// <summary>
            /// metamorphosis table (deprecated)
            /// </summary>
            mort = 0x6D6F7274,
            /// <summary>
            /// extended metamorphosis
            /// </summary>
            morx = 0x6D6F7278,
            /// <summary>
            /// name
            /// </summary>
            name = 0x6E616D65,
            /// <summary>
            /// optical bounds
            /// </summary>
            opbd = 0x6F706264,
            /// <summary>
            /// compatibility
            /// </summary>
            OS_2 = 0x4F532F32,
            /// <summary>
            /// glyph name and PostScript compatibility
            /// </summary>
            post = 0x706F7374,
            /// <summary>
            /// control value program
            /// </summary>
            prep = 0x70726570,
            /// <summary>
            /// properties
            /// </summary>
            prop = 0x70726F70,
            /// <summary>
            /// extended bitmaps
            /// </summary>
            sbix = 0x73626978,
            /// <summary>
            /// tracking
            /// </summary>
            trak = 0x7472616B,
            /// <summary>
            /// vertical header
            /// </summary>
            vhea = 0x76686561,
            /// <summary>
            /// vertical metrics
            /// </summary>
            vmtx = 0x766D7478,
            /// <summary>
            /// cross-reference
            /// </summary>
            xref = 0x78726566,
            /// <summary>
            /// glyph reference
            /// </summary>
            Zapf = 0x5A617066,
        }
        /// <summary>
        /// accent attachment
        /// </summary>
        public static readonly Tag acnt = new Tag((int)Tags.acnt);
        /// <summary>
        /// anchor point
        /// </summary>
        public static readonly Tag ankr = new Tag((int)Tags.ankr);
        /// <summary>
        /// axis variation
        /// </summary>
        public static readonly Tag avar = new Tag((int)Tags.avar);
        /// <summary>
        /// bitmap data
        /// </summary>
        public static readonly Tag bdat = new Tag((int)Tags.bdat);
        /// <summary>
        /// bitmap font header
        /// </summary>
        public static readonly Tag bhed = new Tag((int)Tags.bhed);
        /// <summary>
        /// bitmap location
        /// </summary>
        public static readonly Tag bloc = new Tag((int)Tags.bloc);
        /// <summary>
        /// baseline
        /// </summary>
        public static readonly Tag bsln = new Tag((int)Tags.bsln);
        /// <summary>
        /// character code mapping
        /// </summary>
        public static readonly Tag cmap = new Tag((int)Tags.cmap);
        /// <summary>
        /// CVT variation
        /// </summary>
        public static readonly Tag cvar = new Tag((int)Tags.cvar);
        /// <summary>
        /// control value
        /// </summary>
        public static readonly Tag cvt = new Tag((int)Tags.cvt);
        /// <summary>
        /// embedded bitmap scaling control
        /// </summary>
        public static readonly Tag EBSC = new Tag((int)Tags.EBSC);
        /// <summary>
        /// font descriptor
        /// </summary>
        public static readonly Tag fdsc = new Tag((int)Tags.fdsc);
        /// <summary>
        /// layout feature
        /// </summary>
        public static readonly Tag feat = new Tag((int)Tags.feat);
        /// <summary>
        /// font metrics
        /// </summary>
        public static readonly Tag fmtx = new Tag((int)Tags.fmtx);
        /// <summary>
        /// font family compatibility
        /// </summary>
        public static readonly Tag fond = new Tag((int)Tags.fond);
        /// <summary>
        /// font program
        /// </summary>
        public static readonly Tag fpgm = new Tag((int)Tags.fpgm);
        /// <summary>
        /// font variation
        /// </summary>
        public static readonly Tag fvar = new Tag((int)Tags.fvar);
        /// <summary>
        /// grid-fitting and scan-conversion procedure
        /// </summary>
        public static readonly Tag gasp = new Tag((int)Tags.gasp);
        /// <summary>
        /// glyph outline
        /// </summary>
        public static readonly Tag glyf = new Tag((int)Tags.glyf);
        /// <summary>
        /// glyph variation
        /// </summary>
        public static readonly Tag gvar = new Tag((int)Tags.gvar);
        /// <summary>
        /// horizontal device metrics
        /// </summary>
        public static readonly Tag hdmx = new Tag((int)Tags.hdmx);
        /// <summary>
        /// font header
        /// </summary>
        public static readonly Tag head = new Tag((int)Tags.head);
        /// <summary>
        /// horizontal header
        /// </summary>
        public static readonly Tag hhea = new Tag((int)Tags.hhea);
        /// <summary>
        /// horizontal metrics
        /// </summary>
        public static readonly Tag hmtx = new Tag((int)Tags.hmtx);
        /// <summary>
        /// justification
        /// </summary>
        public static readonly Tag just = new Tag((int)Tags.just);
        /// <summary>
        /// kerning
        /// </summary>
        public static readonly Tag kern = new Tag((int)Tags.kern);
        /// <summary>
        /// extended kerning
        /// </summary>
        public static readonly Tag kerx = new Tag((int)Tags.kerx);
        /// <summary>
        /// ligature caret
        /// </summary>
        public static readonly Tag lcar = new Tag((int)Tags.lcar);
        /// <summary>
        /// glyph location
        /// </summary>
        public static readonly Tag loca = new Tag((int)Tags.loca);
        /// <summary>
        /// language tag
        /// </summary>
        public static readonly Tag ltag = new Tag((int)Tags.ltag);
        /// <summary>
        /// maximum profile
        /// </summary>
        public static readonly Tag maxp = new Tag((int)Tags.maxp);
        /// <summary>
        /// metadata
        /// </summary>
        public static readonly Tag meta = new Tag((int)Tags.meta);
        /// <summary>
        /// metamorphosis table (deprecated)
        /// </summary>
        public static readonly Tag mort = new Tag((int)Tags.mort);
        /// <summary>
        /// extended metamorphosis
        /// </summary>
        public static readonly Tag morx = new Tag((int)Tags.morx);
        /// <summary>
        /// name
        /// </summary>
        public static readonly Tag name = new Tag((int)Tags.name);
        /// <summary>
        /// optical bounds
        /// </summary>
        public static readonly Tag opbd = new Tag((int)Tags.opbd);
        /// <summary>
        /// compatibility
        /// </summary>
        public static readonly Tag OS_2 = new Tag((int)Tags.OS_2);
        /// <summary>
        /// glyph name and PostScript compatibility
        /// </summary>
        public static readonly Tag post = new Tag((int)Tags.post);
        /// <summary>
        /// control value program
        /// </summary>
        public static readonly Tag prep = new Tag((int)Tags.prep);
        /// <summary>
        /// properties
        /// </summary>
        public static readonly Tag prop = new Tag((int)Tags.prop);
        /// <summary>
        /// extended bitmaps
        /// </summary>
        public static readonly Tag sbix = new Tag((int)Tags.sbix);
        /// <summary>
        /// tracking
        /// </summary>
        public static readonly Tag trak = new Tag((int)Tags.trak);
        /// <summary>
        /// vertical header
        /// </summary>
        public static readonly Tag vhea = new Tag((int)Tags.vhea);
        /// <summary>
        /// vertical metrics
        /// </summary>
        public static readonly Tag vmtx = new Tag((int)Tags.vmtx);
        /// <summary>
        /// cross-reference
        /// </summary>
        public static readonly Tag xref = new Tag((int)Tags.xref);
        /// <summary>
        /// glyph reference
        /// </summary>
        public static readonly Tag Zapf = new Tag((int)Tags.Zapf);
    }
}

