using System;
using System.Collections.Generic;
using Utils.VirtualMachine;
using Utils.Fonts.TTF.Tables.Glyf;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Hinting
{
    /// <summary>
    /// Specialized execution context for TrueType hinting.
    /// This context holds the glyph points to be hinted as well as additional registers if needed.
    /// </summary>
    public class TtfHintingContext : DefaultContext
    {
        /// <summary>
        /// Gets or sets the array of glyph points to be processed by the hinting instructions.
        /// </summary>
        public TTFPoint[] GlyphPoints { get; set; }

        /// <summary>
        /// Gets or sets the control values (from the CVT table) used during hinting.
        /// </summary>
        public short[] ControlValues { get; set; }

        /// <summary>
        /// General-purpose registers (exemple : 16 registres).
        /// </summary>
        public int[] Registers { get; } = new int[16];

        /// <summary>
        /// Initializes a new instance of the <see cref="TtfHintingContext"/> class with the given data.
        /// </summary>
        /// <param name="data">The byte array containing the instructions to execute.</param>
        public TtfHintingContext(byte[] data) : base(data) { }
    }

    /// <summary>
    /// A simple virtual processor for TrueType hinting.
    /// This processor interprets a simplified set of hinting instructions.
    /// </summary>
    public class TtfHintingProcessor : VirtualProcessor<TtfHintingContext>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TtfHintingProcessor"/> class.
        /// </summary>
        /// <param name="littleEndian">Indicates if the byte ordering is little-endian. Defaults to true.</param>
        public TtfHintingProcessor(bool littleEndian = true) : base(littleEndian) { }

        /// <summary>
        /// Moves a specific glyph point vertically by a given offset.
        /// Instruction format: [opcode (1 byte), point index (2 bytes), delta (2 bytes)]
        /// </summary>
        [Instruction("MOVE_POINT", 0xA0)]
        private void MovePoint(TtfHintingContext context)
        {
            // Read a 16-bit index and a 16-bit delta (en font units)
            int pointIndex = ReadInt16(context);
            int delta = ReadInt16(context);
            if (pointIndex >= 0 && pointIndex < context.GlyphPoints.Length)
            {
                TTFPoint pt = context.GlyphPoints[pointIndex];
                // On modifie la coordonnée Y (par exemple) en ajoutant le delta.
                context.GlyphPoints[pointIndex] = new TTFPoint(pt.X, (short)(pt.Y + delta), pt.OnCurve);
            }
        }

        /// <summary>
        /// Scales l'ensemble des points du glyphe par un facteur.
        /// Instruction format: [opcode (1 byte), scale factor (2 bytes, en format fixe 2.14)]
        /// </summary>
        [Instruction("SCALE_POINTS", 0xB0)]
        private void ScalePoints(TtfHintingContext context)
        {
            // Lecture d'un entier 16 bits représentant le facteur d'échelle en format fixe (2.14)
            int fixedScale = ReadInt16(context);
            double scale = fixedScale / 16384.0;
            for (int i = 0; i < context.GlyphPoints.Length; i++)
            {
                TTFPoint pt = context.GlyphPoints[i];
                // Appliquer l'échelle aux coordonnées X et Y.
                context.GlyphPoints[i] = new TTFPoint((short)(pt.X * scale), (short)(pt.Y * scale), pt.OnCurve);
            }
        }

        /// <summary>
        /// A no-operation instruction, qui ne fait rien.
        /// </summary>
        [Instruction("NOP", 0x00)]
        private void Nop(TtfHintingContext context)
        {
            // Ne rien faire.
        }

        /// <summary>
        /// Executes the hinting instructions contained in the provided context.
        /// The processor reads instructions byte-by-byte and invokes corresponding handlers.
        /// </summary>
        /// <param name="context">The hinting context, including the glyph points to be adjusted.</param>
        public void ExecuteHinting(TtfHintingContext context)
        {
            Execute(context);
        }
    }
}
