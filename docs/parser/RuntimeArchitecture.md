# Runtime Architecture

## Pipeline

The current parser runtime pipeline is:

Grammar  
→ Preparation  
→ Scheduler  
→ Execution  
→ Observation  
→ Export  
→ Analysis

## Ownership boundaries

- **Preparation** owns metadata production (structural descriptors, lookahead probes, shared-prefix candidates, continuation descriptors).
- **Scheduler** owns deterministic orchestration only and transports prepared metadata.
- **Execution** owns parse decisions through `ParserEngine`.
- **Observation** owns passive runtime observation payloads.
- **Export** owns serialization of observations.
- **Analysis** owns post-runtime tooling and interpretation.

## Authority model

- Metadata is descriptive only.
- Scheduler is not a metadata producer.
- Continuation metadata is not replay/resume authority.
- Parse acceptance, final diagnostics, and parse-tree outcomes remain owned by `ParserEngine`.
