using System;
using System.Text;
using System.Linq;
using System.Runtime.CompilerServices;
using Utils.IO.Serialization;
using Utils.Objects;
using System.IO;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'post' table contains information needed to use a TrueType font on a PostScript printer.
/// It provides data for the FontInfo dictionary, the PostScript names for all glyphs, and memory usage information
/// for the PostScript driver.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6post.html"/>
[TTFTable(TableTypes.Tags.POST)]
public class PostTable : TrueTypeTable
{
	#region Internal Mapping Classes

	/// <summary>
	/// Base class for the glyph name mapping in the 'post' table.
	/// </summary>
	internal class PostMap
	{
		/// <summary>
		/// Returns the glyph name index for the specified character name.
		/// </summary>
		/// <param name="charName">The character name.</param>
		/// <returns>The glyph name index.</returns>
		internal virtual short GetCharIndex(string charName) => 0;

		/// <summary>
		/// Returns the PostScript name corresponding to the given character index.
		/// </summary>
		/// <param name="charIndex">The character index.</param>
		/// <returns>The PostScript name.</returns>
		internal virtual string GetCharName(char charIndex) => null;

		/// <summary>
		/// Gets the length (in bytes) of the mapping data.
		/// </summary>
		internal virtual int Length => 0;

		/// <summary>
		/// Writes the mapping data to the specified writer.
		/// </summary>
		/// <param name="data">The writer.</param>
		internal virtual void WriteData(NewWriter data) { }

		/// <summary>
		/// Reads the mapping data from the specified reader.
		/// </summary>
		/// <param name="data">The reader.</param>
		internal virtual void ReadData(NewReader data) { }
	}

	/// <summary>
	/// Implements PostScript glyph mapping for Format 0.
	/// This format uses a standard set of glyph names.
	/// </summary>
	internal class PostMapFormat0 : PostMap
	{
		protected internal string[] stdNames = {
            /* 0 */     ".notdef", ".null", "nonmarkingreturn", "space", "exclam", "quotedbl", "numbersign", "dollar",
            /* 8 */     "percent", "ampersand", "quotesingle", "parenleft", "parenright", "asterisk", "plus", "comma",
            /* 16 */    "hyphen", "period", "slash", "zero", "one", "two", "three", "four",
            /* 24 */    "five", "six", "seven", "eight", "nine", "colon", "semicolon", "less", 
            /* 32 */    "equal", "greater", "question", "at", "A", "B", "C", "D",
            /* 40 */    "E", "F", "G", "H", "I", "J", "K", "L",
            /* 48 */    "M", "N", "O", "P", "Q", "R", "S", "T", 
            /* 56 */    "U", "V", "W", "X", "Y", "Z", "bracketleft", "ackslash",
            /* 64 */    "bracketright", "asciicircum", "underscore", "grave", "a", "b", "c", "d",
            /* 72 */    "e", "f", "g", "h", "i", "j", "k", "l", 
            /* 80 */    "m", "n", "o", "p", "q", "r", "s", "t",
            /* 88 */    "u", "v", "w", "x", "y", "z", "braceleft", "bar",
            /* 96 */    "braceright", "asciitilde", "Adieresis", "Aring", "Ccedilla", "Eacute", "Ntilde", "Odieresis",
            /* 104 */   "Udieresis", "aacute", "agrave", "acircumflex", "adieresis", "atilde", "aring", "ccedilla",
            /* 112 */   "eacute", "egrave", "ecircumflex", "edieresis", "iacute", "igrave", "icircumflex", "idieresis",
            /* 120 */   "ntilde", "oacute", "ograve", "ocircumflex", "odieresis", "otilde", "uacute", "ugrave", 
            /* 128 */   "ucircumflex", "udieresis", "dagger", "degree", "cent", "sterling", "section", "bullet",
            /* 136 */   "paragraph", "germandbls", "registered", "copyright", "trademark", "acute", "dieresis", "notequal",
            /* 144 */   "AE", "Oslash", "infinity", "plusminus", "lessequal", "greaterequal", "yen", "mu",
            /* 152 */   "partialdiff", "summation", "product", "pi", "integral", "ordfeminine", "ordmasculine", "Omega",
            /* 160 */   "ae", "oslash", "questiondown", "exclamdown", "logicalnot", "radical", "florin", "approxequal",
            /* 168 */   "Delta", "guillemotleft", "guillemotright", "ellipsis", "nonbreakingspace", "Agrave", "Atilde", "Otilde",
            /* 176 */   "OE", "oe", "endash", "emdash", "quotedblleft", "quotedblright", "quoteleft", "quoteright",
            /* 184 */   "divide", "lozenge", "ydieresis", "Ydieresis", "fraction", "currency", "guilsinglleft", "guilsinglright",
            /* 192 */   "fi", "fl", "daggerdbl", "periodcentered", "quotesinglbase", "quotedblbase", "perthousand", "Acircumflex",
            /* 200 */   "Ecircumflex", "Aacute", "Edieresis", "Egrave", "Iacute", "Icircumflex", "Idieresis", "Igrave",
            /* 208 */   "Oacute", "Ocircumflex", "apple", "Ograve", "Uacute", "Ucircumflex", "Ugrave", "dotlessi",
            /* 216 */   "circumflex", "tilde", "macron", "breve", "dotaccent", "ring", "cedilla", "hungarumlaut",
            /* 224 */   "ogonek", "caron", "Lslash", "lslash", "Scaron", "scaron", "Zcaron", "zcaron",
            /* 232 */   "brokenbar", "Eth", "eth", "Yacute", "yacute", "Thorn", "thorn", "minus",
            /* 240 */   "multiply", "onesuperior", "twosuperior", "threesuperior", "onehalf", "onequarter", "threequarters", "franc",
            /* 248 */   "Gbreve", "gbreve", "Idotaccent", "Scedilla", "scedilla", "Cacute", "cacute", "Ccaron",
            /* 256 */   "ccaron", "dcroat"
		};

