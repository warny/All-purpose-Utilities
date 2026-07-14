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
**Corrigé.** Les hooks parser et lexer générés sont désormais représentés par un seul type interne
`GrammarEmitter.EmbeddedCodeHook`. L'ancien type `LexerEmbeddedCodeHook` a été supprimé. Le type
commun conserve le code brut dans `RawEmbeddedCode RawCode` et le code transformé prêt à l'émission
dans `TransformedEmbeddedCode EmittedCode`, sans remplacer ces frontières typées par des chaînes.

La distinction de domaine est explicite grâce à deux discriminants internes :

- `EmbeddedCodeHookOwner` distingue `Parser` et `Lexer` ;
- `EmbeddedCodeHookKind` distingue `SemanticPredicate` et `InlineAction`.

Les quatre catégories sont donc couvertes sans booléen ambigu comme état principal : prédicat parser,
action inline parser, prédicat lexer et action inline lexer. La construction passe par les fabriques
`CreateParser(...)` et `CreateLexer(...)`, qui valident le nom de règle, le code brut, les indices
acceptés (`-1` comme sentinelle historique ou valeur positive/nulle), le nom de méthode générée et les
valeurs d'enum. La lecture de `EmittedCode` avant transformation continue d'échouer, et l'affectation
d'un code transformé nul reste rejetée.

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
des parcours récursifs, formalisation globale du pipeline, renommage complet de `EmittedCode` et
documentation de la façade runtime ne sont pas traités par cette correction.

### 7. Factoriser la construction des symboles d'expressions
`BuildSemanticPredicateSymbols` et `BuildParserActionSymbols`, ainsi que leurs méthodes `Add...Symbol`,
répètent la gestion de `RuleName`, `InputPosition`, `AlternativeIndex` et `ElementIndex`.

**Fix proposé** : conserver ce chantier comme une factorisation locale et limitée de
`ExpressionEmbeddedCodePreparer`. Créer un constructeur privé commun de symboles exploitant les
propriétés partagées par `SemanticPredicateEvaluationContext` et `ParserActionExecutionContext`, avec
des wrappers spécialisés courts si ceux-ci améliorent la lisibilité.

Ne pas introduire une interface publique uniquement pour ces quatre propriétés. Une petite stratégie
privée ou interne n'est justifiée que si l'audit révèle de vraies différences de résolution entre
plusieurs familles de contextes runtime. Ce point ne doit pas servir à anticiper la mutualisation plus
large parser/lexer décrite au point 8.

### 8. Introduire des moteurs communs avec stratégies parser/lexer spécialisées
Plusieurs zones de `GrammarEmitter` répètent le même algorithme général et ne diffèrent que par les
règles propres au parser ou au lexer :

- collecte récursive des hooks embarqués ;
- calcul ou propagation des indices d'alternative et d'élément ;
- choix des emplacements de transformation ;
- conventions de nommage des méthodes générées ;
- génération des dispatchers runtime de prédicats et d'actions ;
- génération des méthodes de hooks et de leurs signatures.

Les différences sont réelles et doivent rester explicites : ordre et priorité des alternatives,
récursion gauche parser, traitement des quantificateurs et négations, modes lexer, types de contexte,
signatures, types de résultat et appels de fallback.

**Direction architecturale retenue** : utiliser la composition sous la forme suivante :

1. un moteur commun porte l'algorithme stable ;
2. une stratégie spécialisée parser ou lexer fournit uniquement les décisions différentes ;
3. un petit contexte immuable transporte l'état variable du parcours, par exemple le nom de règle,
   l'index d'alternative et l'index d'élément ;
4. des descripteurs immuables sont préférés aux interfaces lorsque les différences sont uniquement des
   valeurs de génération ;
5. les wrappers parser et lexer restent explicites et choisissent la stratégie ou le descripteur
   approprié.

Exemples de rôles envisagés, sans imposer prématurément leurs noms exacts :

- `EmbeddedHookCollector` pour le parcours commun ;
- `IEmbeddedHookCollectionStrategy` avec implémentations parser et lexer ;
- `HookTraversalPosition` comme valeur immuable de récursion ;
- un descripteur d'émission pour les dispatchers et méthodes de hooks.

Le moteur commun ne doit pas contenir de booléen `isLexer` ni de gros `switch` central sur le domaine.
Les stratégies ne doivent pas réimplémenter l'algorithme entier : elles répondent uniquement aux
points de variation demandés par le moteur. Préférer la composition à une classe de base abstraite
comportant de nombreuses méthodes virtuelles.

La mise en œuvre doit être progressive et découpée en PR vérifiables :

- **8a — collecte** : extraire le parcours commun et les décisions parser/lexer ;
- **8b — dispatchers** : factoriser la structure commune des évaluateurs/exécuteurs générés ;
- **8c — méthodes de hooks** : factoriser les signatures et corps communs à l'aide de descripteurs ;
- **8d — consolidation** : supprimer les helpers devenus redondants et verrouiller l'architecture avec
  des tests Roslyn.

Chaque étape doit préserver strictement les indices runtime, l'ordre des hooks, les noms générés, les
appels au transformer, la source C# produite et le comportement des fallbacks. La récursion gauche et
les particularités lexer restent des stratégies explicites, pas des branches cachées dans le moteur.

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