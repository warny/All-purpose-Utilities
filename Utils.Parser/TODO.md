# Utils.Parser — Audit qualité (2026-07-10)

Audit du périmètre `Utils.Parser` et de ses projets associés, avec un focus sur les duplications de
code ou d'intention et sur les chemins de transformation, d'injection C# et de compilation
d'expressions.

**État (2026-07-10) :** les chemins d'exécution identifiés passent actuellement par
`IParserEmbeddedCodeTransformer`. La compilation dynamique des expressions passe ensuite par
`IExpressionCompiler`. En revanche, l'injection du code C# reste répartie dans `GrammarEmitter` et
n'est pas isolée derrière une classe d'injection dédiée. Les items ci-dessous sont des propositions
non traitées, sauf mention contraire.

## Architecture du code embarqué (priorité haute)

### 1. Introduire une représentation typée du code transformé
**Corrigé.** Le pipeline distingue désormais `RawEmbeddedCode` et `TransformedEmbeddedCode` dans
`Utils.Parser.Diagnostics.EmbeddedCode`. `EmbeddedCodeSource` expose le texte source sous forme de
`RawEmbeddedCode`, les hooks générés conservent le code brut dans `RawCode`, et leur `TransformedCode`
est un `TransformedEmbeddedCode` rempli uniquement après l'appel à `IParserEmbeddedCodeTransformer`.
`GrammarEmitter.TransformEmbeddedCode` et `ExpressionEmbeddedCodePreparer.TransformSource` passent par
`ParserEmbeddedCodeTransformationService.TransformOrThrow`, qui exécute le transformer, valide les diagnostics
puis retourne un code transformé typé. Les chemins d'émission C# ou de compilation via
`IExpressionCompiler` consomment explicitement ce type avant d'extraire le texte final.

Tests ajoutés :

- `ExpressionEmbeddedCodePreparerTests.EmbeddedCodeSource_WhenCreated_ExposesTypedRawCode` ;
- `ExpressionEmbeddedCodePreparerTests.PrepareSemanticPredicate_WhenTransformerProvided_CompilerReceivesTransformedCode` ;
- `ExpressionEmbeddedCodePreparerTests.PrepareParserAction_WhenNoOpTransformerUsed_CompilerReceivesTextuallyIdenticalTransformedCode` ;
- `ExpressionEmbeddedCodePreparerTests.TransformedEmbeddedCode_DoesNotExposePublicConstructorsOrManualResultConversion` ;
- `ExpressionEmbeddedCodePreparerTests.ParserEmbeddedCodeTransformationService_WhenRawCodeIsNull_DoesNotInvokeTransformer` ;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenTransformerReplacesParserPredicateAndAction_RawCodeDoesNotAppearInGeneratedHookBodies` ;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenTransformerReplacesLexerHook_RawCodeDoesNotAppearInGeneratedHookBodies` ;
- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_UseTypedRawAndTransformedCodeFields` ;
- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_WhenUntransformedHookIsEmitted_ThrowsBeforeWritingSource`.

### 2. Centraliser l'appel au transformer et la validation des diagnostics
**Corrigé.** `ParserEmbeddedCodeTransformationService.TransformOrThrow` est désormais la frontière
unique de transformation : il reçoit le `RawEmbeddedCode`, impose le texte brut dans le
`ParserEmbeddedCodeTransformationContext`, appelle `IParserEmbeddedCodeTransformer.Transform(...)` une
seule fois, valide le résultat, traite une liste de diagnostics nulle comme vide, ignore les entrées
de diagnostic nulles, bloque le premier diagnostic `Error`, vérifie que le code transformé n'est pas
nul, et retourne uniquement un `TransformedEmbeddedCode` validé.

Les erreurs de génération C# et de compilation runtime passent par le même modèle structuré
`ParserEmbeddedCodeTransformationException` du package diagnostics. L'exception expose le chemin
(génération ou compilation runtime), le code et le message du diagnostic, l'emplacement, le nom de
grammaire, le nom de règle et le span disponible ;
`Utils.Parser.Expressions` conserve son type public en l'adaptant depuis cette exception commune sans
perdre l'exception interne du transformer. Les diagnostics `Info` et `Warning` ne bloquent pas la
transformation et sont conservés sur `TransformedEmbeddedCode.Diagnostics`.

Tests ajoutés ou consolidés :

- appel unique du transformer et transmission du contexte brut/métadonnées existante via les tests de
  transformation runtime et générateur ;
- rejet d'un diagnostic `Error` avant compilation runtime ;
- rejet d'un diagnostic `Error` avant injection générée existante ;
- conservation des diagnostics `Warning` ;
- erreurs déterministes pour résultat nul et code nul ;
- diagnostics nuls traités comme une collection vide ;
- conservation de l'exception interne quand le transformer lève ;
- test architectural fonctionnel, basé sur une analyse syntaxique Roslyn légère, scannant les
  invocations plutôt que les fichiers complets et interdisant les appels directs de production à
  `IParserEmbeddedCodeTransformer.Transform(...)` hors de l'appel central exact dans
  `ParserEmbeddedCodeTransformationService.TransformOrThrow(...)`, avec des tests de régression pour les
  appels multi-lignes et les appels inattendus dans le fichier du service central ;
