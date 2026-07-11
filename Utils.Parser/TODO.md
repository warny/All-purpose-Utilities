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
`GrammarEmitter.TransformEmbeddedCode` et `ExpressionEmbeddedCodePreparer.TransformSource` réalisent
la même séquence : construction du contexte, appel à `IParserEmbeddedCodeTransformer`, recherche du
premier diagnostic d'erreur, conversion en exception, puis récupération du code transformé.

Les deux chemins utilisent actuellement des types d'exception et des messages différents.

**Fix proposé** : extraire un service commun de transformation, par exemple
`ParserEmbeddedCodeTransformationService.TransformOrThrow`, dans le périmètre
`Utils.Parser.Diagnostics.EmbeddedCode`.

### 3. Créer une classe dédiée à l'injection du code C#
`GrammarEmitter` assure aujourd'hui à la fois la collecte des fragments, leur transformation, leur
classification, leur indentation et leur écriture dans le `StringBuilder` généré.

Cette organisation respecte actuellement le passage par le transformer, mais ne rend pas cet
invariant structurel : une nouvelle méthode pourrait injecter directement `RawCode` ou `SourceText`.

**Fix proposé** : introduire une classe interne dédiée, par exemple
`CSharpEmbeddedCodeInjector`, responsable de :

- l'injection des headers, members et footers ;
- l'injection des corps de méthodes d'action ;
- l'injection des expressions ou blocs de prédicat ;
- la normalisation des fins de ligne et de l'indentation ;
- les marqueurs de début et de fin du code injecté.

`GrammarEmitter` ne devrait transmettre à cet injecteur que du code déjà transformé.

### 4. Ajouter des tests d'invariant pour tous les emplacements de code embarqué
Les tests doivent couvrir au minimum :

- parser header, members et footer ;
- lexer header, members et footer ;
- action parser et prédicat parser ;
- action lexer et prédicat lexer ;
- hooks `@init` et `@after` ;
- compilation runtime des actions et prédicats sous forme d'expressions.

**Fix proposé** : utiliser un transformer espion qui remplace chaque fragment par un marqueur unique
et vérifier que :

- chaque fragment produit exactement un appel au transformer ;
- le bon `ParserEmbeddedCodeLocation` est transmis ;
- le texte brut n'apparaît jamais dans la sortie générée ou dans l'entrée du compilateur ;
- le texte transformé est injecté ou compilé exactement une fois.

Après introduction de `CSharpEmbeddedCodeInjector`, ajouter également un injecteur espion pour
vérifier que tout code C# transformé passe par cette classe.

## Duplications de code (priorité moyenne)

### 5. Factoriser les named actions parser et lexer
`GrammarEmitter.ParserNamedActions.cs` et `GrammarEmitter.LexerNamedActions.cs` contiennent chacun les
mêmes familles de méthodes : collecte et émission des headers, members et footers.

Les différences portent principalement sur :

- le prédicat de sélection dans `EmbeddedMembersSupport` ;
- le `ParserEmbeddedCodeLocation` ;
- les marqueurs générés ;
- l'indentation.

**Fix proposé** : introduire un descripteur de point d'injection et une méthode commune
d'émission. Conserver éventuellement des wrappers courts et descriptifs tels que
`EmitParserHeaders` ou `EmitLexerMembers`.

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
Un test architectural ou un analyseur Roslyn pourrait interdire l'utilisation directe de `RawCode`,
`SourceText` ou de propriétés équivalentes dans des appels `Append`/`AppendLine` hors des composants
autorisés.

**Fix proposé** : commencer par un test statique ciblé et audit-friendly, puis envisager un analyseur
Roslyn interne si les chemins de génération continuent à se multiplier.

### 13. Uniformiser les exceptions de transformation
Les erreurs du transformer sont aujourd'hui converties différemment selon le chemin de génération ou
de compilation.

**Fix proposé** : utiliser une exception commune contenant au minimum le code du diagnostic, son
message, l'emplacement du code embarqué, le nom de grammaire et le nom de règle lorsqu'ils sont
disponibles.

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
