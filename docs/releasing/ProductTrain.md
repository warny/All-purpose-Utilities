# The synchronized `omy.Utils` product train

`2.0.0-rc.1` is one release candidate for all 24 publishable libraries and analyzers. `Directory.Build.props` owns the only version property, while `eng/product-train-manifest.json` explicitly owns package selection, classification, frameworks, platforms, acceptance profiles, published API baselines, and package-specific assets.

The train contains Core; IO; XML; Net; Data; Fonts; Imaging; Geography; Reflection; Mathematics; Collections; OData; NumberToString; VirtualMachine; DependencyInjection; four source generators; and the six parser packages. Demonstrations, tests, syntax implementation projects, the VSIX, its worker, and generated acceptance consumers are explicitly excluded.

Collections is a manifested member of the train (packed, inspected, API-compared like every other package) but is marked `"versionPolicy": "provisional"` and ships at its own literal `0.0.1` instead of `ProductTrainVersion` - see [provisional versioning](ProvisionalVersioning.md).

The manifest is authoritative, but publication order is not handwritten. `eng/analyze-package-graph.ps1` evaluates `ProjectReference` items through MSBuild, validates an acyclic graph, and writes the derived order under `artifacts/reports`.

All candidates are built and packed before inspection. None of the quality-gate scripts publishes a package. The publication workflow only downloads already validated archives, verifies their hashes and remote all-or-none state, and emits a dry-run plan.

## Published baselines

NuGet discovery selects 1.2.1 for Core, IO, XML, Net, Data, Fonts, Imaging, Geography, Reflection, Mathematics, DependencyInjection, and the IO/DI generators; 0.0.1 for OData and its generator; and 0.1.0 for VirtualMachine. Collections, NumberToString, and all parser packages were not found on NuGet and therefore establish first candidate baselines. The API gate queries NuGet again and fails rather than silently changing a pinned published baseline.