- tests générateur vérifiant qu'une erreur du transformer bloque l'émission C# et que les métadonnées
  d'erreur restent cohérentes entre génération et compilation runtime.

### 3. Créer une classe dédiée à l'injection du code C#
**Corrigé.** L'injection C# générée est désormais centralisée dans
`Utils.Parser.Generators.Internal.CSharpEmbeddedCodeInjector`. `GrammarEmitter` conserve la collecte,
la classification, la création des contextes et l'appel à `TransformEmbeddedCode(...)`, mais transmet
ensuite uniquement des `TransformedEmbeddedCode` à l'injecteur. L'API d'injection n'accepte ni
`RawEmbeddedCode`, ni `G4GrammarAction`, ni `G4EmbeddedAction`, ni chaîne de contenu brut ; les chaînes
restantes sont limitées aux marqueurs/descripteurs internes contrôlés par `CSharpEmbeddedCodeRegion`.

Familles migrées :

- headers, members et footers parser (`@header`, `@members`, `@footer`, `@parser::*`) ;
- headers, members et footers lexer (`@lexer::header`, `@lexer::members`, `@lexer::footer`) ;
- actions inline parser et lexer ;
- prédicats parser et lexer, avec distinction entre expression retournée et fragment complet ;
- hooks de cycle de vie parser `@init` et `@after`.

L'injecteur porte la normalisation déterministe des fins de ligne (`\n`, `\r\n`, `\r`), le découpage
en lignes, l'indentation à quatre espaces par niveau, les marqueurs de régions nommées et l'espacement
final des régions. Une fin de ligne finale continue de produire une ligne vide finale lorsque le texte
n'est pas préalablement rogné, afin de préserver le comportement historique des régions verbatim.

Tests ajoutés :

- `CSharpEmbeddedCodeInjectorTests` couvre les lignes simples/multiples, la normalisation des fins de
  ligne, l'indentation, les corps de méthode, les lignes vides, le texte vide, les marqueurs,
  l'espacement final, les expressions de prédicat, les blocs d'action et la frontière typée de l'API ;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenTransformerReplacesNamedActions_UsesTransformedMarkedRegions` ;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenTransformerReplacesParserHooks_UsesTransformedHookBodies` ;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenTransformerReplacesLexerHooks_UsesTransformedHookBodies` ;
- `CSharpEmbeddedCodeInjectorArchitectureTests` ajoute un garde-fou Roslyn fonctionnel qui interdit les
  append directs de code brut et limite les lectures de texte transformé hors injecteur aux usages de
  classification non injectants explicitement justifiés.

### 4. Ajouter des tests d'invariant pour tous les emplacements de code embarqué
**Corrigé.** La couverture d'invariant est portée par `UtilsTest/Parser/EmbeddedCodeTransformationInvariantTests.cs` avec le transformer espion déterministe `RecordingEmbeddedCodeTransformer`.

Emplacements génération C# couverts :

- parser `@header`, `@members` et `@footer` (`ParserHeader`, `ParserMembers`, `ParserFooter`) ;
- lexer `@header`, `@members` et `@footer` (`LexerHeader`, `LexerMembers`, `LexerFooter`) ;
- prédicat sémantique parser inline (`SemanticPredicate`) ;
- action parser inline (`InlineAction`) ;
- hooks de cycle de vie `@init` et `@after` (`RuleInit`, `RuleAfter`) ;
- prédicat lexer inline (`LexerSemanticPredicate`) ;
- action lexer inline (`LexerInlineAction`).

Emplacements runtime couverts :

- prédicat parser préparé par `ExpressionEmbeddedCodePreparer` puis compilé par `IExpressionCompiler` ;
- action parser préparée par `ExpressionEmbeddedCodePreparer` puis compilée par `IExpressionCompiler`.

Les invariants vérifiés sont les suivants :

- chaque fragment brut attendu produit exactement un appel à `IParserEmbeddedCodeTransformer.Transform(...)` ;
- l'appel contient le `ParserEmbeddedCodeLocation`, le nom de grammaire, le nom de règle et les métadonnées passives disponibles ;
- les paramètres, retours, locaux et labels parser exposés par l'architecture actuelle sont transmis aux fragments de règle ;
- les fragments parser ne sont pas classés comme fragments lexer, et inversement ;
- les marqueurs transformés sont uniques, valides pour leur cible C#, et apparaissent exactement une fois au point d'injection ou de compilation attendu ;
- le texte brut ne réapparaît pas dans les corps exécutables générés ni dans l'entrée du compilateur runtime ;
- les prédicats générés couvrent la forme expression et la forme bloc avec `return` ;
- `@init` et `@after` sur la même règle restent deux appels distincts, ordonnés et non confondus ;
- un diagnostic `Error`, une exception du transformer, un résultat nul ou un code transformé nul bloque l'injection générée ;
- un diagnostic `Error`, une exception du transformer, un résultat nul ou un code transformé nul bloque la compilation runtime ;
- un diagnostic `Warning` runtime conserve un traitement simple : transformation unique et compilation unique.

