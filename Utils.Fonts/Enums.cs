using System;

namespace Utils.Fonts;

/// <summary>
/// Flags indicating various font characteristics.
/// </summary>
[Flags]
public enum FontFlags
{
	/// <summary>
	/// All glyphs have the same width (monospaced font).
	/// </summary>
	FixedPitch = 0x01,

	/// <summary>
	/// Glyphs have serifs.
	/// </summary>
	Serif = 0x02,

	/// <summary>
	/// Font contains glyphs outside the Adobe standard Latin set (e.g., symbols).
	/// </summary>
	Symbolic = 0x04,

	/// <summary>
	/// Glyphs resemble cursive handwriting.
	/// </summary>
	Script = 0x08,

	/// <summary>
	/// Font uses the Adobe standard Latin character set.
	/// </summary>
	NonSymbolic = 0x20,

	/// <summary>
	/// Glyphs have dominant vertical strokes that are slanted (italic or oblique).
	/// </summary>
	Italic = 0x40,

	/// <summary>
	/// Font contains only uppercase glyphs.
	/// </summary>
	AllCap = 0x10000,

	/// <summary>
	/// Font contains both uppercase and small caps (no true lowercase).
	/// </summary>
	SmallCap = 0x20000,

	/// <summary>
	/// Enforces bold rendering with additional pixel coverage even at small sizes.
	/// </summary>
	ForceBold = 0x40000
}

/// <summary>
/// Indicates stylistic attributes of a font.
/// Le Flags permet de combiner Bold et Italic pour obtenir, par exemple, du BoldItalic.
/// </summary>
[Flags]
public enum FontStyle
{
	/// <summary>
	/// Normal style (no bold or italic).
	/// </summary>
	Plain = 0,

	/// <summary>
	/// Bold style.
	/// </summary>
	Bold = 1,

	/// <summary>
	/// Italic style.
	/// </summary>
	Italic = 2
}

/// <summary>
/// Indicates baseline positioning for typesetting in different writing systems.
/// </summary>
public enum FontBaseLine
{
	/// <summary>
	/// The baseline used in most Western (Roman) scripts.
	/// </summary>
	RomanBaseline = 0,

	/// <summary>
	/// Center baseline used for ideographic scripts (e.g., Chinese, Japanese, Korean).
	/// </summary>
	CenterBaseline = 1,

	/// <summary>
	/// Hanging baseline used for scripts like Devanagari.
	/// </summary>
	HangingBaseline = 2
}

/// <summary>
/// Identifies the underlying technology or format of a font resource.
/// </summary>
public enum FontType
{
	/// <summary>
	/// TrueType or OpenType font with TrueType outlines.
	/// </summary>
	TrueType = 0,

	/// <summary>
	/// Type 1 font, including those with PostScript outlines.
	/// </summary>
	Type1 = 1
}
