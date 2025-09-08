using System;

namespace Utils.IO.Serialization;

/// <summary>
/// Marks a type for which reader and writer methods are generated at compile time.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public class GenerateReaderWriterAttribute : Attribute
{
}

