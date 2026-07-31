using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.IO.Serialization;

/// <summary>Describes a defect found while validating a serialization contract.</summary>
public sealed record SerializationContractDiagnostic(string Code, string Message);

/// <summary>Represents one or more defects in an attributed serialization contract.</summary>
public sealed class SerializationContractException : InvalidOperationException
{
    /// <summary>Gets the type whose contract is invalid.</summary>
    public Type ContractType { get; }

    /// <summary>Gets every diagnostic discovered before delegate generation.</summary>
    public IReadOnlyList<SerializationContractDiagnostic> Diagnostics { get; }

    /// <summary>Initializes an aggregated contract exception.</summary>
    public SerializationContractException(Type contractType, IEnumerable<SerializationContractDiagnostic> diagnostics)
        : base(CreateMessage(contractType, diagnostics))
    {
        ContractType = contractType;
        Diagnostics = diagnostics.ToArray();
    }

    /// <summary>Builds the stable, human-readable aggregate message.</summary>
    private static string CreateMessage(Type type, IEnumerable<SerializationContractDiagnostic> diagnostics) =>
        $"Cannot build serialization contract for {type.FullName ?? type.Name}:{Environment.NewLine}" +
        string.Join(Environment.NewLine, diagnostics.Select(d => $"- [{d.Code}] {d.Message}"));
}
