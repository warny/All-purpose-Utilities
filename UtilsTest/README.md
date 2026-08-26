# UtilsTest

`UtilsTest` contains the deterministic unit tests (MSTest + SpecFlow) for the full `Utils.*` project set.

## Purpose

Validate cross-cutting library behavior (collections, IO, networking, mathematics, parsing, imaging, etc.) with no dependency on external systems.

Two sibling projects complete the suite:

- [`UtilsTest.Functional`](../UtilsTest.Functional) — non-security tests that require a real external system: network sockets, OS processes, or environment-dependent file paths.
- [`UtilsTest.Security`](../UtilsTest.Security) — security-invariant tests, regardless of whether they run in memory, over the network, on the filesystem, or against a sandboxed process. See `AGENTS.md` for the full Security > Functional > Unit classification order.

## Examples

### 1) Run the full suite

```bash
dotnet test UtilsTest/UtilsTest.Unit.csproj
```

### 2) Run a targeted subset

```bash
dotnet test UtilsTest/UtilsTest.Unit.csproj --filter "FullyQualifiedName~CSyntaxExpressionCompilerTests"
```

### 3) Run tests for a specific area

```bash
dotnet test UtilsTest/UtilsTest.Unit.csproj --filter "FullyQualifiedName~UtilsTest.Net"
```

### 4) Run the sibling suites

```bash
dotnet test UtilsTest.Functional/UtilsTest.Functional.csproj
dotnet test UtilsTest.Security/UtilsTest.Security.csproj
```

## Useful landmarks

- Expression tests: `UtilsTest/Expressions/`
- Network tests: `UtilsTest/Net/`
- Math tests: `UtilsTest/Mathematics/`
- SpecFlow scenarios: `UtilsTest/Lists/` and `UtilsTest/Mathematics/LinearAlgebra/`