Les garde-fous architecturaux existants restent consolidés par :

- `EmbeddedCodeTransformerArchitectureTests`, qui limite les appels directs à `Transform(...)` au service central et vérifie par modèle sémantique Roslyn que les préparateurs de code embarqué ne créent pas de second chemin direct vers `IExpressionCompiler.Compile(...)` ;
- `CSharpEmbeddedCodeInjectorArchitectureTests`, qui vérifie que les méthodes d'émission ciblées utilisent `CSharpEmbeddedCodeInjector` et que les lectures de `TransformedEmbeddedCode.Text` hors injecteur restent limitées aux classifications non injectantes autorisées ;
- `CSharpEmbeddedCodeInjectorTests`, qui verrouille l'API d'injection sur `TransformedEmbeddedCode` et interdit les paramètres `RawEmbeddedCode` ou chaînes brutes.

Aucun injecteur espion de production n'a été ajouté : la frontière `CSharpEmbeddedCodeInjector` est vérifiée par les tests architecturaux Roslyn et par les marqueurs générés.

## Duplications de code (priorité moyenne)

### 5. Factoriser les named actions parser et lexer
**Corrigé.** Les six familles de named actions générées (`parser @header`, `parser @members`,
`parser @footer`, `lexer @header`, `lexer @members`, `lexer @footer`) passent désormais par un
descripteur interne immuable `NamedActionInjectionDescriptor` et par la méthode commune
`EmitNamedActionRegion`.

Le descripteur porte uniquement les différences nécessaires entre familles :

- le `ParserEmbeddedCodeLocation` transmis à la frontière de transformation ;
- la `CSharpEmbeddedCodeRegion`, qui conserve les marqueurs, l'indentation et l'espacement existants ;
- le sélecteur basé sur `EmbeddedMembersSupport`, afin de ne pas dupliquer la classification
  `@header` / `@parser::header` / `@lexer::header` et équivalents.

Les wrappers explicites `EmitParserHeaders`, `EmitParserMembers`, `EmitParserFooters`,
`EmitLexerHeaders`, `EmitLexerMembers` et `EmitLexerFooters` sont conservés. Ils choisissent
uniquement le descripteur statique approprié et délèguent à la méthode commune. La source C# générée
reste inchangée : mêmes positions d'émission, mêmes régions, même indentation, mêmes lignes vides et
même ordre d'appel au transformer.

Tests ajoutés ou renforcés :

- couverture d'invariant pour l'ordre de plusieurs fragments d'une même catégorie de named action ;
- garde-fous Roslyn vérifiant que les six wrappers délèguent à `EmitNamedActionRegion` ;
- garde-fous Roslyn vérifiant que les wrappers ne transforment pas et n'injectent pas directement ;
- maintien des invariants existants couvrant les six `ParserEmbeddedCodeLocation` nommés, l'absence
  de fallback vers le texte brut et le passage exclusif par `CSharpEmbeddedCodeInjector`.

Les points 6 à 11 restent hors périmètre : actions inline parser/lexer, prédicats, lifecycle hooks,
parcours récursifs, symboles runtime et compilation/préparation d'expressions ne sont pas factorisés
par cette correction.

### 6. Fusionner `EmbeddedCodeHook` et `LexerEmbeddedCodeHook`
**Corrigé.** Les hooks parser et lexer générés sont désormais représentés par un seul type interne
`GrammarEmitter.EmbeddedCodeHook`. L'ancien type `LexerEmbeddedCodeHook` a été supprimé. Le type
commun conserve le code brut dans `RawEmbeddedCode RawCode` et le code transformé prêt à l'émission
dans `TransformedEmbeddedCode? TransformedCode`, sans remplacer ces frontières typées par des chaînes.

La distinction de domaine est explicite grâce à deux discriminants internes :

- `EmbeddedCodeHookOwner` distingue `Parser` et `Lexer` ;
- `EmbeddedCodeHookKind` distingue `SemanticPredicate` et `InlineAction`.

Les quatre catégories sont donc couvertes sans booléen ambigu comme état principal : prédicat parser,
action inline parser, prédicat lexer et action inline lexer. La construction passe par les fabriques
`CreateParser(...)` et `CreateLexer(...)`, qui valident le nom de règle, le code brut, les indices
acceptés (`-1` comme sentinelle historique ou valeur positive/nulle), le nom de méthode générée et les
valeurs d'enum. Le résultat transformé reste absent avant la transition immuable, et le point d’accès unique des
émetteurs rejette explicitement cet état avant toute écriture de source.

