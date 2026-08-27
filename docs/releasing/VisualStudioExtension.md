# Visual Studio extension (VSIX) versioning and release

`Utils.Parser.VisualStudio` packages as a VSIX, not a NuGet package. It is explicitly excluded from
the synchronized `omy.Utils` product train (`eng/product-train-manifest.json`, `exclusions` section,
`classification: "vsix"`) because the VSIX Marketplace version format cannot carry a SemVer
prerelease suffix such as `2.0.0-rc.1`.

## Why the VSIX version differs from `ProductTrainVersion`

`Directory.Build.props` declares `<ProductTrainVersion>2.0.0-rc.1</ProductTrainVersion>` for the
manifested NuGet packages (see [`ProductTrain.md`](ProductTrain.md) for the current count). The
VSIX cannot use this value directly:

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
is the single authoritative value for the VSIX version — it is what `dotnet build`, `VsixPublisher.exe`
(see "Publishing tooling" below), and Visual Studio's Extension Manager all read directly. This
document only records the *policy* that value must follow; it does not derive or override it.

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

## Icon

The VSIX uses the solution's common logo, [`res/AllPurposeUtilities_logo.png`](../../res/AllPurposeUtilities_logo.png)
(also reused by every NuGet package - see [provisional versioning](ProvisionalVersioning.md#solution-logo)),
via `Directory.Build.props`'s `SolutionLogoPath` property. `Utils.Parser.VisualStudio.csproj` links it
into the VSIX at `Resources\AllPurposeUtilities_logo.png` (no source copy under this project), and
`source.extension.vsixmanifest` declares `<Icon>Resources\AllPurposeUtilities_logo.png</Icon>` in
`<Metadata>`, positioned before `<Tags>` - `PackageManifestSchema.Metadata.xsd` enforces a strict
element order (`Icon` before `PreviewImage` before `Tags`), and the build fails with `VSSDK1062`
schema-validation errors if that order is violated. No `<PreviewImage>` is declared; a Marketplace
preview image is out of scope for this change and left as a distinct future step.

## Marketplace blocker: Taggers are a Preview API (`VSEXTPREVIEW_TAGGERS`)

`Utils.Parser.VisualStudio.csproj` explicitly suppresses the `VSEXTPREVIEW_TAGGERS` diagnostic
(`<NoWarn>$(NoWarn);1591;VSEXTPREVIEW_TAGGERS</NoWarn>`) because the extension's syntax
colorization is built on `ITextViewTaggerProvider`/`TextViewTagger<>`, which
`Microsoft.VisualStudio.Extensibility` currently ships as an experimental (`[Experimental]`-attributed)
API - the diagnostic exists specifically to flag that dependency at compile time.

Per Microsoft's own current guidance on the VisualStudio.Extensibility preview surface and on the
extension compatibility model:

- "There are a few of our APIs that don't yet meet this bar for stability... These APIs are
  explicitly labeled using the `[Experimental]` attribute" (*About VisualStudio.Extensibility
  (Preview)*, "Experimental APIs and Breaking Changes").
- "New APIs are additive and preview first. Preview APIs may change or be removed and **are not
  supported for production extensions or publishing to the Visual Studio Marketplace**." (*Extension
  compatibility model for Visual Studio*, <https://learn.microsoft.com/visualstudio/extensibility/migration/extension-compatibility>)

**This is not fixed by this PR and is not a merge blocker for this preparation work** - the VSIX is
still not being published by anything in this repository. It **is** a blocker for the "ready to
publish publicly on the Marketplace" milestone: as long as `VSEXTPREVIEW_TAGGERS` is suppressed
rather than resolved, this extension depends on an API that Microsoft's own documentation says is
not supported for Marketplace publication. Whether an actual upload would be technically rejected
is not something this repository can claim one way or the other; the point is that the extension
should not be represented as production-ready while this dependency exists. Resolving it requires
either an in-proc fallback for classification (see
[In-proc extensions](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/get-started/in-proc-extensions))
or waiting for the Taggers API to graduate out of preview - both are functional changes outside the
scope of this packaging/release-preparation work and are tracked as a separate, distinct follow-up.
The manual publication checklist below reflects this explicitly.

## Publishing tooling: not `tfx`

`tfx-cli`/`tfx extension publish` is the packaging and publishing tool for **Azure DevOps**
extensions, not Visual Studio IDE extensions - it happened to be wired into
`.github/workflows/nuget-publish.yml` but was never the correct mechanism for this VSIX, and has
been removed rather than used to publish anything.

The correct command-line tool for the Visual Studio Marketplace is **`VsixPublisher.exe`**, shipped
with the Visual Studio SDK at `${VSInstallDir}\VSSDK\VisualStudioIntegration\Tools\Bin\VsixPublisher.exe`:

```
VsixPublisher.exe publish -payload "<path to .vsix>" -publishManifest "<path to publishManifest.json>" -personalAccessToken "<PAT>"
```

It reads a `publishManifest.json` (Marketplace-only fields not carried by the VSIX itself) plus an
`overview.md` (long-form listing description). For a VSIX-sourced extension, `publishManifest.json`
only needs `identity.internalName` - the rest of the identity (name, version, icon, description) is
read directly from `source.extension.vsixmanifest`.

[`Utils.Parser.VisualStudio/marketplace/overview.md`](../../Utils.Parser.VisualStudio/marketplace/overview.md)
is ready. `publishManifest.json` is **not** checked into the repository yet, because its `publisher`
field is the real Visual Studio Marketplace Publisher identifier, which is not yet known (see
"Publisher: VSIX metadata vs. Marketplace account" below) - inventing a placeholder there risks it
being used as-is. Once that identifier exists, create
`Utils.Parser.VisualStudio/marketplace/publishManifest.json` from this template:

```json
{
    "$schema": "http://json.schemastore.org/vsix-publish",
    "categories": ["Coding"],
    "identity": {
        "internalName": "Utils.Parser.VisualStudio"
    },
    "overview": "overview.md",
    "priceCategory": "free",
    "publisher": "<REPLACE with the real Visual Studio Marketplace publisher identifier>",
    "private": true,
    "qna": true,
    "repo": "https://github.com/warny/All-purpose-Utilities"
}
```

Leave `"private": true` for the initial `0.0.x` provisional releases (see the checklist below); the
`"categories"` list can grow beyond `"Coding"` once a maintainer picks the best Marketplace category
fit.

## Publisher: VSIX metadata vs. Marketplace account

Do not confuse two different `Publisher`-shaped values:

1. `Publisher="Olivier MARTY"` in `source.extension.vsixmanifest` (and the matching
   `publisherName: "Olivier MARTY"` in `UtilsParserVisualStudioExtension.cs`) - this is
   human-readable metadata embedded in the VSIX/its activation contract, shown in the Extension
   Manager UI. It does not need to change for this repository.
2. The Visual Studio **Marketplace Publisher account identifier** - a separate, registered identity
   at <https://marketplace.visualstudio.com/manage/publishers> that owns the listing, is passed to
   `VsixPublisher.exe`/referenced in `publishManifest.json`'s `"publisher"` field, and is what the
   (now-removed) `VS_MARKETPLACE_PUBLISHER` secret was meant to hold. It is **not** necessarily the
   literal string `"Olivier MARTY"` - it is whatever identifier is chosen when the Marketplace
   Publisher account is created, and is not yet known.

Only change the manifest's `Publisher` attribute if the VSIX metadata itself should say something
different; do not change it merely to match whatever the Marketplace account identifier turns out
to be.

## CI

`.github/workflows/nuget-publish.yml`'s `build-visual-studio-extensions` job builds the VSIX and runs
`eng/test-vsix-package.ps1` on every push to `release`/`releases/**`; the same gate now also runs on
every pull request that touches the extension (`.github/workflows/dotnetcore.yml`) and as part of the
full release quality gates (`.github/workflows/release-quality-gates.yml`). It fails the build if:
more than one `.vsix` is produced, the manifest is missing or malformed, the `Id`/`Publisher` differ
from the recorded expected values, the `Version` does not match the policy above, `displayName`/
`description`/`publisherName` disagree between the manifest and
`UtilsParserVisualStudioExtension.cs`'s `ExtensionMetadata` (note: `Id` is deliberately *not*
compared - see "What must vs. must not stay in sync" below), the `<Icon>`/`<License>`/`<MoreInfo>`
elements or the files they point at are missing, or the `worker/` payload described below is missing
from the archive. **No workflow publishes the VSIX anywhere.**

