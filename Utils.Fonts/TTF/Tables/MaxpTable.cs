using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'maxp' table establishes the memory requirements for a font. It begins with a table version number
/// and the number of glyphs in the font. The remaining entries set maximum values for various parameters
/// (such as maximum points, contours, and storage), which are used to allocate resources for the font.
/// </summary>
[TTFTable(TableTypes.Tags.MAXP)]
public class MaxpTable : TrueTypeTable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaxpTable"/> class.
    /// </summary>
    protected internal MaxpTable() : base(TableTypes.MAXP) { }

    /// <summary>
    /// Gets the fixed length (in bytes) of the maxp table.
    /// </summary>
    public override int Length => 32;

    /// <summary>
    /// Gets or sets the number of glyphs in the font.
    /// </summary>
    public virtual short NumGlyphs { get; set; } = 0;

    /// <summary>
    /// Gets or sets the table version number.
    /// </summary>
    public virtual int Version { get; set; } = 0x10000;

    /// <summary>
    /// Gets or sets the maximum number of points in a non-composite glyph.
    /// </summary>
    public virtual short MaxPoints { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of contours in a non-composite glyph.
    /// </summary>
    public virtual short MaxContours { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of points in a composite glyph.
    /// </summary>
    public virtual short MaxComponentPoints { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of contours in a composite glyph.
    /// </summary>
    public virtual short MaxComponentContours { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of zones (usually 2: one for the twilight zone, one for the actual glyph).
    /// </summary>
    public virtual short MaxZones { get; set; } = 2;

    /// <summary>
    /// Gets or sets the maximum number of points in the twilight zone.
    /// </summary>
    public virtual short MaxTwilightPoints { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum storage area size.
    /// </summary>
    public virtual short MaxStorage { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of function definitions in the font.
    /// </summary>
    public virtual short MaxFunctionDefs { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of instruction definitions.
    /// </summary>
    public virtual short MaxInstructionDefs { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of elements on the function call stack.
    /// </summary>
    public virtual short MaxStackElements { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of bytes for instructions in a glyph.
    /// </summary>
    public virtual short MaxSizeOfInstructions { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of elements in a composite glyph.
    /// </summary>
    public virtual short MaxComponentElements { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum depth of composite glyph nesting.
    /// </summary>
    public virtual short MaxComponentDepth { get; set; } = 0;

    /// <summary>
    /// Reads the maxp table data from the specified reader.
    /// </summary>
    /// <param name="data">The reader from which to read the table data.</param>
    /// <exception cref="ArgumentException">Thrown if the remaining data is not exactly 32 bytes.</exception>
    public override void ReadData(Reader data)
    {
        if (data.BytesLeft != 32)
        {
            throw new ArgumentException("Bad size for Maxp table");
        }
        Version = data.Read<Int32>();
        NumGlyphs = data.Read<Int16>();
        MaxPoints = data.Read<Int16>();
        MaxContours = data.Read<Int16>();
        MaxComponentPoints = data.Read<Int16>();
        MaxComponentContours = data.Read<Int16>();
        MaxZones = data.Read<Int16>();
        MaxTwilightPoints = data.Read<Int16>();
        MaxStorage = data.Read<Int16>();
        MaxFunctionDefs = data.Read<Int16>();
        MaxInstructionDefs = data.Read<Int16>();
        MaxStackElements = data.Read<Int16>();
        MaxSizeOfInstructions = data.Read<Int16>();
        MaxComponentElements = data.Read<Int16>();
        MaxComponentDepth = data.Read<Int16>();
    }

    /// <summary>
    /// Writes the maxp table data to the specified writer.
    /// </summary>
    /// <param name="data">The writer to which to write the table data.</param>
    public override void WriteData(Writer data)
    {
        data.Write<Int32>(Version);
        data.Write<Int16>(NumGlyphs);
        data.Write<Int16>(MaxPoints);
        data.Write<Int16>(MaxContours);
        data.Write<Int16>(MaxComponentPoints);
        data.Write<Int16>(MaxComponentContours);
        data.Write<Int16>(MaxZones);
        data.Write<Int16>(MaxTwilightPoints);
        data.Write<Int16>(MaxStorage);
        data.Write<Int16>(MaxFunctionDefs);
        data.Write<Int16>(MaxInstructionDefs);
        data.Write<Int16>(MaxStackElements);
        data.Write<Int16>(MaxSizeOfInstructions);
        data.Write<Int16>(MaxComponentElements);
        data.Write<Int16>(MaxComponentDepth);
    }

    /// <summary>
    /// Returns a string representation of the maxp table data.
    /// </summary>
    /// <returns>A string containing the maxp table details.</returns>
    public override string ToString()
    {
        StringBuilder result = new StringBuilder();
        result.AppendLine($"    Version          : {Version:X4}");
        result.AppendLine($"    NumGlyphs        : {NumGlyphs}");
        result.AppendLine($"    MaxPoints        : {MaxPoints}");
        result.AppendLine($"    MaxContours      : {MaxContours}");
        result.AppendLine($"    MaxCompPoints    : {MaxComponentPoints}");
        result.AppendLine($"    MaxCompContours  : {MaxComponentContours}");
        result.AppendLine($"    MaxZones         : {MaxZones}");
        result.AppendLine($"    MaxTwilightPoints: {MaxTwilightPoints}");
        result.AppendLine($"    MaxStorage       : {MaxStorage}");
        result.AppendLine($"    MaxFuncDefs      : {MaxFunctionDefs}");
        result.AppendLine($"    MaxInstDefs      : {MaxInstructionDefs}");
        result.AppendLine($"    MaxStackElements : {MaxStackElements}");
        result.AppendLine($"    MaxSizeInst      : {MaxSizeOfInstructions}");
        result.AppendLine($"    MaxCompElements  : {MaxComponentElements}");
        result.AppendLine($"    MaxCompDepth     : {MaxComponentDepth}");
        return result.ToString();
    }
}
