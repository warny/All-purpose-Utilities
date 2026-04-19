# UtilsTest

`UtilsTest` contient les tests unitaires et tests d'intégration (MSTest + SpecFlow) pour l'ensemble des projets `Utils.*`.

## Objectif

Valider les comportements transverses des bibliothèques (collections, IO, réseau, mathématiques, parsing, imagerie, etc.).

## Exemples

### 1) Exécuter toute la suite

```bash
dotnet test UtilsTest/UtilsTest.csproj
```

### 2) Exécuter un sous-ensemble ciblé

```bash
dotnet test UtilsTest/UtilsTest.csproj --filter "FullyQualifiedName~CStyleExpressionCompilerTests"
```

### 3) Exécuter un dossier de tests spécifique

```bash
dotnet test UtilsTest/UtilsTest.csproj --filter "FullyQualifiedName~UtilsTest.Net"
```

## Repères utiles

- Tests d'expressions : `UtilsTest/Expressions/`
- Tests réseau : `UtilsTest/Net/`
- Tests mathématiques : `UtilsTest/Mathematics/`
- Scénarios SpecFlow : `UtilsTest/Lists/` et `UtilsTest/Mathematics/LinearAlgebra/`
