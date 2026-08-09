using System.Collections.Immutable;

namespace Utils.Parser.Antlr4.Common.Diagnostics;

/// <summary>Holds neutral validation facts derived from an ANTLR4 prequel model.</summary>
public sealed record Antlr4PrequelValidationResult(IReadOnlyList<Antlr4PrequelDiagnostic> Diagnostics)
{
    private IReadOnlyList<Antlr4PrequelDiagnostic> _diagnostics = Diagnostics.ToImmutableArray();

    /// <summary>Gets an immutable snapshot of diagnostics in deterministic emission order.</summary>
    public IReadOnlyList<Antlr4PrequelDiagnostic> Diagnostics
    {
        get => _diagnostics;
        init => _diagnostics = value.ToImmutableArray();
    }
}
