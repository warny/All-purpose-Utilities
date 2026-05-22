using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Minimal immutable context required to prepare scheduling inputs.
/// </summary>
/// <param name="ParseContext">Active parse context used to read current token and input position.</param>
/// <param name="StartPosition">Input position where scheduling starts.</param>
/// <param name="Precedence">Minimum precedence for candidate alternatives.</param>
/// <param name="CursorKind">Scheduler cursor kind identifying the orchestration location.</param>
/// <param name="CursorIndex">Scheduler cursor index used to disambiguate orchestration locations.</param>
/// <param name="CaseInsensitive">Whether token matching should use case-insensitive comparison.</param>
/// <param name="PrecedencePolicy">Policy determining whether an alternative is eligible for current precedence.</param>
internal sealed record SchedulingPreparationContext(
    ParseContext ParseContext,
    int StartPosition,
    int Precedence,
    string CursorKind,
    int CursorIndex,
    bool CaseInsensitive,
    Func<Alternative, bool> PrecedencePolicy);