		/// <summary>
		/// Returns the standard glyph index for the specified character name.
		/// </summary>
		/// <param name="charName">The character name to search for.</param>
		/// <returns>The glyph index if found; otherwise, 0.</returns>
		internal override short GetCharIndex(string charName)
		{
			for (int i = 0; i < stdNames.Length; i++)
			{
				if (string.Equals(charName, stdNames[i]))
				{
					return (short)i;
				}
			}
			return 0;
		}

		/// <summary>
		/// Returns the standard glyph name corresponding to the given character index.
		/// </summary>
		/// <param name="charIndex">The character index.</param>
		/// <returns>The corresponding glyph name.</returns>
		internal override string GetCharName(char charIndex)
		{
			return stdNames[(uint)charIndex];
		}
	}

	/// <summary>
	/// Implements an extended mapping format (Format 2) for glyph names.
	/// This format supports additional glyph names beyond the standard set.
	/// </summary>
	internal class PostMapFormat2 : PostMapFormat0
	{
		internal short[] glyphNameIndex;
		internal string[] glyphNames;

		/// <summary>
		/// Gets the total length (in bytes) of the mapping data.
		/// </summary>
		internal override int Length
		{
			get {
				int num = 2 + 2 * glyphNameIndex.Length;
				for (int i = 0; i < glyphNames.Length; i++)
				{
					num += glyphNames[i].Length + 1;
				}
				return num;
			}
		}

		/// <summary>
		/// Returns the glyph index for the specified character name.
		/// </summary>
		/// <param name="charName">The character name to map.</param>
		/// <returns>The corresponding glyph index.</returns>
		internal override short GetCharIndex(string charName)
		{
			int num = -1;
			for (int i = 0; i < glyphNames.Length; i++)
			{
				if (string.Equals(charName, glyphNames[i]))
				{
					num = (short)(stdNames.Length + i);
					break;
				}
			}
			if (num == -1)
			{
				num = base.GetCharIndex(charName);
			}
			for (int i = 0; i < glyphNameIndex.Length; i++)
			{
				if (glyphNameIndex[i] == num)
				{
					return (short)i;
				}
			}
			return 0;
		}

		/// <summary>
		/// Returns the glyph name corresponding to the specified character index.
		/// </summary>
		/// <param name="charIndex">The character index.</param>
		/// <returns>The corresponding glyph name.</returns>
		internal override string GetCharName(char charIndex)
		{
			if ((int)charIndex >= stdNames.Length)
			{
				return glyphNames[(int)charIndex - stdNames.Length];
			}
			string charName = base.GetCharName(charIndex);
			return charName;
		}

		/// <summary>
		/// Writes the mapping data for Format 2 to the specified writer.
		/// </summary>
		/// <param name="data">The writer to which data is written.</param>
		internal override void WriteData(NewWriter data)
		{
			data.WriteInt16((short)glyphNameIndex.Length, true);
			for (int i = 0; i < glyphNameIndex.Length; i++)
			{
				data.WriteInt16(glyphNameIndex[i], true);
			}
			for (int i = 0; i < glyphNames.Length; i++)
			{
                                data.WriteVariableLengthString(glyphNames[i], Encoding.Default, bigEndian: true, sizeLength: 1);
			}
			data.Seek(0, SeekOrigin.Begin);
		}

