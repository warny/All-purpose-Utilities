using System;

namespace Utils.Fonts;

/// <summary>
/// Flags used in the 'head' table of a TrueType font.
/// </summary>
[Flags]
public enum HeadFlags : short
{
	/// <summary>
	/// y=0 specifies the baseline.
	/// </summary>
	YZeroIsBaseline = 0x0000,

	/// <summary>
	/// Left sidebearing point at x=0.
	/// </summary>
	LeftSidebearingIsAtX0 = 0x0001,

	/// <summary>
	/// Font uses different glyph designs at different point sizes.
	/// </summary>
	ScaledDesignsDiffer = 0x0002,

	/// <summary>
	/// Use integer pixel scaling only.
	/// </summary>
	IntegerScaling = 0x0004,

	/// <summary>
	/// Font uses Microsoft's TrueType scaler.
	/// </summary>
	MicrosoftScaler = 0x0008,

	/// <summary>
	/// Font is vertically laid out; x=0 is vertical baseline.
	/// </summary>
	VerticalLayout = 0x0010,

	/// <summary>
	/// Reserved. Must be zero.
	/// </summary>
	ReservedZero = 0x0020,

	/// <summary>
	/// Font requires complex script layout support.
	/// </summary>
	RequiresLinguisticLayout = 0x0040,

	/// <summary>
	/// Font has default metamorphosis (AAT).
	/// </summary>
	HasMetamorphosis = 0x0080,

	/// <summary>
	/// Font contains strong right-to-left glyphs.
	/// </summary>
	HasRightToLeftGlyphs = 0x0100,

	/// <summary>
	/// Font contains Indic-style rearrangement features.
	/// </summary>
	HasIndicRearrangement = 0x0200,

	/// <summary>
	/// Adobe-defined usage flag #1.
	/// </summary>
	AdobeFlag1 = 0x0400,

	/// <summary>
	/// Adobe-defined usage flag #2.
	/// </summary>
	AdobeFlag2 = 0x0800,

	/// <summary>
	/// Adobe-defined usage flag #3.
	/// Remarque : La valeur 0x0F00 peut représenter un ensemble de bits définis par la spécification.
	/// </summary>
	AdobeFlag3 = 0x0F00,

	/// <summary>
	/// Font is a symbolic fallback (e.g., Last Resort).
	/// </summary>
	IsSymbolFallback = 0x1000
}

/// <summary>
/// macStyle field flags from the 'head' table.
/// </summary>
[Flags]
public enum MacStyleFlags : short
{
        /// <summary>
        /// No additional macStyle modifiers are applied to the font.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Indicates that the font should be rendered using a bold weight.
        /// </summary>
        Bold = 0x01,

        /// <summary>
        /// Indicates that the font should be rendered with italic styling.
        /// </summary>
        Italic = 0x02,

        /// <summary>
        /// Marks the font as supporting underlining by default.
        /// </summary>
        Underline = 0x04,

        /// <summary>
        /// Specifies that the font outlines should be drawn instead of filled shapes.
        /// </summary>
        Outline = 0x08,

        /// <summary>
        /// Applies a drop-shadow effect to the glyphs.
        /// </summary>
        Shadow = 0x10,

        /// <summary>
        /// Indicates a condensed version of the font with tighter spacing.
        /// </summary>
        Condensed = 0x20,

        /// <summary>
        /// Indicates an extended version of the font with wider spacing.
        /// </summary>
        Extended = 0x40
}

/// <summary>
/// Indicates glyph layout directionality hints.
/// </summary>
public enum FontDirectionHintEnum : short
{
	/// <summary>
	/// Mixed directional glyphs.
	/// </summary>
	Mixed = 0,

	/// <summary>
	/// Only strong left-to-right glyphs.
	/// </summary>
	LeftToRight = 1,

	/// <summary>
	/// Left-to-right with neutral glyphs.
	/// </summary>
	LeftToRightWithNeutrals = 2,

	/// <summary>
	/// Only strong right-to-left glyphs.
	/// </summary>
	RightToLeft = -1,

	/// <summary>
	/// Right-to-left with neutral glyphs.
	/// </summary>
	RightToLeftWithNeutrals = -2
}

/// <summary>
/// Outline point flags used in glyph definitions.
/// </summary>
[Flags]
public enum OutlineFlags : byte
{
        /// <summary>
        /// No outline flags are set for the point.
        /// </summary>
        None = 0x00,

	/// <summary>
	/// The point is on the curve (versus a control point).
	/// </summary>
	OnCurve = 0x01,

	/// <summary>
	/// X coordinate is stored sur un octet (sinon 2 octets).
	/// </summary>
	XIsByte = 0x02,

