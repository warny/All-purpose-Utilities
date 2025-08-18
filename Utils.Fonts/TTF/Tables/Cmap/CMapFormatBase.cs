using System;
using System.Collections.Generic;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.CMap;

/// <summary>
/// Represents the base class for CMap (character to glyph mapping) formats in a TrueType font.
/// </summary>
public abstract class CMapFormatBase
{
	/// <summary>
	/// Gets the format type.
	/// </summary>
	public virtual short Format { get; private set; }

	/// <summary>
	/// Gets the language identifier.
	/// </summary>
	public virtual short Language { get; private set; }

	/// <summary>
	/// Gets the length (in bytes) of the CMap format data.
	/// </summary>
	public abstract short Length { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="CMapFormatBase"/> class.
	/// </summary>
	/// <param name="format">The format type.</param>
	/// <param name="language">The language identifier.</param>
	protected CMapFormatBase(short format, short language)
	{
		Format = format;
		Language = language;
	}

	/// <summary>
	/// Creates an instance of a CMap format based on the specified format type.
	/// </summary>
	/// <param name="format">The format type.</param>
	/// <param name="language">The language identifier.</param>
	/// <returns>An instance of a derived <see cref="CMapFormatBase"/> class.</returns>
	/// <exception cref="NotSupportedException">Thrown if the format is not supported.</exception>
	public static CMapFormatBase CreateCMap(short format, short language) => format switch
	{
		0 => new CMapFormat0(language),
		4 => new CMapFormat4(language),
		_ => throw new NotSupportedException($"CMap format {format} is not supported.")
	};

	/// <summary>
	/// Maps a character to its corresponding glyph index.
	/// </summary>
	/// <param name="ch">The character to map.</param>
	/// <returns>The glyph index for the character.</returns>
	public abstract short Map(char ch);

	/// <summary>
	/// Performs a reverse mapping from a glyph index to the corresponding character.
	/// </summary>
	/// <param name="s">The glyph index.</param>
	/// <returns>The character corresponding to the glyph index.</returns>
	public abstract char ReverseMap(short s);

	/// <summary>
	/// Reads the format-specific data from the specified reader.
	/// </summary>
	/// <param name="i">The length (in bytes) of the data to read.</param>
	/// <param name="data">The reader to read data from.</param>
	public abstract void ReadData(int i, NewReader data);

	/// <summary>
	/// Writes the format-specific data to the specified writer.
	/// </summary>
	/// <param name="data">The writer to write data to.</param>
	public abstract void WriteData(NewWriter data);

	/// <summary>
	/// Reads a CMap format record from the provided reader.
	/// </summary>
	/// <param name="data">The reader from which to read the CMap data.</param>
	/// <returns>An instance of <see cref="CMapFormatBase"/> containing the parsed data.</returns>
	public static CMapFormatBase GetMap(NewReader data)
	{
		var format = data.ReadInt16(true);
		var length = data.ReadInt16(true);
		var language = data.ReadInt16(true);
		CMapFormatBase cMap = CreateCMap(format, language);
		cMap?.ReadData(length, data);
		return cMap;
	}

	/// <summary>
	/// Returns a string representation of the CMap format.
	/// </summary>
	/// <returns>A string describing the format, length, and language.</returns>
	public override string ToString() => $"        format: {Format}, length: {Length}, language: {Language}";
}
