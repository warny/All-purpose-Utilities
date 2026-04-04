using System.Collections.Generic;

namespace Utils.Parser.VisualStudio.Worker;

/// <summary>
/// Request sent by the extension process to classify a batch of tokens.
/// Must match the record in Utils.Parser.VisualStudio.Worker (JSON wire format).
/// </summary>
internal sealed record ClassifyRequest(
    int Id,
    string[] AssemblyPaths,
    string FileExtension,
    string[] Tokens);

/// <summary>
/// Response received from the worker process with per-token classification results.
/// Must match the record in Utils.Parser.VisualStudio.Worker (JSON wire format).
/// </summary>
internal sealed record ClassifyResponse(
    int Id,
    Dictionary<string, string?>? TokenClassifications,
    string? Error = null);
