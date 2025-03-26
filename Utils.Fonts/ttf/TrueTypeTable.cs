using Utils.IO.Serialization;

namespace Utils.Fonts.TTF;

/// <summary>
/// Represents the base class for a TrueType table.
/// </summary>
/// <remarks>
/// See <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/"/> for more details.
/// </remarks>
public class TrueTypeTable
{
	/// <summary>
	/// Gets the tag that identifies this table.
	/// </summary>
	public Tag Tag { get; }

	/// <summary>
	/// Gets or sets the <see cref="TrueTypeFont"/> that owns this table.
	/// </summary>
	public virtual TrueTypeFont TrueTypeFont { get; protected internal set; }

	// Stores the raw data of the table.
	private byte[] data;

	/// <summary>
	/// Initializes a new instance of the <see cref="TrueTypeTable"/> class with the specified tag.
	/// </summary>
	/// <param name="i">The tag that identifies this table.</param>
	protected internal TrueTypeTable(Tag i)
	{
		Tag = i;
	}

	/// <summary>
	/// Reads the table data from the specified reader.
	/// </summary>
	/// <param name="data">The reader from which to read the table data.</param>
	public virtual void ReadData(Reader data)
	{
		this.data = data.ReadBytes((int)data.BytesLeft);
	}

	/// <summary>
	/// Writes the table data to the specified writer.
	/// </summary>
	/// <param name="data">The writer to which the table data is written.</param>
	public virtual void WriteData(Writer data)
	{
		data.WriteBytes(this.data);
	}

	/// <summary>
	/// Gets the length, in bytes, of the table data.
	/// </summary>
	public virtual int Length => data.Length;

	/// <summary>
	/// Returns a string representation of the table.
	/// </summary>
	/// <returns>A string that includes the table tag and whether its data is set.</returns>
	public override string ToString()
	{
		return $"    {Tag} Table.  Data is: {(data == null ? "not " : "")}set";
	}
}
