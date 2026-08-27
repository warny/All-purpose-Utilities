# Product package graph

The graph is computed from evaluated `ProjectReference` metadata. References are classified as NuGet runtime dependencies, private build dependencies, or embedded analyzer dependencies. A reference from a product project to an excluded project is invalid.

The current derived publication order is:

1. `omy.Utils`
2. `omy.Utils.XML`
3. `omy.Utils.Reflection`
4. `omy.Utils.OData.Generators`
5. `omy.Utils.IO.Serialization.Generators`
6. `omy.Utils.DependencyInjection.Generators`
7. `omy.Utils.Parser.Source`
8. `omy.Utils.Parser.Antlr4.Common`
9. `omy.Utils.IO`
10. `omy.Utils.Net`
11. `omy.Utils.Geography`
12. `omy.Utils.Mathematics`
13. `omy.Utils.OData`
14. `omy.Utils.NumberToString`
15. `omy.Utils.VirtualMachine`
16. `omy.Utils.DependencyInjection`
17. `omy.Utils.Parser.Diagnostics`
18. `omy.Utils.Fonts`
19. `omy.Utils.Parser`
20. `omy.Utils.Parser.Generators`
21. `omy.Utils.Data`
22. `omy.Utils.Imaging`
23. `omy.Utils.Parser.Expressions`

`omy.Utils.Collections` is not part of this graph: it is excluded from the product train (see
[provisional versioning](ProvisionalVersioning.md)) and is packed/published independently.

Independent nodes may have more than one valid ordering. The generated report, not this explanatory snapshot, controls packaging and future publication. This snapshot may also lag behind newly added train packages (for example `omy.Utils.Expressions.CSyntax`/`omy.Utils.Expressions.VBSyntax`); the generated `artifacts/reports/package-publication-order.txt` remains the authoritative source.
