using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables
{
	/// <summary>
	/// The FPGM (Font Program) table contains a set of instructions (bytecode) that are executed
	/// once when the font is loaded. These instructions establish routines used for hinting glyphs.
	/// </summary>
	[TTFTable(TableTypes.Tags.FPGM)]
	public class FpgmTable : TrueTypeTable
	{
		/// <summary>
		/// Gets or sets the raw bytecode instructions for the font program.
		/// </summary>
		public byte[] Instructions { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="FpgmTable"/> class.
		/// </summary>
		protected internal FpgmTable() : base(TableTypes.FPGM) { }

		/// <inheritdoc/>
		public override void ReadData(Reader data)
		{
			Instructions = data.ReadBytes((int)data.BytesLeft);
		}

		/// <inheritdoc/>
		public override void WriteData(Writer data)
		{
			data.Write<byte[]>(Instructions);
		}

		/// <inheritdoc/>
		public override int Length => Instructions?.Length ?? 0;

		/// <inheritdoc/>
		public override string ToString() => $"Fpgm Table: {Length} bytes";
	}
}
