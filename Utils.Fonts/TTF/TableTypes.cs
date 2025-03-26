using System;

namespace Utils.Fonts.TTF;

/// <summary>
/// Provides constants for known OpenType table tags.
/// Includes well-known system tables such as "cmap", "glyf", "name", etc.
/// </summary>
public class TableTypes
{
	/// <summary>
	/// Tags for OpenType font tables, using their 4-byte integer encodings.
	/// </summary>
	public enum Tags
	{
		/// <summary>
		/// Character to glyph mapping. Table tag "cmap" in the OpenType Specification.
		/// </summary>
		CMAP = 0x636d6170,
		/// <summary>
		/// Font header. Table tag "head" in the OpenType Specification.
		/// </summary>
		HEAD = 0x68656164,
		/// <summary>
		/// Naming table. Table tag "name" in the OpenType Specification.
		/// </summary>
		NAME = 0x6e616d65,
		/// <summary>
		/// Glyph data. Table tag "glyf" in the OpenType Specification.
		/// </summary>
		GLYF = 0x676c7966,
		/// <summary>
		/// Maximum profile. Table tag "maxp" in the OpenType Specification.
		/// </summary>
		MAXP = 0x6d617870,
		/// <summary>
		/// CVT preprogram. Table tag "prep" in the OpenType Specification.
		/// </summary>
		PREP = 0x70726570,
		/// <summary>
		/// Horizontal metrics. Table tag "hmtx" in the OpenType Specification.
		/// </summary>
		HMTX = 0x686d7478,
		/// <summary>
		/// Kerning. Table tag "kern" in the OpenType Specification.
		/// </summary>
		KERN = 0x6b65726e,
		/// <summary>
		/// Horizontal device metrics. Table tag "hdmx" in the OpenType Specification.
		/// </summary>
		HDMX = 0x68646d78,
		/// <summary>
		/// Index to location. Table tag "loca" in the OpenType Specification.
		/// </summary>
		LOCA = 0x6c6f6361,
		/// <summary>
		/// PostScript Information. Table tag "post" in the OpenType Specification.
		/// </summary>
		POST = 0x706f7374,
		/// <summary>
		/// OS/2 and Windows specific metrics. Table tag "OS/2" in the OpenType Specification.
		/// </summary>
		OS_2 = 0x4f532f32,
		/// <summary>
		/// Control value table. Table tag "cvt " in the OpenType Specification.
		/// </summary>
		CVT = 0x63767420,
		/// <summary>
		/// Grid-fitting and scan conversion procedure. Table tag "gasp" in the OpenType Specification.
		/// </summary>
		GASP = 0x67617370,
		/// <summary>
		/// Vertical device metrics. Table tag "VDMX" in the OpenType Specification.
		/// </summary>
		VDMX = 0x56444d58,
		/// <summary>
		/// Vertical metrics. Table tag "vmtx" in the OpenType Specification.
		/// </summary>
		VMTX = 0x766d7478,
		/// <summary>
		/// Vertical metrics header. Table tag "vhea" in the OpenType Specification.
		/// </summary>
		VHEA = 0x76686561,
		/// <summary>
		/// Horizontal metrics header. Table tag "hhea" in the OpenType Specification.
		/// </summary>
		HHEA = 0x68686561,
		/// <summary>
		/// Adobe Type 1 font data. Table tag "typ1" in the OpenType Specification.
		/// </summary>
		TYP1 = 0x74797031,
		/// <summary>
		/// Baseline table. Table tag "bsln" in the OpenType Specification.
		/// </summary>
		BSLN = 0x62736c6e,
		/// <summary>
		/// Glyph substitution. Table tag "GSUB" in the OpenType Specification.
		/// </summary>
		GSUB = 0x47535542,
		/// <summary>
		/// Digital signature. Table tag "DSIG" in the OpenType Specification.
		/// </summary>
		DSIG = 0x44534947,
		/// <summary>
		/// Font program. Table tag "fpgm" in the OpenType Specification.
		/// </summary>
		FPGM = 0x6670676d,
		/// <summary>
		/// Font variation. Table tag "fvar" in the OpenType Specification.
		/// </summary>
		FVAR = 0x66766172,
		/// <summary>
		/// Glyph variation. Table tag "gvar" in the OpenType Specification.
		/// </summary>
		GVAR = 0x67766172,
		/// <summary>
		/// Compact font format (Type1 font). Table tag "CFF " in the OpenType Specification.
		/// </summary>
		CFF = 0x43464620,
		/// <summary>
		/// Multiple master supplementary data. Table tag "MMSD" in the OpenType Specification.
		/// </summary>
		MMSD = 0x4d4d5344,
		/// <summary>
		/// Multiple master font metrics. Table tag "MMFX" in the OpenType Specification.
		/// </summary>
		MMFX = 0x4d4d4658,
		/// <summary>
		/// Baseline data. Table tag "BASE" in the OpenType Specification.
		/// </summary>
		BASE = 0x42415345,
		/// <summary>
		/// Glyph definition. Table tag "GDEF" in the OpenType Specification.
		/// </summary>
		GDEF = 0x47444546,
		/// <summary>
		/// Glyph positioning. Table tag "GPOS" in the OpenType Specification.
		/// </summary>
		GPOS = 0x47504f53,
		/// <summary>
		/// Justification. Table tag "JSTF" in the OpenType Specification.
		/// </summary>
		JSTF = 0x4a535446,
		/// <summary>
		/// Embedded bitmap data. Table tag "EBDT" in the OpenType Specification.
		/// </summary>
		EBDT = 0x45424454,
		/// <summary>
		/// Embedded bitmap location. Table tag "EBLC" in the OpenType Specification.
		/// </summary>
		EBLC = 0x45424c43,
		/// <summary>
		/// Embedded bitmap scaling. Table tag "EBSC" in the OpenType Specification.
		/// </summary>
		EBSC = 0x45425343,
		/// <summary>
		/// Linear threshold. Table tag "LTSH" in the OpenType Specification.
		/// </summary>
		LTSH = 0x4c545348,
		/// <summary>
		/// PCL 5 data. Table tag "PCLT" in the OpenType Specification.
		/// </summary>
		PCLT = 0x50434c54,
		/// <summary>
		/// Accent attachment. Table tag "acnt" in the OpenType Specification.
		/// </summary>
		ACNT = 0x61636e74,
		/// <summary>
		/// Axis variation. Table tag "avar" in the OpenType Specification.
		/// </summary>
		AVAR = 0x61766172,
		/// <summary>
		/// Bitmap data. Table tag "bdat" in the OpenType Specification.
		/// </summary>
		BDAT = 0x62646174,
		/// <summary>
		/// Bitmap location. Table tag "bloc" in the OpenType Specification.
		/// </summary>
		BLOC = 0x626c6f63,
		/// <summary>
		/// CVT variation. Table tag "cvar" in the OpenType Specification.
		/// </summary>
		CVAR = 0x63766172,
		/// <summary>
		/// Feature name. Table tag "feat" in the OpenType Specification.
		/// </summary>
		FEAT = 0x66656174,
		/// <summary>
		/// Font descriptors. Table tag "fdsc" in the OpenType Specification.
		/// </summary>
		FDSC = 0x66647363,
		/// <summary>
		/// Font metrics. Table tag "fmtx" in the OpenType Specification.
		/// </summary>
		FMTX = 0x666d7478,
		/// <summary>
		/// Justification. Table tag "just" in the OpenType Specification.
		/// </summary>
		JUST = 0x6a757374,
		/// <summary>
		/// Ligature caret. Table tag "lcar" in the OpenType Specification.
		/// </summary>
		LCAR = 0x6c636172,
		/// <summary>
		/// Glyph metamorphosis. Table tag "mort" in the OpenType Specification.
		/// </summary>
		MORT = 0x6d6f7274,
		/// <summary>
		/// Optical bounds. Table tag "opbd" in the OpenType Specification.
		/// </summary>
		OPBD = 0x6F706264,
		/// <summary>
		/// Glyph properties. Table tag "prop" in the OpenType Specification.
		/// </summary>
		PROP = 0x70726f70,
		/// <summary>
		/// Tracking. Table tag "trak" in the OpenType Specification.
		/// </summary>
		TRAK = 0x7472616b,
	}

