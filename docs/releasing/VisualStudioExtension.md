# Visual Studio extension (VSIX) versioning and release

`Utils.Parser.VisualStudio` packages as a VSIX, not a NuGet package. It is explicitly excluded from
the synchronized `omy.Utils` product train (`eng/product-train-manifest.json`, `exclusions` section,
`classification: "vsix"`) because the VSIX Marketplace version format cannot carry a SemVer
prerelease suffix such as `2.0.0-rc.1`.

## Why the VSIX version differs from `ProductTrainVersion`

`Directory.Build.props` declares `<ProductTrainVersion>2.0.0-rc.1</ProductTrainVersion>` for the 24
manifested NuGet packages. The VSIX cannot use this value directly:

- the Marketplace/`VSIXVersion` format is a plain `Major.Minor.Build[.Revision]` with no prerelease
  label, so `2.0.0-rc.1` has no faithful representation, and
- inventing a lossy encoding (for example `2.0.0.1` for `rc.1`) would risk colliding with, or
  sorting incorrectly against, the eventual stable `2.0.0`.

Instead the VSIX follows an independent, provisional version series while the product train is a
2.0.0 prerelease:

| Product train (`ProductTrainVersion`) | VSIX version |
|---|---|
| `2.0.0-rc.1`, `2.0.0-rc.2`, ... (any 2.0.0 prerelease) | `0.0.1`, `0.0.2`, ... (provisional series) |
| `2.0.0` (first stable) | `2.0.0` |
| `2.0.1`, `2.1.0`, ... (later stable trains) | tracks the product train version |

The `0.0.x` series communicates "provisional, pre-2.0, not the final identity" to anyone who
installs it from the Marketplace before the 2.0.0 stable release. Once the product train reaches
`2.0.0`, the VSIX jumps directly to `2.0.0` and should track the product train version from then on
— it does not need to keep incrementing independently once both are stable.

## Source of authority

The `Version` attribute on `<Identity>` in
[`Utils.Parser.VisualStudio/source.extension.vsixmanifest`](../../Utils.Parser.VisualStudio/source.extension.vsixmanifest)
is the single authoritative value for the VSIX version — it is what `dotnet build`, `tfx extension
publish`, and Visual Studio's Extension Manager all read directly. This document only records the
*policy* that value must follow; it does not derive or override it.

A literal, hand-edited value (rather than a central MSBuild property substituted into the manifest)
was a deliberate choice: the manifest's tokenization/detokenization pipeline
(`DetokenizeVsixManifestFile`, `VsixReplacement` items) depends on the classic VSSDK targets being
importable (`$(VSToolsPath)\vssdk\Microsoft.VsSDK.targets`), which in turn depends on which Visual
Studio component set is installed on the build machine. Making the manifest self-contained avoids a
version silently failing to substitute in an environment where that import does not resolve. The
manifest's `<Identity>` also carries an inline comment restating this policy for anyone editing it
directly.

## Stability of the VSIX `Id`

The `Id` in `<Identity Id="Utils.Parser.VisualStudio.ef18346f-f79e-4e44-86f4-bf8094951570" ...>` must
never change once the extension has been published once — Visual Studio and the Marketplace use it
to decide whether a new upload is an update to an existing extension or a brand-new listing. It must
stay identical across the `0.0.x` provisional series, the first `2.0.0` stable release, and every
release after that.

## Bumping the provisional version

To publish another `0.0.x` prerelease build before the product train reaches `2.0.0` stable:

1. Increment the `Version` attribute in `source.extension.vsixmanifest` (`0.0.1` → `0.0.2`, etc.).
2. Do **not** touch `ProductTrainVersion` in `Directory.Build.props` for this — the two are
   intentionally decoupled until the product train stabilizes.
3. Do **not** touch the `Id`.

When the product train reaches `2.0.0` stable, set the VSIX `Version` to `2.0.0` directly (skipping
any further `0.0.x` numbers), and from then on keep it aligned with `ProductTrainVersion` on
subsequent stable releases.

## CI

`.github/workflows/nuget-publish.yml` already builds, and — on pushes to `release`/`releases/**` —
publishes the VSIX via `tfx extension publish`, gated on the `VS_MARKETPLACE_PUBLISHER`,
`VS_MARKETPLACE_EXTENSION_ID`, and `VS_MARKETPLACE_PAT` secrets. `eng/test-vsix-package.ps1` (invoked
from the `build-visual-studio-extensions` job before those secrets are ever read) fails the build
before publish is even reachable if: more than one `.vsix` is produced, the manifest is missing or
malformed, the `Id`/`Publisher` differ from the recorded expected values, the `Version` does not
match the policy above, or the `worker/` payload described below is missing from the archive.

## Manual Marketplace publication checklist (first publication)

Nothing in this repository publishes to the Marketplace or creates a Marketplace listing — that is
an intentionally manual, one-time act by a maintainer with access to the target Publisher account.

**Automated by the repository:**
- [x] Building the VSIX in Release configuration (`dotnet build Utils.Parser.VisualStudio/Utils.Parser.VisualStudio.csproj -c Release`).
- [x] Bundling the out-of-process worker and its dependencies inside the VSIX.
- [x] Validating the manifest, `Id` stability, version policy, and archive contents (`eng/test-vsix-package.ps1`, wired into CI).
- [x] Publishing to the Marketplace via `tfx extension publish`, once triggered by a push to `release`/`releases/**` with the Marketplace secrets configured.

**Manual, one-time, human-only:**
- [ ] Create or select the target Publisher identity at <https://marketplace.visualstudio.com/manage>.
- [ ] Confirm the Publisher identity matches the `Publisher` value in `source.extension.vsixmanifest` (currently `Olivier MARTY`), or update the manifest deliberately if it must differ.
- [ ] Create the new extension listing on the Marketplace (first publication only; later versions update the existing listing).
- [ ] Configure the `VS_MARKETPLACE_PUBLISHER`, `VS_MARKETPLACE_EXTENSION_ID`, and `VS_MARKETPLACE_PAT` repository secrets used by `.github/workflows/nuget-publish.yml`.
- [ ] Upload the Release VSIX (or let CI do it) and fill in the fields the manifest does not carry: categories, the extension logo/icon (explicitly out of scope for this change — see `Utils.Parser.VisualStudio/README.md`), a Q&A/support link, and any additional screenshots.
- [ ] Leave the listing private/unlisted for the initial `0.0.x` provisional releases if the Marketplace UI offers that option.
- [ ] Install the published VSIX from the Marketplace into a clean Visual Studio instance and verify syntax colorization and the out-of-process worker both work (see "Build and debug" below for what to check).
- [ ] Only then make the listing public.
