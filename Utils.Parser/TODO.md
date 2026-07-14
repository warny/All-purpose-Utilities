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
`RawEmbeddedCode`, les hooks générés conservent le code brut dans `RawCode`, et leur `EmittedCode`
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
- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_WhenEmittedCodeIsReadBeforeTransformation_Throws`.

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
Ces deux classes possèdent les mêmes données : nom de règle, code brut, code émis, nature
prédicat/action, indexes d'alternative et d'élément, et nom de méthode générée.

**Fix proposé** : utiliser une structure commune avec un discriminant explicite parser/lexer ou un
type de domaine équivalent.

### 7. Factoriser la construction des symboles d'expressions
`BuildSemanticPredicateSymbols` et `BuildParserActionSymbols`, ainsi que leurs méthodes `Add...Symbol`,
répètent la gestion de `RuleName`, `InputPosition`, `AlternativeIndex` et `ElementIndex`.

**Fix proposé** : créer une méthode privée commune exploitant les noms de propriétés partagés par les
contextes d'exécution. Éviter d'ajouter une interface publique uniquement pour cette factorisation si
elle n'apporte pas d'autre bénéfice architectural.

### 8. Mutualiser prudemment le parcours des contenus parser et lexer
Les parcours récursifs parser et lexer traitent les mêmes types de nœuds (`G4Alternation`,
`G4Alternative`, `G4Sequence`, `G4Quantifier`, `G4Negation`, `G4EmbeddedAction`).

Les règles d'indexation runtime du parser diffèrent toutefois de celles du lexer, notamment pour les
quantificateurs, la négation et la récursion gauche.

**Fix proposé** : n'extraire qu'un mécanisme de visite neutre ou des helpers réellement communs. Ne
pas fusionner les deux parcours au moyen de booléens ou d'une logique conditionnelle qui masquerait
les invariants runtime.

## Duplications d'intention et lisibilité (priorité moyenne)

### 9. Clarifier la frontière entre préparation, transformation, injection et compilation
Plusieurs composants participent à une même intention sans pipeline central explicite :
`TransformEmbeddedCode`, `TransformSource`, `GeneratedEmbeddedCodeBody`, les méthodes `Emit...` et
`ExpressionEmbeddedCodePreparer`.

**Fix proposé** : formaliser le pipeline suivant :

1. création du contexte de transformation ;
2. transformation et validation ;
3. remise du code transformé à une cible unique ;
4. injection C# via `CSharpEmbeddedCodeInjector` ou compilation via `IExpressionCompiler`.

### 10. Renommer ou supprimer l'état ambigu `EmittedCode`
`EmittedCode` est initialisé avec le code brut avant d'être remplacé par le code transformé. Son nom
indique pourtant qu'il est déjà prêt pour l'émission.

**Fix proposé** : remplacer ce membre par `RawCode` et `TransformedCode`, ce dernier étant absent tant
que la transformation n'a pas été effectuée, ou construire directement les hooks dans un état
transformé valide.

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
