using System;
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
		public CompoundGlyfFlags flags;

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
		/// Transformation coefficient a (default is 1).
		/// </summary>
		public float a { get; internal set; } = 1f;

		/// <summary>
		/// Transformation coefficient b (default is 0).
		/// </summary>
		public float b { get; internal set; } = 0f;

		/// <summary>
		/// Transformation coefficient c (default is 0).
		/// </summary>
		public float c { get; internal set; } = 0f;

		/// <summary>
		/// Transformation coefficient d (default is 1).
		/// </summary>
		public float d { get; internal set; } = 1f;

		/// <summary>
		/// Horizontal translation (default is 0).
		/// </summary>
		public float e { get; internal set; } = 0f;

		/// <summary>
		/// Vertical translation (default is 0).
		/// </summary>
		public float f { get; internal set; } = 0f;

		/// <summary>
		/// Computed horizontal adjustment factor.
		/// </summary>
		public float m { get; private set; } = 0f;

		/// <summary>
		/// Computed vertical adjustment factor.
		/// </summary>
		public float n { get; private set; } = 0f;

		/// <summary>
		/// Computes the transformation adjustment factors based on the current matrix values.
		/// </summary>
		public virtual void ComputeTransform()
		{
			const float limit = (33f / 65535f);
			m = Math.Max(Math.Abs(a), Math.Abs(b));
			if (Math.Abs(Math.Abs(a) - Math.Abs(c)) < limit)
			{
				m *= 2f;
			}
			n = Math.Max(Math.Abs(c), Math.Abs(d));
			if (Math.Abs(Math.Abs(c) - Math.Abs(d)) < limit)
			{
				n *= 2f;
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
				a * x + c * y + m * e,
				b * x + d * y + n * f,
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
	public override void ReadData(NewReader data)
	{
		List<GlyfComponent> comps = new List<GlyfComponent>();
		bool hasInstructions = false;
		GlyfComponent current;
		do
		{
			current = new GlyfComponent();
			current.flags = (CompoundGlyfFlags)data.ReadInt16(true);
			current.GlyphIndex = data.ReadInt16(true);
			if (current.flags.HasFlag(CompoundGlyfFlags.ArgsAreXY))
			{
				current.e = data.ReadInt16(true);
				current.f = data.ReadInt16(true);
			}
			else
			{
				current.CompoundPoint = data.ReadInt16(true);
				current.ComponentPoint = data.ReadInt16(true);
			}

			if (current.flags.HasFlag(CompoundGlyfFlags.HasScale))
			{
				current.a = data.ReadInt16(true) / 16384f;
				current.d = current.a;
			}
			else if (current.flags.HasFlag(CompoundGlyfFlags.HasXYScale))
			{
				current.a = data.ReadInt16(true) / 16384f;
				current.d = data.ReadInt16(true) / 16384f;
			}
			else if (current.flags.HasFlag(CompoundGlyfFlags.HasTwoByTwo))
			{
				current.a = data.ReadInt16(true) / 16384f;
				current.b = data.ReadInt16(true) / 16384f;
				current.c = data.ReadInt16(true) / 16384f;
				current.d = data.ReadInt16(true) / 16384f;
			}
			if (current.flags.HasFlag(CompoundGlyfFlags.HasInstructions))
			{
				hasInstructions = true;
			}
			comps.Add(current);
		}
		while ((current.flags & CompoundGlyfFlags.MoreComponents) != 0);
		Components = comps.ToArray();
		byte[] instructions;
		if (hasInstructions)
		{
			int instructionsCount = data.ReadUInt16(true);
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
		get {
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
