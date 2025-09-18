using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Utils.Fonts;

/// <summary>
/// Provides utilities and encoding tables for working with font character sets and glyph name mappings.
/// Includes standard encodings (WinAnsi, MacRoman, ISO Latin1, etc.) and predefined glyph names used in Type 1 and Type 1C fonts.
/// </summary>
public static class FontSupport
{
        /// <summary>
        /// Gets the table of standard glyph names defined by Adobe for Type 1 fonts.
        /// </summary>
        public static string[] StdNames { get; } = LoadStdNames();
        /// <summary>
        /// Gets the Unicode string values corresponding to <see cref="StdNames"/>.
        /// </summary>
        public static string[] StdValues { get; } = LoadStdValues();
        /// <summary>
        /// Gets the expert glyph charset indexes used by Type 1C fonts.
        /// </summary>
        public static int[] Type1CExpertCharset { get; } = LoadType1CExpertCharset();
        /// <summary>
        /// Gets the expert subset charset indexes used by Type 1C fonts.
        /// </summary>
        public static int[] Type1CExpertSubCharset { get; } = LoadType1CExpertSubCharset();
        /// <summary>
        /// Gets the additional glyph names specific to Mac OS encodings.
        /// </summary>
        public static string[] MacExtras { get; } = LoadMacExtras();
        /// <summary>
        /// Gets the MacRoman encoding table mapping character codes to glyph indexes.
        /// </summary>
        public static int[] MacRomanEncoding { get; } = LoadMacRomanEncoding();
        /// <summary>
        /// Gets the ISO Latin 1 encoding table mapping character codes to glyph indexes.
        /// </summary>
        public static int[] IsoLatin1Encoding { get; } = LoadIsoLatin1Encoding();
        /// <summary>
        /// Gets the Windows ANSI encoding table mapping character codes to glyph indexes.
        /// </summary>
        public static int[] WinAnsiEncoding { get; } = LoadWinAnsiEncoding();
        /// <summary>
        /// Gets the Adobe standard encoding table mapping character codes to glyph indexes.
        /// </summary>
        public static int[] StandardEncoding { get; } = LoadStandardEncoding();

        /// <summary>
        /// Provides a lookup map from glyph name to its index in <see cref="StdNames"/>.
        /// </summary>
        private static IReadOnlyDictionary<string, int> StdNameIndexMap { get; } =
                LoadStdNames()
                        .Select((value, index) => (value, index))
                        .ToImmutableDictionary(vi => vi.value, vi => vi.index);

    /// <summary>
    /// Retrieves the glyph name corresponding to the provided index.
    /// </summary>
    /// <param name="index">The glyph name index.</param>
    /// <returns>The glyph name, or ".notdef" if the index is out of bounds.</returns>
    public static string GetName(int index)
    {
        if (index < StdNames.Length)
            return StdNames[index];

        int offset = index - StdNames.Length;
        if (offset < MacExtras.Length)
            return MacExtras[offset];

        return ".notdef";
    }

