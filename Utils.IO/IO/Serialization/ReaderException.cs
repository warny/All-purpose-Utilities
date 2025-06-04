using System;

namespace Utils.IO.Serialization;

[Serializable]
internal class ReaderException : Exception
{
	public ReaderException()
	{
	}

	public ReaderException(string message) : base(message)
	{
	}

	public ReaderException(string message, Exception innerException) : base(message, innerException)
	{
	}
}