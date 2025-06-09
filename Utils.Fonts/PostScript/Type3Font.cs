using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Fonts.PostScript;

/// <summary>
/// Minimal loader for PostScript Type 3 fonts. Only a subset of the
/// language is interpreted: setcachedevice for metrics and basic path
/// commands such as moveto, lineto, curveto and closepath.
/// </summary>
public class Type3Font : IFont
{
    private readonly Dictionary<char, PostScriptGlyph> _glyphs;

    private Type3Font(Dictionary<char, PostScriptGlyph> glyphs)
    {
        _glyphs = glyphs;
    }

    /// <summary>
    /// Loads a Type 3 font from the provided PostScript stream.
    /// </summary>
    public static Type3Font Load(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        string ps = reader.ReadToEnd();
        var glyphs = new Dictionary<char, PostScriptGlyph>();

        var cpMatch = Regex.Match(ps, @"/CharProcs\s+\d+\s+dict\s+dup\s+begin(?<cp>.*)end", RegexOptions.Singleline);
        if (!cpMatch.Success)
            return new Type3Font(glyphs);

        var procs = cpMatch.Groups["cp"].Value;
        var glyphRegex = new Regex(@"/(?<name>\S+)\s+\{(?<proc>[^}]*)\}\s+bind\s+def", RegexOptions.Singleline);
        foreach (Match g in glyphRegex.Matches(procs))
        {
            string name = g.Groups["name"].Value;
            string proc = g.Groups["proc"].Value;
            float width = 0, height = 0, baseLine = 0;
            var commands = ParseCharProc(proc, ref width, ref height, ref baseLine);
            char ch = MapName(name);
            glyphs[ch] = new PostScriptGlyph(width, height, baseLine, commands);
        }

        return new Type3Font(glyphs);
    }

    /// <inheritdoc />
    public IGlyph GetGlyph(char c) => _glyphs.TryGetValue(c, out var g) ? g : null;

    /// <inheritdoc />
    public float GetSpacingCorrection(char before, char after) => 0f;

    private static List<PostScriptGlyph.PathCommand> ParseCharProc(string proc, ref float width, ref float height, ref float baseLine)
    {
        var commands = new List<PostScriptGlyph.PathCommand>();
        var cache = Regex.Match(proc, @"(?<wx>-?\d+(?:\.\d+)?)\s+-?\d+(?:\.\d+)?\s+(?<llx>-?\d+(?:\.\d+)?)\s+(?<lly>-?\d+(?:\.\d+)?)\s+(?<urx>-?\d+(?:\.\d+)?)\s+(?<ury>-?\d+(?:\.\d+)?)\s+setcachedevice");
        if (cache.Success)
        {
            width = float.Parse(cache.Groups["wx"].Value, CultureInfo.InvariantCulture);
            float llx = float.Parse(cache.Groups["llx"].Value, CultureInfo.InvariantCulture);
            float lly = float.Parse(cache.Groups["lly"].Value, CultureInfo.InvariantCulture);
            float ury = float.Parse(cache.Groups["ury"].Value, CultureInfo.InvariantCulture);
            height = float.Parse(cache.Groups["ury"].Value, CultureInfo.InvariantCulture) - lly;
            baseLine = -lly;
        }

        var tokens = Regex.Matches(proc, @"-?\d+(?:\.\d+)?|[a-zA-Z]+").Select(m => m.Value).ToList();
        var stack = new List<float>();
        for (int i = 0; i < tokens.Count; i++)
        {
            string t = tokens[i];
            if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float num))
            {
                stack.Add(num);
                continue;
            }
            switch (t.ToLowerInvariant())
            {
                case "moveto":
                    if (stack.Count >= 2)
                    {
                        float y = stack[^1];
                        float x = stack[^2];
                        stack.RemoveRange(stack.Count - 2, 2);
                        commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.MoveTo, x, y, 0, 0, 0, 0));
                    }
                    break;
                case "lineto":
                    if (stack.Count >= 2)
                    {
                        float y = stack[^1];
                        float x = stack[^2];
                        stack.RemoveRange(stack.Count - 2, 2);
                        commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.LineTo, x, y, 0, 0, 0, 0));
                    }
                    break;
                case "curveto":
                    if (stack.Count >= 6)
                    {
                        float y3 = stack[^1];
                        float x3 = stack[^2];
                        float y2 = stack[^3];
                        float x2 = stack[^4];
                        float y1 = stack[^5];
                        float x1 = stack[^6];
                        stack.RemoveRange(stack.Count - 6, 6);
                        commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.BezierTo, x1, y1, x2, y2, x3, y3));
                    }
                    break;
                case "closepath":
                    commands.Add(new PostScriptGlyph.PathCommand(PostScriptGlyph.PathCommandType.Close, 0, 0, 0, 0, 0, 0));
                    break;
                default:
                    stack.Clear();
                    break;
            }
        }
        return commands;
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
}
