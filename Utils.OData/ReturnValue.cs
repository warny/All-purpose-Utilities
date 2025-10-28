using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.OData;
public class ReturnValue<T> : Objects.ReturnValue<T, ErrorReturnValue>
{
	public ReturnValue(T value) : base(value) { }

	public ReturnValue(int code, string message) : base(new ErrorReturnValue(code, message)) { }

	public ReturnValue(ErrorReturnValue error) : base(error) { }

	/// <summary>
	/// Creates a successful <see cref="ReturnValue{T, E}"/> from a value.
	/// </summary>
	/// <param name="value">The value to wrap.</param>
		public static implicit operator ReturnValue<T>(T value) => new(value);

}


public record ErrorReturnValue(int code, string message);