	// Static properties for each table tag, allowing direct access via TableTypes.CMAP, etc.

	/// <summary>
	/// Character to glyph mapping. Table tag "cmap".
	/// </summary>
	public static Tag CMAP { get; } = new Tag(Tags.CMAP);

	/// <summary>
	/// Font header. Table tag "head".
	/// </summary>
	public static Tag HEAD { get; } = new Tag(Tags.HEAD);

	/// <summary>
	/// Naming table. Table tag "name".
	/// </summary>
	public static Tag NAME { get; } = new Tag(Tags.NAME);

	/// <summary>
	/// Glyph data. Table tag "glyf".
	/// </summary>
	public static Tag GLYF { get; } = new Tag(Tags.GLYF);

	/// <summary>
	/// Maximum profile. Table tag "maxp".
	/// </summary>
	public static Tag MAXP { get; } = new Tag(Tags.MAXP);

	/// <summary>
	/// CVT preprogram. Table tag "prep".
	/// </summary>
	public static Tag PREP { get; } = new Tag(Tags.PREP);

	/// <summary>
	/// Horizontal metrics. Table tag "hmtx".
	/// </summary>
	public static Tag HMTX { get; } = new Tag(Tags.HMTX);

	/// <summary>
	/// Kerning. Table tag "kern".
	/// </summary>
	public static Tag KERN { get; } = new Tag(Tags.KERN);

