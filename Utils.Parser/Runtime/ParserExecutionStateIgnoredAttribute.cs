namespace Utils.Parser.Runtime;

/// <summary>
/// Marks an instance field as excluded from parser execution-state hashing and copying.
/// Apply to infrastructure fields that are not part of the logical parser execution state,
/// such as runtime policy objects or frame managers that must not be snapshotted or hashed.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class ParserExecutionStateIgnoredAttribute : Attribute;
