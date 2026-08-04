using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Utils.Fonts.TTF.Parsing;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Glyf;

/// <summary>
/// Represents a compound glyph in a TrueType font. A compound glyph is composed of multiple simple glyph
/// components, each with its own transformation.
/// </summary>
public class GlyphCompound : GlyphBase
{
    /// <summary>
    /// Represents a single component of a compound glyph.
    /// Contains transformation parameters and the glyph index of the component.
    /// </summary>
    internal class GlyfComponent
    {
        /// <summary>
        /// The compound glyph flags that specify component properties.
        /// </summary>
        public CompoundGlyfFlags Flags { get; internal set; }

        /// <summary>
        /// The glyph index of this component. Unsigned per the OpenType 'glyf' spec (glyphIndex is
        /// a uint16): a font with more than 32767 glyphs would otherwise have high-numbered
        /// component references misread as negative.
        /// </summary>
        public ushort GlyphIndex { get; internal set; }

        /// <summary>
        /// The compound point index.
        /// </summary>
        public int CompoundPoint { get; internal set; }

        /// <summary>
        /// The component point index.
        /// </summary>
        public int ComponentPoint { get; internal set; }

        /// <summary>
        /// Matrix element [1,1]: horizontal scale (default is 1).
        /// </summary>
        public float M11 { get; internal set; } = 1f;

        /// <summary>
        /// Matrix element [2,1]: vertical shear (default is 0).
        /// </summary>
        public float M21 { get; internal set; } = 0f;

        /// <summary>
        /// Matrix element [1,2]: horizontal shear (default is 0).
        /// </summary>
        public float M12 { get; internal set; } = 0f;

        /// <summary>
        /// Matrix element [2,2]: vertical scale (default is 1).
        /// </summary>
        public float M22 { get; internal set; } = 1f;

        /// <summary>
        /// Horizontal translation offset (default is 0).
        /// </summary>
        public float TranslateX { get; internal set; } = 0f;

        /// <summary>
        /// Vertical translation offset (default is 0).
        /// </summary>
        public float TranslateY { get; internal set; } = 0f;

        /// <summary>
        /// Computed horizontal scale adjustment factor (derived from M11 and M21).
        /// </summary>
        public float AdjustX { get; private set; } = 0f;

        /// <summary>
        /// Computed vertical scale adjustment factor (derived from M12 and M22).
        /// </summary>
        public float AdjustY { get; private set; } = 0f;

        /// <summary>
        /// Computes the transformation adjustment factors based on the current matrix values.
        /// </summary>
        /// <remarks>
        /// Follows the F2Dot14 compensation algorithm from the TrueType/OpenType glyf composite
        /// glyph spec. In the spec's own naming, the transform matrix is <c>[a c; b d]</c> with
        /// <c>a</c>=<see cref="M11"/>, <c>b</c>=<see cref="M21"/>, <c>c</c>=<see cref="M12"/>,
        /// <c>d</c>=<see cref="M22"/> (matching the read order for a full 2x2 matrix and the point
        /// transform formula in <see cref="Transform(float, float, bool)"/>): <c>m0 = max(|a|,|b|)</c>,
        /// doubled when <c>||a|-|c|| &lt;= limit</c>; <c>n0 = max(|c|,|d|)</c>, doubled when
        /// <c>||b|-|d|| &lt;= limit</c>.
        /// </remarks>
        public virtual void ComputeTransform()
        {
            const float limit = (33f / 65535f);
            AdjustX = Math.Max(Math.Abs(M11), Math.Abs(M21));
            if (Math.Abs(Math.Abs(M11) - Math.Abs(M12)) < limit)
            {
                AdjustX *= 2f;
            }
            AdjustY = Math.Max(Math.Abs(M12), Math.Abs(M22));
            if (Math.Abs(Math.Abs(M21) - Math.Abs(M22)) < limit)
            {
                AdjustY *= 2f;
            }
        }

        /// <summary>
        /// Transforms the specified <see cref="TTFPoint"/> using this component's transformation.
        /// </summary>
        /// <param name="point">The point to transform.</param>
        /// <returns>A new transformed <see cref="TTFPoint"/>.</returns>
        public TTFPoint Transform(TTFPoint point) => Transform(point.X, point.Y, point.OnCurve);

