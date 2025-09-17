using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Fonts.TTF;

namespace Utils.Fonts.PostScript;

/// <summary>
/// Minimal loader for PostScript Type&#160;42 fonts which encapsulate a TrueType font.
/// This implementation extracts the embedded TrueType data from the <c>/sfnts</c> array and
/// delegates glyph retrieval to <see cref="TrueTypeFont"/>.  For the official format see
/// <see href="https://adobe-type-tools.github.io/font-tech-notes/pdfs/5012.Type42_Spec.pdf">Adobe Technical Note 5012</see>.
/// </summary>
public class Type42Font : IFont
{
    /// <summary>Parsed TrueType font extracted from the Type&#160;42 wrapper.</summary>
    private readonly TrueTypeFont _ttf;

    /// <summary>
    /// Initializes a new instance using the provided TrueType font.
    /// </summary>
    /// <param name="ttf">Parsed TrueType font.</param>
    private Type42Font(TrueTypeFont ttf)
    {
        _ttf = ttf;
    }

    /// <summary>
    /// Loads a Type&#160;42 font from the provided PostScript stream.
    /// </summary>
    /// <param name="stream">Stream containing the Type&#160;42 font.</param>
    /// <returns>An instance of <see cref="Type42Font"/>.</returns>
    public static Type42Font Load(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        string ps = reader.ReadToEnd();

        var match = Regex.Match(ps, @"/sfnts\s*\[(?<data>[^\]]+)\]", RegexOptions.Singleline);
        if (!match.Success)
            throw new InvalidDataException("Invalid Type 42 font: missing sfnts array");

        string dataBlock = match.Groups["data"].Value;
        var hexRegex = new Regex("<(?<hex>[0-9A-Fa-f]+)>");
        var sb = new StringBuilder();
        foreach (Match m in hexRegex.Matches(dataBlock))
        {
            sb.Append(m.Groups["hex"].Value);
        }

        byte[] bytes = PostScriptFont.ConvertHex(sb.ToString());
        var ttf = TrueTypeFont.ParseFont(bytes);
        return new Type42Font(ttf);
    }

    /// <inheritdoc />
    /// <remarks>Glyphs are served directly from the embedded TrueType font.</remarks>
    public IGlyph GetGlyph(char c) => _ttf.GetGlyph(c);

    /// <inheritdoc />
    public float GetSpacingCorrection(char before, char after) => _ttf.GetSpacingCorrection(before, after);
}
