using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Fonts
{
    [Flags]
    public enum FontFlags
    {
        /** All glyphs have the same width. */
        FixedPitch = 0x01,
        /** Glyphs have serifs. */
        Serif = 0x02,
        /** Font contains glyphs outside the Adobe standard Latin. */
        Symbolic = 0x04,
        /** Glyphs resemble cursive handwriting. */
        Script = 0x08,
        /** Font uses the Adobe standard Latic character set. */
        NonSymbolic = 0x20,
        /** Glyphs have dominant vertical strokes that are slanted. */
        Italic = 0x40,
        /** Font contains no lowercase letters. */
        AllCap = 0x10000,
        /** Font contains both uppercase and lowercase letters.. */
        SmallCap = 0x20000,
        /** Determines whether bold glyphs shall be painted with
         * extra pixels even at very small text sizes. */
        ForceBold = 0x40000
    }


[Flags]
    public enum FontStyle
    {
        /**
         * The plain style constant.
         */
        Plain = 0,

        /**
         * The bold style constant.  This can be combined with the other style
         * constants (except PLAIN) for mixed styles.
         */
        Bold = 1,

        /**
         * The italicized style constant.  This can be combined with the other
         * style constants (except PLAIN) for mixed styles.
         */
        Italic = 2

    }

    [Flags]
    public enum FontBaseLine
    {
        /**
         * The baseline used in most Roman scripts when laying out text.
         */
        RomanBaseline = 0,

        /**
         * The baseline used in ideographic scripts like Chinese, Japanese,
         * and Korean when laying out text.
         */
        CenterBaseline = 1,

        /**
         * The baseline used in Devanagari and similar scripts when laying
         * out text.
         */
        HangingBaseline = 2
    }

    public enum FontType
    {
        /**
         * Identify a font resource of type TRUETYPE.
         * Used to specify a TrueType font resource to the
         * {@link #createFont} method.
         * The TrueType format was extended to become the OpenType
         * format, which adds support for fonts with Postscript outlines,
         * this tag therefore references these fonts, as well as those
         * with TrueType outlines.
         * @since 1.3
         */
        TRUETYPE_FONT = 0,

        /**
         * Identify a font resource of type TYPE1.
         * Used to specify a Type1 font resource to the
         * {@link #createFont} method.
         * @since 1.5
         */
        TYPE1_FONT = 1
    }
}