Les producteurs parser et lexer restent séparés : `CollectEmbeddedCodeHooks(...)` conserve les règles
d'indexation parser, notamment les traitements de séquences, quantificateurs, négations et récursion
gauche, tandis que `CollectLexerEmbeddedCodeHooks(...)` conserve le parcours lexer existant. Les
parcours récursifs ne sont pas mutualisés dans cette correction afin de ne pas masquer les différences
runtime. Les conventions de nommage restent portées par les producteurs (`__Predicate...`,
`__Action...`, `__LexerPredicate...`, `__LexerAction...`) et la source C# générée reste inchangée.

Tests ajoutés ou renforcés :

- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_UseOneTypedHookWithExplicitOwnerAndKind` ;
- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_WhenCreatedThroughFactories_PreserveFourCategories` ;
- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_WhenInvalidStateIsCreated_RejectsIt` ;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenParserAndLexerHooksShareGrammar_PreservesStableCategoriesAndGeneratedBodies` ;
- `EmbeddedCodeTransformerArchitectureTests.GrammarEmitterEmbeddedCodeHooks_UseSingleCommonHookModel`.

Les points 7 à 11 restent hors périmètre : construction des symboles runtime, mutualisation générale
des parcours récursifs, formalisation globale du pipeline, clarification de l’état brut/transformé et
documentation de la façade runtime ne sont pas traités par cette correction.

### 7. Factoriser la construction des symboles d'expressions
**Corrigé.** `ExpressionEmbeddedCodePreparer` conserve les wrappers spécialisés
`BuildSemanticPredicateSymbols(...)` et `BuildParserActionSymbols(...)`, mais ceux-ci ne portent plus
aucune logique de classification : ils délèguent directement à la méthode commune privée
`BuildRuntimeContextSymbols(ParameterExpression runtimeContext, IReadOnlySet<EmbeddedCodeContextSymbol> supportedSymbols)`.

La méthode commune crée le dictionnaire avec `StringComparer.Ordinal`, parcourt `supportedSymbols` une
seule fois et expose les quatre symboles historiques sans changer leurs noms publics :

- `RuleName` -> `ruleName` -> `context.Rule.Name` ;
- `InputPosition` -> `inputPosition` -> `context.InputPosition` ;
- `AlternativeIndex` -> `alternativeIndex` -> `context.AlternativeIndex` ;
- `ElementIndex` -> `elementIndex` -> `context.ElementIndex`.

Les helpers dupliqués `AddSemanticPredicateSymbol(...)` et `AddParserActionSymbol(...)` ont été
supprimés. `BuildRuleName(...)` reste isolé, car il documente explicitement la chaîne de lecture
`Rule.Name` partagée par les deux contextes runtime. Aucune interface ou classe de base publique n'a
été ajoutée pour les contextes `SemanticPredicateEvaluationContext` et `ParserActionExecutionContext` ;
le type de lambda de chaque chemin reste spécialisé. Les expressions construites lisent toujours les
valeurs depuis le contexte fourni au moment de l'exécution, sans capture de valeur à la préparation et
sans appel supplémentaire à `IExpressionCompiler.Compile(...)`. Les valeurs inconnues de
`EmbeddedCodeContextSymbol` restent ignorées, comme dans les anciens `switch` sans branche `default`.

Tests ajoutés ou renforcés :

- matrice unitaire couvrant `PrepareSemanticPredicate` et `PrepareParserAction` pour les quatre
  symboles, le dictionnaire vide, un symbole unique, un sous-ensemble non ordonné et les valeurs
  inconnues de l'enum ;
- inspection des `MemberExpression` pour vérifier les membres runtime ciblés sans dépendre de
  `Expression.ToString()` ;
- tests d'exécution réutilisant le même artefact préparé avec deux contextes différents pour prouver
  la lecture runtime des valeurs ;
- garde-fou Roslyn fonctionnel vérifiant que les wrappers délèguent à `BuildRuntimeContextSymbols`,
  que les anciens helpers `Add...Symbol` n'existent plus, qu'une seule méthode classe
  `EmbeddedCodeContextSymbol`, que les lambdas conservent leurs types runtime spécialisés et qu'aucun
  nouveau contrat public n'a été introduit.

Les points 8 à 11 restent hors périmètre du code de production de cette correction : la mutualisation
générale parser/lexer, le pipeline global, la clarification de l’état brut/transformé et la documentation de façade
runtime demeurent des chantiers séparés.

### 8. Introduire des moteurs communs avec stratégies parser/lexer spécialisées
**Partiellement corrigé.**

#### 8a. Collecte des hooks — corrigé

