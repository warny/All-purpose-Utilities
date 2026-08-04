using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utils.Fonts.TTF.Parsing;
using Utils.Fonts.TTF.Tables;
using Utils.IO;
using Utils.IO.Serialization;
using Utils.Mathematics;

namespace Utils.Fonts.TTF;

/// <summary>
/// Represents a TrueType font.
/// </summary>
/// <remarks>
/// See https://developer.apple.com/fonts/TrueType-Reference-Manual/ for details.
/// </remarks>
public class TrueTypeFont : IFont
{
    private static readonly RawReader rawReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter rawWriter = new RawWriter() { BigEndian = true };

    // Dictionary associating a table tag with its descriptor and type. RawReader/RawWriter and
    // this dictionary are configuration-only after construction (a fixed set of delegates built
    // once from stateless conversion logic): every ReadData/WriteData call constructs its own
    // Reader/Writer instance from these delegate lists, so concurrent parsing/writing of different
    // fonts on different threads shares no mutable state. See TableRegistryTests and
    // ConcurrentParsingTests for regression coverage.
    private static readonly Dictionary<Tag, (TTFTableAttribute Descriptor, Type TableType)> TablesType;

    static TrueTypeFont()
    {
        TablesType = BuildTablesTypeRegistry();
    }

    /// <summary>
    /// Builds the table-type registry by scanning every concrete <see cref="TrueTypeTable"/>
    /// subclass in this assembly for a <see cref="TTFTableAttribute"/>. Exposed as a standalone,
    /// side-effect-free method (rather than folding this logic directly into the static
    /// constructor) so a duplicate-tag or unconstructible-type failure can be asserted directly in
    /// a test, instead of being observed only as an opaque <see cref="TypeInitializationException"/>
    /// on first use of <see cref="TrueTypeFont"/>.
    /// </summary>
    /// <returns>The tag-to-(descriptor,type) registry.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when two types declare the same table tag, when a declared type is abstract, or when
    /// a declared type has no accessible parameterless constructor.
    /// </exception>
    internal static Dictionary<Tag, (TTFTableAttribute Descriptor, Type TableType)> BuildTablesTypeRegistry()
    {
        var registry = new Dictionary<Tag, (TTFTableAttribute Descriptor, Type TableType)>();
        foreach (var type in typeof(TrueTypeTable).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(TrueTypeTable))))
        {
            var descriptor = type.GetCustomAttribute<TTFTableAttribute>();
            if (descriptor is null) { continue; }

            if (type.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"Table type '{type.FullName}' is declared abstract but carries a {nameof(TTFTableAttribute)} for tag '{descriptor.TableTag}'; a concrete type is required.");
            }

            var ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, System.Type.EmptyTypes, null);
            if (ctor is null)
            {
                throw new InvalidOperationException(
                    $"Table type '{type.FullName}' for tag '{descriptor.TableTag}' has no accessible parameterless constructor.");
            }

            if (registry.TryGetValue(descriptor.TableTag, out var existing))
            {
                throw new InvalidOperationException(
                    $"Duplicate table tag '{descriptor.TableTag}' declared by both '{existing.TableType.FullName}' and '{type.FullName}'.");
            }

            registry.Add(descriptor.TableTag, (descriptor, type));
        }

        ValidateNoDependencyCycles(registry);
        return registry;
    }

    /// <summary>
    /// Verifies that the declared <see cref="TTFTableAttribute.DependsOn"/> graph across every
    /// registered table type contains no cycle, which would otherwise cause unbounded recursion
    /// while resolving read order in <see cref="ParseDirectories"/>.
    /// </summary>
    private static void ValidateNoDependencyCycles(Dictionary<Tag, (TTFTableAttribute Descriptor, Type TableType)> registry)
    {
        var state = new Dictionary<Tag, int>(); // 0 = unvisited, 1 = visiting, 2 = done
        foreach (var tag in registry.Keys)
        {
            Visit(tag);
        }

        void Visit(Tag tag)
        {
            if (!registry.TryGetValue(tag, out var entry)) { return; }
            if (state.TryGetValue(tag, out var s))
            {
                if (s == 1)
                {
                    throw new InvalidOperationException($"Table dependency cycle detected involving tag '{tag}'.");
                }
                if (s == 2) { return; }
            }
            state[tag] = 1;
            foreach (var dependency in entry.Descriptor.DependsOn)
            {
                Visit(dependency);
            }
            state[tag] = 2;
        }
    }

    /// <summary>
    /// Gets the type value read from the font file.
    /// </summary>
    public int Type { get; }

    /// <summary>
    /// Gets the diagnostics recorded while parsing this font. Always empty for a font that was
    /// constructed programmatically rather than parsed, and always empty in
    /// <see cref="FontValidationMode.Strict"/> (any anomaly throws instead of being recorded).
    /// </summary>
    public IReadOnlyList<FontDiagnostic> Diagnostics { get; private set; } = [];

    /// <summary>
    /// Gets the parsing context this font was parsed with. Set once, at the start of parsing, and
    /// kept for the lifetime of the font instance -- not just while <see cref="ParseDirectories"/>
    /// is running. Nested table/glyph parsers (e.g. compound glyph resolution) read this to apply
    /// the same options budgets after <c>ParseFont</c> has already returned, since composite-glyph
    /// contour resolution is lazy: if this were cleared once parsing finished, a caller-supplied
    /// <see cref="TrueTypeFontParsingOptions"/> (e.g. a tighter <c>MaximumCompositeDepth</c>) would
    /// silently stop applying the first time <c>Contours</c> is accessed after <c>ParseFont</c>
    /// returns, falling back to <see cref="TrueTypeFontParsingOptions.Default"/> instead.
    /// <see langword="null"/> for a font that was constructed programmatically rather than parsed.
    /// </summary>
    internal FontParsingContext ParsingContext { get; set; }

    /// <summary>
    /// lazy stores the font scale
    /// </summary>
    private float? scale = null;

    /// <summary>
    /// Get the font scale from the font header
    /// </summary>
    public float Scale
        => scale
        ?? (scale = (100f / GetTable<HeadTable>(TableTypes.HEAD).UnitsPerEm))
        ?? 1f;

    /// <summary>
    /// lazy stores the font vertical baseline
    /// </summary>
    public float? baseLineY = null;

    /// <summary>
    /// Get the font vertical baseline from the font horizontal metric headers
    /// </summary>
    public float BaseLineY
        => baseLineY
        ?? (baseLineY = (70f + GetTable<HheaTable>(TableTypes.HHEA).Ascent * Scale))
        ?? 70f;


    // TrueType offset table and directory entry sizes — see TrueType spec §Font Directory.
    private const int OffsetTableSize = 12;           // sfVersion(4) + numTables(2) + searchRange(2) + entrySelector(2) + rangeShift(2)
    private const int TableDirectoryEntrySize = 16;   // tag(4) + checkSum(4) + offset(4) + length(4)
    private const int TtfAlignment = 4;               // all table data must be 4-byte aligned
    private const int TagLength = 4;                  // TrueType table tags are always 4 ASCII characters
    // Required magic value: sum of all UInt32 words in the font file must equal 0xB1B0AFBA — see TrueType spec §head.
    private const uint ChecksumMagicNumber = 0xB1B0AFBAU;

    // Dictionary of tables present in the font.
    private readonly Dictionary<Tag, TrueTypeTable> tables;

    private int Length => OffsetTableSize + tables.Count * TableDirectoryEntrySize + tables.Values.Sum(t => MathEx.Ceiling(t.Length, TtfAlignment));

    /// <summary>
    /// Initializes a new instance of the <see cref="TrueTypeFont"/> class.
    /// </summary>
    /// <param name="type">The type value read from the font file.</param>
    public TrueTypeFont(int type)
    {
        Type = type;
        tables = []; // Using target-typed new empty dictionary syntax.
    }

    #region Parsing entry points

    /// <summary>
    /// Parses a TrueType font from an in-memory buffer.
    /// </summary>
    /// <param name="bytes">The bytes making up the font.</param>
    /// <param name="options">
    /// Parsing options, or <see langword="null"/> to use <see cref="TrueTypeFontParsingOptions.Default"/>.
    /// </param>
    /// <returns>An instance of <see cref="TrueTypeFont"/>.</returns>
    public static TrueTypeFont ParseFont(ReadOnlySpan<byte> bytes, TrueTypeFontParsingOptions options = null)
    {
        options ??= TrueTypeFontParsingOptions.Default;
        options.EnsureValid();
        if (bytes.Length > options.MaximumFontBytes)
        {
            FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                $"Font size {bytes.Length} exceeds MaximumFontBytes ({options.MaximumFontBytes}).");
        }
        using var ms = new MemoryStream(bytes.ToArray(), writable: false);
        return ParseFontFromSeekableStream(ms, options);
    }

    /// <summary>
    /// Parses a TrueType font from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the font data.</param>
    /// <param name="options">
    /// Parsing options, or <see langword="null"/> to use <see cref="TrueTypeFontParsingOptions.Default"/>.
    /// </param>
    /// <returns>An instance of <see cref="TrueTypeFont"/>.</returns>
    public static TrueTypeFont ParseFont(byte[] bytes, TrueTypeFontParsingOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return ParseFont((ReadOnlySpan<byte>)bytes, options);
    }

    /// <summary>
    /// Parses a TrueType font from a stream.
    /// </summary>
    /// <remarks>
    /// For a seekable <paramref name="s"/>, the font is read starting at its current position
    /// (<c>fontStart</c>); every table offset in the SFNT directory is interpreted relative to
    /// that position, not to the start of the stream. On return, the stream position is left at
    /// <c>fontStart + 12 + numTables * 16</c> (the end of the table directory): reading table
    /// payloads uses bounded, independent views over the stream that do not otherwise move its
    /// position. For a non-seekable <paramref name="s"/>, the font is first copied, incrementally
    /// and under <see cref="TrueTypeFontParsingOptions.MaximumFontBytes"/>, into an owned temporary
    /// buffer that is always disposed before this method returns (regardless of
    /// <see cref="TrueTypeFontParsingOptions.LeaveOpen"/>, which only governs <paramref name="s"/> itself).
    /// </remarks>
    /// <param name="s">The stream containing the font data.</param>
    /// <param name="options">
    /// Parsing options, or <see langword="null"/> to use <see cref="TrueTypeFontParsingOptions.Default"/>.
    /// </param>
    /// <returns>An instance of <see cref="TrueTypeFont"/>.</returns>
    public static TrueTypeFont ParseFont(Stream s, TrueTypeFontParsingOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        options ??= TrueTypeFontParsingOptions.Default;
        options.EnsureValid();
        if (!s.CanRead)
            throw new InvalidOperationException("The stream must be readable");

        try
        {
            if (!s.CanSeek)
            {
                using var buffered = CopyToBoundedSeekableStream(s, options.MaximumFontBytes);
                return ParseFontFromSeekableStream(buffered, options);
            }

            long available = s.Length - s.Position;
            if (available > options.MaximumFontBytes)
            {
                FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                    $"Font size {available} exceeds MaximumFontBytes ({options.MaximumFontBytes}).");
            }
            return ParseFontFromSeekableStream(s, options);
        }
        finally
        {
            if (!options.LeaveOpen)
            {
                s.Dispose();
            }
        }
    }

    /// <summary>
    /// Asynchronously parses a TrueType font from a stream, supporting cancellation while copying
    /// a non-seekable source into a bounded temporary buffer.
    /// </summary>
    /// <param name="s">The stream containing the font data.</param>
    /// <param name="options">
    /// Parsing options, or <see langword="null"/> to use <see cref="TrueTypeFontParsingOptions.Default"/>.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel the incremental copy of a non-seekable stream.</param>
    /// <returns>An instance of <see cref="TrueTypeFont"/>.</returns>
    public static async ValueTask<TrueTypeFont> ParseFontAsync(Stream s, TrueTypeFontParsingOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(s);
        options ??= TrueTypeFontParsingOptions.Default;
        options.EnsureValid();
        if (!s.CanRead)
            throw new InvalidOperationException("The stream must be readable");

        try
        {
            if (!s.CanSeek)
            {
                await using var buffered = await CopyToBoundedSeekableStreamAsync(s, options.MaximumFontBytes, cancellationToken).ConfigureAwait(false);
                return ParseFontFromSeekableStream(buffered, options);
            }

            cancellationToken.ThrowIfCancellationRequested();
            long available = s.Length - s.Position;
            if (available > options.MaximumFontBytes)
            {
                FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                    $"Font size {available} exceeds MaximumFontBytes ({options.MaximumFontBytes}).");
            }
            return ParseFontFromSeekableStream(s, options);
        }
        finally
        {
            if (!options.LeaveOpen)
            {
                await s.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Copies a non-seekable stream into an owned, seekable <see cref="MemoryStream"/>, in bounded
    /// blocks, rejecting the input as soon as it would exceed <paramref name="maximumBytes"/>
    /// rather than after buffering it in full.
    /// </summary>
    private static MemoryStream CopyToBoundedSeekableStream(Stream source, long maximumBytes)
    {
        var buffer = new byte[81920];
        var destination = new MemoryStream();
        try
        {
            long total = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > maximumBytes)
                {
                    FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                        $"Font stream exceeds MaximumFontBytes ({maximumBytes}).");
                }
                destination.Write(buffer, 0, read);
            }
            destination.Position = 0;
            return destination;
        }
        catch
        {
            destination.Dispose();
            throw;
        }
    }

    /// <summary>Asynchronous, cancellable counterpart to <see cref="CopyToBoundedSeekableStream"/>.</summary>
    private static async Task<MemoryStream> CopyToBoundedSeekableStreamAsync(Stream source, long maximumBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        var destination = new MemoryStream();
        try
        {
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > maximumBytes)
                {
                    FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                        $"Font stream exceeds MaximumFontBytes ({maximumBytes}).");
                }
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            destination.Position = 0;
            return destination;
        }
        catch
        {
            await destination.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Parses a font whose bytes are fully available through a seekable stream, starting at the
    /// stream's current position. This is the single entry point every public overload converges
    /// on once it holds a bounded, seekable source.
    /// </summary>
    private static TrueTypeFont ParseFontFromSeekableStream(Stream seekableStream, TrueTypeFontParsingOptions options)
    {
        long fontStart = seekableStream.Position;
        long available = seekableStream.Length - fontStart;
        if (available > options.MaximumFontBytes)
        {
            FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                $"Font size {available} exceeds MaximumFontBytes ({options.MaximumFontBytes}).");
        }
        // A PartialStream view remaps position 0 to fontStart, so every SFNT offset (already
        // relative to the start of the font per spec) can be used directly as a position/Slice
        // argument without an extra "fontStart +" at each call site.
        var view = new PartialStream(seekableStream, fontStart, available);
        var data = new Reader(view, rawReader.ReaderDelegates);
        return ParseFont(data, options);
    }

    #endregion

    /// <summary>
    /// Parses a TrueType font from a Reader bounded to exactly the font's own bytes.
    /// </summary>
    /// <param name="data">The reader from which to read the font data.</param>
    /// <param name="options">The active parsing options.</param>
    /// <returns>An instance of <see cref="TrueTypeFont"/>.</returns>
    private static TrueTypeFont ParseFont(Reader data, TrueTypeFontParsingOptions options)
    {
        long fontLength = data.Stream.Length;
        if (fontLength < OffsetTableSize)
        {
            FontParsingContext.Reject(FontDiagnosticCode.InvalidOffsetTable,
                $"Font is too small ({fontLength} bytes) to contain an SFNT offset table.");
        }

        int type = data.Read<Int32>();
        ushort numTables = data.Read<UInt16>();
        ushort declaredSearchRange = data.Read<UInt16>();
        ushort declaredEntrySelector = data.Read<UInt16>();
        ushort declaredRangeShift = data.Read<UInt16>();

        if (numTables > options.MaximumTables)
        {
            FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                $"numTables ({numTables}) exceeds MaximumTables ({options.MaximumTables}).");
        }

        long directoryLength = checked(OffsetTableSize + (long)numTables * TableDirectoryEntrySize);
        if (directoryLength > fontLength)
        {
            FontParsingContext.Reject(FontDiagnosticCode.InvalidDirectoryRange,
                $"Table directory ({directoryLength} bytes for {numTables} tables) does not fit within the font ({fontLength} bytes).");
        }

        TrueTypeFont trueTypeFont = new TrueTypeFont(type);
        var context = new FontParsingContext(options);
        // Kept for the lifetime of the font (not reset once parsing finishes): see the
        // ParsingContext property doc for why lazy composite-glyph resolution needs it later.
        trueTypeFont.ParsingContext = context;
        ValidateDerivedOffsetTableFields(context, numTables, declaredSearchRange, declaredEntrySelector, declaredRangeShift);
        var entries = ReadDirectoryEntries(data, numTables);
        var accepted = ValidateDirectory(context, entries, directoryLength, fontLength);
        ParseDirectories(data, accepted, trueTypeFont, context);
        trueTypeFont.Diagnostics = context.Diagnostics;
        return trueTypeFont;
    }

    /// <summary>
    /// Validates the header's derived fields (searchRange/entrySelector/rangeShift) against the
    /// values implied by <paramref name="numTables"/>, per the TrueType spec's binary-search
    /// layout contract. A mismatch does not affect correctness of this parser (the directory is
    /// still read explicitly, in full), but signals a font that was not produced by a conformant
    /// tool, which is itself worth surfacing.
    /// </summary>
    private static void ValidateDerivedOffsetTableFields(FontParsingContext context, ushort numTables, ushort searchRange, ushort entrySelector, ushort rangeShift)
    {
        ushort expectedSearchRange, expectedEntrySelector, expectedRangeShift;
        if (numTables == 0)
        {
            expectedSearchRange = 0;
            expectedEntrySelector = 0;
            expectedRangeShift = 0;
        }
        else
        {
            int maxPowerOfTwo = 1 << BitOperations.Log2(numTables);
            expectedSearchRange = (ushort)(maxPowerOfTwo * TableDirectoryEntrySize);
            expectedEntrySelector = (ushort)BitOperations.Log2((uint)maxPowerOfTwo);
            expectedRangeShift = (ushort)(numTables * TableDirectoryEntrySize - expectedSearchRange);
        }

        if (searchRange != expectedSearchRange || entrySelector != expectedEntrySelector || rangeShift != expectedRangeShift)
        {
            context.ReportError(FontDiagnosticCode.InvalidOffsetTable,
                $"Offset table declares searchRange={searchRange}, entrySelector={entrySelector}, rangeShift={rangeShift}; " +
                $"expected {expectedSearchRange}, {expectedEntrySelector}, {expectedRangeShift} for numTables={numTables}.");
        }
    }

    /// <summary>Reads the raw directory entries, preserving every record (no de-duplication yet).</summary>
    private static List<TableDirectoryEntry> ReadDirectoryEntries(Reader data, ushort numTables)
    {
        var entries = new List<TableDirectoryEntry>(numTables);
        for (int i = 0; i < numTables; i++)
        {
            Tag tag = data.ReadFixedLengthString(TagLength, Encoding.ASCII);
            uint checksum = data.Read<UInt32>();
            uint offset = data.Read<UInt32>();
            uint length = data.Read<UInt32>();
            entries.Add(new TableDirectoryEntry(tag, checksum, offset, length, i));
        }
        return entries;
    }

    /// <summary>
    /// Validates every directory entry's range, tag uniqueness, and overlap/alias policy, and
    /// returns the entries accepted for table parsing. Range and allocation-limit violations
    /// always drop just that one entry (never dereferenced, so always safe); in
    /// <see cref="FontValidationMode.Strict"/> any dropped entry aborts the whole parse via
    /// <see cref="FontParsingContext.ReportError"/> throwing, while in
    /// <see cref="FontValidationMode.Permissive"/> the font is parsed without it.
    /// </summary>
    private static List<TableDirectoryEntry> ValidateDirectory(FontParsingContext context, List<TableDirectoryEntry> entries, long directoryLength, long fontLength)
    {
        var accepted = new List<TableDirectoryEntry>(entries.Count);
        var seenTags = new HashSet<Tag>();

        foreach (var entry in entries)
        {
            long end;
            try
            {
                end = entry.End;
            }
            catch (OverflowException)
            {
                // Always fatal: an overflowing range calculation must never be used to seek/slice.
                FontParsingContext.Reject(FontDiagnosticCode.InvalidDirectoryRange,
                    $"Table '{entry.Tag}' offset+length overflows a 64-bit range (offset={entry.Offset}, length={entry.Length}).",
                    entry.Tag, entry.Offset, entry.Length);
                throw; // unreachable: Reject always throws, but keeps `end` definitely-assigned for the compiler.
            }

            if (end > fontLength)
            {
                // Always fatal, in both validation modes: an out-of-file range is a memory-safety
                // hazard (it would seek/slice past the end of the source), not a policy choice.
                FontParsingContext.Reject(FontDiagnosticCode.InvalidDirectoryRange,
                    $"Table '{entry.Tag}' range [{entry.Offset}, {end}) exceeds the font length ({fontLength}).",
                    entry.Tag, entry.Offset, entry.Length);
            }

            if (entry.Length > context.Options.MaximumTableBytes)
            {
                // Always fatal: this is an allocation-limit violation, not a policy choice.
                FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                    $"Table '{entry.Tag}' length ({entry.Length}) exceeds MaximumTableBytes ({context.Options.MaximumTableBytes}).",
                    entry.Tag, entry.Offset, entry.Length);
            }

            if (entry.Length > 0 && entry.Offset < directoryLength)
            {
                context.ReportError(FontDiagnosticCode.OverlappingTableRange,
                    $"Table '{entry.Tag}' at offset {entry.Offset} starts before the end of the table directory ({directoryLength}).",
                    entry.Tag, entry.Offset, entry.Length);
                continue;
            }

            if (entry.Offset % TtfAlignment != 0)
            {
                context.ReportWarning(FontDiagnosticCode.InvalidDirectoryRange,
                    $"Table '{entry.Tag}' offset {entry.Offset} is not 4-byte aligned.", entry.Tag, entry.Offset, entry.Length);
            }

            if (!seenTags.Add(entry.Tag))
            {
                context.ReportError(FontDiagnosticCode.DuplicateTableTag,
                    $"Duplicate table tag '{entry.Tag}' (directory entry #{entry.OriginalIndex}); keeping the first occurrence.",
                    entry.Tag, entry.Offset, entry.Length);
                continue;
            }

            accepted.Add(entry);
        }

        DetectOverlaps(context, accepted);
        return accepted;
    }

    /// <summary>
    /// Walks the accepted entries sorted by offset and reports exact aliases, same-offset
    /// conflicts, and partial or containing overlaps -- against every earlier entry whose range
    /// has not yet ended, not merely the immediately preceding one in offset order. A single wide
    /// table can contain several disjoint smaller tables; comparing only adjacent pairs after
    /// sorting would miss the later ones once a nearer, shorter overlap had already "moved past"
    /// the wide table in the scan. Two entries with identical offset and length are treated as a
    /// suspicious alias and rejected in strict mode; every other overlap shape (same offset with
    /// different lengths, partial overlap, or full containment) is always rejected in strict mode.
    /// Neither shape is a memory-safety issue on its own (both ranges were already validated to fit
    /// within the font), so permissive mode records a diagnostic and keeps every entry.
    /// </summary>
    private static void DetectOverlaps(FontParsingContext context, List<TableDirectoryEntry> accepted)
    {
        var ordered = accepted.OrderBy(e => e.Offset).ThenBy(e => e.OriginalIndex).ToList();
        // Active entries whose range might still overlap a later one, ordered so the smallest End
        // is first; entries whose End is at or before the current one's Offset are pruned before
        // comparing, so this stays a simple growing list.
        var active = new List<TableDirectoryEntry>();
        foreach (var current in ordered)
        {
            active.RemoveAll(e => e.End <= current.Offset);
            foreach (var previous in active)
            {
                bool exactAlias = current.Offset == previous.Offset && current.Length == previous.Length;
                var code = exactAlias ? FontDiagnosticCode.AliasedTableRange : FontDiagnosticCode.OverlappingTableRange;
                context.ReportError(code,
                    exactAlias
                        ? $"Tables '{previous.Tag}' and '{current.Tag}' declare an identical range [{current.Offset}, {current.End}) (exact alias)."
                        : $"Tables '{previous.Tag}' [{previous.Offset}, {previous.End}) and '{current.Tag}' [{current.Offset}, {current.End}) overlap.",
                    current.Tag, current.Offset, current.Length);
            }
            active.Add(current);
        }
    }

    /// <summary>
    /// Serializes the TrueType font into a byte array using the default writing options.
    /// </summary>
    /// <returns>A byte array representing the font file.</returns>
    public virtual byte[] WriteFont() => WriteFont(TrueTypeFontWritingOptions.Default);

    /// <summary>
    /// Serializes the TrueType font into a byte array.
    /// </summary>
    /// <param name="options">
    /// Writing options, or <see langword="null"/> to use <see cref="TrueTypeFontWritingOptions.Default"/>.
    /// </param>
    /// <returns>A byte array representing the font file.</returns>
    public virtual byte[] WriteFont(TrueTypeFontWritingOptions options)
    {
        options ??= TrueTypeFontWritingOptions.Default;
        options.EnsureValid();
        PrepareForSerialization();

        if (options.ValidateBeforeWrite && Length > options.MaximumOutputBytes)
        {
            throw new InvalidOperationException($"Serialized font length ({Length}) exceeds MaximumOutputBytes ({options.MaximumOutputBytes}).");
        }

        using var ms = new MemoryStream(Length);
        var data = new Writer(ms, rawWriter.WriterDelegates);

        data.Write<Int32>(Type);
        data.Write<UInt16>(TablesCount);
        data.Write<UInt16>(SearchRange);
        data.Write<UInt16>(EntrySelector);
        data.Write<UInt16>(RangeShift);
        int currentoffset = OffsetTableSize + TablesCount * TableDirectoryEntrySize;
        foreach (var tagTable in tables)
        {
            var tag = tagTable.Key;
            TrueTypeTable obj = tagTable.Value;

            using var datasStream = new MemoryStream();
            Writer w = new Writer(datasStream, rawWriter.WriterDelegates);
            obj.WriteData(w);
            var datas = datasStream.ToArray();
            int dataLength = datas.Length;
            data.WriteFixedLengthString(tag, TagLength, Encoding.ASCII);
            using (var tableStream = new MemoryStream(datas, writable: false))
            {
                data.Write<UInt32>(TableChecksum.ComputeTableChecksum(tableStream, 0, (uint)dataLength, zeroHeadChecksumAdjustment: tag == TableTypes.HEAD));
            }
            data.Write<UInt32>((uint)currentoffset);
            data.Write<UInt32>((uint)dataLength);
            data.Push();
            data.Seek(currentoffset, SeekOrigin.Begin);
            data.Write<byte[]>(datas);
            currentoffset += dataLength;
            while (currentoffset % TtfAlignment > 0)
            {
                data.WriteByte(0);
                currentoffset++;
            }
            data.Pop();
        }
        data.Position = 0;
        UpdateChecksumAdj(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Serializes the TrueType font directly to a destination stream.
    /// </summary>
    /// <remarks>
    /// Implemented as a thin wrapper over <see cref="WriteFont(TrueTypeFontWritingOptions)"/>: the
    /// whole font is still assembled in memory first (so that a validation failure never leaves a
    /// partial font in the in-memory buffer), then copied to <paramref name="destination"/> in one
    /// write. If <paramref name="destination"/> itself fails partway through that write (an I/O
    /// error), it can be left holding a partial, invalid font -- this method only guarantees
    /// atomicity for the in-memory layout, not for the destination stream.
    /// </remarks>
    /// <param name="destination">The stream to write the serialized font to.</param>
    /// <param name="options">
    /// Writing options, or <see langword="null"/> to use <see cref="TrueTypeFontWritingOptions.Default"/>.
    /// </param>
    public virtual void WriteFont(Stream destination, TrueTypeFontWritingOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        byte[] bytes = WriteFont(options);
        destination.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Asynchronously serializes the TrueType font directly to a destination stream.
    /// </summary>
    /// <param name="destination">The stream to write the serialized font to.</param>
    /// <param name="options">
    /// Writing options, or <see langword="null"/> to use <see cref="TrueTypeFontWritingOptions.Default"/>.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel the write to <paramref name="destination"/>.</param>
    public virtual async ValueTask WriteFontAsync(Stream destination, TrueTypeFontWritingOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        byte[] bytes = WriteFont(options);
        await destination.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs every preparation step required before the font's layout can be measured or written:
    /// currently, freezing the 'loca' table's offsets and short/long format decision (see
    /// <see cref="LocaTable.PrepareForSerialization"/>). Idempotent and safe to call multiple times.
    /// </summary>
    /// <remarks>
    /// Previously, <c>LocaTable.Length</c> silently recomputed 'loca' offsets from 'glyf' as a side
    /// effect of being read (<c>RefreshOffsetsFromGlyf()</c> called from the property getter),
    /// which meant a mere property read could change what a subsequent <c>WriteData</c> call
    /// serialized depending on call order. This method makes that step explicit and single-shot,
    /// invoked once up front by every serialization entry point.
    /// </remarks>
    private void PrepareForSerialization()
    {
        if (TryGetTable<LocaTable>(TableTypes.LOCA, out var loca))
        {
            loca.PrepareForSerialization();
        }
    }

    /// <summary>
    /// Parses the directory of tables from the font data, resolving each table's declared
    /// dependencies before it is read, using a bounded slice of the source stream for each
    /// table's payload rather than copying every table into a separate in-memory buffer up front.
    /// </summary>
    /// <param name="data">The reader from which to read the table directory.</param>
    /// <param name="entries">The validated, deduplicated directory entries to read.</param>
    /// <param name="ttf">The TrueTypeFont instance to populate.</param>
    /// <param name="context">The active parsing context (options + diagnostics sink).</param>
    private static void ParseDirectories(Reader data, List<TableDirectoryEntry> entries, TrueTypeFont ttf, FontParsingContext context)
    {
        var remaining = entries.ToDictionary(e => e.Tag);

        void ReadTable(TableDirectoryEntry entry)
        {
            if (ttf.ContainsTable(entry.Tag)) { return; }
            remaining.Remove(entry.Tag);

            TrueTypeTable ttt;
            if (TablesType.TryGetValue(entry.Tag, out var d))
            {
                foreach (var dependency in d.Descriptor.DependsOn)
                {
                    if (remaining.TryGetValue(dependency, out var dependencyEntry))
                    {
                        ReadTable(dependencyEntry);
                    }
                }
                ttt = (TrueTypeTable)Activator.CreateInstance(d.TableType, true);
            }
            else
            {
                context.ReportWarning(FontDiagnosticCode.UnsupportedTable,
                    $"Table '{entry.Tag}' has no known implementation; its data is preserved verbatim.",
                    entry.Tag, entry.Offset, entry.Length);
                ttt = new TrueTypeTable(entry.Tag);
            }

            uint computed = TableChecksum.ComputeTableChecksum(data.Stream, entry.Offset, entry.Length, zeroHeadChecksumAdjustment: entry.Tag == TableTypes.HEAD);
            if (computed != entry.Checksum)
            {
                context.ReportError(FontDiagnosticCode.TableChecksumMismatch,
                    $"Table '{entry.Tag}' declared checksum {entry.Checksum:X8} does not match computed checksum {computed:X8}.",
                    entry.Tag, entry.Offset, entry.Length);
            }

            ttf.AddTable(entry.Tag, ttt);
            ttt.ReadData(data.Slice(entry.Offset, entry.Length));
        }

        while (remaining.Count > 0)
        {
            ReadTable(remaining.Values.First());
        }

        VerifyFontChecksum(data.Stream, ttf, entries, context);
    }

    /// <summary>
    /// Verifies the whole-font checksum: the sum of every 32-bit big-endian word in the font
    /// (including 'head'.checksumAdjustment as stored, unmodified) must equal
    /// <see cref="ChecksumMagicNumber"/>. Fonts with no 'head' table have no checksumAdjustment
    /// slot to make this identity hold and are skipped rather than unconditionally failed.
    /// </summary>
    private static void VerifyFontChecksum(Stream fontStream, TrueTypeFont ttf, List<TableDirectoryEntry> entries, FontParsingContext context)
    {
        if (!ttf.ContainsTable(TableTypes.HEAD) || entries.Count == 0)
        {
            return;
        }

        long directoryLength = OffsetTableSize + entries.Count * TableDirectoryEntrySize;
        long fontExtent = entries.Aggregate(directoryLength, (max, e) => Math.Max(max, MathEx.Ceiling(e.End, TtfAlignment)));

        uint computed = TableChecksum.ComputeTableChecksum(fontStream, 0, (uint)Math.Min(fontExtent, uint.MaxValue), zeroHeadChecksumAdjustment: false);
        if (computed != ChecksumMagicNumber)
        {
            context.ReportError(FontDiagnosticCode.FontChecksumMismatch,
                $"Whole-font checksum {computed:X8} does not equal the required magic number {ChecksumMagicNumber:X8}.");
        }
    }

    /// <summary>
    /// Creates a new instance of a table corresponding to the specified tag.
    /// </summary>
    /// <param name="tag">The tag identifying the table.</param>
    /// <returns>An instance of <see cref="TrueTypeTable"/>.</returns>
    public TrueTypeTable CreateTable(Tag tag)
    {
        if (TablesType.TryGetValue(tag, out var d))
        {
            return (TrueTypeTable)Activator.CreateInstance(d.TableType, true);
        }
        else
        {
            return new TrueTypeTable(tag);
        }
    }

    /// <summary>
    /// Gets the number of tables in the font.
    /// </summary>
    public virtual ushort TablesCount => (ushort)tables.Count;

    /// <summary>
    /// Gets the search range used in the font header. Explicitly zero for a font with no tables
    /// (rather than relying on <see cref="BitOperations.Log2(uint)"/>'s behavior at zero).
    /// </summary>
    public virtual ushort SearchRange =>
        TablesCount == 0 ? (ushort)0 : (ushort)(TableDirectoryEntrySize * (1 << BitOperations.Log2((uint)TablesCount)));

    /// <summary>
    /// Gets the entry selector used in the font header. Explicitly zero for a font with no tables.
    /// </summary>
    public virtual ushort EntrySelector =>
        TablesCount == 0 ? (ushort)0 : (ushort)BitOperations.Log2((uint)TablesCount);

    /// <summary>
    /// Gets the range shift used in the font header. Explicitly zero for a font with no tables.
    /// </summary>
    public virtual ushort RangeShift =>
        // TTF spec: rangeShift = numTables * 16 - searchRange
        TablesCount == 0 ? (ushort)0 : (ushort)(TablesCount * TableDirectoryEntrySize - SearchRange);

    /// <summary>
    /// Updates the checksum adjustment value in the 'head' table.
    /// </summary>
    /// <param name="fontStream">The freshly written, fully-owned in-memory font buffer.</param>
    private void UpdateChecksumAdj(MemoryStream fontStream)
    {
        unchecked
        {
            uint checksum = TableChecksum.ComputeTableChecksum(fontStream, 0, (uint)fontStream.Length, zeroHeadChecksumAdjustment: false);
            uint checksumAdj = ChecksumMagicNumber - checksum;
            int offset = OffsetTableSize + TablesCount * TableDirectoryEntrySize;
            foreach (var table in tables)
            {
                var tag = table.Key;
                if (tag == TableTypes.HEAD)
                {
                    fontStream.Position = offset + HeadTable.ChecksumAdjustmentOffset;
                    var w = new Writer(fontStream, rawWriter.WriterDelegates);
                    w.Write<UInt32>(checksumAdj);
                    break;
                }
                // Must mirror the padded layout WriteFont just produced (each table's data is
                // padded up to a 4-byte boundary before the next table starts); using the raw,
                // unpadded Length here would misplace 'head'.checksumAdjustment whenever an
                // earlier table's length is not itself a multiple of 4.
                offset += MathEx.Ceiling(table.Value.Length, TtfAlignment);
            }
        }
    }

    /// <summary>
    /// Retrieves the table corresponding to the specified tag.
    /// </summary>
    /// <typeparam name="T">The type of the table.</typeparam>
    /// <param name="tag">The table tag.</param>
    /// <returns>An instance of the table.</returns>
    public virtual T GetTable<T>(Tag tag) where T : TrueTypeTable => (T)tables[tag];

    /// <summary>
    /// Attempts to retrieve the table corresponding to the specified tag.
    /// </summary>
    /// <typeparam name="T">The type of the table.</typeparam>
    /// <param name="tag">The table tag.</param>
    /// <param name="table">When this method returns, contains the table if found; otherwise, null.</param>
    /// <returns><see langword="true"/> if the table was found; otherwise, <see langword="false"/>.</returns>
    public virtual bool TryGetTable<T>(Tag tag, out T table) where T : TrueTypeTable
    {
        if (tables.TryGetValue(tag, out var result))
        {
            table = (T)result;
            return true;
        }
        else
        {
            table = null;
            return false;
        }
    }

    /// <summary>
    /// Adds a table to the font.
    /// </summary>
    /// <param name="tag">The table tag.</param>
    /// <param name="ttf">The table instance.</param>
    public virtual void AddTable(Tag tag, TrueTypeTable ttf)
    {
        ttf.TrueTypeFont = this;
        tables[tag] = ttf;
    }

    /// <summary>
    /// Indicates whether the font contains a table with the specified tag.
    /// </summary>
    /// <param name="tag">The table tag.</param>
    /// <returns><see langword="true"/> if the table exists; otherwise, <see langword="false"/>.</returns>
    public bool ContainsTable(Tag tag) => tables.ContainsKey(tag);

    /// <summary>
    /// Removes the table with the specified tag from the font.
    /// </summary>
    /// <param name="tag">The table tag.</param>
    public virtual void RemoveTable(Tag tag) => tables.Remove(tag);

    /// <summary>
    /// Returns a string representation of the font, including information from each table.
    /// </summary>
    /// <returns>A string describing the font.</returns>
    public override string ToString()
    {
        StringBuilder result = new StringBuilder();
        result.AppendLine($"Type         : {Type}");
        result.AppendLine($"NumTables    : {TablesCount}");
        result.AppendLine($"SearchRange  : {SearchRange}");
        result.AppendLine($"EntrySelector: {EntrySelector}");
        result.AppendLine($"RangeShift   : {RangeShift}");
        foreach (var table in tables)
        {
            result.AppendLine(table.Value.ToString());
        }
        return result.ToString();
    }

    /// <summary>
    /// Resolves a character to its glyph index using the font's 'cmap' table.
    /// </summary>
    /// <param name="cmap">The font's cmap table.</param>
    /// <param name="c">The character to resolve.</param>
    /// <returns>The glyph index for <paramref name="c"/>, or 0 (<c>.notdef</c>) if no subtable maps it.</returns>
    private static int ResolveGlyphIndex(CmapTable cmap, char c)
    {
        foreach (var map in cmap.CMaps)
        {
            int index = map.Map(c);
            if (index > 0)
            {
                return index;
            }
        }
        return 0;
    }

    /// <summary>
    /// Retrieves the glyph corresponding to the specified character.
    /// </summary>
    /// <param name="c">The character for which to retrieve the glyph.</param>
    /// <returns>An <see cref="IGlyph"/> representing the character glyph, or null if not found.</returns>
    public IGlyph GetGlyph(char c)
    {
        var cmap = GetTable<CmapTable>(TableTypes.CMAP);
        var glyf = GetTable<GlyfTable>(TableTypes.GLYF);
        var hmtx = GetTable<HmtxTable>(TableTypes.HMTX);
        int index = ResolveGlyphIndex(cmap, c);
        if (index > 0 && glyf.TryGetGlyph(index, out var glyphBase) && glyphBase is not null)
        {
            return new TrueTypeGlyph(glyphBase, hmtx.GetAdvance(index));
        }
        return null;
    }

    /// <summary>
    /// Returns the spacing correction (kerning) between two adjacent characters.
    /// If a kern table is present, its value is returned; otherwise, 0 is returned.
    /// </summary>
    /// <param name="before">The preceding character.</param>
    /// <param name="after">The following character.</param>
    /// <returns>The spacing correction in font units.</returns>
    /// <remarks>
    /// The 'kern' table stores kerning pairs by glyph index, never by character code, so
    /// <paramref name="before"/> and <paramref name="after"/> are resolved through 'cmap' first
    /// (the same resolution <see cref="GetGlyph"/> performs) before consulting the kern table.
    /// </remarks>
    public float GetSpacingCorrection(char before, char after)
    {
        // If the font contains a kern table, retrieve it and use it to compute spacing correction.
        if (ContainsTable(TableTypes.KERN))
        {
            var cmap = GetTable<CmapTable>(TableTypes.CMAP);
            ushort beforeGlyph = (ushort)ResolveGlyphIndex(cmap, before);
            ushort afterGlyph = (ushort)ResolveGlyphIndex(cmap, after);
            var kernTable = GetTable<KernTable>(TableTypes.KERN);
            return kernTable.GetSpacingCorrection(beforeGlyph, afterGlyph);
        }
        return 0f;
    }
}
