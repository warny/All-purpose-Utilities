using System;
using System.Runtime.Serialization;

namespace Utils.Objects;

[Serializable]
internal class ArrayDimensionException : Exception
{
    public ArrayDimensionException() { }
    public ArrayDimensionException(string message) : base(message) { }
    public ArrayDimensionException(string message, Exception innerException) : base(message, innerException) { }
    protected ArrayDimensionException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}