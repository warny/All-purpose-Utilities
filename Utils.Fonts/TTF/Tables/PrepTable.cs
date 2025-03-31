using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables
{
	/// <summary>
	/// The PREP (Control Value Program) table contains instructions (bytecode) that are executed
	/// before rendering any glyph. These instructions typically perform scaling and other pre-processing
	/// needed for proper hinting.
	/// </summary>
	[TTFTable(TableTypes.Tags.PREP)]
	public class PrepTable : TrueTypeTable
	{
		/// <summary>
		/// Gets or sets the raw bytecode instructions for the control value program.
		/// </summary>
		public byte[] Instructions { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PrepTable"/> class.
		/// </summary>
		protected internal PrepTable() : base(TableTypes.PREP) { }

		/// <inheritdoc/>
		public override void ReadData(Reader data)
		{
			Instructions = data.ReadBytes((int)data.BytesLeft);
		}

		/// <inheritdoc/>
		public override void WriteData(Writer data)
		{
			data.WriteBytes(Instructions);
		}

		/// <inheritdoc/>
		public override int Length => Instructions?.Length ?? 0;

		/// <inheritdoc/>
		public override string ToString() => $"Prep Table: {Length} bytes";
	}
}
