using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Fonts.PostScript;

/// <summary>
/// Very small PostScript font loader based on a simple text representation.
/// Each glyph is defined in the following form:
///
/// Glyph: A
/// Width: 500
/// Height: 600
/// Baseline: 0
/// Path:
/// M 0 0
/// L 10 0
/// ...
/// EndGlyph
/// </summary>
public class PostScriptFont : IFont
{
    private readonly Dictionary<char, PostScriptGlyph> _glyphs;

    private PostScriptFont(Dictionary<char, PostScriptGlyph> glyphs)
    {
        _glyphs = glyphs;
    }

    /// <summary>Loads a PostScript font from the provided stream.</summary>
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
    public IGlyph GetGlyph(char c) => _glyphs.TryGetValue(c, out var g) ? g : null;

    /// <inheritdoc />
    public float GetSpacingCorrection(char before, char after) => 0f;

    /// <summary>
    /// Loads a PostScript Type 1 PFA font stream.
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
    /// Loads a PostScript Type 1 PFB font stream.
    /// </summary>
    /// <param name="stream">Input PFB stream.</param>
    /// <returns>Instance of <see cref="PostScriptFont"/>.</returns>
    public static PostScriptFont LoadPfb(Stream stream)
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
            _ => '?'
        };
    }

    internal static byte[] ConvertHex(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

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
        var cmds = new List<PostScriptGlyph.PathCommand>();
        var stack = new Stack<int>();
        float x = 0, y = 0;
        float minX = 0, minY = 0, maxX = 0, maxY = 0;
        width = 0; baseLine = 0; height = 0;
        int i = 0;
        while (i < data.Length)
        {
            int b = data[i++];
            if (b >= 32 && b <= 246)
            {
                stack.Push(b - 139);
            }
            else if (b >= 247 && b <= 250)
            {
                int b2 = data[i++];
                stack.Push((b - 247) * 256 + b2 + 108);
            }
            else if (b >= 251 && b <= 254)
            {
                int b2 = data[i++];
                stack.Push(-(b - 251) * 256 - b2 - 108);
            }
            else if (b == 255)
            {
                int num = (data[i] << 24) | (data[i + 1] << 16) | (data[i + 2] << 8) | data[i + 3];
                i += 4;
                stack.Push(num);
            }
            else
            {
                switch (b)
                {
                    case 4: // vmoveto
                        y += stack.Pop();
                        cmds.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.MoveTo, x, y, 0, 0, 0, 0));
                        UpdateBounds();
                        break;
                    case 5: // rlineto
                        while (stack.Count >= 2)
                        {
                            int dy = stack.Pop();
                            int dx = stack.Pop();
                            x += dx; y += dy; cmds.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, x, y, 0, 0, 0, 0));
                            UpdateBounds();
                        }
                        break;
                    case 6: // hlineto
                        while (stack.Count > 0)
                        {
                            int dx = stack.Pop();
                            x += dx; cmds.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, x, y, 0, 0, 0, 0));
                            UpdateBounds();
                            if (stack.Count > 0)
                            {
                                int dy = stack.Pop();
                                y += dy; cmds.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, x, y, 0, 0, 0, 0));
                                UpdateBounds();
                            }
                        }
                        break;
                    case 7: // vlineto
                        while (stack.Count > 0)
                        {
                            int dy = stack.Pop();
                            y += dy; cmds.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, x, y, 0, 0, 0, 0));
                            UpdateBounds();
                            if (stack.Count > 0)
                            {
                                int dx = stack.Pop();
                                x += dx; cmds.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, x, y, 0, 0, 0, 0));
                                UpdateBounds();
                            }
                        }
                        break;
                    case 8: // rrcurveto
                        while (stack.Count >= 6)
                        {
                            int dy3 = stack.Pop();
                            int dx3 = stack.Pop();
                            int dy2 = stack.Pop();
                            int dx2 = stack.Pop();
                            int dy1 = stack.Pop();
                            int dx1 = stack.Pop();
                            float x1 = x + dx1; float y1 = y + dy1;
                            float x2 = x1 + dx2; float y2 = y1 + dy2;
                            float x3 = x2 + dx3; float y3 = y2 + dy3;
                            cmds.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.BezierTo, x1, y1, x2, y2, x3, y3));
                            x = x3; y = y3; UpdateBounds();
                        }
                        break;
                    case 9: // closepath
                        cmds.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.Close, 0, 0, 0, 0, 0, 0));
                        break;
                    case 13: // hsbw
                        width = stack.Pop();
                        x = stack.Pop();
                        stack.Clear();
                        break;
                    case 21: // rmoveto
                        {
                            int dy = stack.Pop();
                            int dx = stack.Pop();
                            x += dx; y += dy;
                            cmds.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.MoveTo, x, y, 0, 0, 0, 0));
                            UpdateBounds();
                        }
                        break;
                    case 22: // hmoveto
                        x += stack.Pop();
                        cmds.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.MoveTo, x, y, 0, 0, 0, 0));
                        UpdateBounds();
                        break;
                    case 14: // endchar
                        i = data.Length;
                        break;
                    default:
                        stack.Clear();
                        break;
                }
            }
        }

        height = maxY - minY;
        baseLine = -minY;
        return cmds;

        void UpdateBounds()
        {
            if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
    }
}
