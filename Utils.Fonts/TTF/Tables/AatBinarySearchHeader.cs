using System.Numerics;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// Computes the binary-search header fields (<c>searchRange</c>, <c>entrySelector</c>,
/// <c>rangeShift</c>) shared by several Apple Advanced Typography (AAT) lookup table formats
/// (e.g. 'kern', 'bsln', 'lcar', 'opbd', 'prop').
/// </summary>
internal static class AatBinarySearchHeader
{
    /// <summary>
    /// Computes the binary-search header fields for an AAT lookup with <paramref name="unitCount"/>
    /// units, each <paramref name="unitSize"/> bytes wide.
    /// </summary>
    /// <param name="unitCount">Number of units in the lookup (including any sentinel entry).</param>
    /// <param name="unitSize">Size, in bytes, of one unit.</param>
    /// <returns>The <c>searchRange</c>, <c>entrySelector</c>, and <c>rangeShift</c> values, in that order.</returns>
    public static (ushort searchRange, ushort entrySelector, ushort rangeShift) Compute(int unitCount, int unitSize)
    {
        int log2 = BitOperations.Log2((uint)unitCount);
        int powerOfTwo = 1 << log2;
        ushort searchRange = (ushort)(powerOfTwo * unitSize);
        ushort entrySelector = (ushort)log2;
        ushort rangeShift = (ushort)((unitCount - powerOfTwo) * unitSize);
        return (searchRange, entrySelector, rangeShift);
    }
}
