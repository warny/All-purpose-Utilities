# Continuation Metadata

## Purpose

Continuation metadata provides a deterministic, structural description of potential future parser paths.

**Continuation metadata is descriptive only.**

**Continuation metadata does not imply resumability.**

## Ownership and lifecycle

The current ownership flow is:

Scheduler preparation  
→ continuation metadata production  
→ runtime transport

Continuation metadata is produced during scheduling preparation and transported during execution.  
The scheduler does not gain continuation execution authority.

## Descriptor model

Current continuation descriptors carry only structural metadata:

- origin alternative (`AlternativeIndex`);
- continuation category (`ParserContinuationCategory`);
- continuation position (`SequencePosition`);
- optional shallow expected token names.

They do not carry parser frames, callbacks, parse results, or execution delegates.

## Conservative categories

Current categories are intentionally conservative:

- `Terminal`;
- `Sequential`;
- `SharedPrefixCandidate`;
- `Deferred`.

These categories are deterministic labels for metadata inspection and testing.
They do not imply replay, resume, execute, or schedule semantics.

## Distinctions

| Concept | Meaning |
| --- | --- |
| LookAhead | Observation |
| SharedPrefix | Structural grouping |
| Continuation | Structural future path |
| Alternative | Execution candidate |

## Limitations

- Continuation metadata is non-authoritative.
- Continuation metadata does not grant runtime control.
- Continuation metadata can be discarded without changing parse acceptance authority.
- Under-detection is preferred over over-classification.
