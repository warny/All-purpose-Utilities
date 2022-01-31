using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'post' table contains information needed to use a TrueType font on a PostScript printer. It contains the data needed for the FontInfo 
/// dictionary entry as well as the PostScript names for all of the glyphs in the font. It also contains memory usage information needed by 
/// the PostScript driver for memory management.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6post.html"/>
[TTFTable(TrueTypeTableTypes.Tags.post)]
public class PostTable : TrueTypeTable
{
	internal class PostMap
	{
		internal virtual short GetCharIndex(string charName) => 0;

		internal virtual string GetCharName(char charIndex) => null;

		internal virtual int Length => 0;

		internal virtual void WriteData(Writer data) { }

		internal virtual void ReadData(Reader data) { }
	}

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

		internal override short GetCharIndex(string charName)
		{
			for (int i = 0; i < stdNames.Length; i++)
			{
				if (string.Equals(charName, (object)stdNames[i]))
				{
					return (short)i;
				}
			}
			return 0;
		}

		internal override string GetCharName(char charIndex)
		{
			return stdNames[(uint)charIndex];
		}
	}

	internal class PostMapFormat2 : PostMapFormat0
	{
		internal short[] glyphNameIndex;

		internal string[] glyphNames;

		internal override int Length
		{
			get
			{
				int num = 2 + 2 * glyphNameIndex.Length;
				for (int i = 0; i < glyphNames.Length; i++)
				{
					num += glyphNames[i].Length + 1;
				}
				return num;
			}
		}

		internal override short GetCharIndex(string charName)
		{
			int num = -1;
			for (int i = 0; i < glyphNames.Length; i++)
			{
				if (string.Equals(charName, (object)glyphNames[i]))
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

		internal override string GetCharName(char charIndex)
		{
			if ((int)charIndex >= stdNames.Length)
			{
				return glyphNames[(int)charIndex - stdNames.Length];
			}
			string charName = base.GetCharName(charIndex);
			return charName;
		}

		internal override void WriteData(Writer data)
		{
			data.WriteInt16((short)glyphNameIndex.Length, true);
			for (int i = 0; i < glyphNameIndex.Length; i++)
			{
				data.WriteInt16(glyphNameIndex[i], true);
			}
			for (int i = 0; i < glyphNames.Length; i++)
			{
				data.WriteVariableLengthString(glyphNames[i], Encoding.Default, bigIndian: true, sizeLength: 1);
			}
			data.Seek(0, SeekOrigin.Begin);
		}

		internal override void ReadData(Reader data)
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

	public int Format { get; set; }
	public int ItalicAngle { get; set; }
	public short UnderlinePosition { get; set; }
	public short UnderlineThickness { get; set; }
	public short IsFixedPitch { get; set; }
	public int MinMemType42 { get; set; }
	public int MaxMemType42 { get; set; }
	public int MinMemType1 { get; set; }
	public int MaxMemType1 { get; set; }

	private PostMap nameMap;

	public virtual short getGlyphNameIndex(string str)
	{
		short charIndex = nameMap.GetCharIndex(str);
		return charIndex;
	}

	protected internal PostTable() : base(TrueTypeTableTypes.post)
	{
		nameMap = new PostMap();
	}

	public override int Length
	{
		get
		{
			int num = 32;
			if (nameMap is not null)
			{
				num += nameMap.Length;
			}
			return num;
		}
	}

	public override void WriteData(Writer data)
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

	public override void ReadData(Reader data)
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

		if (nameMap == null)
		{
			nameMap = new PostMap();
			Console.WriteLine($"Unknown post map type: {Format:X2}");
		}
		nameMap.ReadData(data);
	}
}