    /// <summary>
    /// Finds the index of a glyph name in a provided name table.
    /// </summary>
    /// <param name="name">The glyph name to look for.</param>
    /// <param name="table">The name table to search.</param>
    /// <returns>The index of the name in the table, or -1 if not found.</returns>
    public static int FindName(string name, string[] table)
    {
        for (int i = 0; i < table.Length; i++)
        {
            if (string.Equals(name, table[i], StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Finds the index of a glyph name by comparing names in a name index table.
    /// </summary>
    /// <param name="name">The glyph name to search for.</param>
    /// <param name="indexTable">An array of indexes into the StdNames or MacExtras tables.</param>
    /// <returns>The index in the indexTable if found, -1 otherwise.</returns>
    public static int FindName(string name, int[] indexTable)
    {
        for (int i = 0; i < indexTable.Length; i++)
        {
            if (string.Equals(name, GetName(indexTable[i]), StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Gets the index of a glyph name in the StdNames table.
    /// </summary>
    /// <param name="name">The glyph name.</param>
    /// <returns>The index if found, -1 otherwise.</returns>
    public static int GetStrIndex(string name)
    {
        return StdNameIndexMap.TryGetValue(name, out int index) ? index : -1;
    }


        /// <summary>
        /// Loads the Adobe standard glyph name table.
        /// </summary>
        /// <remarks>
        /// In a production scenario the data should be sourced from embedded resources or precompiled lookup tables
        /// to avoid embedding large literals directly in code.
        /// </remarks>
        private static string[] LoadStdNames() =>
        [
            ".notdef", "space", "exclam", "quotedbl", "numbersign", "dollar", "percent", "ampersand", "quoteright", "parenleft",
            "parenright", "asterisk", "plus", "comma", "hyphen", "period", "slash", "zero", "one", "two",
            "three", "four", "five", "six", "seven", "eight", "nine", "colon", "semicolon", "less",
            "equal", "greater", "question", "at", "A", "B", "C", "D", "E", "F",
            "G", "H", "I", "J", "K", "L", "M", "N", "O", "P",
            "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "bracketleft", "backslash", "bracketright", "asciicircum", "underscore", "quoteleft", "a", "b", "c", "d",
            "e", "f", "g", "h", "i", "j", "k", "l", "m", "n",
            "o", "p", "q", "r", "s", "t", "u", "v", "w", "x",
            "y", "z", "braceleft", "bar", "braceright", "asciitilde", "exclamdown", "cent", "sterling", "fraction",
            "yen", "florin", "section", "currency", "quotesingle", "quotedblleft", "guillemotleft", "guilsinglleft", "guilsinglright", "fi",
            "fl", "endash", "dagger", "daggerdbl", "periodcentered", "paragraph", "bullet", "quotesinglbase", "quotedblbase", "quotedblright",
            "guillemotright", "ellipsis", "perthousand", "questiondown", "grave", "acute", "circumflex", "tilde", "macron", "breve",
            "dotaccent", "dieresis", "ring", "cedilla", "hungarumlaut", "ogonek", "caron", "emdash", "AE", "ordfeminine",
            "Lslash", "Oslash", "OE", "ordmasculine", "ae", "dotlessi", "lslash", "oslash", "oe", "germandbls",
            "onesuperior", "logicalnot", "mu", "trademark", "Eth", "onehalf", "plusminus", "Thorn", "onequarter", "divide",
            "brokenbar", "degree", "thorn", "threequarters", "twosuperior", "registered", "minus", "eth", "multiply", "threesuperior",
            "copyright", "Aacute", "Acircumflex", "Adieresis", "Agrave", "Aring", "Atilde", "Ccedilla", "Eacute", "Ecircumflex",
            "Edieresis", "Egrave", "Iacute", "Icircumflex", "Idieresis", "Igrave", "Ntilde", "Oacute", "Ocircumflex", "Odieresis",
            "Ograve", "Otilde", "Scaron", "Uacute", "Ucircumflex", "Udieresis", "Ugrave", "Yacute", "Ydieresis", "Zcaron",
            "aacute", "acircumflex", "adieresis", "agrave", "aring", "atilde", "ccedilla", "eacute", "ecircumflex", "edieresis",
            "egrave", "iacute", "icircumflex", "idieresis", "igrave", "ntilde", "oacute", "ocircumflex", "odieresis", "ograve",
            "otilde", "scaron", "uacute", "ucircumflex", "udieresis", "ugrave", "yacute", "ydieresis", "zcaron", "exclamsmall",
            "Hungarumlautsmall", "dollaroldstyle", "dollarsuperior", "ampersandsmall", "Acutesmall", "parenleftsuperior", "parenrightsuperior", "twodotenleader", "onedotenleader", "zerooldstyle",
            "oneoldstyle", "twooldstyle", "threeoldstyle", "fouroldstyle", "fiveoldstyle", "sixoldstyle", "sevenoldstyle", "eightoldstyle", "nineoldstyle", "commasuperior",
            "threequartersemdash", "periodsuperior", "questionsmall", "asuperior", "bsuperior", "centsuperior", "dsuperior", "esuperior", "isuperior", "lsuperior",
            "msuperior", "nsuperior", "osuperior", "rsuperior", "ssuperior", "tsuperior", "ff", "ffi", "ffl", "parenleftinferior",
            "parenrightinferior", "Circumflexsmall", "hyphensuperior", "Gravesmall", "Asmall", "Bsmall", "Csmall", "Dsmall", "Esmall", "Fsmall",
            "Gsmall", "Hsmall", "Ismall", "Jsmall", "Ksmall", "Lsmall", "Msmall", "Nsmall", "Osmall", "Psmall",
            "Qsmall", "Rsmall", "Ssmall", "Tsmall", "Usmall", "Vsmall", "Wsmall", "Xsmall", "Ysmall", "Zsmall",
            "colonmonetary", "onefitted", "rupiah", "Tildesmall", "exclamdownsmall", "centoldstyle", "Lslashsmall", "Scaronsmall", "Zcaronsmall", "Dieresissmall",
            "Brevesmall", "Caronsmall", "Dotaccentsmall", "Macronsmall", "figuredash", "hypheninferior", "Ogoneksmall", "Ringsmall", "Cedillasmall", "questiondownsmall",
            "oneeighth", "threeeighths", "fiveeighths", "seveneighths", "onethird", "twothirds", "zerosuperior", "foursuperior", "fivesuperior", "sixsuperior",
            "sevensuperior", "eightsuperior", "ninesuperior", "zeroinferior", "oneinferior", "twoinferior", "threeinferior", "fourinferior", "fiveinferior", "sixinferior",
            "seveninferior", "eightinferior", "nineinferior", "centinferior", "dollarinferior", "periodinferior", "commainferior", "Agravesmall", "Aacutesmall", "Acircumflexsmall",
            "Atildesmall", "Adieresissmall", "Aringsmall", "AEsmall", "Ccedillasmall", "Egravesmall", "Eacutesmall", "Ecircumflexsmall", "Edieresissmall", "Igravesmall",
            "Iacutesmall", "Icircumflexsmall", "Idieresissmall", "Ethsmall", "Ntildesmall", "Ogravesmall", "Oacutesmall", "Ocircumflexsmall", "Otildesmall", "Odieresissmall",
            "OEsmall", "Oslashsmall", "Ugravesmall", "Uacutesmall", "Ucircumflexsmall", "Udieresissmall", "Yacutesmall", "Thornsmall", "Ydieresissmall", "001.000",
            "001.001", "001.002", "001.003", "Black", "Bold", "Book", "Light", "Medium", "Regular", "Roman",
            "Semibold"
        ];
        /// <summary>
        /// Loads the Unicode string values associated with the Adobe standard glyph names.
        /// </summary>
        /// <remarks>
        /// These values mirror the Adobe Glyph List for New Fonts and allow mapping glyph names to Unicode strings.
        /// </remarks>
        private static string[] LoadStdValues() =>
        [
            "", " ", "!", "\"", "#", "$", "%", "&", "'", "(",
            ")", "*", "+", ",", "-", ".", "/", "0", "1", "2",
            "3", "4", "5", "6", "7", "8", "9", ":", ";", "<",
            "=", ">", "?", "@", "A", "B", "C", "D", "E", "F",
            "G", "H", "I", "J", "K", "L", "M", "N", "O", "P",
            "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "[", "\\", "]", "^", "_", "`", "a", "b", "c", "d",
            "e", "f", "g", "h", "i", "j", "k", "l", "m", "n",
            "o", "p", "q", "r", "s", "t", "u", "v", "w", "x",
            "y", "z", "{", "|", "}", "~", "¡", "¢", "£", "/fraction",
            "¥", "Fflorin", "§", "¤", "\u00b4quotesingle", "“", "?guillemotleft", "‹", "›", "fi",
            "fl", "--", "†", "‡", "·", "¶", "•", "'quotesinglbase", "\"quotedblbase", "\"quotedblright",
            "?guillemotright", "...ellipsis", "%perthousand", "?questiondown", "`grave", "'acute", "^circumflex", "~tilde", "-macron", "?breve",
            "?dotaccent", "?dieresis", "oring", "ccedilla", ":hungarumlaut", "?ogonek", ",caron", "---emdash", "AE", "aordfeminine",
            "LLslash", "OOslash", "OE", "oordmasculine", "ae", "idotlessi", "llslash", "ooslash", "oe", "Bgermandbls",
            "1onesuperior", "~logicalnot", "?mu", "(TM)trademark", "?Eth", "1/2", "+/-", "?Thorn", "1/4", "/divide",
            "|brokenbar", "*degree", "?thorn", "3/4", "2twosuperior", "(R)", "-minus", "?eth", "*multiply", "3threesuperior",
            "(C)", "AAacute", "AAcircumflex", "AAdieresis", "AAgrave", "AAring", "AAtilde", "CCcedilla", "EEacute", "EEcircumflex",
            "EEdieresis", "EEgrave", "IIacute", "IIcircumflex", "IIdieresis", "IIgrave", "NNtilde", "OOacute", "OOcircumflex", "OOdieresis",
            "OOgrave", "OOtilde", "SScaron", "UUacute", "UUcircumflex", "UUdieresis", "UUgrave", "YYacute", "YYdieresis", "ZZcaron",
            "aaacute", "aacircumflex", "aadieresis", "aagrave", "aaring", "aatilde", "cccedilla", "eeacute", "eecircumflex", "eedieresis",
            "eegrave", "iiacute", "iicircumflex", "iidieresis", "iigrave", "nntilde", "ooacute", "oocircumflex", "oodieresis", "oograve",
            "ootilde", "sscaron", "uuacute", "uucircumflex", "uudieresis", "uugrave", "yyacute", "yydieresis", "zzcaron", "!exclamsmall",
            "?Hungarumlautsmall", "$dollaroldstyle", "$dollarsuperior", "&ampersandsmall", "'Acutesmall", "/parenleftsuperior", "\\parenrightsuperior", "?twodotenleader", "?onedotenleader", "0zerooldstyle",
            "1oneoldstyle", "2twooldstyle", "3threeoldstyle", "4fouroldstyle", "5fiveoldstyle", "6sixoldstyle", "7sevenoldstyle", "8eightoldstyle", "9nineoldstyle", "'commasuperior",
            "--threequartersemdash", ".periodsuperior", "?questionsmall", "aasuperior", "bbsuperior", "ccentsuperior", "ddsuperior", "eesuperior", "iisuperior", "llsuperior",
            "mmsuperior", "nnsuperior", "oosuperior", "rrsuperior", "sssuperior", "ttsuperior", "ff", "ffi", "ffl", "\\parenleftinferior",
            "/parenrightinferior", "^Circumflexsmall", "-hyphensuperior", "`Gravesmall", "AAsmall", "BBsmall", "CCsmall", "DDsmall", "EEsmall", "FFsmall",
            "GGsmall", "HHsmall", "IIsmall", "JJsmall", "KKsmall", "LLsmall", "MMsmall", "NNsmall", "OOsmall", "PPsmall",
            "QQsmall", "RRsmall", "SSsmall", "TTsmall", "UUsmall", "VVsmall", "WWsmall", "XXsmall", "YYsmall", "ZZsmall",
            ":colonmonetary", "1onefitted", "?rupiah", "~Tildesmall", "!exclamdownsmall", "ccentoldstyle", "LLslashsmall", "SScaronsmall", "ZZcaronsmall", "?Dieresissmall",
            "?Brevesmall", "^Caronsmall", "?Dotaccentsmall", "?Macronsmall", "--figuredash", "-hypheninferior", "?Ogoneksmall", "oRingsmall", ",Cedillasmall", "?questiondownsmall",
            "1/8oneeighth", "3/8threeeighths", "5/8fiveeighths", "7/8seveneighths", "1/3onethird", "2/3twothirds", "0zerosuperior", "4foursuperior", "5fivesuperior", "6sixsuperior",
            "7sevensuperior", "8eightsuperior", "9ninesuperior", "0zeroinferior", "1oneinferior", "2twoinferior", "3threeinferior", "4fourinferior", "5fiveinferior", "6sixinferior",
            "7seveninferior", "8eightinferior", "9nineinferior", "ccentinferior", "$dollarinferior", ".periodinferior", ",commainferior", "AAgravesmall", "AAacutesmall", "AAcircumflexsmall",
            "AAtildesmall", "AAdieresissmall", "AAringsmall", "AEAEsmall", "CCcedillasmall", "EEgravesmall", "EEacutesmall", "EEcircumflexsmall", "EEdieresissmall", "IIgravesmall",
            "IIacutesmall", "IIcircumflexsmall", "IIdieresissmall", "EthEthsmall", "NNtildesmall", "OOgravesmall", "OOacutesmall", "OOcircumflexsmall", "OOtildesmall", "OOdieresissmall",
            "OEOEsmall", "OOslashsmall", "UUgravesmall", "UUacutesmall", "UUcircumflexsmall", "UUdieresissmall", "YYacutesmall", "?Thornsmall", "YYdieresissmall", "?001.000",
            "?001.001", "?001.002", "?001.003", " Black", " Bold", " Book", " Light", " Medium", " Regular", " Roman",
            " Semibold", "?NUL", "?HT", " LF", " CR", "?DLE", "?DC1", "?DC2", "?DC3", "?DC4",
            "?RS", "?US", "!=", "?DEL", "?infinity", "<=", ">=", "?partialdiff", "?summation", "xproduct",
            "?pi", "?integral", "?Omega", "?radical", "~=", "?Delta", " nbspace", "?lozenge", "?apple"
        ];

        /// <summary>
        /// Loads the Type 1C expert charset index table.
        /// </summary>
        private static int[] LoadType1CExpertCharset() =>
        [
            1, 229, 230, 231, 232, 233, 234, 235, 236, 237,
            238, 13, 14, 15, 99, 239, 240, 241, 242, 243,
            244, 245, 246, 247, 248, 27, 28, 249, 250, 251,
            252, 253, 254, 255, 256, 257, 258, 259, 260, 261,
            262, 263, 264, 265, 266, 109, 110, 267, 268, 269,
            270, 271, 272, 273, 274, 275, 276, 277, 278, 279,
            280, 281, 282, 283, 284, 285, 286, 287, 288, 289,
            290, 291, 292, 293, 294, 295, 296, 297, 298, 299,
            300, 301, 302, 303, 304, 305, 306, 307, 308, 309,
            310, 311, 312, 313, 314, 315, 316, 317, 318, 158,
            155, 163, 319, 320, 321, 322, 323, 324, 325, 326,
            150, 164, 169, 327, 328, 329, 330, 331, 332, 333,
            334, 335, 336, 337, 338, 339, 340, 341, 342, 343,
            344, 345, 346, 347, 348, 349, 350, 351, 352, 353,
            354, 355, 356, 357, 358, 359, 360, 361, 362, 363,
            364, 365, 366, 367, 368, 369, 370, 371, 372, 373,
            374, 375, 376, 377, 378
        ];

        /// <summary>
        /// Loads the Type 1C expert subset charset index table.
        /// </summary>
        private static int[] LoadType1CExpertSubCharset() =>
        [
            1, 231, 232, 235, 236, 237, 238, 13, 14, 15,
            99, 239, 240, 241, 242, 243, 244, 245, 246, 247,
            248, 27, 28, 249, 250, 251, 253, 254, 255, 256,
            257, 258, 259, 260, 261, 262, 263, 264, 265, 266,
            109, 110, 267, 268, 269, 270, 272, 300, 301, 302,
            305, 314, 315, 158, 155, 163, 320, 321, 322, 323,
            324, 325, 326, 150, 164, 169, 327, 328, 329, 330,
            331, 332, 333, 334, 335, 336, 337, 338, 339, 340,
            341, 342, 343, 344, 345, 346
        ];

        /// <summary>
        /// Loads the Mac-specific glyph name extensions.
        /// </summary>
        private static string[] LoadMacExtras() =>
        [
            "NUL", "HT", "LF", "CR", "DLE", "DC1", "DC2", "DC3", "DC4", "RS",
            "US", "notequal", "DEL", "infinity", "lessequal", "greaterequal", "partialdiff", "summation", "product", "pi",
            "integral", "Omega", "radical", "approxequal", "Delta", "nbspace", "lozenge", "apple"
        ];

        /// <summary>
        /// Loads the MacRoman encoding table.
        /// </summary>
        private static int[] LoadMacRomanEncoding() =>
        [
            391, 154, 167, 140, 146, 192, 221, 197, 226, 392,
            393, 157, 162, 394, 199, 228, 395, 396, 397, 398,
            399, 155, 158, 150, 163, 169, 164, 160, 166, 168,
            400, 401, 1, 2, 3, 4, 5, 6, 7, 104,
            9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
            19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
            29, 30, 31, 32, 33, 34, 35, 36, 37, 38,
            39, 40, 41, 42, 43, 44, 45, 46, 47, 48,
            49, 50, 51, 52, 53, 54, 55, 56, 57, 58,
            59, 60, 61, 62, 63, 64, 124, 66, 67, 68,
            69, 70, 71, 72, 73, 74, 75, 76, 77, 78,
            79, 80, 81, 82, 83, 84, 85, 86, 87, 88,
            89, 90, 91, 92, 93, 94, 95, 403, 173, 175,
            177, 178, 186, 189, 195, 200, 203, 201, 202, 205,
            204, 206, 207, 210, 208, 209, 211, 214, 212, 213,
            215, 216, 219, 217, 218, 220, 222, 225, 223, 224,
            112, 161, 97, 98, 102, 116, 115, 149, 165, 170,
            153, 125, 131, 402, 138, 141, 404, 156, 405, 406,
            100, 152, 407, 408, 409, 410, 411, 139, 143, 412,
            144, 147, 123, 96, 151, 413, 101, 414, 415, 106,
            120, 121, 416, 174, 176, 191, 142, 148, 111, 137,
            105, 119, 65, 8, 159, 417, 227, 198, 99, 103,
            107, 108, 109, 110, 113, 114, 117, 118, 122, 172,
            179, 171, 180, 181, 182, 183, 184, 185, 187, 188,
            418, 190, 193, 194, 196, 145, 126, 127, 128, 129,
            130, 132, 133, 134, 135, 136
        ];

        /// <summary>
        /// Loads the ISO Latin 1 encoding table.
        /// </summary>
        private static int[] LoadIsoLatin1Encoding() =>
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 166, 15, 16, 17, 18,
            19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
            29, 30, 31, 32, 33, 34, 35, 36, 37, 38,
            39, 40, 41, 42, 43, 44, 45, 46, 47, 48,
            49, 50, 51, 52, 53, 54, 55, 56, 57, 58,
            59, 60, 61, 62, 63, 64, 65, 66, 67, 68,
            69, 70, 71, 72, 73, 74, 75, 76, 77, 78,
            79, 80, 81, 82, 83, 84, 85, 86, 87, 88,
            89, 90, 91, 92, 93, 94, 95, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 145, 124, 125, 126, 127, 128,
            129, 130, 131, 0, 132, 133, 0, 134, 135, 136,
            1, 96, 97, 98, 103, 100, 160, 102, 131, 170,
            139, 106, 151, 14, 165, 128, 161, 156, 164, 169,
            125, 152, 115, 114, 133, 150, 143, 120, 158, 155,
            163, 123, 174, 171, 172, 176, 173, 175, 138, 177,
            181, 178, 179, 180, 185, 182, 183, 184, 154, 186,
            190, 187, 188, 191, 189, 168, 141, 196, 193, 194,
            195, 197, 157, 149, 203, 200, 201, 205, 202, 204,
            144, 206, 210, 207, 208, 209, 214, 211, 212, 213,
            167, 215, 219, 216, 217, 220, 218, 159, 147, 225,
            222, 223, 224, 226, 162, 227
        ];

        /// <summary>
        /// Loads the Windows ANSI encoding table.
        /// </summary>
        private static int[] LoadWinAnsiEncoding() =>
        [
            124, 125, 126, 127, 128, 129, 130, 131, 132, 133,
            134, 135, 136, 145, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
            19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
            29, 30, 31, 32, 33, 34, 35, 36, 37, 38,
            39, 40, 41, 42, 43, 44, 45, 46, 47, 48,
            49, 50, 51, 52, 53, 54, 55, 56, 57, 58,
            59, 60, 61, 62, 63, 64, 65, 66, 67, 68,
            69, 70, 71, 72, 73, 74, 75, 76, 77, 78,
            79, 80, 81, 82, 83, 84, 85, 86, 87, 88,
            89, 90, 91, 92, 93, 94, 95, 0, 0, 0,
            117, 101, 118, 121, 112, 113, 0, 122, 192, 107,
            142, 0, 0, 0, 0, 65, 8, 105, 119, 116,
            111, 137, 0, 153, 221, 108, 148, 0, 0, 198,
            1, 96, 97, 98, 103, 100, 160, 102, 131, 170,
            139, 106, 151, 14, 165, 128, 161, 156, 164, 169,
            125, 152, 115, 114, 133, 150, 143, 120, 158, 155,
            163, 123, 174, 171, 172, 176, 173, 175, 138, 177,
            181, 178, 179, 180, 185, 182, 183, 184, 154, 186,
            190, 187, 188, 191, 189, 168, 141, 196, 193, 194,
            195, 197, 157, 149, 203, 200, 201, 205, 202, 204,
            144, 206, 210, 207, 208, 209, 214, 211, 212, 213,
            167, 215, 219, 216, 217, 220, 218, 159, 147, 225,
            222, 223, 224, 226, 162, 227
        ];

        /// <summary>
        /// Loads the Adobe standard encoding table.
        /// </summary>
        private static int[] LoadStandardEncoding() =>
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
            19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
            29, 30, 31, 32, 33, 34, 35, 36, 37, 38,
            39, 40, 41, 42, 43, 44, 45, 46, 47, 48,
            49, 50, 51, 52, 53, 54, 55, 56, 57, 58,
            59, 60, 61, 62, 63, 64, 65, 66, 67, 68,
            69, 70, 71, 72, 73, 74, 75, 76, 77, 78,
            79, 80, 81, 82, 83, 84, 85, 86, 87, 88,
            89, 90, 91, 92, 93, 94, 95, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 96, 97, 98, 99, 100, 101, 102, 103, 104,
            105, 106, 107, 108, 109, 110, 0, 111, 112, 113,
            114, 0, 115, 116, 117, 118, 119, 120, 121, 122,
            0, 123, 0, 124, 125, 126, 127, 128, 129, 130,
            131, 0, 132, 133, 0, 134, 135, 136, 137, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 138, 0, 139, 0, 0,
            0, 0, 140, 141, 142, 143, 0, 0, 0, 0,
            0, 144, 0, 0, 0, 145, 0, 0, 146, 147,
            148, 149, 0, 0, 0, 0
        ];
}


