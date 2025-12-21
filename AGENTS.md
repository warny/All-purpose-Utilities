# AGENTS Guidelines

This project targets **.NET 9**.

---

## Documentation  
- All classes and methods **must be documented in English**, including private ones.  
- Methods that handle **data streams** or **binary data transformations** must include clear comments.  

---

## Design Principles  
- Follow the **separation of concerns** principle:  
  - **Data classes** should only hold data.  
  - **Processing classes** should contain logic.  
- Processing logic should rely on **interfaces**, including **generic interfaces** where appropriate.  

---

## Testing  
- Every change must include a corresponding **test**.  
- The only exception is when modifying **library metadata**.  

---

## README  
- The project’s **README.md** must include an **example snippet**.  

---

## Coding Standards  
- Arrays must use **bracket syntax** (`[]`).  
- If a method uses `params` and elements are read sequentially, prefer `params IEnumerable<T>`.  
- File-reading methods must **only open the file** and then delegate content processing to a dedicated method.  
- Large `switch` statements (more than **10 cases** or **30 lines**) must be replaced by either:  
  - `Dictionary<case, method>` (each method handling one case), or  
  - `Dictionary<case, class>` depending on code complexity.  
- Code indentation must use **spaces, 4 per level**.  

---

## Codex Mission — Documentation & Discoverability (omy.Utils)

This section **extends** the existing guidelines above.  
All previous rules remain fully applicable.

### Scope

Improve **documentation, metadata, and discoverability** of the repository and its
NuGet packages (`omy.Utils` and `omy.Utils.*`), **without changing runtime behavior
or public APIs**.

This work focuses on:
- consumer-first documentation,
- NuGet package clarity and trust,
- GitHub discoverability.

---

## Compact version (mandatory)

**Do not break anything.**

1. Do **not** change public APIs or runtime behavior.
2. Rewrite the root `README.md` to be **consumer-first** (install & usage first).
3. Add a dedicated README for the **root package `omy.Utils`**.
4. Ensure the `omy.Utils` README is displayed on **nuget.org** (`PackageReadmeFile`).
5. Improve NuGet metadata (description, tags, repository URL) where missing.
6. Clearly separate:
   - *building the repo* (may require .NET 9 preview),
   - *consuming the packages* (stable TFMs).
7. Add a minimal `CHANGELOG.md` and release documentation.
8. Do **not** invent APIs, packages, or target frameworks.

If unsure, **prefer accuracy over completeness**.

---

## Detailed version (reference)

### Constraints (in addition to existing rules)

- No breaking changes.
- No behavioral changes.
- No large refactors or repo-wide restructuration.
- No new heavy dependencies.
- Metadata-only changes do **not** require tests (per Testing rules).

---

### Step 1 — Inventory (do not commit)

Identify:
- the `omy.Utils` `.csproj`,
- all packable projects,
- existing NuGet metadata,
- existing documentation locations (`/docs`, `/docs/fr`, etc.).

---

### Step 2 — GitHub discoverability

Create `docs/github-about.md` containing:
- a proposed GitHub **description** (1 sentence),
- a proposed **website** URL,
- a proposed list of **topics** (10–15 max).

Note: GitHub “About” settings cannot be changed by commit; this file is informational.

---

### Step 3 — Root README (consumer-first)

Rewrite `README.md` with this priority order:
1. what the libraries are for,
2. list of NuGet packages with short descriptions,
3. quick install examples (`dotnet add package ...`),
4. short usage snippets (real APIs only),
5. documentation links,
6. build-from-source notes at the end.

Avoid any wording suggesting preview SDKs are required to *use* the packages.

---

### Step 4 — Root package (`omy.Utils`) README & NuGet display

- Add a README next to the `omy.Utils` `.csproj`.
- Explain:
  - purpose of the root package,
  - its role as shared foundation,
  - links to sub-packages,
  - stability and versioning expectations.
- Configure `PackageReadmeFile` so the README appears on nuget.org.
- Ensure the README is included in the `.nupkg`.

---

### Step 5 — NuGet metadata (packable projects)

For each packable project, ensure (without overwriting valid existing values):
- `Description`
- `PackageTags`
- `RepositoryUrl` / `RepositoryType`
- `PackageProjectUrl`
- `PackageLicenseExpression`
- `PackageReadmeFile` (when applicable)

Do not alter versioning strategy or license choices.

---

### Step 6 — Changelog & releases

- Add `CHANGELOG.md` with:
  - `[Unreleased]`
  - an entry for documentation/metadata improvements.
- Add `docs/releasing.md` describing:
  - how to create GitHub releases aligned with NuGet,
  - tag naming,
  - how the existing CI pipeline publishes packages.

---

### Step 7 — Getting started docs

Add `docs/getting-started.md` (and optional `/docs/fr` version) covering:
- package selection,
- installation,
- supported TFMs (from csproj only),
- versioning policy,
- feedback / issues.

---

### Validation checklist (required)

- Root README is consumer-first and includes an example snippet.
- `omy.Utils` displays a README on nuget.org.
- No preview SDK requirement implied for consumers.
- No invented APIs or packages.
- Build and pack still succeed.

---

### Final output

Report:
- list of modified files,
- summary of improvements,
- any manual follow-up required (e.g. updating GitHub “About” panel).
