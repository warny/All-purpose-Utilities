# Product package graph

The graph is computed from evaluated `ProjectReference` metadata. References are classified as NuGet runtime dependencies, private build dependencies, or embedded analyzer dependencies. A reference from a product project to an excluded project is invalid.

The current derived publication order is:

1. `omy.Utils`
2. `omy.Utils.XML`
3. `omy.Utils.Reflection`
4. `omy.Utils.Collections`
5. `omy.Utils.OData.Generators`
6. `omy.Utils.IO.Serialization.Generators`
7. `omy.Utils.DependencyInjection.Generators`
8. `omy.Utils.Parser.Source`
9. `omy.Utils.Parser.Antlr4.Common`
10. `omy.Utils.IO`
11. `omy.Utils.Net`
12. `omy.Utils.Geography`
13. `omy.Utils.Mathematics`
14. `omy.Utils.OData`
15. `omy.Utils.NumberToString`
16. `omy.Utils.VirtualMachine`
17. `omy.Utils.DependencyInjection`
18. `omy.Utils.Parser.Diagnostics`
19. `omy.Utils.Fonts`
20. `omy.Utils.Parser`
21. `omy.Utils.Parser.Generators`
22. `omy.Utils.Data`
23. `omy.Utils.Imaging`
24. `omy.Utils.Parser.Expressions`

Independent nodes may have more than one valid ordering. The generated report, not this explanatory snapshot, controls packaging and future publication.
