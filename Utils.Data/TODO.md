# Utils.Data — Audit qualité (2026-07-10)

Premier passage d'audit qualité sur `Utils.Data/` (18 fichiers : parsing/pretty-printing SQL,
mapping de résultats de requête, extensions `IDbConnection`/`IDbCommand`). Même méthodologie que les
audits précédents : exploration large puis vérification manuelle de chaque piste par une trace
concrète avant de la retenir. Contrairement aux audits précédents (Fonts, Mathematics), ce passage
n'a pas révélé de bug fonctionnel sévère confirmé — plusieurs pistes prometteuses se sont révélées
fausses après une trace complète (voir section dédiée). Ce qui reste est surtout un manque de tests
et deux points cosmétiques mineurs.

## Incohérences mineures

### 1. `FieldMap.Name` peut être `null` malgré son type non-nullable
**✅ Corrigé (2026-07-17).** `Name` et `FieldAttribute` typés en `string?` / `FieldAttribute?`.

`Utils.Data/FieldMap.cs:56,73`. Le constructeur `FieldMap(member, index)` assigne `Name = null`
(commentaire : "Name is not used when mapping by index"), mais la propriété était déclarée
`internal string Name { get; }` (type non-nullable, le projet n'a pas `<Nullable>` activé donc pas
d'avertissement de compilation). En pratique, ce n'est **pas exploité en bug actif** : `getValue`
pour ce constructeur utilise toujours `record.GetValue(Index)`, jamais `Name`, et ce constructeur à
2 paramètres (`member, index`) n'est d'ailleurs appelé nulle part dans la base de code actuelle.
Le risque est latent : un futur appelant qui lirait `.Name` pour du diagnostic/logging après un
mapping par index obtiendrait `null` sans avertissement de type.

### 2. `DbConnectionExtentions.cs` — typo dans le nom de fichier
**✅ Corrigé (2026-07-17).** Fichier renommé en `DbConnectionExtensions.cs` via `git mv`.

Le fichier s'appelait "Extentions" (faute d'orthographe) alors que la classe qu'il contient est
correctement nommée `DbConnectionExtensions`. Sans impact fonctionnel, mais incohérent avec
`DbCommandExtensions.cs`.

## Manque de tests

### 3. `SqlPrettyPrinter.cs` — aucune couverture de test (658 lignes, la logique la plus complexe du package)
Aucun des 4 fichiers de test existants (`SqlQueryAnalyzerTests.cs`, `SqlStatementPartTests.cs`,
`UtilsParserSqlQueryParserTests.cs`, `SqlCommandFactoryTests.cs` — 14 méthodes de test au total pour
tout le package) n'exerce `SqlPrettyPrinter`, ni directement ni indirectement (aucune mention du nom
de la classe ni d'appel `Format`/`PrettyPrint` dans les tests). C'est pourtant le fichier le plus
volumineux et le plus dense en logique conditionnelle du package (modes d'indentation, placement des
virgules en tête/fin de ligne, gestion de l'indentation par clause). Une relecture manuelle de la
logique de virgule en début de ligne (`PrepareClauseLine`, mode `Prefixed`) n'a pas révélé
d'incohérence flagrante, mais l'absence totale de test signifie qu'aucune régression n'y serait
détectée.
**Fix proposé** : ajouter des tests couvrant au minimum les deux modes de formatage
(`SqlFormattingMode.Inline`/`Prefixed`), le placement des virgules, et quelques requêtes réalistes
avec sous-requêtes/JOIN pour vérifier l'indentation des clauses imbriquées.
**Sévérité** : manque de test (fichier le plus complexe du package, actuellement non exercé).

## Pistes rejetées après vérification (ne pas re-signaler)
- **`SqlBuilderInterpolator.AppendFormatted` — cache de paramètres par nom d'expression** — un
  balayage initial a suspecté un bug quand la même expression source (via `CallerArgumentExpression`)
  est interpolée deux fois dans la même chaîne (ex. `$"... WHERE a = {x} AND b = {x}"`). Tracé à la
  main : la déduplication produit `@p0` référencé deux fois dans le texte SQL, avec exactement un
  paramètre `@p0` dans `DbCommand.Parameters` — c'est cohérent et correct (SQL exige qu'un nom de
  paramètre référencé plusieurs fois corresponde à une seule valeur liée). Le scénario où ce serait un
  bug (muter la variable *entre* deux appels avec le même nom) est impossible dans l'usage normal :
  tous les placeholders d'une même chaîne interpolée sont construits en un seul appel atomique, sans
  code exécutable entre deux `{...}`.
- **`SqlParsingInfrastructure.ReadString` — guillemets échappés (`'O''Brien'`)** — tracé à la main,
  le mécanisme de détection du guillemet doublé fonctionne correctement.
- **`SqlParsingInfrastructure.ReadNumber` — pas de support des exposants (`1.5e10`)** — le nombre est
  tokenisé comme `1.5` suivi de l'identifiant `e10` séparément ; limitation acceptée dans un tokenizer
  destiné au pretty-printing, pas à l'évaluation numérique.
- **`SqlPrettyPrinter` — espace avant parenthèse ouvrante selon le dernier caractère du token
  précédent** — tracé à la main sur `func(...)` et `SELECT (...)`, comportement correct dans les deux
  cas.
- **`params IEnumerable<T>`** — non applicable comme anti-pattern sur ce projet (C# 13/.NET 9 supporte
  les params collections ; convention déjà établie ailleurs dans le projet).