        /// <summary>
        /// Transforms the specified coordinates and on-curve flag using this component's transformation.
        /// </summary>
        /// <param name="x">The x-coordinate to transform.</param>
        /// <param name="y">The y-coordinate to transform.</param>
        /// <param name="onCurve">Indicates whether the point is on the curve.</param>
        /// <returns>A new <see cref="TTFPoint"/> representing the transformed point.</returns>
        /// <remarks>
        /// The translation offset (<see cref="TranslateX"/>/<see cref="TranslateY"/>) is only scaled
        /// by <see cref="AdjustX"/>/<see cref="AdjustY"/> when the component explicitly declares
        /// <see cref="CompoundGlyfFlags.ScaledComponentOffset"/>. When neither that flag nor
        /// <see cref="CompoundGlyfFlags.UnscaledComponentOffset"/> is present -- the common case for
        /// fonts built with Microsoft-oriented tooling -- the offset is used unscaled, matching the
        /// de facto convention (also followed by FreeType) rather than the historical Apple default
        /// of always scaling it.
        /// </remarks>
        public TTFPoint Transform(float x, float y, bool onCurve)
        {
            bool scaleOffset = Flags.HasFlag(CompoundGlyfFlags.ScaledComponentOffset);
            float offsetScaleX = scaleOffset ? AdjustX : 1f;
            float offsetScaleY = scaleOffset ? AdjustY : 1f;
            return new TTFPoint(
                M11 * x + M12 * y + offsetScaleX * TranslateX,
                M21 * x + M22 * y + offsetScaleY * TranslateY,
                onCurve
            );
        }
    }

    /// <inheritdoc/>
    public override bool IsCompound => true;

    /// <summary>
    /// Gets the array of glyph components that make up this compound glyph.
    /// </summary>
    private GlyfComponent[] Components { get; set; }

    /// <summary>
    /// Gets the instruction bytes for the compound glyph. A defensive copy is not needed on every
    /// access: the backing array is never exposed or mutated after <see cref="ReadData"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Instructions { get; private set; }

    /// <summary>
    /// Gets the number of components in this compound glyph.
    /// </summary>
    public virtual int ComponentsCount => Components.Length;

    /// <summary>
    /// Gets the glyph index of the component at the specified index.
    /// </summary>
    /// <param name="componentIndex">The zero-based index of the component.</param>
    /// <returns>The glyph index of the component.</returns>
    /// <remarks>
    /// Renamed from the previous <c>getGlyphIndex</c> (non-standard casing) and widened from
    /// <see cref="short"/> to <see cref="ushort"/>, matching <see cref="GlyfComponent.GlyphIndex"/>:
    /// a 2.0 breaking change, tracked in <c>eng/api-breaking-changes/2.0.0.json</c>.
    /// </remarks>
    public virtual ushort GetGlyphIndex(int componentIndex) => Components[componentIndex].GlyphIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphCompound"/> class.
    /// </summary>
    protected internal GlyphCompound() { }

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        var options = GlyfTable?.TrueTypeFont?.ParsingContext?.Options ?? TrueTypeFontParsingOptions.Default;
        List<GlyfComponent> comps = [];
        bool hasInstructions = false;
        GlyfComponent current;
        do
        {
            if (comps.Count >= options.MaximumCompositeComponents)
            {
                FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                    $"Compound glyph exceeds MaximumCompositeComponents ({options.MaximumCompositeComponents}).");
            }
            current = new GlyfComponent();
            current.Flags = (CompoundGlyfFlags)data.Read<Int16>();
            current.GlyphIndex = data.Read<UInt16>();

            ValidateFlags(current.Flags);

            bool argsAreWords = current.Flags.HasFlag(CompoundGlyfFlags.ArgsAreWords);
            if (current.Flags.HasFlag(CompoundGlyfFlags.ArgsAreXY))
            {
                // Offsets are signed: int16 when ARGS_ARE_WORDS, otherwise int8.
                current.TranslateX = argsAreWords ? data.Read<Int16>() : data.Read<SByte>();
                current.TranslateY = argsAreWords ? data.Read<Int16>() : data.Read<SByte>();
            }
            else
            {
                // Point-matching indices are unsigned: uint16 when ARGS_ARE_WORDS, otherwise uint8.
                current.CompoundPoint = argsAreWords ? data.Read<UInt16>() : data.Read<Byte>();
                current.ComponentPoint = argsAreWords ? data.Read<UInt16>() : data.Read<Byte>();
            }

