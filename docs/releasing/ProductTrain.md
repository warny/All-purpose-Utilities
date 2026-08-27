# The synchronized `omy.Utils` product train

`2.0.0-rc.1` is one release candidate for the publishable libraries and analyzers manifested in `eng/product-train-manifest.json` (see that file's `packages` array for the exact, current count - it changes as packages join or leave the train; do not hard-code a number here). `Directory.Build.props` owns the only product-train-wide version property, while `eng/product-train-manifest.json` explicitly owns package selection, classification, frameworks, platforms, acceptance profiles, published API baselines, and package-specific assets.

The train contains Core; IO; XML; Net; Data; Fonts; Imaging; Geography; Reflection; Mathematics; Expressions.CSyntax; Expressions.VBSyntax; OData; NumberToString; VirtualMachine; DependencyInjection; three non-parser source generators (OData.Generators, IO.Serialization.Generators, DependencyInjection.Generators); and the six-package parser ecosystem (Parser.Source, Parser.Diagnostics, Parser.Antlr4.Common, Parser, Parser.Expressions, and Parser.Generators - itself a fourth source generator, embedded in the "six" count rather than double-counted with the three above). Demonstrations, tests, the VSIX, its worker, generated acceptance consumers, and `omy.Utils.Collections` are explicitly excluded.

`omy.Utils.Collections` is **not** a manifested member of the train: it is listed in the manifest's `exclusions` array (`"classification": "provisional-package"`) and ships independently at its own literal `0.0.1` instead of `ProductTrainVersion` - see [provisional versioning](ProvisionalVersioning.md).

The manifest is authoritative, but publication order is not handwritten. `eng/analyze-package-graph.ps1` evaluates `ProjectReference` items through MSBuild, validates an acyclic graph, and writes the derived order under `artifacts/reports`.

All candidates are built and packed before inspection. None of the quality-gate scripts publishes a package. The publication workflow only downloads already validated archives, verifies their hashes and remote all-or-none state, and emits a dry-run plan.

## Published baselines

NuGet discovery selects 1.2.1 for Core, IO, XML, Net, Data, Fonts, Imaging, Geography, Reflection, Mathematics, DependencyInjection, and the IO/DI generators; 0.0.1 for OData and its generator; and 0.1.0 for VirtualMachine. NumberToString, all parser packages, and the newly-published Expressions.CSyntax/Expressions.VBSyntax were not found on NuGet and therefore establish first candidate baselines. The API gate queries NuGet again and fails rather than silently changing a pinned published baseline. `omy.Utils.Collections` is outside the train's `packages` array entirely, so this gate does not run against it at all.
