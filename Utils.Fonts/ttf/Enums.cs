using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Fonts
{
	[Flags]
	public enum HeadFlags : short
	{
		/// <summary>
		/// y value of 0 specifies baseline
		/// </summary>
		YValueOf0SpecifiesBaseline = 0x0,

		/// <summary>
		/// x position of left most black bit is LSB
		/// </summary>
		XPositionOfLeftMostBlackBitIsLSB = 0x1,

		/// <summary>
		/// scaled point size and actual point size will differ (i.e. 24 point glyph differs from 12 point glyph scaled by factor of 2)
		/// </summary>
		ScaledPointSizeAndActualPointSizeWillDiffer = 0x2,

		/// <summary>
		/// use integer scaling instead of fractional
		/// </summary>
		UseIntegerScalingInsteadOfFractional = 0x4,

		/// <summary>
		/// (used by the Microsoft implementation of the TrueType scaler)
		/// </summary>
		MSTrueTypeScaler = 0x8,

		/// <summary>
		/// This bit should be set in fonts that are intended to be laid out vertically, and in which the glyphs have been drawn such that an x-coordinate of 0 corresponds to the desired vertical baseline.
		/// </summary>
		VerticalLayout = 0x10,

		/// <summary>
		/// This bit must be set to zero.
		/// </summary>
		Zero = 0x20,

		/// <summary>
		/// This bit should be set if the font requires layout for correct linguistic rendering (e.g. Arabic fonts).
		/// </summary>
		RequireLinguisticRenderingLayout = 0x40,

		/// <summary>
		/// This bit should be set for an AAT font which has one or more metamorphosis effects designated as happening by default.
		/// </summary>
		AATFontWithMetamorphosis = 0x80,

		/// <summary>
		/// This bit should be set if the font contains any strong right-to-left glyphs.
		/// </summary>
		ContainsStrongRightToLeftGlyphs =  0x100,

		/// <summary>
		/// This bit should be set if the font contains Indic-style rearrangement effects.
		/// </summary>
		ContainsIndicStyleRearragementEffetc = 0x200,

		/// <summary>
		/// Defined by Adobe.
		/// </summary>
		AdobeDefined1 = 0x400,

		/// <summary>
		/// Defined by Adobe.
		/// </summary>
		AdobeDefined2 = 0x800,

		/// <summary>
		/// Defined by Adobe.
		/// </summary>
		AdobeDefined3 = 0xF00,

		/// <summary>
		/// This bit should be set if the glyphs in the font are simply generic symbols for code point ranges, such as for a last resort font.
		/// </summary>
		GenericSymbols = 0x1000,
	}


	[Flags]
	public enum MacStyleFlags : short {
		None = 0,
		Bold = 0x1,
		Italic = 0x2,
		Underline = 0x4,
		Outline = 0x8,
		Shadow = 0x10,
		Condensend = 0x20,
		Extended = 0x40,
	}

	public enum FontDirectionHintEnum : short
	{
		/// <summary>
		/// Mixed directional glyphs
		/// </summary>
		Mixed = 0,
		/// <summary>
		/// Only strongly left to right glyphs
		/// </summary>
		LeftToRight = 1,
		/// <summary>
		/// Like 1 but also contains neutrals
		/// </summary>
		LefttoRightWithNeutrals = 2,
		/// <summary>
		/// Only strongly right to left glyphs
		/// </summary>
		RightToLeft = -1,
		/// <summary>
		/// Like -1 but also contains neutrals
		/// </summary>
		RightToLeftWithNeutrals = -2,
	}

	[Flags]
	public enum OutLineFlags : byte
	{
		None = 0x0,
		/// <summary>
		/// If set, the point is on the curve;
		/// Otherwise, it is off the curve.
		/// </summary>
		OnCurve = 0x1,
		/// <summary>
		/// If set, the corresponding x-coordinate is 1 byte long;
		/// Otherwise, the corresponding x-coordinate is 2 bytes long
		/// </summary>
		XIsByte = 0x2,
		/// <summary>
		/// If set, the corresponding y-coordinate is 1 byte long;
		/// Otherwise, the corresponding y-coordinate is 2 bytes long
		/// </summary>
		YIsByte = 0x4,
		/// <summary>
		/// If set, the next byte specifies the number of additional times this set of flags is to be repeated. In this way, the number of flags listed can be smaller than the number of points in a character.
		/// </summary>
		Repeat = 0x8,
		/// <summary>
		/// This flag has one of two meanings, depending on how the x-Short Vector flag is set.
		/// If the x-Short Vector bit is set, this bit describes the sign of the value, with a value of 1 equalling positive and a zero value negative.
		/// If the x-short Vector bit is not set, and this bit is set, then the current x-coordinate is the same as the previous x-coordinate.
		/// If the x-short Vector bit is not set, and this bit is not set, the current x-coordinate is a signed 16-bit delta vector. In this case, the delta vector is the change in x
		/// </summary>
		XIsSame = 0x10,
		/// <summary>
		/// This flag has one of two meanings, depending on how the y-Short Vector flag is set.
		/// If the y-Short Vector bit is set, this bit describes the sign of the value, with a value of 1 equalling positive and a zero value negative.
		/// If the y-short Vector bit is not set, and this bit is set, then the current y-coordinate is the same as the previous y-coordinate.
		/// If the y-short Vector bit is not set, and this bit is not set, the current y-coordinate is a signed 16-bit delta vector. In this case, the delta vector is the change in y
		/// </summary>
		YIsSame = 0x20,
	}

	[Flags]
	public enum CompoundGlyfFlags : short
	{
		/// <summary>
		/// If set, the arguments are words;
		/// If not set, they are bytes.
		/// </summary>
		ARG_1_AND_2_ARE_WORDS = 0x0001,
		/// <summary>
		/// If set, the arguments are xy values;
		/// If not set, they are points.
		/// </summary>
		ARGS_ARE_XY_VALUES = 0x0002,
		/// <summary>
		/// If set, round the xy values to grid;
		/// if not set do not round xy values to grid (relevant only to bit 1 is set)
		/// </summary>
		ROUND_XY_TO_GRID = 0x0004,
		/// <summary>
		/// If set, there is a simple scale for the component.
		/// If not set, scale is 1.0.
		/// </summary>
		WE_HAVE_A_SCALE = 0x0008,
		/// <summary>
		/// If set, at least one additional glyph follows this one.
		/// </summary>
		MORE_COMPONENTS = 0x0020,
		/// <summary>
		/// If set the x direction will use a different scale than the y direction.
		/// </summary>
		WE_HAVE_AN_X_AND_Y_SCALE = 0x0040,
		/// <summary>
		/// If set there is a 2-by-2 transformation that will be used to scale the component.
		/// </summary>
		WE_HAVE_A_TWO_BY_TWO = 0x0080,
		/// <summary>
		/// If set, instructions for the component character follow the last component.
		/// </summary>
		WE_HAVE_INSTRUCTIONS = 0x0100,
		/// <summary>
		/// Use metrics from this component for the compound glyph.
		/// </summary>
		USE_MY_METRICS = 0x0200,
		/// <summary>
		/// If set, the components of this compound glyph overlap.
		/// </summary>
		OVERLAP_COMPOUND = 0x0400
	}

}
