using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.IO.Serialization
{
	public interface IObjectReader
	{
		Type[] Types { get; }
		bool Read(Reader reader, out object result);
	}

	public interface IObjectWriter
	{
		Type[] Types { get; }
		bool Write(Writer writer, object obj);
	}
}
