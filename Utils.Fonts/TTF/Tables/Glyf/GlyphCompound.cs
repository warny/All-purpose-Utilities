using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
        /// The glyph index of this component.
        /// </summary>
        public short GlyphIndex { get; internal set; }

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
    /// Gets the instruction bytes for the compound glyph.
    /// </summary>
    public byte[] Instructions { get; private set; }

    /// <summary>
    /// Gets the number of components in this compound glyph.
    /// </summary>
    public virtual int ComponentsCount => Components.Length;

    /// <summary>
    /// Gets the glyph index of the component at the specified index.
    /// </summary>
    /// <param name="i">The zero-based index of the component.</param>
    /// <returns>The glyph index of the component.</returns>
    public virtual short getGlyphIndex(int i) => Components[i].GlyphIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphCompound"/> class.
    /// </summary>
    protected internal GlyphCompound() { }

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        const int MaxComponents = 1000;
        List<GlyfComponent> comps = [];
        bool hasInstructions = false;
        GlyfComponent current;
        do
        {
            if (comps.Count >= MaxComponents)
                throw new InvalidDataException($"Compound glyph exceeds the maximum allowed component count ({MaxComponents}). The font data may be malformed.");
            current = new GlyfComponent();
            current.Flags = (CompoundGlyfFlags)data.Read<Int16>();
            current.GlyphIndex = data.Read<Int16>();
            if (current.Flags.HasFlag(CompoundGlyfFlags.ArgsAreXY))
            {
                current.TranslateX = data.Read<Int16>();
                current.TranslateY = data.Read<Int16>();
            }
            else
            {
                current.CompoundPoint = data.Read<Int16>();
                current.ComponentPoint = data.Read<Int16>();
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
    public override short Length
    {
        get
        {
            int size = base.Length;
            foreach (var component in Components)
            {
                size += 4; // flags (Int16) + glyphIndex (Int16)
                size += 4; // translate/point-matching args, always word-sized (2 x Int16)
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
            return (short)size;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Mirrors the wire format read by <see cref="ReadData"/> exactly, including its limitations:
    /// point-matching arguments (<see cref="GlyfComponent.CompoundPoint"/>/
    /// <see cref="GlyfComponent.ComponentPoint"/>) and translation offsets are always written as
    /// 16-bit words (the <c>ARGS_ARE_WORDS</c> flag is not checked, matching <see cref="ReadData"/>
    /// not checking it either).
    /// </remarks>
    public override void WriteData(Writer data)
    {
        base.WriteData(data);
        foreach (var component in Components)
        {
            data.Write<Int16>((short)component.Flags);
            data.Write<Int16>(component.GlyphIndex);
            if (component.Flags.HasFlag(CompoundGlyfFlags.ArgsAreXY))
            {
                data.Write<Int16>((short)component.TranslateX);
                data.Write<Int16>((short)component.TranslateY);
            }
            else
            {
                data.Write<Int16>((short)component.CompoundPoint);
                data.Write<Int16>((short)component.ComponentPoint);
            }

            if (component.Flags.HasFlag(CompoundGlyfFlags.HasScale))
            {
                data.Write<Int16>((short)Math.Round(component.M11 * 16384f));
            }
            else if (component.Flags.HasFlag(CompoundGlyfFlags.HasXYScale))
            {
                data.Write<Int16>((short)Math.Round(component.M11 * 16384f));
                data.Write<Int16>((short)Math.Round(component.M22 * 16384f));
            }
            else if (component.Flags.HasFlag(CompoundGlyfFlags.HasTwoByTwo))
            {
                data.Write<Int16>((short)Math.Round(component.M11 * 16384f));
                data.Write<Int16>((short)Math.Round(component.M21 * 16384f));
                data.Write<Int16>((short)Math.Round(component.M12 * 16384f));
                data.Write<Int16>((short)Math.Round(component.M22 * 16384f));
            }
        }
        if (HasInstructionsFlag)
        {
            data.Write<UInt16>((ushort)Instructions.Length);
            foreach (byte b in Instructions)
            {
                data.WriteByte(b);
            }
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<IEnumerable<TTFPoint>> Contours
    {
        get
        {
            return Components.SelectMany(component =>
            {
                component.ComputeTransform();
                var glyph = GlyfTable.GetGlyph(component.GlyphIndex);
                return glyph?.Contours
                    .Select(contour => contour.Select(point => component.Transform(point)))
                    ?? Enumerable.Empty<IEnumerable<TTFPoint>>();
            });
        }
    }
}
