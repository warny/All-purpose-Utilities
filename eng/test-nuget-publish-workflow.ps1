<#
.SYNOPSIS
Verifies the safeguards around nuget-publish.yml's irreversible, OIDC-backed publish job.
.DESCRIPTION
The publish-to-nuget job is the only place in this repository that actually calls
`dotnet nuget push`, gated by nuget.org's Trusted Publishing policy (locked to this exact
workflow file and the Production environment). This is a workflow-contract test, not an
integration test: it reads the raw YAML and asserts the specific safeguards that keep a
workflow_dispatch from accidentally running the wrong job, publishing without the
Production environment's required-reviewer gate, or losing the OIDC token permission -
without ever dispatching the workflow, requesting an OIDC token, or contacting NuGet.org.
#>
[CmdletBinding()] param()
$repoRoot = Split-Path -Parent $PSScriptRoot
$workflowPath = Join-Path $repoRoot '.github/workflows/nuget-publish.yml'
$workflow = Get-Content $workflowPath -Raw

if ($workflow -notmatch '(?ms)^    workflow_dispatch:\s+inputs:') {
    throw "nuget-publish.yml must declare a workflow_dispatch trigger with inputs."
}
foreach ($input in @('validation-run-id', 'confirm-version', 'resume-partial-publication')) {
    if ($workflow -notmatch "(?ms)^            $([regex]::Escape($input)):") {
        throw "nuget-publish.yml's workflow_dispatch is missing the '$input' input."
    }
}

# Dispatch isolation: a workflow_dispatch must run only publish-to-nuget, and a push must not
# also run it - otherwise an accidental push could either skip validation or, far worse, an
# accidental dispatch could re-run the packaging jobs instead of (or in addition to) publishing.
foreach ($pushOnlyJob in @('build', 'build-visual-studio-extensions')) {
    if ($workflow -notmatch "(?ms)^    $([regex]::Escape($pushOnlyJob)):\s+if: github\.event_name == 'push'\s") {
        throw "nuget-publish.yml job '$pushOnlyJob' must be gated to if: github.event_name == 'push'."
    }
}
if ($workflow -notmatch "(?ms)^    publish-to-nuget:\s+if: github\.event_name == 'workflow_dispatch'\s") {
    throw "nuget-publish.yml's publish-to-nuget job must be gated to if: github.event_name == 'workflow_dispatch'."
}

# The Production environment carries the required-reviewer approval gate (configured on
# GitHub, not in this file) and is also the exact claim nuget.org's Trusted Publishing policy
# validates - losing this line would silently drop both protections at once.
if ($workflow -notmatch "(?ms)^    publish-to-nuget:.*?environment: Production\s") {
    throw "publish-to-nuget must run under environment: Production (required-reviewer gate and Trusted Publishing claim)."
}

# id-token: write is what lets the job request a GitHub OIDC token at all; without it,
# NuGet/login has nothing to exchange and the step fails outright (fails closed), but the
# safeguard is worth asserting explicitly rather than relying on that failure mode.
if ($workflow -notmatch "(?ms)^    publish-to-nuget:.*?permissions:\s+contents: read\s+actions: read\s+id-token: write\s") {
    throw "publish-to-nuget must declare permissions: contents: read, actions: read, id-token: write."
}

# The run-ID resolution step must still validate that the candidate came from
# release-quality-gates.yml and belongs to this repository - the same rule
# publish-validated-product-train.yml enforces for its own dry-run download.
if ($workflow -notmatch [regex]::Escape("run.path !== '.github/workflows/release-quality-gates.yml'")) {
    throw "publish-to-nuget must reject a validation-run-id that did not come from release-quality-gates.yml."
}

# The typo guard: refuses to publish unless confirm-version matches the manifest's actual
# ProductTrainVersion-derived version.
if ($workflow -notmatch [regex]::Escape("if ('`${{ inputs.confirm-version }}' -ne `$manifest.version)")) {
    throw "publish-to-nuget must compare inputs.confirm-version against the manifest's actual version before publishing."
}

# NuGet/login output wiring: the temporary API key must flow from the login step's output into
# the environment the publish step reads - not be hard-coded, not read from a long-lived secret.
if ($workflow -notmatch [regex]::Escape('uses: NuGet/login@v1')) {
    throw "publish-to-nuget must use NuGet/login@v1 to exchange the OIDC token for a temporary API key."
}
if ($workflow -notmatch [regex]::Escape('NUGET_API_KEY: ${{ steps.nuget-login.outputs.NUGET_API_KEY }}')) {
    throw "The publish step must read NUGET_API_KEY from the NuGet/login step's output, not a stored secret."
}
if ($workflow -match 'secrets\.NUGET_API_KEY') {
    throw "publish-to-nuget must not fall back to a long-lived NUGET_API_KEY secret - Trusted Publishing replaces it entirely."
}

# The arguments actually passed to the publication script: -Publish must always be present,
# and -ResumePartialPublication must be conditional on the matching input rather than always on
# (which would silently weaken every publish to --skip-duplicate) or always off (which would
# make the resume input a no-op).
if ($workflow -notmatch [regex]::Escape("@('-ArtifactsPath', 'artifacts', '-Publish', '-ApiKey', `$env:NUGET_API_KEY)")) {
    throw "publish-to-nuget must call eng/publish-product-train.ps1 with -ArtifactsPath, -Publish, and -ApiKey from the temporary key."
}
if ($workflow -notmatch [regex]::Escape("if ('`${{ inputs.resume-partial-publication }}' -eq 'true') { `$arguments += '-ResumePartialPublication' }")) {
    throw "publish-to-nuget must add -ResumePartialPublication only when the matching input is true."
}

Write-Host 'NuGet publish workflow contract tests passed.'
