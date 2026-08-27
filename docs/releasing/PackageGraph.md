# Product package graph

The graph is computed from evaluated `ProjectReference` metadata. References are classified as
NuGet runtime dependencies, private build dependencies, or embedded analyzer dependencies. A
reference from a product project to an excluded project is invalid.

## Getting the authoritative publication order

The publication order is derived, not hand-maintained: `eng/analyze-package-graph.ps1` evaluates
every manifested project's `ProjectReference` items through MSBuild, validates that the resulting
dependency graph is acyclic, and writes the derived order to
`artifacts/reports/package-publication-order.txt` (plus the full edge list in
`artifacts/reports/package-graph.json`). Independent nodes (packages with no dependency relationship
between them) may have more than one valid ordering; both files reflect one specific valid ordering
for the current source tree, not a stable ranking.

A hand-written snapshot of that order used to live in this document. It went stale every time a
package joined or left the train (most recently when `omy.Utils.Expressions.CSyntax`/
`omy.Utils.Expressions.VBSyntax` joined, and when `omy.Utils.Collections` left - see
[provisional versioning](ProvisionalVersioning.md)), so it has been removed rather than
re-synchronized again. To see the current order, run the graph analysis and read its report instead
of trusting prose here:

```powershell
./eng/analyze-package-graph.ps1 -Configuration Release
Get-Content artifacts/reports/package-publication-order.txt
```

`omy.Utils.Collections` is not part of this graph at all: it is excluded from the product train (see
[provisional versioning](ProvisionalVersioning.md)) and is packed/published independently.