La collecte des hooks embarqués parser et lexer est désormais mutualisée par le moteur commun privé
`EmbeddedHookCollector`. Ce moteur porte la création de la collection, le parcours récursif stable des
nœuds `G4Alternation`, `G4Alternative`, `G4Sequence`, `G4Quantifier`, `G4Negation` et
`G4EmbeddedAction`, l'accumulation des hooks, puis la transformation ordonnée de chaque hook une seule
fois via `TransformEmbeddedCode(...)`.

Les différences réelles restent concentrées dans deux stratégies privées :

- `ParserEmbeddedHookCollectionStrategy` énumère les règles parser, applique l'ordre par priorité,
  prépare les racines de parcours des règles direct-left-recursive en séparant alternatives de base et
  queues récursives, conserve les sentinelles historiques `-1`, les règles parser d'indexation des
  alternatives, séquences, quantificateurs et négations, les préfixes `__Predicate` / `__Action`, les
  localisations `SemanticPredicate` / `InlineAction`, et la fabrique typée `EmbeddedCodeHook.CreateParser(...)` ;
- `LexerEmbeddedHookCollectionStrategy` énumère les règles lexer de `DEFAULT_MODE` puis les modes
  supplémentaires, conserve l'ordre source, garde les index lexer historiques pour alternatives,
  séquences, quantificateurs et négations, les préfixes `__LexerPredicate` / `__LexerAction`, les
  localisations `LexerSemanticPredicate` / `LexerInlineAction`, et la fabrique typée
  `EmbeddedCodeHook.CreateLexer(...)`.

Le contexte immuable `HookTraversalPosition` transporte explicitement `AlternativeIndex` et
`ElementIndex`, y compris les sentinelles `-1`, sans collection mutable ni état global. Les racines de
parcours sont transmises par `HookTraversalRoot`, ce qui laisse la récursion gauche au périmètre parser
spécialisé et les modes lexer au périmètre lexer spécialisé. Les wrappers lisibles
`CollectEmbeddedCodeHooks(...)` et `CollectLexerEmbeddedCodeHooks(...)` sont conservés, mais ils ne
contiennent plus de `switch` sur `G4Content`, de récursion, de création directe de hooks, de
transformation directe ni de logique d'indexation.

Aucun booléen `isLexer`, aucune classe de base extensive et aucune nouvelle API publique n'ont été
introduits. La source C# générée est préservée par les tests existants de source générée et par les
tests d'invariants de transformation. Le garde-fou Roslyn
`GrammarEmitterEmbeddedCodeHookCollection_UsesSharedCollectorAndStrategies` vérifie la délégation des
wrappers, l'existence du moteur et des stratégies, l'absence de parcours dupliqués, l'absence de
paramètre `bool isLexer`, la centralisation de la transformation, l'utilisation de `CreateParser(...)`
et `CreateLexer(...)`, le portage de la récursion gauche par la stratégie parser, le portage des modes
lexer par la stratégie lexer, et la présence du contexte immuable d'indices.

Tests ajoutés ou renforcés :

- `EmbeddedCodeTransformerArchitectureTests.GrammarEmitterEmbeddedCodeHookCollection_UsesSharedCollectorAndStrategies` ;
- tests existants de caractérisation `Antlr4GeneratedEmbeddedCodeTests` couvrant parser non récursif,
  parser récursif gauche, lexer, grammaires combinées, noms de méthodes, dispatchers, corps transformés
  et stabilité de la source générée ;
- tests existants `EmbeddedCodeTransformationInvariantTests` couvrant ordre, nombre d'appels au
  transformer, localisations, noms de règles, code brut et absence de double transformation.

#### 8b. Dispatchers runtime — corrigé

L'émission des dispatchers runtime parser et lexer est désormais mutualisée par le moteur privé
`EmbeddedHookDispatcherEmitter`. Ce moteur porte l'algorithme stable : déclaration de la classe
générée, signature de la méthode de dispatch, boucle ordonnée sur les hooks déjà sélectionnés,
validation des discriminants `Owner` et `Kind`, comparaisons dans l'ordre historique
`Rule.Name`, code brut (`PredicateCode` ou `ActionCode`), `AlternativeIndex`, `ElementIndex`, appel de
la méthode de hook, retour de succès et retour de fallback. Aucun tri supplémentaire n'est introduit ;
l'ordre reste celui des listes `predicates`, `actions`, `lexerActions` et `lexerPredicates` produites par
la collecte existante.

Les différences déclaratives sont concentrées dans le descripteur immuable privé
`EmbeddedHookDispatcherDescriptor`, qui expose explicitement les quatre configurations constantes :

- `ParserPredicate` pour `GeneratedSemanticPredicateEvaluator` / `ISemanticPredicateEvaluator`,
  `SemanticPredicateEvaluationContext`, `SemanticPredicateEvaluationOutcome`, succès
  `Satisfied`/`Rejected` et fallback `_fallback.Evaluate(context)` ;
