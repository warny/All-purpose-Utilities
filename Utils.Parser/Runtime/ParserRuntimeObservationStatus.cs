namespace Utils.Parser.Runtime;

/// <summary>
/// Defines normalized observation status values exposed by the runtime observation contract.
/// </summary>
public enum ParserRuntimeObservationStatus
{
    Active,
    Completed,
    Failed,
    Pruned,
    Unknown
}
