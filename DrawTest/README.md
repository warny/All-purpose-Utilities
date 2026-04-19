# DrawTest

`DrawTest` est une application Windows Forms de démonstration pour les primitives de dessin de `Utils.Imaging` et `Utils.Fonts`.

## Objectif

Ce projet permet de visualiser rapidement des tracés (lignes, courbes de Bézier, ellipses, texte) pour valider le rendu graphique.

## Exemples

### 1) Lancer l'application

```bash
dotnet run --project DrawTest/DrawTest.csproj
```

### 2) Construire uniquement le projet

```bash
dotnet build DrawTest/DrawTest.csproj
```

### 3) Explorer un exemple de rendu

Le rendu principal se trouve dans `TestForm.Draw()` :
- tracés de lignes et de courbes,
- rendu de texte vectoriel,
- remplissages et contours.

Voir le code : `DrawTest/TestForm.cs`.

## Projets liés

- [`Utils.Imaging`](../Utils.Imaging/README.md)
- [`Utils.Fonts`](../Utils.Fonts/README.md)
- [`Utils`](../Utils/README.md)