            if (current.Flags.HasFlag(CompoundGlyfFlags.HasScale))
            {
                current.M11 = data.Read<Int16>() / 16384f;
                current.M22 = current.M11;
            }
            else if (current.Flags.HasFlag(CompoundGlyfFlags.HasXYScale))
            {
                current.M11 = data.Read<Int16>() / 16384f;
                current.M22 = data.Read<Int16>() / 16384f;
            }
            else if (current.Flags.HasFlag(CompoundGlyfFlags.HasTwoByTwo))
            {
                current.M11 = data.Read<Int16>() / 16384f;
                current.M21 = data.Read<Int16>() / 16384f;
                current.M12 = data.Read<Int16>() / 16384f;
                current.M22 = data.Read<Int16>() / 16384f;
            }
            if (current.Flags.HasFlag(CompoundGlyfFlags.HasInstructions))
            {
                hasInstructions = true;
            }
            comps.Add(current);
        }
        while ((current.Flags & CompoundGlyfFlags.MoreComponents) != 0);
        Components = comps.ToArray();

        ValidateComponentReferences();

        byte[] instructions;
        if (hasInstructions)
        {
            int instructionsCount = data.Read<UInt16>();
            instructions = data.ReadBytes(instructionsCount);
        }
        else
        {
            instructions = []; // Using target-typed empty array syntax
        }
        Instructions = instructions;
    }

    /// <summary>
    /// Rejects flag combinations that the spec declares mutually exclusive: at most one of
    /// <see cref="CompoundGlyfFlags.HasScale"/>, <see cref="CompoundGlyfFlags.HasXYScale"/>, and
    /// <see cref="CompoundGlyfFlags.HasTwoByTwo"/> may be set, and at most one of
    /// <see cref="CompoundGlyfFlags.ScaledComponentOffset"/>/<see cref="CompoundGlyfFlags.UnscaledComponentOffset"/>.
    /// </summary>
    private static void ValidateFlags(CompoundGlyfFlags flags)
    {
        int scaleFlagCount =
            (flags.HasFlag(CompoundGlyfFlags.HasScale) ? 1 : 0) +
            (flags.HasFlag(CompoundGlyfFlags.HasXYScale) ? 1 : 0) +
            (flags.HasFlag(CompoundGlyfFlags.HasTwoByTwo) ? 1 : 0);
        if (scaleFlagCount > 1)
        {
            FontParsingContext.Reject(FontDiagnosticCode.InvalidCompositeGlyph,
                $"Compound glyph component declares mutually exclusive scale flags ({flags}).");
        }
        if (flags.HasFlag(CompoundGlyfFlags.ScaledComponentOffset) && flags.HasFlag(CompoundGlyfFlags.UnscaledComponentOffset))
        {
            FontParsingContext.Reject(FontDiagnosticCode.InvalidCompositeGlyph,
                $"Compound glyph component declares both ScaledComponentOffset and UnscaledComponentOffset ({flags}).");
        }
    }

    /// <summary>
    /// Validates every component's <see cref="GlyfComponent.GlyphIndex"/> against the font's total
    /// glyph count, once <see cref="GlyfTable"/> is known to have been assigned (see
    /// <see cref="GlyphBase.CreateGlyf"/>). Policy-dependent: strict mode rejects the whole font,
    /// permissive mode records a diagnostic (the reference then resolves to no contours, via
    /// <see cref="GlyfTable.TryGetGlyph"/>, rather than throwing again at resolution time).
    /// </summary>
    private void ValidateComponentReferences()
    {
        var context = GlyfTable?.TrueTypeFont?.ParsingContext;
        int numGlyphs = GlyfTable?.NumGlyphs ?? -1;
        if (context is null || numGlyphs < 0)
        {
            return;
        }
        for (int i = 0; i < Components.Length; i++)
        {
            var component = Components[i];
            if (component.GlyphIndex >= numGlyphs)
            {
                context.ReportError(FontDiagnosticCode.InvalidCompositeGlyph,
                    $"Composite glyph component {i} references glyph ID {component.GlyphIndex}, but the font declares only {numGlyphs} glyphs.");
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether any component declared <see cref="CompoundGlyfFlags.HasInstructions"/>,
    /// meaning a trailing instruction-length word (and instruction bytes) follow the components --
    /// even when there happen to be zero instruction bytes. Mirrors the condition <see cref="ReadData"/>
    /// uses to decide whether to read that trailing data.
    /// </summary>
    private bool HasInstructionsFlag => Components.Any(c => c.Flags.HasFlag(CompoundGlyfFlags.HasInstructions));

    /// <summary>
    /// Gets the length (in bytes) of the compound-glyph-specific data (components plus any
    /// trailing instructions), on top of the 10-byte header written by <see cref="GlyphBase"/>.
    /// </summary>
    /// <exception cref="NullReferenceException">
    /// Thrown if this glyph has no components (e.g. constructed without calling
    /// <see cref="ReadData"/>).
    /// </exception>
    public override int Length
    {
        get
        {
            int size = base.Length;
            foreach (var component in Components)
            {
                size += 4; // flags (Int16) + glyphIndex (UInt16)
                size += component.Flags.HasFlag(CompoundGlyfFlags.ArgsAreWords) ? 4 : 2; // translate/point-matching args
                size += component.Flags switch
                {
                    var f when f.HasFlag(CompoundGlyfFlags.HasTwoByTwo) => 8,
                    var f when f.HasFlag(CompoundGlyfFlags.HasXYScale) => 4,
                    var f when f.HasFlag(CompoundGlyfFlags.HasScale) => 2,
                    _ => 0,
                };
            }
            if (HasInstructionsFlag)
            {
                size += 2 + Instructions.Length;
            }
            return size;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Mirrors the wire format read by <see cref="ReadData"/> exactly: point-matching arguments
    /// (<see cref="GlyfComponent.CompoundPoint"/>/<see cref="GlyfComponent.ComponentPoint"/>) and
    /// translation offsets are written as bytes or words depending on <c>ARGS_ARE_WORDS</c>. Every
    /// narrowing conversion is checked and throws rather than silently truncating a value that
    /// cannot be represented on the wire.
    /// </remarks>
    public override void WriteData(Writer data)
    {
        base.WriteData(data);
        foreach (var component in Components)
        {
            data.Write<Int16>((short)component.Flags);
            data.Write<UInt16>(component.GlyphIndex);
            bool argsAreWords = component.Flags.HasFlag(CompoundGlyfFlags.ArgsAreWords);
            if (component.Flags.HasFlag(CompoundGlyfFlags.ArgsAreXY))
            {
                if (argsAreWords)
                {
                    data.Write<Int16>(CheckedInt16(component.TranslateX, "TranslateX"));
                    data.Write<Int16>(CheckedInt16(component.TranslateY, "TranslateY"));
                }
                else
                {
                    data.Write<SByte>(CheckedSByte(component.TranslateX, "TranslateX"));
                    data.Write<SByte>(CheckedSByte(component.TranslateY, "TranslateY"));
                }
            }
            else
            {
                if (argsAreWords)
                {
                    data.Write<UInt16>(CheckedUInt16(component.CompoundPoint, "CompoundPoint"));
                    data.Write<UInt16>(CheckedUInt16(component.ComponentPoint, "ComponentPoint"));
                }
                else
                {
                    data.Write<Byte>(CheckedByte(component.CompoundPoint, "CompoundPoint"));
                    data.Write<Byte>(CheckedByte(component.ComponentPoint, "ComponentPoint"));
                }
            }

            if (component.Flags.HasFlag(CompoundGlyfFlags.HasScale))
            {
                data.Write<Int16>(CheckedF2Dot14(component.M11, "M11"));
            }
            else if (component.Flags.HasFlag(CompoundGlyfFlags.HasXYScale))
            {
                data.Write<Int16>(CheckedF2Dot14(component.M11, "M11"));
                data.Write<Int16>(CheckedF2Dot14(component.M22, "M22"));
            }
            else if (component.Flags.HasFlag(CompoundGlyfFlags.HasTwoByTwo))
            {
                data.Write<Int16>(CheckedF2Dot14(component.M11, "M11"));
                data.Write<Int16>(CheckedF2Dot14(component.M21, "M21"));
                data.Write<Int16>(CheckedF2Dot14(component.M12, "M12"));
                data.Write<Int16>(CheckedF2Dot14(component.M22, "M22"));
            }
        }
        if (HasInstructionsFlag)
        {
            var instructions = Instructions.Span;
            data.Write<UInt16>((ushort)instructions.Length);
            foreach (byte b in instructions)
            {
                data.WriteByte(b);
            }
        }
    }

    /// <summary>Validates that a translation offset fits a signed 16-bit word before writing.</summary>
    private static short CheckedInt16(float value, string fieldName)
    {
        if (value < short.MinValue || value > short.MaxValue || float.IsNaN(value))
        {
            throw new InvalidOperationException($"Compound glyph component {fieldName} value {value} is not representable as Int16.");
        }
        return (short)value;
    }

    /// <summary>Validates that a translation offset fits a signed byte before writing.</summary>
    private static sbyte CheckedSByte(float value, string fieldName)
    {
        if (value < sbyte.MinValue || value > sbyte.MaxValue || float.IsNaN(value))
        {
            throw new InvalidOperationException($"Compound glyph component {fieldName} value {value} is not representable as SByte.");
        }
        return (sbyte)value;
    }

    /// <summary>Validates that a point-matching index fits an unsigned 16-bit word before writing.</summary>
    private static ushort CheckedUInt16(int value, string fieldName)
    {
        if (value < 0 || value > ushort.MaxValue)
        {
            throw new InvalidOperationException($"Compound glyph component {fieldName} value {value} is not representable as UInt16.");
        }
        return (ushort)value;
    }

    /// <summary>Validates that a point-matching index fits an unsigned byte before writing.</summary>
    private static byte CheckedByte(int value, string fieldName)
    {
        if (value < 0 || value > byte.MaxValue)
        {
            throw new InvalidOperationException($"Compound glyph component {fieldName} value {value} is not representable as Byte.");
        }
        return (byte)value;
    }

    /// <summary>Validates that a transform matrix coefficient fits the F2Dot14 fixed-point format before writing.</summary>
    private static short CheckedF2Dot14(float value, string fieldName)
    {
        if (float.IsNaN(value) || !float.IsFinite(value))
        {
            throw new InvalidOperationException($"Compound glyph component {fieldName} value {value} is not finite.");
        }
        double scaled = Math.Round(value * 16384f);
        if (scaled < short.MinValue || scaled > short.MaxValue)
        {
            throw new InvalidOperationException($"Compound glyph component {fieldName} value {value} is not representable as F2Dot14.");
        }
        return (short)scaled;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Resolved lazily, once, through <see cref="ResolveContours"/> with a fresh, bounded
    /// <see cref="CompositeGlyphResolutionContext"/>, and cached: repeated access (e.g. rendering
    /// the same glyph for every occurrence of a character) does not re-walk or re-validate the
    /// component graph.
    /// </remarks>
    public override IEnumerable<IEnumerable<TTFPoint>> Contours
    {
        get
        {
            if (resolvedContours is not null)
            {
                return resolvedContours;
            }
            var options = GlyfTable?.TrueTypeFont?.ParsingContext?.Options ?? TrueTypeFontParsingOptions.Default;
            var context = new CompositeGlyphResolutionContext { Options = options };
            resolvedContours = ResolveContours(context).Select(c => (IReadOnlyList<TTFPoint>)c).ToList();
            return resolvedContours;
        }
    }

    private List<IReadOnlyList<TTFPoint>> resolvedContours;

    /// <inheritdoc/>
    internal override IEnumerable<IEnumerable<TTFPoint>> ResolveContours(CompositeGlyphResolutionContext context)
    {
        var result = new List<List<TTFPoint>>();
        foreach (var component in Components)
        {
            component.ComputeTransform();
            ushort targetIndex = component.GlyphIndex;

            if (context.ActiveGlyphs.Contains(targetIndex))
            {
                string path = string.Join(" -> ", context.ActiveGlyphs.Reverse().Select(g => g.ToString()));
                FontParsingContext.Reject(FontDiagnosticCode.InvalidCompositeGlyph,
                    $"Composite glyph cycle detected: {path} -> {targetIndex}.");
            }
            if (context.Depth >= context.Options.MaximumCompositeDepth)
            {
                FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                    $"Composite glyph resolution exceeds MaximumCompositeDepth ({context.Options.MaximumCompositeDepth}).");
            }
            if (++context.ExpandedComponents > context.Options.MaximumExpandedComponents)
            {
                FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                    $"Composite glyph resolution exceeds MaximumExpandedComponents ({context.Options.MaximumExpandedComponents}).");
            }

            if (GlyfTable is null || !GlyfTable.TryGetGlyph(targetIndex, out var target) || target is null)
            {
                continue; // Out-of-range (permissive mode only) or empty (e.g. space) component: contributes nothing.
            }

            context.ActiveGlyphs.Push(targetIndex);
            context.Depth++;
            try
            {
                foreach (var contour in target.ResolveContours(context))
                {
                    var transformed = contour.Select(component.Transform).ToList();
                    context.ExpandedPoints += transformed.Count;
                    if (context.ExpandedPoints > context.Options.MaximumExpandedPoints)
                    {
                        FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                            $"Composite glyph resolution exceeds MaximumExpandedPoints ({context.Options.MaximumExpandedPoints}).");
                    }
                    result.Add(transformed);
                }
            }
            finally
            {
                context.Depth--;
                context.ActiveGlyphs.Pop();
            }
        }
        return result;
    }
}
