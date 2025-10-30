using System;
using System.Runtime.Serialization;

namespace Utils.Objects;

[Serializable]
internal class ArrayDimensionException : Exception
{
    public ArrayDimensionException() { }
    public ArrayDimensionException(string message) : base(message) { }
    public ArrayDimensionException(string message, Exception innerException) : base(message, innerException) { }
#pragma warning disable SYSLIB0051 // Le type ou le membre est obsolète
    protected ArrayDimensionException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#pragma warning restore SYSLIB0051 // Le type ou le membre est obsolète
}