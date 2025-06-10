using System;
using System.Collections.Generic;
using Utils.Fonts;

namespace Utils.Fonts.PostScript;

/// <summary>
/// Represents a glyph defined by a sequence of basic PostScript path commands.
/// The commands correspond loosely to the operators described in
/// <see href="https://adobe-type-tools.github.io/font-tech-notes/pdfs/T1_SPEC.pdf">Adobe's Type&nbsp;1 specification</see>.
/// </summary>
public class PostScriptGlyph : IGlyph
{
    /// <summary>Internal list of path commands describing the glyph outline.</summary>
    private readonly List<PathCommand> _commands;

    /// <summary>Creates a new <see cref="PostScriptGlyph"/>.</summary>
    /// <param name="width">Advance width of the glyph.</param>
    /// <param name="height">Height of the glyph.</param>
    /// <param name="baseLine">Baseline of the glyph.</param>
    /// <param name="commands">List of drawing commands forming the glyph.</param>
    public PostScriptGlyph(float width, float height, float baseLine, List<PathCommand> commands)
    {
        Width = width;
        Height = height;
        BaseLine = baseLine;
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    /// <inheritdoc />
    public float Width { get; }

    /// <inheritdoc />
    public float Height { get; }

    /// <inheritdoc />
    public float BaseLine { get; }

    /// <inheritdoc />
    /// <remarks>
    /// The drawing callbacks correspond to <c>moveto</c>, <c>lineto</c> and
    /// <c>curveto</c> operations in PostScript.  The <c>closepath</c> operator is
    /// implemented by drawing a line back to the starting point.
    /// </remarks>
    public void ToGraphic(IGraphicConverter graphicConverter)
    {
        if (graphicConverter == null) throw new ArgumentNullException(nameof(graphicConverter));
        (float startX, float startY) = (0, 0);
        foreach (var cmd in _commands)
        {
            switch (cmd.Type)
            {
                case PathCommandType.MoveTo:
                    startX = cmd.X1;
                    startY = cmd.Y1;
                    graphicConverter.StartAt(cmd.X1, cmd.Y1);
                    break;
                case PathCommandType.LineTo:
                    graphicConverter.LineTo(cmd.X1, cmd.Y1);
                    break;
                case PathCommandType.BezierTo:
                    graphicConverter.BezierTo((cmd.X1, cmd.Y1), (cmd.X2, cmd.Y2), (cmd.X3, cmd.Y3));
                    break;
                case PathCommandType.Close:
                    graphicConverter.LineTo(startX, startY);
                    break;
            }
        }
    }

    /// <summary>
    /// Represents a single drawing command extracted from the font.  The
    /// coordinates use the same units as the original charstring.
    /// </summary>
    public record struct PathCommand(PathCommandType Type, float X1, float Y1, float X2, float Y2, float X3, float Y3);

    /// <summary>
    /// Enumeration of possible path command types supported by this
    /// simplified glyph representation.
    /// </summary>
    public enum PathCommandType
    {
        MoveTo,
        LineTo,
        BezierTo,
        Close
    }
}