	/// <summary>
	/// Horizontal device metrics. Table tag "hdmx".
	/// </summary>
	public static Tag HDMX { get; } = new Tag(Tags.HDMX);

	/// <summary>
	/// Index to location. Table tag "loca".
	/// </summary>
	public static Tag LOCA { get; } = new Tag(Tags.LOCA);

	/// <summary>
	/// PostScript Information. Table tag "post".
	/// </summary>
	public static Tag POST { get; } = new Tag(Tags.POST);

	/// <summary>
	/// OS/2 and Windows specific metrics. Table tag "OS/2".
	/// </summary>
	public static Tag OS_2 { get; } = new Tag(Tags.OS_2);

	/// <summary>
	/// Control value table. Table tag "cvt ".
	/// </summary>
	public static Tag CVT { get; } = new Tag(Tags.CVT);

	/// <summary>
	/// Grid-fitting and scan conversion procedure. Table tag "gasp".
	/// </summary>
	public static Tag GASP { get; } = new Tag(Tags.GASP);

	/// <summary>
	/// Vertical device metrics. Table tag "VDMX".
	/// </summary>
	public static Tag VDMX { get; } = new Tag(Tags.VDMX);

	/// <summary>
	/// Vertical metrics. Table tag "vmtx".
	/// </summary>
	public static Tag VMTX { get; } = new Tag(Tags.VMTX);

	/// <summary>
	/// Vertical metrics header. Table tag "vhea".
	/// </summary>
	public static Tag VHEA { get; } = new Tag(Tags.VHEA);

	/// <summary>
	/// Horizontal metrics header. Table tag "hhea".
	/// </summary>
	public static Tag HHEA { get; } = new Tag(Tags.HHEA);

	/// <summary>
	/// Adobe Type 1 font data. Table tag "typ1".
	/// </summary>
	public static Tag TYP1 { get; } = new Tag(Tags.TYP1);

	/// <summary>
	/// Baseline table. Table tag "bsln".
	/// </summary>
	public static Tag BSLN { get; } = new Tag(Tags.BSLN);

	/// <summary>
	/// Glyph substitution. Table tag "GSUB".
	/// </summary>
	public static Tag GSUB { get; } = new Tag(Tags.GSUB);

	/// <summary>
	/// Digital signature. Table tag "DSIG".
	/// </summary>
	public static Tag DSIG { get; } = new Tag(Tags.DSIG);

	/// <summary>
	/// Font program. Table tag "fpgm".
	/// </summary>
	public static Tag FPGM { get; } = new Tag(Tags.FPGM);

	/// <summary>
	/// Font variation. Table tag "fvar".
	/// </summary>
	public static Tag FVAR { get; } = new Tag(Tags.FVAR);

	/// <summary>
	/// Glyph variation. Table tag "gvar".
	/// </summary>
	public static Tag GVAR { get; } = new Tag(Tags.GVAR);

	/// <summary>
	/// Compact font format (Type1 font). Table tag "CFF ".
	/// </summary>
	public static Tag CFF { get; } = new Tag(Tags.CFF);

