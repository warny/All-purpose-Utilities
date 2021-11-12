using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Fonts.TTF
{
	public class OpenFontTableTypes
	{
		public enum Tags
		{
			/// <summary>
			/// Character to glyph mapping.  Table tag ""cmap"" in the Open Type Specification.
			/// </summary>
			CMAP = 0x636d6170,
			/// <summary>
			/// Font header.  Table tag ""head"" in the Open Type Specification.
			/// </summary>
			HEAD = 0x68656164,
			/// <summary>
			/// Naming table.  Table tag ""name"" in the Open Type Specification.
			/// </summary>
			NAME = 0x6e616d65,
			/// <summary>
			/// Glyph data.  Table tag ""glyf"" in the Open Type Specification.
			/// </summary>
			GLYF = 0x676c7966,
			/// <summary>
			/// Maximum profile.  Table tag ""maxp"" in the Open Type Specification.
			/// </summary>
			MAXP = 0x6d617870,
			/// <summary>
			/// CVT preprogram.  Table tag ""prep"" in the Open Type Specification.
			/// </summary>
			PREP = 0x70726570,
			/// <summary>
			/// Horizontal metrics.  Table tag ""hmtx"" in the Open Type Specification.
			/// </summary>
			HMTX = 0x686d7478,
			/// <summary>
			/// Kerning.  Table tag ""kern"" in the Open Type Specification.
			/// </summary>
			KERN = 0x6b65726e,
			/// <summary>
			/// Horizontal device metrics.  Table tag ""hdmx"" in the Open Type Specification.
			/// </summary>
			HDMX = 0x68646d78,
			/// <summary>
			/// Index to location.  Table tag ""loca"" in the Open Type Specification.
			/// </summary>
			LOCA = 0x6c6f6361,
			/// <summary>
			/// PostScript Information.  Table tag ""post"" in the Open Type Specification.
			/// </summary>
			POST = 0x706f7374,
			/// <summary>
			/// OS/2 and Windows specific metrics.  Table tag ""OS/2"" in the Open Type Specification.
			/// </summary>
			OS_2 = 0x4f532f32,
			/// <summary>
			/// Control value table.  Table tag ""cvt "" in the Open Type Specification.
			/// </summary>
			CVT = 0x63767420,
			/// <summary>
			/// Grid-fitting and scan conversion procedure.  Table tag ""gasp"" in the Open Type Specification.
			/// </summary>
			GASP = 0x67617370,
			/// <summary>
			/// Vertical device metrics.  Table tag ""VDMX"" in the Open Type Specification.
			/// </summary>
			VDMX = 0x56444d58,
			/// <summary>
			/// Vertical metrics.  Table tag ""vmtx"" in the Open Type Specification.
			/// </summary>
			VMTX = 0x766d7478,
			/// <summary>
			/// Vertical metrics header.  Table tag ""vhea"" in the Open Type Specification.
			/// </summary>
			VHEA = 0x76686561,
			/// <summary>
			/// Horizontal metrics header.  Table tag ""hhea"" in the Open Type Specification.
			/// </summary>
			HHEA = 0x68686561,
			/// <summary>
			/// Adobe Type 1 font data.  Table tag ""typ1"" in the Open Type Specification.
			/// </summary>
			TYP1 = 0x74797031,
			/// <summary>
			/// Baseline table.  Table tag ""bsln"" in the Open Type Specification.
			/// </summary>
			BSLN = 0x62736c6e,
			/// <summary>
			/// Glyph substitution.  Table tag ""GSUB"" in the Open Type Specification.
			/// </summary>
			GSUB = 0x47535542,
			/// <summary>
			/// Digital signature.  Table tag ""DSIG"" in the Open Type Specification.
			/// </summary>
			DSIG = 0x44534947,
			/// <summary>
			/// Font program.   Table tag ""fpgm"" in the Open Type Specification.
			/// </summary>
			FPGM = 0x6670676d,
			/// <summary>
			/// Font variation.   Table tag ""fvar"" in the Open Type Specification.
			/// </summary>
			FVAR = 0x66766172,
			/// <summary>
			/// Glyph variation.  Table tag ""gvar"" in the Open Type Specification.
			/// </summary>
			GVAR = 0x67766172,
			/// <summary>
			/// Compact font format (Type1 font).  Table tag ""CFF "" in the Open Type Specification.
			/// </summary>
			CFF = 0x43464620,
			/// <summary>
			/// Multiple master supplementary data.  Table tag ""MMSD"" in the Open Type Specification.
			/// </summary>
			MMSD = 0x4d4d5344,
			/// <summary>
			/// Multiple master font metrics.  Table tag ""MMFX"" in the Open Type Specification.
			/// </summary>
			MMFX = 0x4d4d4658,
			/// <summary>
			/// Baseline data.  Table tag ""BASE"" in the Open Type Specification.
			/// </summary>
			BASE = 0x42415345,
			/// <summary>
			/// Glyph definition.  Table tag ""GDEF"" in the Open Type Specification.
			/// </summary>
			GDEF = 0x47444546,
			/// <summary>
			/// Glyph positioning.  Table tag ""GPOS"" in the Open Type Specification.
			/// </summary>
			GPOS = 0x47504f53,
			/// <summary>
			/// Justification.  Table tag ""JSTF"" in the Open Type Specification.
			/// </summary>
			JSTF = 0x4a535446,
			/// <summary>
			/// Embedded bitmap data.  Table tag ""EBDT"" in the Open Type Specification.
			/// </summary>
			EBDT = 0x45424454,
			/// <summary>
			/// Embedded bitmap location.  Table tag ""EBLC"" in the Open Type Specification.
			/// </summary>
			EBLC = 0x45424c43,
			/// <summary>
			/// Embedded bitmap scaling.  Table tag ""EBSC"" in the Open Type Specification.
			/// </summary>
			EBSC = 0x45425343,
			/// <summary>
			/// Linear threshold.  Table tag ""LTSH"" in the Open Type Specification.
			/// </summary>
			LTSH = 0x4c545348,
			/// <summary>
			/// PCL 5 data.  Table tag ""PCLT"" in the Open Type Specification.
			/// </summary>
			PCLT = 0x50434c54,
			/// <summary>
			/// Accent attachment.  Table tag ""acnt"" in the Open Type Specification.
			/// </summary>
			ACNT = 0x61636e74,
			/// <summary>
			/// Axis variation.  Table tag ""avar"" in the Open Type Specification.
			/// </summary>
			AVAR = 0x61766172,
			/// <summary>
			/// Bitmap data.  Table tag ""bdat"" in the Open Type Specification.
			/// </summary>
			BDAT = 0x62646174,
			/// <summary>
			/// Bitmap location.  Table tag ""bloc"" in the Open Type Specification.
			/// </summary>
			BLOC = 0x626c6f63,
			/// <summary>
			/// CVT variation.  Table tag ""cvar"" in the Open Type Specification.
			/// </summary>
			CVAR = 0x63766172,
			/// <summary>
			/// Feature name.  Table tag ""feat"" in the OpenType Specification.
			/// </summary>
			FEAT = 0x66656174,
			/// <summary>
			/// Font descriptors.  Table tag ""fdsc"" in the Open Type Specification.
			/// </summary>
			FDSC = 0x66647363,
			/// <summary>
			/// Font metrics.  Table tag ""fmtx"" in the Open Type Specification.
			/// </summary>
			FMTX = 0x666d7478,
			/// <summary>
			/// Justification.  Table tag ""just"" in the Open Type Specification.
			/// </summary>
			JUST = 0x6a757374,
			/// <summary>
			/// Ligature caret.   Table tag ""lcar"" in the Open Type Specification.
			/// </summary>
			LCAR = 0x6c636172,
			/// <summary>
			/// Glyph metamorphosis.  Table tag ""mort"" in the Open Type Specification.
			/// </summary>
			MORT = 0x6d6f7274,
			/// <summary>
			/// Optical bounds.  Table tag ""opbd"" in the Open Type Specification.
			/// </summary>
			OPBD = 0x6F706264,
			/// <summary>
			/// Glyph properties.  Table tag ""prop"" in the Open Type Specification.
			/// </summary>
			PROP = 0x70726f70,
			/// <summary>
			/// Tracking.  Table tag ""trak"" in the Open Type Specification.
			/// </summary>
			TRAK = 0x7472616b,
		}

		/// <summary>
		/// Character to glyph mapping.  Table tag "cmap" in the Open Type Specification.
		/// </summary>
		public static Tag CMAP = new Tag((int)Tags.CMAP);
		/// <summary>
		/// Font header.  Table tag "head" in the Open Type Specification.
		/// </summary>
		public static Tag HEAD = new Tag((int)Tags.HEAD);
		/// <summary>
		/// Naming table.  Table tag "name" in the Open Type Specification.
		/// </summary>
		public static Tag NAME = new Tag((int)Tags.NAME);
		/// <summary>
		/// Glyph data.  Table tag "glyf" in the Open Type Specification.
		/// </summary>
		public static Tag GLYF = new Tag((int)Tags.GLYF);
		/// <summary>
		/// Maximum profile.  Table tag "maxp" in the Open Type Specification.
		/// </summary>
		public static Tag MAXP = new Tag((int)Tags.MAXP);
		/// <summary>
		/// CVT preprogram.  Table tag "prep" in the Open Type Specification.
		/// </summary>
		public static Tag PREP = new Tag((int)Tags.PREP);
		/// <summary>
		/// Horizontal metrics.  Table tag "hmtx" in the Open Type Specification.
		/// </summary>
		public static Tag HMTX = new Tag((int)Tags.HMTX);
		/// <summary>
		/// Kerning.  Table tag "kern" in the Open Type Specification.
		/// </summary>
		public static Tag KERN = new Tag((int)Tags.KERN);
		/// <summary>
		/// Horizontal device metrics.  Table tag "hdmx" in the Open Type Specification.
		/// </summary>
		public static Tag HDMX = new Tag((int)Tags.HDMX);
		/// <summary>
		/// Index to location.  Table tag "loca" in the Open Type Specification.
		/// </summary>
		public static Tag LOCA = new Tag((int)Tags.LOCA);
		/// <summary>
		/// PostScript Information.  Table tag "post" in the Open Type Specification.
		/// </summary>
		public static Tag POST = new Tag((int)Tags.POST);
		/// <summary>
		/// OS/2 and Windows specific metrics.  Table tag "OS/2" in the Open Type Specification.
		/// </summary>
		public static Tag OS_2 = new Tag((int)Tags.OS_2);
		/// <summary>
		/// Control value table.  Table tag "cvt " in the Open Type Specification.
		/// </summary>
		public static Tag CVT = new Tag((int)Tags.CVT);
		/// <summary>
		/// Grid-fitting and scan conversion procedure.  Table tag "gasp" in the Open Type Specification.
		/// </summary>
		public static Tag GASP = new Tag((int)Tags.GASP);
		/// <summary>
		/// Vertical device metrics.  Table tag "VDMX" in the Open Type Specification.
		/// </summary>
		public static Tag VDMX = new Tag((int)Tags.VDMX);
		/// <summary>
		/// Vertical metrics.  Table tag "vmtx" in the Open Type Specification.
		/// </summary>
		public static Tag VMTX = new Tag((int)Tags.VMTX);
		/// <summary>
		/// Vertical metrics header.  Table tag "vhea" in the Open Type Specification.
		/// </summary>
		public static Tag VHEA = new Tag((int)Tags.VHEA);
		/// <summary>
		/// Horizontal metrics header.  Table tag "hhea" in the Open Type Specification.
		/// </summary>
		public static Tag HHEA = new Tag((int)Tags.HHEA);
		/// <summary>
		/// Adobe Type 1 font data.  Table tag "typ1" in the Open Type Specification.
		/// </summary>
		public static Tag TYP1 = new Tag((int)Tags.TYP1);
		/// <summary>
		/// Baseline table.  Table tag "bsln" in the Open Type Specification.
		/// </summary>
		public static Tag BSLN = new Tag((int)Tags.BSLN);
		/// <summary>
		/// Glyph substitution.  Table tag "GSUB" in the Open Type Specification.
		/// </summary>
		public static Tag GSUB = new Tag((int)Tags.GSUB);
		/// <summary>
		/// Digital signature.  Table tag "DSIG" in the Open Type Specification.
		/// </summary>
		public static Tag DSIG = new Tag((int)Tags.DSIG);
		/// <summary>
		/// Font program.   Table tag "fpgm" in the Open Type Specification.
		/// </summary>
		public static Tag FPGM = new Tag((int)Tags.FPGM);
		/// <summary>
		/// Font variation.   Table tag "fvar" in the Open Type Specification.
		/// </summary>
		public static Tag FVAR = new Tag((int)Tags.FVAR);
		/// <summary>
		/// Glyph variation.  Table tag "gvar" in the Open Type Specification.
		/// </summary>
		public static Tag GVAR = new Tag((int)Tags.GVAR);
		/// <summary>
		/// Compact font format (Type1 font).  Table tag "CFF " in the Open Type Specification.
		/// </summary>
		public static Tag CFF = new Tag((int)Tags.CFF);
		/// <summary>
		/// Multiple master supplementary data.  Table tag "MMSD" in the Open Type Specification.
		/// </summary>
		public static Tag MMSD = new Tag((int)Tags.MMSD);
		/// <summary>
		/// Multiple master font metrics.  Table tag "MMFX" in the Open Type Specification.
		/// </summary>
		public static Tag MMFX = new Tag((int)Tags.MMFX);
		/// <summary>
		/// Baseline data.  Table tag "BASE" in the Open Type Specification.
		/// </summary>
		public static Tag BASE = new Tag((int)Tags.BASE);
		/// <summary>
		/// Glyph definition.  Table tag "GDEF" in the Open Type Specification.
		/// </summary>
		public static Tag GDEF = new Tag((int)Tags.GDEF);
		/// <summary>
		/// Glyph positioning.  Table tag "GPOS" in the Open Type Specification.
		/// </summary>
		public static Tag GPOS = new Tag((int)Tags.GPOS);
		/// <summary>
		/// Justification.  Table tag "JSTF" in the Open Type Specification.
		/// </summary>
		public static Tag JSTF = new Tag((int)Tags.JSTF);
		/// <summary>
		/// Embedded bitmap data.  Table tag "EBDT" in the Open Type Specification.
		/// </summary>
		public static Tag EBDT = new Tag((int)Tags.EBDT);
		/// <summary>
		/// Embedded bitmap location.  Table tag "EBLC" in the Open Type Specification.
		/// </summary>
		public static Tag EBLC = new Tag((int)Tags.EBLC);
		/// <summary>
		/// Embedded bitmap scaling.  Table tag "EBSC" in the Open Type Specification.
		/// </summary>
		public static Tag EBSC = new Tag((int)Tags.EBSC);
		/// <summary>
		/// Linear threshold.  Table tag "LTSH" in the Open Type Specification.
		/// </summary>
		public static Tag LTSH = new Tag((int)Tags.LTSH);
		/// <summary>
		/// PCL 5 data.  Table tag "PCLT" in the Open Type Specification.
		/// </summary>
		public static Tag PCLT = new Tag((int)Tags.PCLT);
		/// <summary>
		/// Accent attachment.  Table tag "acnt" in the Open Type Specification.
		/// </summary>
		public static Tag ACNT = new Tag((int)Tags.ACNT);
		/// <summary>
		/// Axis variation.  Table tag "avar" in the Open Type Specification.
		/// </summary>
		public static Tag AVAR = new Tag((int)Tags.AVAR);
		/// <summary>
		/// Bitmap data.  Table tag "bdat" in the Open Type Specification.
		/// </summary>
		public static Tag BDAT = new Tag((int)Tags.BDAT);
		/// <summary>
		/// Bitmap location.  Table tag "bloc" in the Open Type Specification.
		/// </summary>
		public static Tag BLOC = new Tag((int)Tags.BLOC);
		/// <summary>
		/// CVT variation.  Table tag "cvar" in the Open Type Specification.
		/// </summary>
		public static Tag CVAR = new Tag((int)Tags.CVAR);
		/// <summary>
		/// Feature name.  Table tag "feat" in the OpenType Specification.
		/// </summary>
		public static Tag FEAT = new Tag((int)Tags.FEAT);
		/// <summary>
		/// Font descriptors.  Table tag "fdsc" in the Open Type Specification.
		/// </summary>
		public static Tag FDSC = new Tag((int)Tags.FDSC);
		/// <summary>
		/// Font metrics.  Table tag "fmtx" in the Open Type Specification.
		/// </summary>
		public static Tag FMTX = new Tag((int)Tags.FMTX);
		/// <summary>
		/// Justification.  Table tag "just" in the Open Type Specification.
		/// </summary>
		public static Tag JUST = new Tag((int)Tags.JUST);
		/// <summary>
		/// Ligature caret.   Table tag "lcar" in the Open Type Specification.
		/// </summary>
		public static Tag LCAR = new Tag((int)Tags.LCAR);
		/// <summary>
		/// Glyph metamorphosis.  Table tag "mort" in the Open Type Specification.
		/// </summary>
		public static Tag MORT = new Tag((int)Tags.MORT);
		/// <summary>
		/// Optical bounds.  Table tag "opbd" in the Open Type Specification.
		/// </summary>
		public static Tag OPBD = new Tag((int)Tags.OPBD);
		/// <summary>
		/// Glyph properties.  Table tag "prop" in the Open Type Specification.
		/// </summary>
		public static Tag PROP = new Tag((int)Tags.PROP);
		/// <summary>
		/// Tracking.  Table tag "trak" in the Open Type Specification.
		/// </summary>
		public static Tag TRAK = new Tag((int)Tags.TRAK);
	}
}