- `ParserAction` pour `GeneratedParserActionExecutor` / `IParserActionExecutor`,
  `ParserActionExecutionContext`, `ParserActionExecutionOutcome.Executed` et fallback
  `_fallback.Execute(context)` ;
- `LexerPredicate` pour `GeneratedLexerPredicateEvaluator` / `ILexerPredicateEvaluator`,
  `LexerPredicateEvaluationContext`, `LexerPredicateEvaluationOutcome.True`/`False` et fallback
  `_fallback.Evaluate(context)` ;
- `LexerAction` pour `GeneratedLexerActionExecutor` / `ILexerActionExecutor`,
  `LexerActionExecutionContext` avec `LexerActionExecutionResult`,
  `LexerActionExecutionOutcome.Executed` et fallback `_fallback.Execute(context, result)`.

Les wrappers explicites `EmitSemanticPredicateEvaluator(...)`, `EmitParserActionExecutor(...)`,
`EmitLexerPredicateEvaluator(...)` et `EmitLexerActionExecutor(...)` sont conservés comme points de
lecture parser/lexer et prédicat/action ; ils sélectionnent seulement le descripteur correspondant et
délèguent au moteur commun. Aucune stratégie comportementale supplémentaire n'a été nécessaire : les
différences constatées sont les types, signatures, propriétés de code, expressions de succès, arguments
d'appel et fallbacks, donc elles restent décrites par données. Aucun paramètre `isLexer` ou
`isPredicate`, aucun gros `switch` de domaine et aucune nouvelle API publique ne sont introduits.

La source générée est préservée : noms de classes, interfaces, signatures, conditions, ordre des
conditions, appels `__Predicate...`, `__Action...`, `__LexerPredicate...`, `__LexerAction...`, retours de
succès et fallbacks restent identiques. Les méthodes de hooks elles-mêmes ne sont pas mutualisées et
restent explicitement ouvertes pour 8c.

Tests ajoutés ou renforcés :

- `Antlr4GeneratedEmbeddedCodeTests.Emit_CombinedEmbeddedCode_GeneratesEquivalentRuntimeDispatchers`
  caractérise les quatre dispatchers sur une grammaire combinée avec plusieurs hooks parser et lexer ;
- `EmbeddedCodeTransformerArchitectureTests.GrammarEmitterEmbeddedHookDispatchers_UseSharedEmitterAndImmutableDescriptors`
  vérifie par Roslyn la délégation des quatre wrappers, l'absence de boucle et de structure complète
  dans les wrappers, l'émetteur commun unique, le descripteur immuable privé, l'absence de `isLexer` /
  `isPredicate`, l'absence de branches parser/lexer dispersées dans l'émetteur, la validation des
  discriminants `Owner` et `Kind`, les quatre configurations explicites, les fallbacks et le maintien des
  méthodes de hooks séparées pour 8c.

#### 8c. Méthodes de hooks — corrigé

L'émission des quatre familles de méthodes de hooks générées est désormais mutualisée par le moteur
privé `EmbeddedHookMethodEmitter`. Ce moteur porte l'algorithme stable : validation typée du hook par
`Owner` et `Kind`, création du `GeneratedEmbeddedCodeBody`, commentaire XML, signature, accolade
d'ouverture, préambule éventuel de locaux de contexte, appel centralisé à
`EmitGeneratedEmbeddedCodeBody(...)`, accolade de fermeture et ligne vide finale. La source générée
reste préservée : mêmes commentaires XML, mêmes signatures, même indentation, mêmes corps injectés et
même ordre d'émission.

Les différences déclaratives sont concentrées dans le descripteur immuable privé
`EmbeddedHookMethodDescriptor`, qui expose explicitement les quatre configurations :

- `ParserPredicate` valide `Parser` / `SemanticPredicate`, émet `private bool`, reçoit
  `SemanticPredicateEvaluationContext context`, utilise `GeneratedEmbeddedCodeBody.ForPredicate(...)`
  et conserve les locaux parser predicate (`ruleName`, `inputPosition`, `alternativeIndex`,
  `elementIndex`, `predicateCode`) ;
