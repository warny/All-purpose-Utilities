namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Represents a single SFNT table directory record, using the unsigned wire types defined by the
/// OpenType/TrueType specification. Replaces the previous mutable, signed-field
/// <c>TrueTypeFont.TableDeclaration</c> class, which was also stored in a <c>SortedSet</c> keyed
/// only by <see cref="Offset"/> -- two entries at the same offset compared equal and one was
/// silently dropped even when its tag, length, or checksum differed.
/// </summary>
/// <param name="Tag">The 4-byte table tag.</param>
/// <param name="Checksum">The declared checksum, as an unsigned 32-bit word sum.</param>
/// <param name="Offset">The declared byte offset of the table data, relative to the start of the font.</param>
/// <param name="Length">The declared byte length of the table data.</param>
/// <param name="OriginalIndex">The zero-based position of this entry within the directory as read from the file.</param>
internal sealed record TableDirectoryEntry(Tag Tag, uint Checksum, uint Offset, uint Length, int OriginalIndex)
{
    /// <summary>
    /// Gets the exclusive end offset of this table's declared range (<see cref="Offset"/> + <see cref="Length"/>),
    /// computed with checked 64-bit arithmetic so the addition itself can never silently overflow.
    /// </summary>
    public long End => checked((long)Offset + Length);
}
