# Audit de sécurité — All-purpose-Utilities

Date: 2026-02-23
Portée: revue statique ciblée des composants exposés à des entrées externes (authentification, DNS, chargement réseau/XML, réflexion).

## Objectif de ce document

Ce document ne liste plus que les éléments restants après les remédiations sécurité.

## Statut actuel

Les points précédemment ouverts dans cet audit sont maintenant couverts:

1. **Durcissement explicite des entrées XML non fiables**
   - Ajout d'une entrée sécurisée dédiée avec paramètres parser durcis.
   - Conservation de l'entrée historique pour compatibilité, marquée obsolète afin de guider vers l'entrée sécurisée.

2. **Vérification continue sécurité dépendances**
   - Intégration d'un scan dépendances vulnérables dans la CI (`dotnet list Utils.sln package --vulnerable --include-transitive`).

## Reste à faire

- **Aucun point de sécurité ouvert** dans ce périmètre d'audit au moment de cette mise à jour.

## Limites de cet audit

- Audit statique partiel (pas de revue exhaustive de tous les projets).
- Pas de test dynamique/pentest.
