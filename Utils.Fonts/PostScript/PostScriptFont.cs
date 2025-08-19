using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Utils.IO.Serialization;

namespace Utils.Fonts.PostScript;

/// <summary>
/// Very small PostScript font loader based on a custom text representation.
/// The intention is to mimic a subset of the Type&nbsp;1 syntax without parsing
/// the full PostScript language.  Each glyph is declared as shown below:
///
/// <code>
/// Glyph: A
/// Width: 500
/// Height: 600
/// Baseline: 0
/// Path:
/// M 0 0
/// L 10 0
/// ...
/// EndGlyph
/// </code>
///
/// For a description of the real Type&nbsp;1 format see
/// <see href="https://adobe-type-tools.github.io/font-tech-notes/pdfs/T1_SPEC.pdf">Adobe Technical Note 5040</see>.
/// </summary>
public class PostScriptFont : IFont
{
    /// <summary>
    /// Storage of glyphs indexed by character code.
    /// </summary>
    private readonly Dictionary<char, PostScriptGlyph> _glyphs;

    /// <summary>
    /// Initializes the font with a set of parsed glyphs.
    /// </summary>
    /// <param name="glyphs">Glyph table built by one of the loader methods.</param>
    private PostScriptFont(Dictionary<char, PostScriptGlyph> glyphs)
    {
        _glyphs = glyphs;
    }

