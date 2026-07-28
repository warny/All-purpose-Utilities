# Préparation de la mise en production de `Utils.Parser`

## Objet

Ce document décrit les travaux restant à mener pour préparer une première mise en production de `Utils.Parser` et de `Utils.Parser.Generators`.

L’objectif n’est pas de terminer l’intégralité de la roadmap ANTLR4, mais de figer un périmètre de production fiable, documenté, testé et distribuable de manière reproductible.

## Positionnement recommandé

Une première mise en production est réaliste pour le périmètre suivant :

- grammaires maîtrisées et testées par l’équipe ;
- comportement conservateur de `ParserEngine` ;
- génération C# sans promesse de compatibilité ANTLR4 exhaustive ;
- fonctionnalités avancées activées explicitement ;
- constructions non prises en charge signalées par diagnostics ;
- absence de grammaires arbitraires provenant d’utilisateurs non fiables.

Le projet ne doit pas encore être présenté comme un remplacement général d’ANTLR4 capable de compiler et d’exécuter toute grammaire ANTLR4.

## Version cible

La première release candidate devrait utiliser une version de type :

```text
2.0.0-rc.1
```

La version `2.0.0` ne devrait être publiée qu’après :

- stabilisation explicite de l’API publique ;
- validation du contrat de compatibilité ;
- retour d’expérience sur une release candidate ;
- confirmation de la reproductibilité des packages ;
- validation des performances et de la sécurité.

---

# P0 — Bloquant avant une première release de production

## 1. Définir le premier contrat de production formalisé

**État : terminé pour la documentation de `2.0.0-rc.1`.**

Le contrat normatif est publié dans [`docs/parser/ProductionSupportContract.md`](docs/parser/ProductionSupportContract.md). Sa validation produit reste distincte des étapes fonctionnelles, de packaging et de qualité ci-dessous.

Créer une matrice de support courte et normative couvrant au minimum :

- chemin runtime avec `Antlr4GrammarProjectCompiler` ;
- chemin générateur avec `Utils.Parser.Generators` ;
- `Parse(...)` conservateur ;
- `ParseWithEmbeddedCode(...)` opt-in ;
- imports ;
- `tokenVocab` ;
- règles lexer et parser ;
- paramètres, retours et labels ;
- actions et prédicats ;
- modes lexer ;
- options ;
- diagnostics ;
- comportements non pris en charge.

Chaque fonctionnalité doit être classée comme :

- supportée et stable ;
- supportée sous option ;
- métadonnée seulement ;
- rejetée avec diagnostic ;
- expérimentale.

### Critère de sortie

Aucune fonctionnalité ne doit pouvoir être interprétée comme « supportée » uniquement parce qu’elle est parsée ou conservée dans le modèle.

## 2. Finaliser les imports dans le générateur

La composition des imports a été unifiée entre runtime et générateur, mais le générateur ne produit pas encore une définition effective contenant les imports.

Le travail restant doit couvrir au minimum :

- imports directs ;
- imports transitifs ;
- priorité locale ;
- collisions selon le plan commun ;
- règles parser ;
- règles lexer ;
- modes lexer ;
- `tokenVocab` ;
- tokens déclarés ;
- canaux ;
- racine locale ;
- provenance ;
- diagnostics ;
- suppression des éléments devenus obsolètes après retrait d’un import ;
- incrémentalité ciblée.

Après cette étape :

- [x] `GrammarEmitter` consomme une projection mécanique du plan commun ;
- [x] `APU0107` est réactivé pour les règles importées réellement émises et résolues avec certitude ;
- des tests de parité runtime/générateur doivent être ajoutés.

### Alternative temporaire

Si cette fonctionnalité n’est pas terminée avant la release, les imports doivent être explicitement exclus du périmètre du générateur de production et signalés clairement dans la documentation et les diagnostics.

## 3. Mettre en place un pipeline de release

Le pipeline de release doit au minimum exécuter :

```powershell
dotnet restore Utils.sln
dotnet build Utils.sln --configuration Release --no-restore
dotnet test UtilsTest/UtilsTest.Unit.csproj --configuration Release --no-build
dotnet test UtilsTest.Functional/UtilsTest.Functional.csproj --configuration Release --no-build
dotnet pack Utils.Parser/Utils.Parser.csproj --configuration Release --no-build
dotnet pack Utils.Parser.Generators/Utils.Parser.Generators.csproj --configuration Release --no-build
dotnet list Utils.sln package --vulnerable --include-transitive
```

