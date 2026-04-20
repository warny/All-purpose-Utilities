# UtilsTest

`UtilsTest` contains unit tests and integration tests (MSTest + SpecFlow) for the full `Utils.*` project set.

## Purpose

Validate cross-cutting library behavior (collections, IO, networking, mathematics, parsing, imaging, etc.).

## Examples

### 1) Run the full suite

```bash
dotnet test UtilsTest/UtilsTest.csproj
```

### 2) Run a targeted subset

```bash
dotnet test UtilsTest/UtilsTest.csproj --filter "FullyQualifiedName~CSyntaxExpressionCompilerTests"
```

### 3) Run tests for a specific area

```bash
dotnet test UtilsTest/UtilsTest.csproj --filter "FullyQualifiedName~UtilsTest.Net"
```

## Useful landmarks

- Expression tests: `UtilsTest/Expressions/`
- Network tests: `UtilsTest/Net/`
- Math tests: `UtilsTest/Mathematics/`
- SpecFlow scenarios: `UtilsTest/Lists/` and `UtilsTest/Mathematics/LinearAlgebra/`
