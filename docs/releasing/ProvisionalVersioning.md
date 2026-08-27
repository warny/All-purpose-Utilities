# Provisional versioning

Every package in `eng/product-train-manifest.json`'s `packages` array follows the synchronized
`omy.Utils` product train: it declares `<Version>$(ProductTrainVersion)</Version>` and is
validated, packed, and published at exactly `ProductTrainVersion` (`Directory.Build.props`,
currently `2.0.0-rc.1`). There is no per-package exception inside `packages` - every release gate
(`eng/validate-product-train.ps1` and everything downstream of it) enforces the single rule
`manifest package version == ProductTrainVersion` for the whole array.

A small number of components are not yet mature enough to make that train-wide commitment, but
still need to be packed and published as real artifacts (a NuGet package, or - for the Visual
Studio extension - a VSIX). Those components are simply **not** in `packages`: they live in the
manifest's `exclusions` array instead, each with its own independent, explicit version and a
`reason` explaining why it is out of the train.

## Current provisional components

| Component | Manifest location | Version | Reason |
|---|---|---|---|
| `omy.Utils.Collections` (NuGet package) | `exclusions[]`, `"classification": "provisional-package"` | `0.0.1` (literal `<Version>` in `Utils.Collections.csproj`) | Only contains `SkipList`/`SkipListDictionary` today; not yet considered mature enough to join the 2.0 product train as a stable component. |
| `Utils.Parser.VisualStudio` (VSIX, not a NuGet package - see below) | `exclusions[]`, `"classification": "vsix"` | `0.0.1` | The VSIX version format has no representation for a SemVer prerelease suffix like `2.0.0-rc.1`; see [VSIX versioning and release](VisualStudioExtension.md). |

Both are excluded from the train for different reasons (maturity vs. VSIX versioning format), but
the mechanics are the same in spirit: an explicit, independent version declared where each
packaging format expects it - a literal MSBuild `<Version>` in `Utils.Collections.csproj` for the
NuGet package, and `<Identity Version="0.0.1">` in `source.extension.vsixmanifest` for the VSIX
(there is no MSBuild `<Version>` involved for the VSIX). Either way, packed/published independently
of the train, and never part of the train's canonical package set, candidate manifest, publication
order, or all-or-none publish preflight (`eng/publish-product-train.ps1` only ever iterates the
manifest's `packages` array).

A `"classification": "provisional-package"` exclusion skips every train-wide package gate (canonical
packaging, `inspect-packages.ps1`, `validate-sourcelink.ps1`, packaged-consumer acceptance, API
compatibility, reproducibility), since none of them iterate anything outside `packages[]`.
`eng/test-provisional-package.ps1` is a lighter, package-scoped substitute: it packs the real
project, inspects the resulting `.nupkg`/`.snupkg` (version, README, license, icon), validates
SourceLink, and restores/builds/runs a minimal real consumer against the packed package. Run it
before publishing any `provisional-package` exclusion, and add a case for a new one if it is ever
added - the script fails closed (throws) rather than silently skipping functional verification for
a provisional package it does not yet know how to exercise.

`omy.Utils.Collections` will jump directly to the product train's *then-current* version (for
example `2.0.0` or later) once it is considered mature enough - there is no obligation to publish
an intermediate `1.x`, and no obligation to keep incrementing `0.0.x` once the train itself is
stable. Rejoining the train is a two-line change: remove the literal `<Version>`, restore
`<Version>$(ProductTrainVersion)</Version>`, and move the manifest entry from `exclusions` to
`packages`. The VSIX's exclusion is permanent and structural rather than a maturity gate (see
below) - it does not have an equivalent "rejoin the train" path.

## The VSIX is not a NuGet package

The VSIX is deliberately **not** modeled in the manifest's `packages` array: that array (and the
release gates that iterate it - packing, public API comparison, SourceLink validation,
packaged-consumer acceptance) is specifically about the NuGet product train. The VSIX's own
independent version policy is documented and enforced separately
(`docs/releasing/VisualStudioExtension.md`, `eng/test-vsix-package.ps1`).

## Solution logo

All publishable NuGet packages and the VSIX reuse a single source file,
[`res/AllPurposeUtilities_logo.png`](../../res/AllPurposeUtilities_logo.png), referenced by the
`SolutionLogoPath` MSBuild property in `Directory.Build.props`. No project keeps its own copy:

- **NuGet packages:** `Directory.Build.props` sets `PackageIcon` and packs `SolutionLogoPath` at
  the archive root for every project where `IsPackable != 'false'`, with no per-project
  configuration needed.
- **VSIX:** `Utils.Parser.VisualStudio.csproj` links `SolutionLogoPath` into the VSIX at
  `Resources\AllPurposeUtilities_logo.png`, matching the `<Icon>` element declared in
  `source.extension.vsixmanifest`.

Only the packaged copies inside `.nupkg`/`.vsix` archives are packaging artifacts; the repository
itself keeps exactly one physical copy of the image.

## Versioned API documentation for a provisional package

`.github/workflows/docs.yml` builds and deploys **one** DocFX site covering every project in the
repository, labeled with a single version derived from the product train's version (see
`docs/releasing.md`) - it does not generate a separate site per package version. A provisional
package's own README should therefore keep linking to the product-train's version label (currently
`v2.0.0-rc.1`), not to its own package version (`v0.0.1` would be a dead link, since that folder is
never generated), and should say so explicitly so the discrepancy reads as intentional rather than
a mistake. See `Utils.Collections/README.md` for the wording used.

## Package count

`docs/releasing/ProductTrain.md` and `docs/releasing.md` describe the *set* of manifested packages
rather than hard-coding a count anywhere that could drift; if you need the current number, count
`eng/product-train-manifest.json`'s `packages` array (`(Get-Content eng/product-train-manifest.json -Raw | ConvertFrom-Json).packages.Count`)
rather than trusting a number written in prose - it changes whenever a package joins or leaves the
train (for example, `omy.Utils.Expressions.CSyntax`/`omy.Utils.Expressions.VBSyntax` joining as
product-train packages, or `omy.Utils.Collections` moving to `exclusions` as a provisional
package).