	/// <summary>
	/// Y coordinate is stored sur un octet (sinon 2 octets).
	/// </summary>
	YIsByte = 0x04,

	/// <summary>
	/// Le prochain octet répète ce flag pour des points supplémentaires.
	/// </summary>
	Repeat = 0x08,

	/// <summary>
	/// La coordonnée X est la même ou positive, selon XIsByte.
	/// </summary>
	XIsSame = 0x10,

	/// <summary>
	/// La coordonnée Y est la même ou positive, selon YIsByte.
	/// </summary>
	YIsSame = 0x20
}

/// <summary>
/// Flags for compound glyph components.
/// </summary>
[Flags]
public enum CompoundGlyfFlags : short
{
	/// <summary>
	/// Arguments are 16-bit words (otherwise, they are bytes).
	/// </summary>
	ArgsAreWords = 0x0001,

	/// <summary>
	/// Arguments represent (x, y) values (otherwise, point indices).
	/// </summary>
	ArgsAreXY = 0x0002,

	/// <summary>
	/// Round (x, y) values to grid.
	/// </summary>
	RoundToGrid = 0x0004,

	/// <summary>
	/// Component has a uniform scale.
	/// </summary>
	HasScale = 0x0008,

	/// <summary>
	/// More components follow.
	/// </summary>
	MoreComponents = 0x0020,

	/// <summary>
	/// Component has independent x and y scales.
	/// </summary>
	HasXYScale = 0x0040,

	/// <summary>
	/// Component has a full 2x2 transformation matrix.
	/// </summary>
	HasTwoByTwo = 0x0080,

	/// <summary>
	/// Instructions follow all components.
	/// </summary>
	HasInstructions = 0x0100,

	/// <summary>
	/// Use this component's metrics in the compound glyph.
	/// </summary>
	UseMyMetrics = 0x0200,

	/// <summary>
	/// Components overlap.
	/// </summary>
	OverlapCompound = 0x0400
}

/// <summary>
/// Enumerates the platform families that can appear in TrueType name records.
/// </summary>
public enum TtfPlatFormId : short
{
#pragma warning disable CS1591
        Unicode = 0,
        Macintosh = 1,
        Microsoft = 3
#pragma warning restore CS1591
}

/// <summary>
/// Identifies the platform-specific encodings associated with a <see cref="TtfPlatFormId"/>.
/// </summary>
public enum TtfPlatformSpecificID : short
{
#pragma warning disable CS1591
        MAC_ROMAN = 0,
        UNICODE_DEFAULT = 0,
        UNICODE_V11 = 1,
        UNICODE_V2 = 3
#pragma warning restore CS1591
}

