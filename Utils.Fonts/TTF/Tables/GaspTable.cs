using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'gasp' table contains information which describes the preferred rasterization techniques
/// for the typeface when it is rendered on grayscale-capable devices at various sizes.
/// Each entry specifies a maximum pixels-per-em threshold and the corresponding rendering behaviour.
/// </summary>
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/gasp"/>
[TTFTable(TableTypes.Tags.GASP)]
public class GaspTable : TrueTypeTable
{
    /// <summary>
    /// Rasterization behaviour flags for a gasp range.
    /// Version 0 supports only <see cref="Gridfit"/> and <see cref="DoGray"/>;
    /// version 1 additionally supports <see cref="SymmetricGridfit"/> and <see cref="SymmetricSmoothing"/>.
    /// </summary>
    [Flags]
    public enum GaspBehavior : ushort
    {
        /// <summary>Use grid-fitting (hinting) for this size range.</summary>
        Gridfit            = 0x0001,
        /// <summary>Use grayscale rendering for this size range.</summary>
        DoGray             = 0x0002,
        /// <summary>Use symmetric grid-fitting (version 1, ClearType).</summary>
        SymmetricGridfit   = 0x0004,
        /// <summary>Use symmetric smoothing (version 1, ClearType).</summary>
        SymmetricSmoothing = 0x0008,
    }

    /// <summary>
    /// Describes the recommended rendering behaviour for one PPEM size range.
    /// </summary>
    public sealed class GaspRange
    {
        /// <summary>Gets or sets the inclusive maximum pixels-per-em size to which this range applies.</summary>
        public ushort RangeMaxPPEM { get; set; }

        /// <summary>Gets or sets the rendering behaviour flags for this range.</summary>
        public GaspBehavior RangeGaspBehavior { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GaspTable"/> class.
    /// </summary>
    /// <summary>Initializes a new instance of the <see cref="GaspTable"/> class.</summary>
    public GaspTable() : base(TableTypes.GASP) { }

    /// <summary>Gets or sets the table version. 0 = original; 1 = ClearType symmetric flags supported.</summary>
    public ushort Version { get; set; }

    /// <summary>Gets or sets the array of PPEM ranges and their associated rendering behaviour, sorted ascending by PPEM.</summary>
    public GaspRange[] Ranges { get; set; } = [];

    /// <inheritdoc/>
    public override int Length => 4 + Ranges.Length * 4;

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Version = data.Read<UInt16>();
        int numRanges = data.Read<UInt16>();
        Ranges = new GaspRange[numRanges];
        for (int i = 0; i < numRanges; i++)
        {
            Ranges[i] = new GaspRange
            {
                RangeMaxPPEM      = data.Read<UInt16>(),
                RangeGaspBehavior = (GaspBehavior)data.Read<UInt16>(),
            };
        }
    }

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<UInt16>(Version);
        data.Write<UInt16>((ushort)Ranges.Length);
        foreach (var r in Ranges)
        {
            data.Write<UInt16>(r.RangeMaxPPEM);
            data.Write<UInt16>((ushort)r.RangeGaspBehavior);
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version    : {Version}");
        sb.AppendLine($"    NumRanges  : {Ranges.Length}");
        foreach (var r in Ranges)
            sb.AppendLine($"      PPEM <= {r.RangeMaxPPEM,5}  Behavior: {r.RangeGaspBehavior}");
        return sb.ToString();
    }
}
