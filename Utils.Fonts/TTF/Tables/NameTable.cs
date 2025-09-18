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

