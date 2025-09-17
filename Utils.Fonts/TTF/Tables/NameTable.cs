using System;
using System.Collections.Generic;
using System.Text;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.Fonts.TTF;

/// <summary>
/// The name table (tag: 'name') allows inclusion of human‑readable names for features and settings,
/// copyright notices, font names, style names, and other information related to the font. These
/// name strings can be provided in any language, and are referenced by other TrueType tables.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6name.html"/>
[TTFTable(TableTypes.Tags.NAME)]
public class NameTable : TrueTypeTable
{
	/// <summary>
	/// Represents a record in the name table, defined by the triplet
	/// (PlatformId, PlatformSpecificId, LanguageId) and a NameId.
	/// </summary>
	public class NameRecord : IEquatable<NameRecord>
	{
		/// <summary>
		/// Gets the platform ID.
		/// </summary>
		public TtfPlatFormId PlatformID { get; }

		/// <summary>
		/// Gets the platform-specific ID.
		/// </summary>
		public TtfPlatformSpecificID PlatformSpecificID { get; }

		/// <summary>
		/// Gets the language ID.
		/// </summary>
		public TtfLanguageID LanguageID { get; }

		/// <summary>
		/// Gets the name ID.
		/// </summary>
		public TtfNameID NameID { get; }

		internal NameRecord(TtfPlatFormId platformID, TtfPlatformSpecificID platformSpecificID, TtfLanguageID languageID, TtfNameID nameID)
		{
			PlatformID = platformID;
			PlatformSpecificID = platformSpecificID;
			LanguageID = languageID;
			NameID = nameID;
		}

		/// <inheritdoc/>
		public virtual bool Equals(NameRecord other)
		{
			return other != null
				&& PlatformID == other.PlatformID
				&& PlatformSpecificID == other.PlatformSpecificID
				&& LanguageID == other.LanguageID
				&& NameID == other.NameID;
		}

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is NameRecord nr && Equals(nr);

		/// <inheritdoc/>
		public override int GetHashCode() => ObjectUtils.ComputeHash(PlatformID, PlatformSpecificID, LanguageID, NameID);

		/// <inheritdoc/>
		public override string ToString() => $"{PlatformID} - {PlatformSpecificID} - {LanguageID} - {NameID}";
	}

	private readonly Dictionary<NameRecord, string> records = new Dictionary<NameRecord, string>();

	/// <summary>
	/// Initializes a new instance of the <see cref="NameTable"/> class.
	/// </summary>
	protected internal NameTable() : base(TableTypes.NAME) { }

	/// <summary>
	/// Gets the appropriate encoding for a given name record by using the TtfEncoderFactory.
	/// </summary>
	/// <param name="record">The name record for which to obtain the encoding.</param>
	/// <returns>The selected <see cref="Encoding"/>.</returns>
	public static Encoding GetEncoding(NameRecord record) =>
		TtfEncoderFactory.GetEncoding(record.PlatformID, record.PlatformSpecificID, record.LanguageID);

	/// <summary>
	/// Gets the total length (in bytes) of the name table.
	/// </summary>
	public override int Length
	{
		get {
			int result = 6 + 12 * Count;
			foreach (var record in records)
			{
				Encoding encoding = GetEncoding(record.Key);
				result += encoding.GetByteCount(record.Value);
			}
			return result;
		}
	}

	/// <summary>
	/// Gets the number of name records in the table.
	/// </summary>
	public virtual short Count => (short)records.Count;

	/// <summary>
	/// Gets or sets the table format.
	/// </summary>
	public virtual short Format { get; set; }

	/// <summary>
	/// Retrieves the name string associated with the specified name record triplet.
	/// </summary>
	/// <param name="platformID">The platform ID.</param>
	/// <param name="platformSpecificID">The platform-specific ID.</param>
	/// <param name="languageID">The language ID.</param>
	/// <param name="nameID">The name ID.</param>
	/// <returns>The corresponding name string.</returns>
	public virtual string GetRecord(TtfPlatFormId platformID, TtfPlatformSpecificID platformSpecificID, TtfLanguageID languageID, TtfNameID nameID)
		=> records[new NameRecord(platformID, platformSpecificID, languageID, nameID)];

	/// <summary>
	/// Adds a name record to the table.
	/// </summary>
	/// <param name="platformID">The platform ID.</param>
	/// <param name="platformSpecificID">The platform-specific ID.</param>
	/// <param name="languageID">The language ID.</param>
	/// <param name="nameID">The name ID.</param>
	/// <param name="str">The name string.</param>
	public virtual void AddRecord(TtfPlatFormId platformID, TtfPlatformSpecificID platformSpecificID, TtfLanguageID languageID, TtfNameID nameID, string str)
		=> records[new NameRecord(platformID, platformSpecificID, languageID, nameID)] = str;