- `ParserAction` valide `Parser` / `InlineAction`, émet `private void`, reçoit
  `ParserActionExecutionContext context`, utilise `GeneratedEmbeddedCodeBody.ForAction(...)` et
  conserve les locaux parser action (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`,
  `actionCode`) ;
- `LexerPredicate` valide `Lexer` / `SemanticPredicate`, émet `private bool`, reçoit
  `LexerPredicateEvaluationContext context`, utilise `GeneratedEmbeddedCodeBody.ForPredicate(...)` et
  ne reçoit aucun local parser ;
- `LexerAction` valide `Lexer` / `InlineAction`, émet `private void`, reçoit
  `LexerActionExecutionContext context, LexerActionExecutionResult result`, utilise
  `GeneratedEmbeddedCodeBody.ForAction(...)`, conserve le paramètre mutable `result` et ne reçoit aucun
  local parser.

Le profil explicite `EmbeddedHookContextLocalProfile` limite le préambule aux trois cas réels
(`None`, `ParserPredicate`, `ParserAction`) sans introduire de booléen de domaine. Les wrappers
`EmitPredicateHook(...)`, `EmitActionHook(...)`, `EmitLexerPredicateHook(...)` et
`EmitLexerActionHook(...)` restent les points de lecture spécialisés ; ils sélectionnent uniquement le
descripteur et délèguent au moteur commun. Les hooks lifecycle restent hors périmètre : ils reposent
sur `LifecycleHook`, une visibilité `internal`, `ParserRuleLifecycleContext`, des locaux et phases
`@init` / `@after`, sans discriminants `EmbeddedCodeHookOwner` / `EmbeddedCodeHookKind`.

Tests ajoutés ou renforcés :

- `Antlr4GeneratedEmbeddedCodeTests.Emit_CombinedEmbeddedCode_GeneratesStableHookMethodBlocks`
  caractérise les commentaires, signatures, locaux parser, absence de locaux parser côté lexer,
  transformation expression/bloc, ordre des familles et conservation du paramètre lexer `result` ;
- `EmbeddedCodeTransformerArchitectureTests.GrammarEmitterEmbeddedHookMethods_UseSharedEmitterAndImmutableDescriptors`
  vérifie par Roslyn l'émetteur commun, le descripteur immuable, les quatre configurations explicites,
  la délégation des wrappers, l'absence d'appels directs interdits dans les wrappers, la validation
  typée, l'appel unique à `EmitGeneratedEmbeddedCodeBody(...)`, la distinction typée
  `ForPredicate(...)` / `ForAction(...)`, les profils de locaux explicites, l'absence de `isLexer` /
  `isPredicate`, et le maintien des hooks lifecycle hors abstraction.

#### 8d. Garde-fous globaux — corrigé

Les garde-fous architecturaux couvrent maintenant la collecte, les dispatchers runtime et les méthodes
de hooks. Ils vérifient l'absence de réintroduction d'algorithmes parallèles, l'absence de booléens de
domaine `isLexer` / `isPredicate`, l'absence de nouvelle API publique, la conservation des wrappers
explicites, la validation typée et la séparation volontaire des hooks lifecycle.

## Duplications d'intention et lisibilité (priorité moyenne)

### 9. Clarifier la frontière entre préparation, transformation, injection et compilation
**Corrigé.** Le noyau interne `EmbeddedCodeTransformationPipeline.TransformAndValidate(...)`, placé
dans l'assembly `netstandard2.0` de diagnostics partagée, reçoit un `RawEmbeddedCode`, un
`IParserEmbeddedCodeTransformer`, un `ParserEmbeddedCodeTransformationContext` fortement typé et les
métadonnées d'échec. Il appelle le transformer exactement une fois, centralise les validations des
arguments, du résultat nul, des diagnostics bloquants et du code transformé nul, puis remet un
`TransformedEmbeddedCode` validé. L'ancien service public délègue au noyau afin de préserver l'API.

La frontière s'arrête strictement à cet artefact : elle ne connaît ni `StringBuilder`, ni injection,
ni expression, ni lambda, ni compilation. Le générateur construit son contexte enrichi (règle,
déclarations et labels), appelle le noyau, conserve la distinction parser/lexer et predicate/action,
puis classe le résultat via `GeneratedEmbeddedCodeBody.ForPredicate(...)` ou `ForAction(...)` et
délègue exclusivement à `CSharpEmbeddedCodeInjector`. Le chemin runtime, limité aux prédicats et
actions parser, construit son contexte dans `ExpressionEmbeddedCodePreparer`, appelle le même noyau,
construit ensuite les symboles et la lambda spécialisés, et conserve l'appel à
`IExpressionCompiler.Compile(...)`.

Les tests de caractérisation couvrent les quatre catégories générées et les deux catégories runtime,
les contextes et textes bruts exacts, l'appel unique, les échecs de transformation, la classification,
l'injection, la compilation unique et la réutilisation d'artefacts avec plusieurs contextes runtime.
Le garde-fou Roslyn `EmbeddedCodeTransformerArchitectureTests` interdit les appels directs parallèles
au transformer et vérifie que le noyau est interne, commun aux deux cibles, retourne le type transformé
et ne dépend d'aucun détail de cible. `CSharpEmbeddedCodeInjectorArchitectureTests` maintient la
frontière d'injection. Aucune API publique ni aucun comportement observable n'a été modifié.

Le point 10 est corrigé ci-dessous. Le point 11 (statut documenté de la façade runtime) reste
explicitement ouvert.

### 10. Distinguer explicitement les états brut et transformé
**Corrigé.** Le modèle interne unique `EmbeddedCodeHook` est désormais un record immuable qui conserve
le texte collecté dans `RawEmbeddedCode RawCode` et expose séparément
`TransformedEmbeddedCode? TransformedCode`. Les fabriques parser et lexer créent uniquement l’état
brut. Le collecteur effectue ensuite une transition explicite par expression `with`, après l’unique
appel à `EmbeddedCodeTransformationPipeline.TransformAndValidate(...)`; le code brut n’est ni écrasé
ni reconstruit.

Les quatre émetteurs parser/lexer délèguent toujours à `EmbeddedHookMethodEmitter`. Avant toute écriture
dans le `StringBuilder`, cet émetteur obtient le résultat par le point d’accès central
`RequireTransformedCode(...)`. Un hook encore brut provoque une `InvalidOperationException`
déterministe mentionnant le nom de méthode concerné : aucun fallback vers `RawCode` et aucune source
partielle ne sont possibles. `GeneratedEmbeddedCodeBody.ForPredicate(...)` et `ForAction(...)`
continuent ainsi de recevoir exclusivement un `TransformedEmbeddedCode` validé.

Le code brut est volontairement conservé après transformation pour les diagnostics, l’audit du flux
et les tests d’invariant. `LifecycleHook` n’a pas été modifié : il ne représente qu’un état déjà
transformé et ne portait donc pas la même ambiguïté. Les tests unitaires caractérisent la collecte
brute, la conservation du texte et l’échec d’émission avant transformation. Les garde-fous Roslyn
vérifient les types des deux propriétés, l’absence de noms de remplacement ambigus, la transition par
le pipeline commun, le point d’accès de phase unique, les entrées typées de
`GeneratedEmbeddedCodeBody` et l’absence de lecture de `RawCode.Text` par les émetteurs.

Cette correction est interne au générateur : elle ne modifie ni la source C# générée, ni le
comportement runtime, ni les diagnostics, ni les API publiques.

### 11. Documenter `ExpressionEmbeddedCodePreparer` comme façade unique de compilation runtime
La classe transforme le texte, appelle `IExpressionCompiler`, construit une lambda CLR puis produit
l'artefact préparé. Son rôle dépasse donc une simple préparation de métadonnées.

**Fix proposé** : préciser dans sa documentation et dans le README du projet qu'elle constitue la
façade supportée pour la compilation runtime du code embarqué, afin d'éviter l'introduction future
d'un pipeline de compilation parallèle.

## Garde-fous et contrôles (priorité basse)

### 12. Ajouter un contrôle statique contre l'injection directe de code brut

**Corrigé.** `CSharpEmbeddedCodeInjectorArchitectureTests` utilise une analyse sémantique Roslyn pour
interdire les écritures directes de `RawEmbeddedCode.Text`, `TransformedEmbeddedCode.Text` et
`SourceText` vers `Append` ou `AppendLine` hors de `CSharpEmbeddedCodeInjector`.

Le contrôle suit également les affectations dans des variables locales afin d'interdire les
contournements par alias. La seule lecture de texte transformé autorisée hors de l'injecteur est la
classification non injectante effectuée dans `GeneratedEmbeddedCodeBody.ForPredicate`.

Le garde-fou reste volontairement ciblé et audit-friendly. Si les chemins de génération deviennent
plus indirects, une analyse de flux plus générale pourra être envisagée sans remettre en cause la
frontière actuelle.

### 13. Uniformiser les exceptions de transformation

**Corrigé.** Les chemins de génération C# et de compilation runtime reposent sur
`Utils.Parser.Diagnostics.EmbeddedCode.ParserEmbeddedCodeTransformationException`.

L'exception commune expose le code et le message du diagnostic, le chemin de transformation, le
`ParserEmbeddedCodeLocation`, le nom de grammaire, le nom de règle, le span et l'exception interne
éventuelle. `ParserEmbeddedCodeTransformationService.TransformOrThrow` l'utilise pour les diagnostics
bloquants, les exceptions du transformer, les résultats nuls et les codes transformés nuls.

`Utils.Parser.Expressions.ParserEmbeddedCodeTransformationException` dérive du type commun afin de
préserver l'API publique du projet Expressions sans perdre les métadonnées structurées. Le constructeur
historique prenant uniquement un message reste conservé pour compatibilité, mais les chemins de
production utilisent la forme structurée.

### 14. Vérifier la documentation après chaque refactorisation du pipeline
Toute modification du comportement ou des frontières du code embarqué doit être répercutée dans les
documents imposés par `AGENTS.md`, notamment :

- `Utils.Parser/ROADMAP.md` ;
- `docs/parser/ANTLRCompatibility.md` ;
- `docs/parser/EmbeddedCodeExecutionModel.md` ;
- `docs/parser/EmbeddedCodeTransactionalState.md` ;
- `docs/parser/Antlr4CompatibilityMatrix.md` ;
- `Utils.Parser.Generators/README.md`.

Pour chaque PR, documenter explicitement les fichiers mis à jour ou la raison pour laquelle aucune
mise à jour n'était nécessaire.