/// <summary>
/// Lists the language identifiers used by TrueType name records for localization.
/// </summary>
public enum TtfLanguageID : ushort
{
#pragma warning disable CS1591
        MAC_English = 0,
        MAC_French = 1,
        MAC_German = 2,
        MAC_Italian = 3,
	MAC_Dutch = 4,
	MAC_Swedish = 5,
	MAC_Spanish = 6,
	MAC_Danish = 7,
	MAC_Portuguese = 8,
	MAC_Norwegian = 9,
	MAC_Hebrew = 10,
	MAC_Japanese = 11,
	MAC_Arabic = 12,
	MAC_Finnish = 13,
	MAC_Greek = 14,
	MAC_Icelandic = 15,
	MAC_Maltese = 16,
	MAC_Turkish = 17,
	MAC_Croatian = 18,
	MAC_Chinese_traditional = 19,
	MAC_Urdu = 20,
	MAC_Hindi = 21,
	MAC_Thai = 22,
	MAC_Korean = 23,
	MAC_Lithuanian = 24,
	MAC_Polish = 25,
	MAC_Hungarian = 26,
	MAC_Estonian = 27,
	MAC_Latvian = 28,
	MAC_Sami = 29,
	MAC_Faroese = 30,
	MAC_Farsi_Persian = 31,
	MAC_Russian = 32,
	MAC_Chinese_simplified = 33,
	MAC_Flemish = 34,
	MAC_Irish_Gaelic = 35,
	MAC_Albanian = 36,
	MAC_Romanian = 37,
	MAC_Czech = 38,
	MAC_Slovak = 39,
	MAC_Slovenian = 40,
	MAC_Yiddish = 41,
	MAC_Serbian = 42,
	MAC_Macedonian = 43,
	MAC_Bulgarian = 44,
	MAC_Ukrainian = 45,
	MAC_Byelorussian = 46,
	MAC_Uzbek = 47,
	MAC_Kazakh = 48,
	MAC_Azerbaijani_Cyrillic_script = 49,
	MAC_Azerbaijani_Arabic_script = 50,
	MAC_Armenian = 51,
	MAC_Georgian = 52,
	MAC_Moldavian = 53,
	MAC_Kirghiz = 54,
	MAC_Tajiki = 55,
	MAC_Turkmen = 56,
	MAC_Mongolian_Mongolian_script = 57,
	MAC_Mongolian_Cyrillic_script = 58,
	MAC_Pashto = 59,
	MAC_Kurdish = 60,
	MAC_Kashmiri = 61,
	MAC_Sindhi = 62,
	MAC_Tibetan = 63,
	MAC_Nepali = 64,
	MAC_Sanskrit = 65,
	MAC_Marathi = 66,
	MAC_Bengali = 67,
	MAC_Assamese = 68,
	MAC_Gujarati = 69,
	MAC_Punjabi = 70,
	MAC_Oriya = 71,
	MAC_Malayalam = 72,
	MAC_Kannada = 73,
	MAC_Tamil = 74,
	MAC_Telugu = 75,
	MAC_Sinhalese = 76,
	MAC_Burmese = 77,
	MAC_Khmer = 78,
	MAC_Lao = 79,
	MAC_Vietnamese = 80,
	MAC_Indonesian = 81,
	MAC_Tagalog = 82,
	MAC_Malay_Roman_script = 83,
	MAC_Malay_Arabic_script = 84,
	MAC_Amharic = 85,
	MAC_Tigrinya = 86,
	MAC_Galla = 87,
	MAC_Somali = 88,
	MAC_Swahili = 89,
	MAC_Kinyarwanda_Ruanda = 90,
	MAC_Rundi = 91,
	MAC_Nyanja_Chewa = 92,
	MAC_Malagasy = 93,
	MAC_Esperanto = 94,
	MAC_Welsh = 128,
	MAC_Basque = 129,
	MAC_Catalan = 130,
	MAC_Latin = 131,
	MAC_Quechua = 132,
	MAC_Guarani = 133,
	MAC_Aymara = 134,
	MAC_Tatar = 135,
	MAC_Uighur = 136,
	MAC_Dzongkha = 137,
	MAC_Javanese_Roman_script = 138,
	MAC_Sundanese_Roman_script = 139,
	MAC_Galician = 140,
	MAC_Afrikaans = 141,
	MAC_Breton = 142,
	MAC_Inuktitut = 143,
	MAC_Scottish_Gaelic = 144,
	MAC_Manx_Gaelic = 145,
	MAC_Irish_Gaelic_with_dot_above = 146,
	MAC_Tongan = 147,
	MAC_Greek_polytonic = 148,
	MAC_Greenlandic = 149,
	MAC_Azerbaijani_Roman_script = 150,