	/// <summary>
	/// Multiple master supplementary data. Table tag "MMSD".
	/// </summary>
	public static Tag MMSD { get; } = new Tag(Tags.MMSD);

	/// <summary>
	/// Multiple master font metrics. Table tag "MMFX".
	/// </summary>
	public static Tag MMFX { get; } = new Tag(Tags.MMFX);

	/// <summary>
	/// Baseline data. Table tag "BASE".
	/// </summary>
	public static Tag BASE { get; } = new Tag(Tags.BASE);

	/// <summary>
	/// Glyph definition. Table tag "GDEF".
	/// </summary>
	public static Tag GDEF { get; } = new Tag(Tags.GDEF);

	/// <summary>
	/// Glyph positioning. Table tag "GPOS".
	/// </summary>
	public static Tag GPOS { get; } = new Tag(Tags.GPOS);

	/// <summary>
	/// Justification. Table tag "JSTF".
	/// </summary>
	public static Tag JSTF { get; } = new Tag(Tags.JSTF);

	/// <summary>
	/// Embedded bitmap data. Table tag "EBDT".
	/// </summary>
	public static Tag EBDT { get; } = new Tag(Tags.EBDT);

	/// <summary>
	/// Embedded bitmap location. Table tag "EBLC".
	/// </summary>
	public static Tag EBLC { get; } = new Tag(Tags.EBLC);

	/// <summary>
	/// Embedded bitmap scaling. Table tag "EBSC".
	/// </summary>
	public static Tag EBSC { get; } = new Tag(Tags.EBSC);

	/// <summary>
	/// Linear threshold. Table tag "LTSH".
	/// </summary>
	public static Tag LTSH { get; } = new Tag(Tags.LTSH);

	/// <summary>
	/// PCL 5 data. Table tag "PCLT".
	/// </summary>
	public static Tag PCLT { get; } = new Tag(Tags.PCLT);

	/// <summary>
	/// Accent attachment. Table tag "acnt".
	/// </summary>
	public static Tag ACNT { get; } = new Tag(Tags.ACNT);

	/// <summary>
	/// Axis variation. Table tag "avar".
	/// </summary>
	public static Tag AVAR { get; } = new Tag(Tags.AVAR);

	/// <summary>
	/// Bitmap data. Table tag "bdat".
	/// </summary>
	public static Tag BDAT { get; } = new Tag(Tags.BDAT);

	/// <summary>
	/// Bitmap location. Table tag "bloc".
	/// </summary>
	public static Tag BLOC { get; } = new Tag(Tags.BLOC);

	/// <summary>
	/// CVT variation. Table tag "cvar".
	/// </summary>
	public static Tag CVAR { get; } = new Tag(Tags.CVAR);

	/// <summary>
	/// Feature name. Table tag "feat".
	/// </summary>
	public static Tag FEAT { get; } = new Tag(Tags.FEAT);

	/// <summary>
	/// Font descriptors. Table tag "fdsc".
	/// </summary>
	public static Tag FDSC { get; } = new Tag(Tags.FDSC);

	/// <summary>
	/// Font metrics. Table tag "fmtx".
	/// </summary>
	public static Tag FMTX { get; } = new Tag(Tags.FMTX);

	/// <summary>
	/// Justification. Table tag "just".
	/// </summary>
	public static Tag JUST { get; } = new Tag(Tags.JUST);

	/// <summary>
	/// Ligature caret. Table tag "lcar".
	/// </summary>
	public static Tag LCAR { get; } = new Tag(Tags.LCAR);

	/// <summary>
	/// Glyph metamorphosis. Table tag "mort".
	/// </summary>
	public static Tag MORT { get; } = new Tag(Tags.MORT);

	/// <summary>
	/// Optical bounds. Table tag "opbd".
	/// </summary>
	public static Tag OPBD { get; } = new Tag(Tags.OPBD);

	/// <summary>
	/// Glyph properties. Table tag "prop".
	/// </summary>
	public static Tag PROP { get; } = new Tag(Tags.PROP);

	/// <summary>
	/// Tracking. Table tag "trak".
	/// </summary>
	public static Tag TRAK { get; } = new Tag(Tags.TRAK);
}