Le pipeline doit également :

- publier les packages comme artefacts ;
- vérifier le contenu des `.nupkg` ;
- vérifier la présence des symboles ;
- valider SourceLink ;
- installer les packages dans un projet consommateur vierge ;
- compiler ce projet sans référence directe au dépôt ;
- tester les assets du générateur en tant qu’analyzer ;
- restaurer depuis un feed de staging ;
- conserver les résultats de tests ;
- conserver les résultats de couverture ;
- échouer en cas de régression de packaging.

## 4. Figer la version et la compatibilité

Avant la release candidate :

- définir les règles SemVer ;
- produire un changelog ;
- recenser les API publiques ;
- comparer les surfaces publiques entre versions ;
- interdire les ruptures silencieuses ;
- documenter les changements de comportement ;
- publier une politique de dépréciation ;
- publier des notes de migration lorsque nécessaire.

### Critère de sortie

Toute modification d’API publique doit être détectée et explicitement acceptée avant publication.

---

# P1 — À finaliser pendant la phase de release candidate

## 5. Nettoyer les warnings des projets publiés

Les projets suivants doivent être prioritaires :

- `Utils.Parser` ;
- `Utils.Parser.Antlr4.Common` ;
- `Utils.Parser.Diagnostics` ;
- `Utils.Parser.Source` ;
- `Utils.Parser.Expressions` ;
- `Utils.Parser.Generators`.

Les principales catégories à traiter sont :

- nullable reference types ;
- documentation XML ;
- analyzers Roslyn ;
- métadonnées de release des diagnostics ;
- warnings SourceLink ou repository metadata.

Approche recommandée :