	MS_Arabic_Saudi_Arabia = 1025,
	MS_Bulgarian = 1026,
	MS_Catalan = 1027,
	MS_Chinese_Taiwan = 1028,
	MS_Czech = 1029,
	MS_Danish = 1030,
	MS_German_Germany = 1031,
	MS_Greek = 1032,
	MS_English_United_States = 1033,
	MS_Spanish_Spain_Traditional_Sort = 1034,
	MS_Finnish = 1035,
	MS_French_France = 1036,
	MS_Hebrew = 1037,
	MS_Hungarian = 1038,
	MS_Icelandic = 1039,
	MS_Italian_Italy = 1040,
	MS_Japanese = 1041,
	MS_Korean = 1042,
	MS_Dutch_Netherlands = 1043,
	MS_Norwegian_Bokmal = 1044,
	MS_Polish = 1045,
	MS_Portuguese_Brazil = 1046,
	MS_Rhaeto_Romanic = 1047,
	MS_Romanian = 1048,
	MS_Russian = 1049,
	MS_Croatian = 1050,
	MS_Slovak = 1051,
	MS_Albanian_Albania = 1052,
	MS_Swedish = 1053,
	MS_Thai = 1054,
	MS_Turkish = 1055,
	MS_Urdu_Pakistan = 1056,
	MS_Indonesian = 1057,
	MS_Ukrainian = 1058,
	MS_Belarusian = 1059,
	MS_Slovenian = 1060,
	MS_Estonian = 1061,
	MS_Latvian = 1062,
	MS_Lithuanian = 1063,
	MS_Tajik = 1064,
	MS_Persian = 1065,
	MS_Vietnamese = 1066,
	MS_Armenian_Armenia = 1067,
	MS_Azeri_Latin = 1068,
	MS_Basque = 1069,
	MS_Sorbian = 1070,
	MS_FYRO_Macedonian = 1071,
	MS_Sutu = 1072,
	MS_Tsonga = 1073,
	MS_Tswana = 1074,
	MS_Venda = 1075,
	MS_Xhosa = 1076,
	MS_Zulu = 1077,
	MS_Afrikaans_South_Africa = 1078,
	MS_Georgian = 1079,
	MS_Faroese = 1080,
	MS_Hindi = 1081,
	MS_Maltese = 1082,
	MS_Sami = 1083,
	MS_Gaelic_Scotland = 1084,
	MS_Yiddish = 1085,
	MS_Malay_Malaysia = 1086,
	MS_Kazakh = 1087,
	MS_Kyrgyz_Cyrillic = 1088,
	MS_Swahili = 1089,
	MS_Turkmen = 1090,
	MS_Uzbek_Latin = 1091,
	MS_Tatar = 1092,
	MS_Bengali_India = 1093,
	MS_Punjabi = 1094,
	MS_Gujarati = 1095,
	MS_Oriya = 1096,
	MS_Tamil = 1097,
	MS_Telugu = 1098,
	MS_Kannada = 1099,
	MS_Malayalam = 1100,
	MS_Assamese = 1101,
	MS_Marathi = 1102,
	MS_Sanskrit = 1103,
	MS_Mongolian_Cyrillic = 1104,
	MS_Tibetan_Peoples_Republic_of_China = 1105,
	MS_Welsh = 1106,
	MS_Khmer = 1107,
	MS_Lao = 1108,
	MS_Burmese = 1109,
	MS_Galician = 1110,
	MS_Konkani = 1111,
	MS_Manipuri = 1112,
	MS_Sindhi_India = 1113,
	MS_Syriac = 1114,
	MS_Sinhalese_Sri_Lanka = 1115,
	MS_Cherokee_United_States = 1116,
	MS_Inuktitut = 1117,
	MS_Amharic_Ethiopia = 1118,
	MS_Tamazight_Arabic = 1119,
	MS_Kashmiri_Arabic = 1120,
	MS_Nepali = 1121,
	MS_Frisian_Netherlands = 1122,
	MS_Pashto = 1123,
	MS_Filipino = 1124,
	MS_Divehi = 1125,
	MS_Edo = 1126,
	MS_Fulfulde_Nigeria = 1127,
	MS_Hausa_Nigeria = 1128,
	MS_Ibibio_Nigeria = 1129,
	MS_Yoruba = 1130,
	MS_Quecha_Bolivia = 1131,
	MS_Sepedi = 1132,
	MS_Igbo_Nigeria = 1136,
	MS_Kanuri_Nigeria = 1137,
	MS_Oromo = 1138,
	MS_Tigrigna_Ethiopia = 1139,
	MS_Guarani_Paraguay = 1140,
	MS_Hawaiian_United_States = 1141,
	MS_Latin = 1142,
	MS_Somali = 1143,
	MS_Yi = 1144,
	MS_Papiamentu = 1145,
	MS_Uighur_China = 1152,
	MS_Maori_New_Zealand = 1153,
	MS_Arabic_Iraq = 2049,
	MS_Chinese_Peoples_Republic_of_China = 2052,
	MS_German_Switzerland = 2055,
	MS_English_United_Kingdom = 2057,
	MS_Spanish_Mexico = 2058,
	MS_French_Belgium = 2060,
	MS_Italian_Switzerland = 2064,
	MS_Dutch_Belgium = 2067,
	MS_Norwegian_Nynorsk = 2068,
	MS_Portuguese_Portugal = 2070,
	MS_Romanian_Moldava = 2072,
	MS_Russian_Moldava = 2073,
	MS_Serbian_Latin = 2074,
	MS_Swedish_Finland = 2077,
	MS_Urdu_India = 2080,
	MS_Azeri_Cyrillic = 2092,
	MS_Gaelic_Ireland = 2108,
	MS_Malay_Brunei_Darussalam = 2110,
	MS_Uzbek_Cyrillic = 2115,
	MS_Bengali_Bangladesh = 2117,
	MS_Punjabi_Pakistan = 2118,
	MS_Mongolian_Mongolian = 2128,
	MS_Tibetan_Bhutan = 2129,
	MS_Sindhi_Pakistan = 2137,
	MS_Tamazight_Latin = 2143,
	MS_Kashmiri_Devanagari = 2144,
	MS_Nepali_India = 2145,
	MS_Quecha_Ecuador = 2155,
	MS_Tigrigna_Eritrea = 2163,
	MS_Arabic_Egypt = 3073,
	MS_Chinese_Hong_Kong_SAR = 3076,
	MS_German_Austria = 3079,
	MS_English_Australia = 3081,
	MS_Spanish_Spain_Modern_Sort = 3082,
	MS_French_Canada = 3084,
	MS_Serbian_Cyrillic = 3098,
	MS_Quecha_Peru = 3179,
	MS_Arabic_Libya = 4097,
	MS_Chinese_Singapore = 4100,
	MS_German_Luxembourg = 4103,
	MS_English_Canada = 4105,
	MS_Spanish_Guatemala = 4106,
	MS_French_Switzerland = 4108,
	MS_Croatian_Bosnia_Herzegovina = 4122,
	MS_Arabic_Algeria = 5121,
	MS_Chinese_Macao_SAR = 5124,
	MS_German_Liechtenstein = 5127,
	MS_English_New_Zealand = 5129,
	MS_Spanish_Costa_Rica = 5130,
	MS_French_Luxembourg = 5132,
	MS_Bosnian_Bosnia_Herzegovina = 5146,
	MS_Arabic_Morocco = 6145,
	MS_English_Ireland = 6153,
	MS_Spanish_Panama = 6154,
	MS_French_Monaco = 6156,
	MS_Arabic_Tunisia = 7169,
	MS_English_South_Africa = 7177,
	MS_Spanish_Dominican_Republic = 7178,
	MS_French_West_Indies = 7180,
	MS_Arabic_Oman = 8193,
	MS_English_Jamaica = 8201,
	MS_Spanish_Venezuela = 8202,
	MS_French_Reunion = 8204,
	MS_Arabic_Yemen = 9217,
	MS_English_Caribbean = 9225,
	MS_Spanish_Colombia = 9226,
	MS_French_Democratic_Rep_of_Congo = 9228,
	MS_Arabic_Syria = 10241,
	MS_English_Belize = 10249,
	MS_Spanish_Peru = 10250,
	MS_French_Senegal = 10252,
	MS_Arabic_Jordan = 11265,
	MS_English_Trinidad = 11273,
	MS_Spanish_Argentina = 11274,
	MS_French_Cameroon = 11276,
	MS_Arabic_Lebanon = 12289,
	MS_English_Zimbabwe = 12297,
	MS_Spanish_Ecuador = 12298,
	MS_French_Cote_dIvoire = 12300,
	MS_Arabic_Kuwait = 13313,
	MS_English_Philippines = 13321,
	MS_Spanish_Chile = 13322,
	MS_French_Mali = 13324,
	MS_Arabic_UAE = 14337,
	MS_English_Indonesia = 14345,
	MS_Spanish_Uruguay = 14346,
	MS_French_Morocco = 14348,
	MS_Arabic_Bahrain = 15361,
	MS_English_Hong_Kong_SAR = 15369,
	MS_Spanish_Paraguay = 15370,
	MS_French_Haiti = 15372,
	MS_Arabic_Qatar = 16385,
	MS_English_India = 16393,
	MS_Spanish_Bolivia = 16394,
	MS_English_Malaysia = 17417,
	MS_Spanish_El_Salvador = 17418,
	MS_English_Singapore = 18441,
	MS_Spanish_Honduras = 18442,
	MS_Spanish_Nicaragua = 19466,
	MS_Spanish_Puerto_Rico = 20490,
	MS_Spanish_United_States = 21514,
        MS_Spanish_Latin_America = 58378,
        MS_French_North_Africa = 58380,
#pragma warning restore CS1591
}

/// <summary>
/// Represents the name identifiers stored in a TrueType font name table.
/// </summary>
public enum TtfNameID : short
{
#pragma warning disable CS1591
        COPYRIGHT = 0,
        FAMILY = 1,
        SUBFAMILY = 2,
        SUBFAMILY_UNIQUE = 3,
	FULL_NAME = 4,
	VERSION = 5,
	POSTSCRIPT_NAME = 6,
	TRADEMARK = 7,
	MANUFACTURER = 8,
	DESIGNER = 9,
	DESCRIPTION = 10,
	FONTVENDOR_URL = 11,
	FONTDESIGNER_URL = 12,
	LICENSEDESCRIPTION = 13,
	LICENSEINFORMATION_URL = 14,
	PREFERREDFAMILY = 16,
	PREFEREDSUBFAMILY = 17,
	COMPATIBLEFULL = 18,
	SAMPLETEXT = 19,
	OPENTYPE1 = 20,
	OPENTYPE2 = 21,
        OPENTYPE3 = 22,
        OPENTYPE4 = 23,
        OPENTYPE5 = 24,
        VARIATIONSPOSTSCRIPT_NAMEPREFIX = 25
#pragma warning restore CS1591
}