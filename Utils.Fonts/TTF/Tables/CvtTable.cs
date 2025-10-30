using System;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables
{
    /// <summary>
    /// The CVT (Control Value Table) contains a series of signed 16-bit integers that are used as control values
    /// during the hinting process. These values are used by the font program to adjust glyph outlines.
    /// </summary>
    [TTFTable(TableTypes.Tags.CVT)]
    public class CvtTable : TrueTypeTable
    {
        /// <summary>
        /// Gets or sets the control values.
        /// </summary>
        public short[] ControlValues { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CvtTable"/> class.
        /// </summary>
        protected internal CvtTable() : base(TableTypes.CVT) { }

        /// <inheritdoc/>
        public override void ReadData(Reader data)
        {
            int count = (int)(data.BytesLeft >> 1);
            ControlValues = new short[count];
            for (int i = 0; i < count; i++)
            {
                ControlValues[i] = data.Read<Int16>();
            }
        }

        /// <inheritdoc/>
        public override void WriteData(Writer data)
        {
            if (ControlValues != null)
            {
                foreach (var value in ControlValues)
                {
                    data.Write<Int16>(value);
                }
            }
        }

        /// <inheritdoc/>
        public override int Length => ControlValues != null ? ControlValues.Length * 2 : 0;

        /// <inheritdoc/>
        public override string ToString() => $"CVT Table: {Length} bytes";
    }
}