```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Cette option peut être activée progressivement sur les projets publiés, après nettoyage des warnings existants.

### Critère de sortie

Le build Release des packages publiés doit passer sans warning non explicitement autorisé.

## 6. Actualiser l’audit de sécurité

L’audit doit être rejoué sur la version candidate.

Pour `Utils.Parser`, il faut tester particulièrement :

- grammaires très profondes ;
- récursions pathologiques ;
- quantificateurs produisant de nombreuses branches ;
- grands jeux de caractères ;
- très gros littéraux ;
- embedded code invalide ou hostile ;
- chemins de fichiers des imports ;
- graphes d’imports volumineux ;
- collisions de noms ;
- consommation mémoire ;
- temps d’exécution ;
- stack overflow ;
- comportement avec entrées malformées.

Il faut également préciser le modèle de confiance :

- grammaires de développeurs de confiance ;
- grammaires externes semi-fiables ;
- grammaires fournies par des utilisateurs.

### Critère de sortie

Le niveau de confiance accepté par le premier contrat de production doit être clairement documenté.

## 7. Ajouter une suite d’acceptation production

Créer une suite dédiée, par exemple :

```text
ProductionAcceptanceTests
```

Elle doit couvrir de bout en bout :

- grammaire combinée simple ;
- lexer et parser séparés ;
- import direct ;
- import transitif ;
- `tokenVocab` ;
- modes lexer ;
- priorité locale ;
- collision entre imports ;
- parsing réussi ;
- parsing invalide ;
- diagnostics attendus ;
- génération incrémentale ;
- compilation du C# généré ;
- exécution à partir des packages NuGet ;
- parité runtime/générateur sur le sous-ensemble garanti.

### Critère de sortie

Cette suite devient un gate obligatoire de release.

## 8. Établir une baseline de performances

Mesurer au minimum :

- temps de génération pour 1, 10, 50 et 100 grammaires ;
- temps de compilation incrémentale après modification d’un fichier ;
- temps de construction d’une définition ;
- débit de tokenisation ;
- débit de parsing ;
- allocations ;
- taille du code généré ;
- parsing répété ;
- parsing concurrent ;
- stabilité du cache `Grammar`.

Le but initial n’est pas d’optimiser, mais de détecter les régressions.

### Critère de sortie

Les résultats de référence doivent être conservés et comparables entre releases.

---

# P2 — Finalisation produit et exploitation

## 9. Produire une documentation d’entrée unique

La documentation doit proposer trois parcours principaux :

1. utiliser le runtime ;
2. utiliser le générateur ;
3. activer l’embedded code explicitement.

Chaque parcours doit inclure :

- installation avec version exacte ;
- exemple minimal complet ;
- diagnostics habituels ;
- limitations ;
- recommandations runtime/générateur ;
- durée de vie des objets ;
- comportement thread-safe ;
- politique de confiance des fichiers `.g4` ;
- options expérimentales ;
- comportement des imports.

Les exemples de production doivent toujours épingler une version exacte et ne doivent pas utiliser de version NuGet flottante.

## 10. Ajouter un projet consommateur packagé

Créer un projet de test ou un exemple qui :

- restaure les packages depuis un feed local ;
- référence `omy.Utils.Parser` ;
- référence `omy.Utils.Parser.Generators` comme analyzer ;
- utilise `AdditionalFiles` ;
- configure les métadonnées requises ;
- compile une grammaire ;
- exécute lexer et parser ;
- vérifie le résultat ;
- ne référence aucun projet source du dépôt.

### Critère de sortie

Le scénario doit réussir uniquement à partir des `.nupkg` produits.

## 11. Définir la politique des fonctionnalités expérimentales

Les fonctionnalités suivantes doivent rester désactivées par défaut ou explicitement qualifiées :

- binding automatique des arguments ;
- embedded code ;
- politiques de rule call ;
- lifecycle hooks ;
- embedded code lexer limité ;
- compilation d’expressions ;
- APIs préparatoires ;
- métadonnées non exécutables.

Pour chaque fonctionnalité :

- définir une option explicite ;
- documenter les risques ;
- documenter les limites ;
- produire un diagnostic en cas de support partiel ;
- éviter toute activation implicite lors d’une mise à jour.

## 12. Préparer l’exploitation et le support

Mettre en place :

- documentation des diagnostics ;
- index des codes d’erreur ;
- template de bug ;
- collecte de la version utilisée ;
- reproduction minimale de grammaire ;
- politique de stabilité des diagnostics ;
- trace optionnelle non intrusive ;
- SBOM ;
- provenance des packages ;
- éventuelle signature des packages ;
- procédure de retrait d’une release ;
- procédure de patch rapide ;
- politique de support des versions.

---

# Fonctionnalités ne bloquant pas le premier contrat de production

Les éléments suivants peuvent rester hors périmètre :

- shared-prefix execution ;
- continuation replay ;
- parsing parallèle ;
- runtime async ;
- GLL ;
- adaptive LL ;
- rollback complet des effets externes ;
- compatibilité ANTLR4 exhaustive ;
- signatures ANTLR typées générées ;
- expressions arbitraires dans les arguments ;
- support complet des actions lexer ;
- action buffering ;
- speculative action replay.

Ces limites doivent être documentées clairement, mais ne bloquent pas une première mise en production conservatrice.

---

# Séquence de PR recommandée

## PR 1 — Contrat de support

```text
docs(parser): define production support contract
```

Contenu :

- matrice normative ;
- statut stable/expérimental ;
- périmètre du premier contrat de production ;
- limitations ;
- critères d’acceptation.

## PR 2 — Imports générés

```text
feat(generator): emit effective imported grammar composition
```

Contenu :

- consommation du plan commun ;
- règles parser ;
- règles lexer ;
- modes ;
- `tokenVocab` ;
- provenance ;
- diagnostics ;
- incrémentalité ;
- parité runtime/générateur.

## PR 3 — Acceptation et packaging

```text
test(parser): add packaged production acceptance suite
```

Contenu :

- feed local ;
- projet consommateur ;
- tests `.nupkg` ;
- tests end-to-end ;
- vérification des analyzer assets.

## PR 4 — Gates qualité

```text
build(parser): enforce release quality gates
```

Contenu :

- warnings ciblés ;
- vulnérabilités ;
- SourceLink ;
- contenu des packages ;
- reproductibilité ;
- baseline de performances.

## PR 5 — Release candidate

```text
chore(parser): prepare 2.0.0-rc.1
```

Contenu :

- version ;
- changelog ;
- notes de migration ;
- artefacts ;
- résultats de tests ;
- résultats de sécurité ;
- publication sur feed de staging.

---

# Checklist de readiness

## Fonctionnel

- [x] Contrat de support de `2.0.0-rc.1` défini (validation produit restante)
- [ ] Imports générés finalisés ou explicitement exclus
- [ ] Parité runtime/générateur validée
- [ ] Diagnostics des fonctionnalités partielles validés
- [ ] Fonctionnalités expérimentales désactivées par défaut

## Qualité

- [ ] Build Release sans warning sur les projets publiés
- [ ] Tests unitaires réussis
- [ ] Tests fonctionnels réussis
- [ ] `ProductionAcceptanceTests` réussis
- [ ] Tests incrémentaux réussis
- [ ] Tests de concurrence réussis

## Packaging

- [ ] Packages générés
- [ ] Contenu des `.nupkg` vérifié
- [ ] Symboles présents
- [ ] SourceLink validé
- [ ] Projet consommateur vierge validé
- [ ] Feed de staging validé

## Sécurité

- [ ] Audit NuGet vulnérabilités réussi
- [ ] Audit parser actualisé
- [ ] Modèle de confiance documenté
- [ ] Tests de profondeur et volumétrie réussis
- [ ] Tests de chemins d’import réussis

## Produit

- [ ] README de production finalisé
- [ ] Matrice de compatibilité publiée
- [ ] Changelog publié
- [ ] Notes de migration publiées
- [ ] Politique SemVer publiée
- [ ] Politique de support publiée

## Exploitation

- [ ] Diagnostics indexés
- [ ] Template de bug disponible
- [ ] Procédure de retrait d’une release
- [ ] Procédure de patch rapide
- [ ] SBOM générée
- [ ] Provenance des packages conservée

---

# Décision de release

La release peut être autorisée lorsque :

1. le périmètre du premier contrat de production est figé ;
2. les imports générés sont finalisés ou officiellement exclus ;
3. les packages sont testés comme produits consommables ;
4. les gates qualité et sécurité passent ;
5. une release candidate a été validée dans un environnement réel ;
6. aucune fonctionnalité partielle n’est présentée comme complète.

La première cible recommandée reste :

```text
2.0.0-rc.1
```

## Packaged product train acceptance

The `omy-product-train-2.0.0-rc.1` release manifest now fixes the candidate set and topological order:

1. `omy.Utils` and `omy.Utils.Parser.Source` 2.0.0-rc.1;
2. `omy.Utils.Parser.Diagnostics` and `omy.Utils.Parser.Antlr4.Common` 2.0.0-rc.1;
3. `omy.Utils.Parser` 2.0.0-rc.1;
4. `omy.Utils.Parser.Expressions` and `omy.Utils.Parser.Generators` 2.0.0-rc.1.

`eng/test-packaged-product-train.ps1` builds, explicitly packs only the manifest, inspects the exact archives, restores isolated consumers, compiles, executes, publishes the Utils consumer, checks generator option combinations, and audits restored dependencies. It never pushes packages. The generator embeds Source, Diagnostics, and Antlr4.Common beside the analyzer; those assemblies are intentionally not runtime dependencies. NuGet multi-package publication is not transactional, so a future publication workflow must publish these already-validated artifacts in topological order and report partial publication.

SourceLink commit validation requires a checkout with remote source-control metadata. Reproducibility, signing, trimming/AOT, performance, and full cross-platform compatibility remain release gates rather than claims established by this suite.

The packaged gate now executes the shared import-composition contract through both `Antlr4GrammarProjectCompiler` and the NuGet analyzer. Its real incremental consumer modifies imported grammar content and graph edges between builds and verifies obsolete effective rules disappear. Symbol checks cover repository identity and portable PDB presence only; SourceLink mapping/retrieval is still pending.

## Global release-chain integration

The parser acceptance scenarios remain specialized parser evidence, but their packages now participate in the repository-wide 24-package `omy.Utils` 2.0.0-rc.1 train. Package discovery, graph ordering, inspection, API/warning/SourceLink/reproducibility gates, hashes, and dry-run publication are owned by `eng/product-train-manifest.json` and the global release-quality scripts.
