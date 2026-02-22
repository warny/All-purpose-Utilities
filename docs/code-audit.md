# Audit global du code — All-purpose-Utilities

Date: 2026-02-21
Mise à jour: 2026-02-21

## Objectif de ce document

Ce document ne liste plus que les éléments **restant à traiter** après les actions déjà réalisées sur:

- la centralisation des métadonnées NuGet,
- la suppression du projet backup,
- la clarification du périmètre packable,
- l'adaptation du workflow de publication NuGet.

## Reste à faire

### 1) Dette technique explicite dans `Utils.Fonts`

Des `TODO` indiquent des sous-tables non implémentées pour la lecture/écriture des tables de polices.

Actions recommandées:

- créer des issues dédiées pour chaque TODO,
- documenter explicitement les limites actuelles dans `Utils.Fonts/README.md`,
- ajouter des tests de non-régression au moment de l'implémentation.

### 2) Documentation de compatibilité TFM

Les TFMs présents dans le dépôt sont `net8.0`, `net9.0`, `netstandard2.0` et `net8.0-windows`.

Actions recommandées:

- publier une matrice de compatibilité claire par package dans `docs/getting-started.md`,
- distinguer explicitement les packages runtime, generators et projets applicatifs d'exemple,
- préciser la politique de support minimal côté consommateurs.

### 3) Renforcement qualité CI / analyzers

Le dépôt gagnerait à homogénéiser les contrôles de qualité entre modules.

Actions recommandées:

- activer/standardiser les analyzers .NET au niveau solution,
- définir une stratégie progressive pour transformer certains warnings en erreurs,
- ajouter un contrôle CI dédié aux conventions (docs + packaging + analyzers).

## Plan proposé

### Court terme (1-2 sprints)

- transformer les TODO critiques en tickets priorisés,
- compléter `docs/getting-started.md` avec une matrice TFM exploitable,
- ajouter un premier niveau de contrôle CI de conformité.

### Moyen terme (1-2 mois)

- harmoniser les règles analyzers à l'échelle de la solution,
- formaliser la politique de support par famille de packages.

### Long terme

- suivre des indicateurs de dette technique et de qualité (stabilité build, warning trend, couverture ciblée).
