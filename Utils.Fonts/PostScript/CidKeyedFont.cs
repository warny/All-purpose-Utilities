using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Fonts.PostScript;

/// <summary>
/// Very small loader for CID-Keyed Type&nbsp;1 PostScript fonts. Only a subset
/// of the format is implemented. Refer to
/// <see href="https://adobe-type-tools.github.io/font-tech-notes/pdfs/5014.CIDFont_Spec.pdf">Adobe Technical Note 5014</see>
/// for the full specification.
/// </summary>
public class CidKeyedFont : IFont
{
    /// <summary>Glyph table indexed by CID.</summary>
    private readonly Dictionary<int, PostScriptGlyph> _glyphs;

    /// <summary>
    /// Initializes a new instance with the specified glyph collection.
    /// </summary>
    /// <param name="glyphs">Mapping from CID to glyph.</param>
    private CidKeyedFont(Dictionary<int, PostScriptGlyph> glyphs)
    {
        _glyphs = glyphs;
    }

    /// <inheritdoc />
    /// <remarks>Glyphs are looked up by their character code treated as CID.</remarks>
    public IGlyph GetGlyph(char c) => _glyphs.TryGetValue(c, out var g) ? g : null;

    /// <inheritdoc />
    /// <remarks>No kerning data is parsed from the font.</remarks>
    public float GetSpacingCorrection(char before, char after) => 0f;

    /// <summary>
    /// Loads a CID-Keyed PostScript Type&nbsp;1 <em>PFA</em> font stream.
    /// </summary>
    /// <param name="stream">ASCII Type&nbsp;1 font stream.</param>
    /// <returns>A new <see cref="CidKeyedFont"/>.</returns>
    public static CidKeyedFont LoadPfa(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII);
        string pfaText = reader.ReadToEnd();
        int eexecIndex = pfaText.IndexOf("eexec", StringComparison.OrdinalIgnoreCase);
        if (eexecIndex < 0)
            throw new InvalidDataException("Invalid PFA file: missing eexec section");
        eexecIndex += 5;
        var hexBuilder = new StringBuilder();
        for (int i = eexecIndex; i < pfaText.Length; i++)
        {
            char c = pfaText[i];
            if (char.IsWhiteSpace(c))
                continue;
            if (pfaText.AsSpan(i).StartsWith("cleartomark"))
                break;
            if (Uri.IsHexDigit(c))
                hexBuilder.Append(c);
        }
        byte[] encrypted = PostScriptFont.ConvertHex(hexBuilder.ToString());
        byte[] decrypted = PostScriptFont.DecryptType1(encrypted, 55665, 4);
        string decryptedText = Encoding.ASCII.GetString(decrypted);
        return ParseCidType1(decryptedText);
    }

    /// <summary>
    /// Loads a CID-Keyed PostScript Type&nbsp;1 <em>PFB</em> font stream.
    /// </summary>
    /// <param name="stream">Binary PFB font data.</param>
    /// <returns>A new <see cref="CidKeyedFont"/>.</returns>
    public static CidKeyedFont LoadPfb(Stream stream)
    {
        using var br = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        var sb = new StringBuilder();
        while (true)
        {
            int marker = br.ReadByte();
            if (marker != 0x80)
                throw new InvalidDataException("Invalid PFB marker");
            int type = br.ReadByte();
            if (type == 3)
                break;
            int len = br.ReadInt32();
            byte[] data = br.ReadBytes(len);
            switch (type)
            {
                case 1:
                    sb.Append(Encoding.ASCII.GetString(data));
                    break;
                case 2:
                    foreach (byte b in data)
                        sb.AppendFormat("{0:X2}", b);
                    break;
                default:
                    throw new InvalidDataException($"Unknown PFB block type {type}");
            }
        }
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes(sb.ToString()));
        return LoadPfa(ms);
    }

    /// <summary>
    /// Parses the decrypted portion of a CID-keyed Type&nbsp;1 font.
    /// </summary>
    /// <param name="text">ASCII text after eexec decryption.</param>
    /// <returns>Constructed <see cref="CidKeyedFont"/>.</returns>
    private static CidKeyedFont ParseCidType1(string text)
    {
        var glyphs = new Dictionary<int, PostScriptGlyph>();
        int lenIV = 4;
        var mLenIv = Regex.Match(text, @"/lenIV\s+(\d+)");
        if (mLenIv.Success)
            lenIV = int.Parse(mLenIv.Groups[1].Value, CultureInfo.InvariantCulture);

        var csMatch = Regex.Match(text, @"/CharStrings\s+\d+\s+dict\s+dup\s+begin(?<cs>.*)end", RegexOptions.Singleline);
        if (!csMatch.Success)
            return new CidKeyedFont(glyphs);
        var csSection = csMatch.Groups["cs"].Value;
        var glyphRegex = new Regex(@"(?<cid>\d+)\s+(?<len>\d+)\s+RD\s*(?<data>[0-9A-Fa-f]+)\s+ND", RegexOptions.Singleline);
        foreach (Match g in glyphRegex.Matches(csSection))
        {
            int cid = int.Parse(g.Groups["cid"].Value, CultureInfo.InvariantCulture);
            int len = int.Parse(g.Groups["len"].Value, CultureInfo.InvariantCulture);
            string hex = g.Groups["data"].Value;
            byte[] data = PostScriptFont.ConvertHex(hex);
            byte[] plain = PostScriptFont.DecryptType1(data, 4330, lenIV).Take(len).ToArray();
            var commands = PostScriptFont.ParseCharString(plain, out float width, out float height, out float baseLine);
            glyphs[cid] = new PostScriptGlyph(width, height, baseLine, commands);
        }
        return new CidKeyedFont(glyphs);
    }
}
