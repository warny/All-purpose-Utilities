# Utils.NumberToString — Composite finalization follow-up

Deferred during the repository-wide TODO arbitration on 2026-08-16.

## Context

Historical audit item NTS-03 raised a possible issue where composite number rendering could apply the same finalization step at more than one layer. Because recent NumberToString changes may already have removed or altered that behavior, this must be proved before production code is refactored.

## Contract to verify

A public conversion result should undergo global finalization exactly once.

Internal composition steps may perform transformations that are specific to their local linguistic rule, but they must not accidentally re-apply the global finalizer to already finalized fragments.

## Investigation plan

1. Add a deliberately non-idempotent finalizer for tests, for example a transform that wraps the result in markers.
2. Build a regression matrix covering at least:
   - units and tens;
   - hundreds plus remainder;
   - thousands and larger grouped numbers;
   - negative values;
   - decimal values;
   - grammatical/variant paths;
   - units/connectors when they use the same rendering pipeline;
   - nested composite conversions.
3. Verify that the public result shows exactly one application of the global finalizer.
4. If all cases pass, close NTS-03 without changing production code.
5. If a failure is reproduced, fix only the path responsible for duplicate finalization and add the failing case as a permanent regression test.

## Architectural guidance if a defect is reproduced

Prefer a separation where internal conversion/composition returns an unfinalized intermediate result and the public conversion boundary applies the global finalizer once.

Do not perform a broad refactor unless the regression matrix demonstrates that the existing layering cannot satisfy this contract locally.

## Priority

Deferred / proof-first. This is not a blocking 2.0 correctness fix until a reproducible case exists.