	/// <summary>
	/// Removes the specified name record from the table.
	/// </summary>
	/// <param name="platformID">The platform ID.</param>
	/// <param name="platformSpecificID">The platform-specific ID.</param>
	/// <param name="languageID">The language ID.</param>
	/// <param name="nameID">The name ID.</param>
	public virtual void RemoveRecord(TtfPlatFormId platformID, TtfPlatformSpecificID platformSpecificID, TtfLanguageID languageID, TtfNameID nameID)
		=> records.Remove(new NameRecord(platformID, platformSpecificID, languageID, nameID));

	/// <summary>
	/// Determines whether the table contains any records for the specified platform.
	/// </summary>
	/// <param name="platformId">The platform ID.</param>
	/// <returns><see langword="true"/> if records exist for the specified platform; otherwise, <see langword="false"/>.</returns>
	public virtual bool HasRecords(TtfPlatFormId platformId)
	{
		foreach (NameRecord nameRecord in records.Keys)
		{
			if (nameRecord.PlatformID == platformId)
				return true;
		}
		return false;
	}

	/// <summary>
	/// Determines whether the table contains any records for the specified platform and platform-specific ID.
	/// </summary>
	/// <param name="platformId">The platform ID.</param>
	/// <param name="platformSpecificID">The platform-specific ID.</param>
	/// <returns><see langword="true"/> if records exist for the specified platform and platform-specific ID; otherwise, <see langword="false"/>.</returns>
	public virtual bool HasRecords(TtfPlatFormId platformId, TtfPlatformSpecificID platformSpecificID)
	{
		foreach (NameRecord nameRecord in records.Keys)
		{
			if (nameRecord.PlatformID == platformId && nameRecord.PlatformSpecificID == platformSpecificID)
				return true;
		}
		return false;
	}

	/// <inheritdoc/>
	public override void ReadData(Reader data)
	{
		Format = data.Read<Int16>();
		int count = data.Read<Int16>();
		int stringOffset = data.Read<Int16>();
		for (int i = 0; i < count; i++)
		{
			var platformId = (TtfPlatFormId)data.Read<Int16>();
			var platformSpecificId = (TtfPlatformSpecificID)data.Read<Int16>();
			var languageId = (TtfLanguageID)data.Read<Int16>();
			var nameId = (TtfNameID)data.Read<Int16>();
			var length = data.Read<Int16>();
			var offset = data.Read<Int16>();
			data.Push();
			Reader val = data.Slice(stringOffset + offset, length);
			data.Pop();
			Encoding encoding = TtfEncoderFactory.GetEncoding(platformId, platformSpecificId, languageId);
			string str = val.ReadFixedLengthString(length, encoding);
			AddRecord(platformId, platformSpecificId, languageId, nameId, str);
		}
	}

	/// <inheritdoc/>
	public override void WriteData(Writer data)
	{
		data.Write<Int16>(Format);
		data.Write<Int16>(Count);
		data.Write<Int16>((short)(6 + 12 * Count));
		int offset = 0;
		foreach (var record in records)
		{
			NameRecord nameRecord = record.Key;
			string text = record.Value;
			Encoding encoding = TtfEncoderFactory.GetEncoding(nameRecord.PlatformID, nameRecord.PlatformSpecificID, nameRecord.LanguageID);
			int length = encoding.GetByteCount(text);
			data.Write<Int16>((short)nameRecord.PlatformID);
			data.Write<Int16>((short)nameRecord.PlatformSpecificID);
			data.Write<Int16>((short)nameRecord.LanguageID);
			data.Write<Int16>((short)nameRecord.NameID);
			data.Write<Int16>((short)length);
			data.Write<Int16>((short)offset);
			data.Push();
			data.Seek((6 + 12 * Count) + offset, System.IO.SeekOrigin.Begin);
			data.WriteFixedLengthString(text, text.Length, encoding);
			data.Pop();
			offset += length;
		}
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		StringBuilder result = new StringBuilder();
		result.AppendLine($"    Format: {(int)Format}");
		result.AppendLine($"    Count : {(int)Count}");
		foreach (var record in records)
		{
			NameRecord nameRecord = record.Key;
			string text = record.Value;
			result.Append($"     platformID: {nameRecord.PlatformID}");
			result.Append($" - platformSpecificID: {nameRecord.PlatformSpecificID}");
			result.Append($" - languageID: {nameRecord.LanguageID}");
			result.Append($" - nameID: {nameRecord.NameID}");
			result.AppendLine();
			result.AppendLine($"      {text}");
		}
		return result.ToString();
	}
}

public enum TtfPlatFormId : short
{
	Unicode = 0,
	Macintosh = 1,
	Microsoft = 3
}

public enum TtfPlatformSpecificID : short
{
	MAC_ROMAN = 0,
	UNICODE_DEFAULT = 0,
	UNICODE_V11 = 1,
	UNICODE_V2 = 3
}

public enum TtfLanguageID : ushort
{
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

}

public enum TtfNameID : short
{
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
}