## What must vs. must not stay in sync between the manifest and the C# metadata

`UtilsParserVisualStudioExtension.cs` declares a second, independent `ExtensionMetadata` consumed by
the `Microsoft.VisualStudio.Extensibility` framework, separate from `source.extension.vsixmanifest`:

| Field | Manifest | `ExtensionMetadata` | Kept in sync? |
|---|---|---|---|
| Version | `<Identity Version>` | `version:` | Yes - same fact, two files |
| Display name | `<DisplayName>` | `displayName:` | Yes |
| Description | `<Description>` | `description:` | Yes |
| Publisher | `<Identity Publisher>` | `publisherName:` | Yes |
| Identifier | `<Identity Id>` (GUID-suffixed, VSIX/Marketplace package identity) | `id:` (short activation-contract identifier, no GUID) | **No** - different kind of identifier for a different consumer; see below |

The `Id`s are not the same kind of value: `<Identity Id>` is what Visual Studio and the Marketplace
use to recognize a new upload as an update to the same extension (must never change - see "Stability
of the VSIX `Id`" above). `ExtensionMetadata.Id` is a short identifier used internally by the
`VisualStudio.Extensibility` activation contract. Forcing them to be textually identical would not
make either one more correct, and neither the framework's documentation nor the generated
`manifest.json`/`catalog.json` packaging artifacts (which mirror the vsixmanifest's `Identity`, not
the C# class) suggest they should match. `eng/test-vsix-package.ps1` therefore does not compare them.

## Manual Marketplace publication checklist (first publication)

Nothing in this repository publishes to the Marketplace or creates a Marketplace listing — that is
an intentionally manual, one-time act by a maintainer with access to the target Publisher account.

**Automated by the repository:**
- [x] Building the VSIX in Release configuration (`dotnet build Utils.Parser.VisualStudio/Utils.Parser.VisualStudio.csproj -c Release`).
- [x] Bundling the out-of-process worker and its dependencies inside the VSIX.
- [x] Validating the manifest, `Id` stability, version policy, icon/license/more-info presence, cross-file metadata sync, and archive contents (`eng/test-vsix-package.ps1`, wired into pull-request CI, release CI, and full release quality gates).
- [x] Preparing `Utils.Parser.VisualStudio/marketplace/overview.md` for `VsixPublisher.exe`.
- [ ] Publishing anywhere - deliberately **not** automated. See "Publishing tooling" above for why `tfx` was removed and what a future automated `VsixPublisher.exe` step would need.

**Manual, one-time, human-only:**
- [ ] **Blocking, resolve first:** decide how to handle the `VSEXTPREVIEW_TAGGERS` Preview API dependency (see "Marketplace blocker" above) before treating this extension as ready for public Marketplace publication. This does not block merging this preparation PR, but it should block checking off any of the steps below with the intent of making the listing public.
- [ ] Create or select the target Publisher identity at <https://marketplace.visualstudio.com/manage/publishers>, and note its real identifier (see "Publisher: VSIX metadata vs. Marketplace account" above - it does not have to be `Olivier MARTY`).
- [ ] Create `Utils.Parser.VisualStudio/marketplace/publishManifest.json` from the template above with that real identifier.
- [ ] Create the new extension listing on the Marketplace (first publication only; later versions update the existing listing) by running `VsixPublisher.exe publish` locally, or decide whether/when to wire this into CI as a separate, deliberate follow-up.
- [ ] Fill in the fields neither the manifest nor `publishManifest.json` carry: a dedicated Marketplace preview image (the packaged `<Icon>` covers the Extension Manager/listing icon already; a larger preview image is a distinct future step - see `Utils.Parser.VisualStudio/README.md`), and any additional screenshots.
- [ ] Leave the listing private (`publishManifest.json`'s `"private": true`) for the initial `0.0.x` provisional releases - this is also the state to stay in for as long as the `VSEXTPREVIEW_TAGGERS` blocker above is unresolved.
- [ ] Install the published VSIX from the Marketplace into a clean Visual Studio instance and verify syntax colorization and the out-of-process worker both work (see "Build and debug" below for what to check).
- [ ] Only after the Preview API blocker is resolved, set `"private": false` and republish to make the listing public.
