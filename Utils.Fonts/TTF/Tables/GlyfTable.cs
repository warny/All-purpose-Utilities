using System;
using System.IO;
using System.Linq;
using System.Text;
using Utils.Fonts.TTF.Parsing;
using Utils.IO.Serialization;
using Utils.Fonts.TTF.Tables.Glyf;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'glyf' table contains the data that defines the appearance of the glyphs in the font.
/// It includes the specification of the points that describe the contours that make up each glyph outline
/// as well as the instructions for grid-fitting the glyph. The table supports both simple glyphs and
/// compound glyphs (i.e. glyphs composed of other glyphs).
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6glyf.html"/>
[TTFTable(TableTypes.Tags.GLYF, TableTypes.Tags.LOCA, TableTypes.Tags.MAXP)]
public class GlyfTable : TrueTypeTable
{
    /// <summary>
    /// Array containing the glyph data for each glyph in the font.
    /// </summary>
    private GlyphBase[] glyphs;

    /// <summary>
    /// Reference to the 'loca' table, used to determine the offsets and sizes of glyph data.
    /// </summary>
    private LocaTable loca;

    /// <summary>
    /// Reference to the 'maxp' table, used to determine the total number of glyphs.
    /// </summary>
    private MaxpTable maxp;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlyfTable"/> class.
    /// </summary>
    protected internal GlyfTable() : base(TableTypes.GLYF) { }

    /// <summary>
    /// Gets or sets the owning TrueType font. When set, the dependent 'loca' and 'maxp' tables are retrieved.
    /// </summary>
    public override TrueTypeFont TrueTypeFont
    {
        get => base.TrueTypeFont;
        protected internal set
        {
            base.TrueTypeFont = value;
            loca = TrueTypeFont.GetTable<LocaTable>(TableTypes.LOCA);
            maxp = TrueTypeFont.GetTable<MaxpTable>(TableTypes.MAXP);
        }
    }

    /// <summary>
    /// Gets the total length (in bytes) of the glyf table, calculated as the sum of the lengths of all glyph data.
    /// </summary>
    /// <remarks>
    /// A glyph index with a zero-length 'loca' entry (e.g. the space character, which has no
    /// contours) has a null entry in <see cref="glyphs"/> rather than an empty <see cref="GlyphBase"/>
    /// instance -- see <see cref="ReadData"/> -- so this must be null-safe, matching
    /// <see cref="WriteData"/>'s use of <c>glyf?.WriteData(data)</c>.
    /// </remarks>
    public override int Length => glyphs.Sum(g => g?.Length ?? 0);

    /// <summary>
    /// Gets the total number of glyphs declared by 'maxp', used to validate composite glyph
    /// component references.
    /// </summary>
    internal int NumGlyphs => maxp.NumGlyphs;

    /// <summary>
    /// Retrieves the glyph data for the glyph at the specified index.
    /// </summary>
    /// <param name="i">The glyph index.</param>
    /// <returns>The glyph data as a <see cref="GlyphBase"/> instance, or <see langword="null"/> if the glyph has no outline (e.g. space).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="i"/> is outside <c>[0, NumGlyphs)</c>.</exception>
    public virtual GlyphBase GetGlyph(int i)
    {
        if (i < 0 || i >= glyphs.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(i), i, $"Glyph index must be in [0, {glyphs.Length}).");
        }
        return glyphs[i];
    }

    /// <summary>
    /// Attempts to retrieve the glyph data for the glyph at the specified index, without throwing
    /// for an out-of-range index.
    /// </summary>
    /// <param name="i">The glyph index.</param>
    /// <param name="glyph">
    /// When this method returns <see langword="true"/>, the glyph at <paramref name="i"/> (which may
    /// itself be <see langword="null"/> for a glyph with no outline, e.g. space). Undefined when
    /// this method returns <see langword="false"/>.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="i"/> is a valid glyph index; otherwise, <see langword="false"/>.</returns>
    public virtual bool TryGetGlyph(int i, out GlyphBase glyph)
    {
        if (i < 0 || i >= glyphs.Length)
        {
            glyph = null;
            return false;
        }
        glyph = glyphs[i];
        return true;
    }

    /// <summary>
    /// Writes the glyf table data to the specified writer.
    /// </summary>
    /// <param name="data">The writer to which the glyf table data is written.</param>
    public override void WriteData(Writer data)
    {
        foreach (var glyf in glyphs)
        {
            glyf?.WriteData(data);
        }
    }

    /// <summary>
    /// Reads the glyf table data from the specified reader.
    /// It uses the 'loca' table to determine the offset and size for each glyph and then
    /// creates the appropriate glyph instance.
    /// </summary>
    /// <param name="data">The reader from which the glyf table data is read.</param>
    public override void ReadData(Reader data)
    {
        // Allocate an array to hold all glyphs based on the number provided in the maxp table.
        glyphs = new GlyphBase[maxp.NumGlyphs];
        long glyfLength = data.BytesLeft;

        // Iterate over each loca record to retrieve glyph offset and size. 'loca' already validates
        // monotonicity and non-negative sizes on read (see LocaTable.ReadData), but the offset+size
        // <= glyf length relationship is cross-table and re-checked here as a local defense, in case
        // 'loca' and 'glyf' were constructed independently of the normal parse pipeline.
        foreach ((int index, int offset, int size) in loca)
        {
            if (size == 0)
            {
                continue;
            }
            if (offset < 0 || size < 0 || offset + (long)size > glyfLength)
            {
                FontParsingContext.Reject(FontDiagnosticCode.InvalidLoca,
                    $"loca entry {index} range [{offset}, {offset + (long)size}) does not fit within the glyf table ({glyfLength} bytes).",
                    TableTypes.GLYF, offset, size);
            }
            // Create a new glyph from the data slice.
            glyphs[index] = GlyphBase.CreateGlyf(data.Slice(offset, size), this);
        }
    }

    /// <summary>
    /// Returns a string representation of the glyf table.
    /// </summary>
    /// <returns>A string that includes the number of glyphs and a summary of the first glyph.</returns>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"    Glyf Table: ({glyphs.Length} glyphs)");
        sb.AppendLine($"      Glyf 0: {GetGlyph(0)}");
        return sb.ToString();
    }
}