		/// <summary>
		/// Reads the mapping data for Format 2 from the specified reader.
		/// </summary>
		/// <param name="data">The reader from which to read data.</param>
		internal override void ReadData(NewReader data)
		{
			int length = data.ReadInt16(true);
			glyphNameIndex = new short[length];
			int maxGlyph = 257;
			for (int i = 0; i < length; i++)
			{
				glyphNameIndex[i] = data.ReadInt16(true);
				if (glyphNameIndex[i] > maxGlyph)
				{
					maxGlyph = glyphNameIndex[i];
				}
			}
			maxGlyph -= 257;
			glyphNames = new string[maxGlyph];
			Array.Fill(glyphNames, "");
			for (int i = 0; i < maxGlyph; i++)
			{
                                glyphNames[i] = data.ReadVariableLengthString(Encoding.Default, sizeLength: 1);
			}
		}
	}

	#endregion

	/// <summary>
	/// Gets or sets the post table format.
	/// </summary>
	public int Format { get; set; }

	/// <summary>
	/// Gets or sets the italic angle, in fixed-point format.
	/// </summary>
	public int ItalicAngle { get; set; }

	/// <summary>
	/// Gets or sets the underline position.
	/// </summary>
	public short UnderlinePosition { get; set; }

	/// <summary>
	/// Gets or sets the underline thickness.
	/// </summary>
	public short UnderlineThickness { get; set; }

	/// <summary>
	/// Gets or sets a flag indicating whether the font is fixed pitch.
	/// </summary>
	public short IsFixedPitch { get; set; }

	/// <summary>
	/// Gets or sets the minimum memory usage for Type 42 fonts.
	/// </summary>
	public int MinMemType42 { get; set; }

	/// <summary>
	/// Gets or sets the maximum memory usage for Type 42 fonts.
	/// </summary>
	public int MaxMemType42 { get; set; }

	/// <summary>
	/// Gets or sets the minimum memory usage for Type 1 fonts.
	/// </summary>
	public int MinMemType1 { get; set; }

	/// <summary>
	/// Gets or sets the maximum memory usage for Type 1 fonts.
	/// </summary>
	public int MaxMemType1 { get; set; }

	private PostMap nameMap;

	/// <summary>
	/// Returns the glyph name index for the specified PostScript glyph name.
	/// </summary>
	/// <param name="str">The PostScript glyph name.</param>
	/// <returns>The corresponding glyph name index.</returns>
	public virtual short getGlyphNameIndex(string str)
	{
		short charIndex = nameMap.GetCharIndex(str);
		return charIndex;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostTable"/> class.
	/// </summary>
	protected internal PostTable() : base(TableTypes.POST)
	{
		nameMap = new PostMap();
	}

	/// <summary>
	/// Gets the total length (in bytes) of the post table.
	/// </summary>
	public override int Length
	{
		get {
			int num = 32;
			if (nameMap is not null)
			{
				num += nameMap.Length;
			}
			return num;
		}
	}

	/// <summary>
	/// Writes the post table data to the specified writer.
	/// </summary>
	/// <param name="data">The writer to which the data is written.</param>
	public override void WriteData(NewWriter data)
	{
		data.WriteInt32(Format, true);
		data.WriteInt32(ItalicAngle, true);
		data.WriteInt16(UnderlinePosition, true);
		data.WriteInt16(UnderlineThickness, true);
		data.WriteInt16(IsFixedPitch, true);
		data.WriteInt16(0, true);
		data.WriteInt32(MinMemType42, true);
		data.WriteInt32(MaxMemType42, true);
		data.WriteInt32(MinMemType1, true);
		data.WriteInt32(MaxMemType1, true);
		this.nameMap?.WriteData(data);
	}

	/// <summary>
	/// Reads the post table data from the specified reader.
	/// </summary>
	/// <param name="data">The reader from which the data is read.</param>
	public override void ReadData(NewReader data)
	{
		Format = data.ReadInt32(true);
		ItalicAngle = data.ReadInt32(true);
		UnderlinePosition = data.ReadInt16(true);
		UnderlineThickness = data.ReadInt16(true);
		IsFixedPitch = data.ReadInt16(true);
		data.ReadInt16(true);
		MinMemType42 = data.ReadInt32(true);
		MaxMemType42 = data.ReadInt32(true);
		MinMemType1 = data.ReadInt32(true);
		MaxMemType1 = data.ReadInt32(true);

		nameMap = Format switch
		{
			0x10000 => new PostMapFormat0(),
			0x20000 => new PostMapFormat2(),
			0x30000 => new PostMap(),
			_ => null
		};

		if (nameMap is null)
		{
			nameMap = new PostMap();
			Console.WriteLine($"Unknown post map type: {Format:X2}");
		}
		nameMap.ReadData(data);
	}
}
