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
        public virtual void ComputeTransform()
        {
            const float limit = (33f / 65535f);
            AdjustX = Math.Max(Math.Abs(M11), Math.Abs(M21));
            if (Math.Abs(Math.Abs(M11) - Math.Abs(M12)) < limit)
            {
                AdjustX *= 2f;
            }
            AdjustY = Math.Max(Math.Abs(M12), Math.Abs(M22));
            if (Math.Abs(Math.Abs(M12) - Math.Abs(M22)) < limit)
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
        public TTFPoint Transform(float x, float y, bool onCurve)
            => new TTFPoint(
                M11 * x + M12 * y + AdjustX * TranslateX,
                M21 * x + M22 * y + AdjustY * TranslateY,
                onCurve
            );
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
