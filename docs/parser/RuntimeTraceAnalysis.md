# Runtime Trace Analysis

This document defines a conservative, tooling-only analysis layer for runtime trace observations.

See also:

- [`RuntimeObservationAndExportContract.md`](./RuntimeObservationAndExportContract.md)
- [`DiagnosticsObservationCorrelation.md`](./DiagnosticsObservationCorrelation.md)
- [`RuntimeStateOwnership.md`](./RuntimeStateOwnership.md)

## Intent

Runtime trace analysis validates that external tooling can extract useful descriptive information from passive observations. Export identity can be reported as informational metadata only.

The analysis layer consumes only:

- `AlternativeRuntimeObservation` sequences.

The analysis layer produces only:

- summaries,
- distributions,
- deterministic descriptive comparisons.

## Explicit non-authority boundaries

Runtime trace analysis does **not** imply:

- replay,
- runtime ownership,
- parser authority,
- diagnostics authority,
- parser execution control,
- scheduling control,
- runtime object navigation.

## Current abstractions

The current tooling abstractions are:

- `RuntimeTraceSummary`: deterministic counts and distributions.
- `RuntimeTraceAnalyzer`: summary and comparison entry points.
- `RuntimeTraceComparison`: deterministic descriptive comparison values.

These abstractions are read-only and do not access runtime internals.

## Typical outputs

Examples of allowed descriptive outputs:

- total observation count,
- event-kind distribution,
- status distribution,
- rule-name distribution,
- alternative-index distribution,
- deterministic event count deltas,
- optional export identity indicators (`AreTextExportsIdentical`, `AreJsonExportsIdentical`) treated as informational only.

These outputs are diagnostic aids for tooling only.


## End-to-end usage examples (runtime → observation → export → analysis)

The examples below intentionally demonstrate consumption of **existing** APIs only.
They are illustrative tooling snippets, not a supported framework surface.

### Example A — Record observations during parsing

```csharp
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

var definition = Antlr4GrammarConverter.Parse("""
    grammar Sample;
    start : Number ('+' Number)* EOF ;
    Number : [0-9]+ ;
    WS : [ 	
]+ -> skip ;
    """);

var recorder = new RuntimeObservationRecorder();
var policy = ParserRuntimeFeaturePolicy.Default with
{
    RuntimeObserver = recorder
};

var parser = new ParserEngine(definition, policy);
var lexer = new LexerEngine(definition);
var tokens = lexer.Tokenize(new StringReader("1+2+3")).ToList();

var parseResult = parser.Parse(tokens);

// Immutable deterministic payloads emitted in scheduler order.
IReadOnlyList<AlternativeRuntimeObservation> observations = recorder.Observations;
```

### Example B — Produce deterministic exports from observations

```csharp
using Utils.Parser.Runtime;

IReadOnlyList<AlternativeRuntimeObservation> observations = recorder.Observations;

var textTrace = RuntimeObservationTextWriter.Write(observations);
var jsonTrace = RuntimeObservationJsonWriter.Write(observations);

File.WriteAllText("trace.txt", textTrace);
File.WriteAllText("trace.json", jsonTrace);
```

### Example C — Run deterministic analysis and compare traces

```csharp
using Utils.Parser.Runtime;

IReadOnlyList<AlternativeRuntimeObservation> baseline = baselineRecorder.Observations;
IReadOnlyList<AlternativeRuntimeObservation> candidate = candidateRecorder.Observations;

RuntimeTraceSummary baselineSummary = RuntimeTraceAnalyzer.Summarize(baseline);
RuntimeTraceSummary candidateSummary = RuntimeTraceAnalyzer.Summarize(candidate);
RuntimeTraceComparison comparison = RuntimeTraceAnalyzer.Compare(baseline, candidate);

Console.WriteLine($"Baseline events: {baselineSummary.TotalObservationCount}");
Console.WriteLine($"Candidate events: {candidateSummary.TotalObservationCount}");
Console.WriteLine($"Delta events: {comparison.TotalObservationCountDelta}");
Console.WriteLine($"Text export identical: {comparison.AreTextExportsIdentical}");
Console.WriteLine($"Json export identical: {comparison.AreJsonExportsIdentical}");
```

## Tooling boundary guidance

Keep runtime trace tooling strictly on the descriptive side:

- ✅ allowed: record observations, export deterministic text/JSON, summarize distributions, compare trace sets.
- ❌ not allowed: parser replay, scheduler manipulation, branch forcing, diagnostics ownership changes, parse-tree control.
- ❌ not exposed: runtime object graph navigation, execution handles, parser state mutation APIs.

Practical interpretation:

- Runtime remains the only execution authority.
- Observation remains passive.
- Export remains neutral formatting.
- Analysis remains read-only interpretation.