    /// <summary>
    /// Loads a font in the simplified text form used by this demo.  This is
    /// <strong>not</strong> a full Type&nbsp;1 parser but follows a similar
    /// structure for learning purposes.
    /// </summary>
    /// <param name="stream">Stream containing the custom font text.</param>
    /// <returns>Parsed <see cref="PostScriptFont"/> instance.</returns>
    public static PostScriptFont Load(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var glyphs = new Dictionary<char, PostScriptGlyph>();
        string? line;
        char currentChar = '\0';
        float width = 0, height = 0, baseline = 0;
        List<PostScriptGlyph.PathCommand>? commands = null;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;
            if (line.StartsWith("Glyph:", StringComparison.OrdinalIgnoreCase))
            {
                currentChar = line.Split(':', 2)[1].Trim()[0];
                commands = new();
            }
            else if (line.StartsWith("Width:", StringComparison.OrdinalIgnoreCase))
            {
                width = float.Parse(line.Split(':', 2)[1].Trim(), CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("Height:", StringComparison.OrdinalIgnoreCase))
            {
                height = float.Parse(line.Split(':', 2)[1].Trim(), CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("Baseline:", StringComparison.OrdinalIgnoreCase))
            {
                baseline = float.Parse(line.Split(':', 2)[1].Trim(), CultureInfo.InvariantCulture);
            }
            else if (line.Equals("EndGlyph", StringComparison.OrdinalIgnoreCase))
            {
                if (currentChar != '\0' && commands != null)
                {
                    glyphs[currentChar] = new PostScriptGlyph(width, height, baseline, commands);
                }
                currentChar = '\0';
                commands = null;
            }
            else if (commands != null)
            {
                ParsePathLine(line, commands);
            }
        }
        return new PostScriptFont(glyphs);
    }

    /// <summary>
    /// Parses a single <c>Path:</c> line from the simplified format and appends
    /// the resulting drawing command to <paramref name="commands"/>.
    /// </summary>
    /// <param name="line">Line of text describing a path command.</param>
    /// <param name="commands">Collection receiving the parsed command.</param>
    private static void ParsePathLine(string line, List<PostScriptGlyph.PathCommand> commands)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        switch (parts[0].ToUpperInvariant())
        {
            case "M":
                commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.MoveTo,
                    float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), 0, 0, 0, 0));
                break;
            case "L":
                commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo,
                    float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), 0, 0, 0, 0));
                break;
            case "C":
                commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.BezierTo,
                    float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture),
                    float.Parse(parts[3], CultureInfo.InvariantCulture), float.Parse(parts[4], CultureInfo.InvariantCulture),
                    float.Parse(parts[5], CultureInfo.InvariantCulture), float.Parse(parts[6], CultureInfo.InvariantCulture)));
                break;
            case "Z":
                commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.Close, 0, 0, 0, 0, 0, 0));
                break;
        }
    }

    /// <inheritdoc />
    /// <remarks>Only glyphs defined in the input are available.</remarks>
    public IGlyph GetGlyph(char c) => _glyphs.TryGetValue(c, out var g) ? g : null;

    /// <inheritdoc />
    /// <remarks>Kerning is not supported for this simple implementation.</remarks>
    public float GetSpacingCorrection(char before, char after) => 0f;

    /// <summary>
    /// Loads a PostScript Type&nbsp;1 <em>PFA</em> font stream.  The implementation
    /// only understands a tiny fraction of the format sufficient for the unit
    /// tests.  See
    /// <see href="https://adobe-type-tools.github.io/font-tech-notes/pdfs/T1_SPEC.pdf">Adobe Technical Note 5040</see>
    /// for the real specification.
    /// </summary>
    /// <param name="stream">Input PFA stream.</param>
    /// <returns>Instance of <see cref="PostScriptFont"/>.</returns>
    public static PostScriptFont LoadPfa(Stream stream)
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
            if (char.IsWhiteSpace(c)) continue;
            if (pfaText.Substring(i).StartsWith("cleartomark", StringComparison.OrdinalIgnoreCase))
                break;
            if (Uri.IsHexDigit(c)) hexBuilder.Append(c);
        }
        byte[] encrypted = ConvertHex(hexBuilder.ToString());
        byte[] decrypted = DecryptType1(encrypted, 55665, 4);
        string decryptedText = Encoding.ASCII.GetString(decrypted);
        return ParseType1(decryptedText);
    }

    /// <summary>
    /// Loads a binary Type&nbsp;1 (<em>PFB</em>) font using a <see cref="Reader"/>.
    /// Blocks from the PFB file are concatenated and translated to the ASCII
    /// representation consumed by the <see cref="LoadPfa"/> method.
    /// </summary>
    /// <param name="reader">Reader supplying PFB data.</param>
    /// <returns>Instance of <see cref="PostScriptFont"/>.</returns>
    public static PostScriptFont LoadPfb(Reader reader)
    {
        var sb = new StringBuilder();
        while (true)
        {
            int marker = reader.ReadByte();
            if (marker != 0x80)
                throw new InvalidDataException("Invalid PFB marker");
            int type = reader.ReadByte();
            if (type == 3)
                break;
            int len = reader.Read<int>();
            byte[] data = reader.ReadBytes(len);
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
    /// Loads a binary Type&nbsp;1 (<em>PFB</em>) font stream.
    /// </summary>
    /// <param name="stream">Input PFB stream.</param>
    /// <returns>Instance of <see cref="PostScriptFont"/>.</returns>
    public static PostScriptFont LoadPfb(Stream stream)
    {
        var reader = new Reader(stream);
        return LoadPfb(reader);
    }

    /// <summary>
    /// Writes ASCII Type&nbsp;1 content to the binary <em>PFB</em> format using a
    /// <see cref="Writer"/>.
    /// </summary>
    /// <param name="ascii">Plain ASCII Type&nbsp;1 data.</param>
    /// <param name="writer">Destination binary writer.</param>
    public static void WritePfb(string ascii, Writer writer)
    {
        if (ascii == null) throw new ArgumentNullException(nameof(ascii));
        if (writer == null) throw new ArgumentNullException(nameof(writer));

        byte[] asciiBytes = Encoding.ASCII.GetBytes(ascii);
        writer.WriteByte(0x80);
        writer.WriteByte(1);
        writer.Write<int>(asciiBytes.Length);
        writer.WriteBytes(asciiBytes);
        writer.WriteByte(0x80);
        writer.WriteByte(2);
        writer.Write<int>(0); // no binary segment present
        writer.WriteByte(0x80);
        writer.WriteByte(3);
    }

    /// <summary>
    /// Writes ASCII Type&nbsp;1 content to the binary <em>PFB</em> format.
    /// </summary>
    /// <param name="ascii">Plain ASCII Type&nbsp;1 data.</param>
    /// <param name="stream">Destination stream receiving the binary data.</param>
    public static void WritePfb(string ascii, Stream stream)
    {
        var writer = new Writer(stream);
        WritePfb(ascii, writer);
    }

    /// <summary>
    /// Parses the decrypted portion of a Type&nbsp;1 font.  Only the
    /// <c>CharStrings</c> dictionary is interpreted.  For the complete
    /// specification refer to
    /// <see href="https://adobe-type-tools.github.io/font-tech-notes/pdfs/T1_SPEC.pdf">Adobe Technical Note 5040</see>.
    /// </summary>
    /// <param name="text">ASCII text starting after the <c>eexec</c> section.</param>
    /// <returns>Newly created <see cref="PostScriptFont"/>.</returns>
    private static PostScriptFont ParseType1(string text)
    {
        var glyphs = new Dictionary<char, PostScriptGlyph>();
        int lenIV = 4;
        var mLenIv = Regex.Match(text, @"/lenIV\s+(\d+)");
        if (mLenIv.Success) lenIV = int.Parse(mLenIv.Groups[1].Value, CultureInfo.InvariantCulture);

        var csMatch = Regex.Match(text, @"/CharStrings\s+\d+\s+dict\s+begin(?<cs>.*)end", RegexOptions.Singleline);
        if (!csMatch.Success)
            return new PostScriptFont(glyphs);
        var csSection = csMatch.Groups["cs"].Value;
        var glyphRegex = new Regex(@"/(?<name>\S+)\s+(?<len>\d+)\s+RD\s*(?<data>[0-9A-Fa-f]+)\s+ND", RegexOptions.Singleline);
        foreach (Match g in glyphRegex.Matches(csSection))
        {
            string name = g.Groups["name"].Value;
            int len = int.Parse(g.Groups["len"].Value, CultureInfo.InvariantCulture);
            string hex = g.Groups["data"].Value;
            byte[] data = ConvertHex(hex);
            byte[] plain = DecryptType1(data, 4330, lenIV).Take(len).ToArray();
            var commands = ParseCharString(plain, out float width, out float height, out float baseLine);
            char ch = MapName(name);
            glyphs[ch] = new PostScriptGlyph(width, height, baseLine, commands);
        }
        return new PostScriptFont(glyphs);
    }

    /// <summary>
    /// Maps a glyph name from the font to a Unicode character.  Only a few
    /// names are recognised as this loader is intentionally minimal.
    /// </summary>
    /// <param name="name">Glyph name from the CharStrings dictionary.</param>
    /// <returns>Approximate character code.</returns>
    private static char MapName(string name)
    {
        if (name.Length == 1)
            return name[0];
        return name switch
        {
            "space" => ' ',
            "comma" => ',',
            "period" => '.',
            "hyphen" => '-',
            _ => '?' // unknown names map to '?'
        };
    }

    /// <summary>
    /// Converts an ASCII hex string to a byte array.
    /// </summary>
    /// <param name="hex">Hexadecimal characters with no separators.</param>
    /// <returns>Decoded binary data.</returns>
    internal static byte[] ConvertHex(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    /// <summary>
    /// Decrypts Type&nbsp;1 charstring data using the algorithm described in the
    /// specification (<see href="https://adobe-type-tools.github.io/font-tech-notes/pdfs/T1_SPEC.pdf">section 4</see>).
    /// </summary>
    /// <param name="data">Encrypted bytes.</param>
    /// <param name="r">Initial random number value.</param>
    /// <param name="discard">Number of decrypted bytes to discard from the beginning.</param>
    /// <returns>Decrypted byte array.</returns>
    /// <summary>
    /// Decodes a Type&nbsp;1 charstring into a list of drawing commands.  Only a
    /// very small subset of operators is supported.  The algorithm roughly
    /// follows the description in
    /// <see href="https://adobe-type-tools.github.io/font-tech-notes/pdfs/T1_SPEC.pdf">Adobe Technical Note 5040</see>.
    /// </summary>
    /// <param name="data">Plain (already decrypted) charstring bytes.</param>
    /// <param name="width">Receives the glyph width.</param>
    /// <param name="height">Receives the glyph height.</param>
    /// <param name="baseLine">Receives the baseline value.</param>
    /// <returns>List of parsed path commands.</returns>
    public static byte[] DecryptType1(byte[] data, int r, int discard)
    {
        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            byte cipher = data[i];
            byte plain = (byte)(cipher ^ (r >> 8));
            r = ((cipher + r) * 52845 + 22719) & 0xFFFF;
            result[i] = plain;
        }
        if (discard > 0 && discard < result.Length)
            return result.Skip(discard).ToArray();
        return result;
    }

    internal static List<PostScriptGlyph.PathCommand> ParseCharString(byte[] data, out float width, out float height, out float baseLine)
    {
        var state = new CharStringParserState(data);
        while (state.Index < data.Length)
        {
            int b = data[state.Index++];
            if (b >= 32 && b <= 246)
            {
                state.Stack.Push(b - 139);
            }
            else if (b >= 247 && b <= 250)
            {
                int b2 = data[state.Index++];
                state.Stack.Push((b - 247) * 256 + b2 + 108);
            }
            else if (b >= 251 && b <= 254)
            {
                int b2 = data[state.Index++];
                state.Stack.Push(-(b - 251) * 256 - b2 - 108);
            }
            else if (b == 255)
            {
                int num = (data[state.Index] << 24) | (data[state.Index + 1] << 16) | (data[state.Index + 2] << 8) | data[state.Index + 3];
                state.Index += 4;
                state.Stack.Push(num);
            }
            else if (s_charStringHandlers.TryGetValue(b, out var handler))
            {
                handler(state);
            }
            else
            {
                state.Stack.Clear();
            }
        }

        width = state.Width;
        height = state.MaxY - state.MinY;
        baseLine = -state.MinY;
        return state.Commands;
    }

    /// <summary>
    /// Internal parser state used when interpreting Type&nbsp;1 charstrings.
    /// </summary>
    private sealed class CharStringParserState
    {
        /// <summary>Initialises the state with the data being parsed.</summary>
        /// <param name="data">Charstring bytes.</param>
        public CharStringParserState(byte[] data)
        {
            DataLength = data.Length;
        }

        /// <summary>Index of the next byte to read.</summary>
        public int Index { get; set; }

        /// <summary>Length of the input data.</summary>
        public int DataLength { get; }

        /// <summary>Current horizontal position.</summary>
        public float X { get; set; }

        /// <summary>Current vertical position.</summary>
        public float Y { get; set; }

        /// <summary>Minimum X seen so far.</summary>
        public float MinX { get; private set; }

        /// <summary>Minimum Y seen so far.</summary>
        public float MinY { get; private set; }

        /// <summary>Maximum X seen so far.</summary>
        public float MaxX { get; private set; }

        /// <summary>Maximum Y seen so far.</summary>
        public float MaxY { get; private set; }

        /// <summary>Width extracted from the charstring.</summary>
        public float Width { get; set; }

        /// <summary>Working stack used by the charstring operators.</summary>
        public Stack<int> Stack { get; } = new();

        /// <summary>List of parsed drawing commands.</summary>
        public List<PostScriptGlyph.PathCommand> Commands { get; } = new();

        /// <summary>Updates the bounding box.</summary>
        public void UpdateBounds()
        {
            if (X < MinX) MinX = X;
            if (X > MaxX) MaxX = X;
            if (Y < MinY) MinY = Y;
            if (Y > MaxY) MaxY = Y;
        }
    }

    /// <summary>
    /// Delegate representing a charstring operator implementation.
    /// </summary>
    /// <param name="state">Parser state to update.</param>
    private delegate void CharStringHandler(CharStringParserState state);

    private static readonly Dictionary<int, CharStringHandler> s_charStringHandlers = new()
    {
        { 4, HandleVMoveTo },
        { 5, HandleRLineTo },
        { 6, HandleHLineTo },
        { 7, HandleVLineTo },
        { 8, HandleRRCurveTo },
        { 9, HandleClosePath },
        { 13, HandleHsbw },
        { 21, HandleRMoveTo },
        { 22, HandleHMoveTo },
        { 14, HandleEndChar }
    };

    /// <summary>Implements the <c>vmoveto</c> operator.</summary>
    /// <param name="state">Parser state to update.</param>
    private static void HandleVMoveTo(CharStringParserState state)
    {
        state.Y += state.Stack.Pop();
        state.Commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.MoveTo, state.X, state.Y, 0, 0, 0, 0));
        state.UpdateBounds();
    }

    /// <summary>Implements the <c>rlineto</c> operator.</summary>
    /// <param name="state">Parser state to update.</param>
    private static void HandleRLineTo(CharStringParserState state)
    {
        while (state.Stack.Count >= 2)
        {
            int dy = state.Stack.Pop();
            int dx = state.Stack.Pop();
            state.X += dx;
            state.Y += dy;
            state.Commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, state.X, state.Y, 0, 0, 0, 0));
            state.UpdateBounds();
        }
    }

    /// <summary>Implements the <c>hlineto</c> operator.</summary>
    /// <param name="state">Parser state to update.</param>
    private static void HandleHLineTo(CharStringParserState state)
    {
        while (state.Stack.Count > 0)
        {
            int dx = state.Stack.Pop();
            state.X += dx;
            state.Commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, state.X, state.Y, 0, 0, 0, 0));
            state.UpdateBounds();
            if (state.Stack.Count > 0)
            {
                int dy = state.Stack.Pop();
                state.Y += dy;
                state.Commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, state.X, state.Y, 0, 0, 0, 0));
                state.UpdateBounds();
            }
        }
    }

    /// <summary>Implements the <c>vlineto</c> operator.</summary>
    /// <param name="state">Parser state to update.</param>
    private static void HandleVLineTo(CharStringParserState state)
    {
        while (state.Stack.Count > 0)
        {
            int dy = state.Stack.Pop();
            state.Y += dy;
            state.Commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, state.X, state.Y, 0, 0, 0, 0));
            state.UpdateBounds();
            if (state.Stack.Count > 0)
            {
                int dx = state.Stack.Pop();
                state.X += dx;
                state.Commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, state.X, state.Y, 0, 0, 0, 0));
                state.UpdateBounds();
            }
        }
    }

    /// <summary>Implements the <c>rrcurveto</c> operator.</summary>
    /// <param name="state">Parser state to update.</param>
    private static void HandleRRCurveTo(CharStringParserState state)
    {
        while (state.Stack.Count >= 6)
        {
            int dy3 = state.Stack.Pop();
            int dx3 = state.Stack.Pop();
            int dy2 = state.Stack.Pop();
            int dx2 = state.Stack.Pop();
            int dy1 = state.Stack.Pop();
            int dx1 = state.Stack.Pop();
            float x1 = state.X + dx1; float y1 = state.Y + dy1;
            float x2 = x1 + dx2; float y2 = y1 + dy2;
            float x3 = x2 + dx3; float y3 = y2 + dy3;
            state.Commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.BezierTo, x1, y1, x2, y2, x3, y3));
            state.X = x3; state.Y = y3; state.UpdateBounds();
        }
    }

    /// <summary>Implements the <c>closepath</c> operator.</summary>
    /// <param name="state">Parser state to update.</param>
    private static void HandleClosePath(CharStringParserState state)
    {
        state.Commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.Close, 0, 0, 0, 0, 0, 0));
    }

    /// <summary>Implements the <c>hsbw</c> operator.</summary>
    /// <param name="state">Parser state to update.</param>
    private static void HandleHsbw(CharStringParserState state)
    {
        state.Width = state.Stack.Pop();
        state.X = state.Stack.Pop();
        state.Stack.Clear();
    }

    /// <summary>Implements the <c>rmoveto</c> operator.</summary>
    /// <param name="state">Parser state to update.</param>
    private static void HandleRMoveTo(CharStringParserState state)
    {
        int dy = state.Stack.Pop();
        int dx = state.Stack.Pop();
        state.X += dx;
        state.Y += dy;
        state.Commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.MoveTo, state.X, state.Y, 0, 0, 0, 0));
        state.UpdateBounds();
    }

    /// <summary>Implements the <c>hmoveto</c> operator.</summary>
    /// <param name="state">Parser state to update.</param>
    private static void HandleHMoveTo(CharStringParserState state)
    {
        state.X += state.Stack.Pop();
        state.Commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.MoveTo, state.X, state.Y, 0, 0, 0, 0));
        state.UpdateBounds();
    }

    /// <summary>Implements the <c>endchar</c> operator.</summary>
    /// <param name="state">Parser state to update.</param>
    private static void HandleEndChar(CharStringParserState state)
    {
        state.Index = state.DataLength;
    }
}
