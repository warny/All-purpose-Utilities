# Fractals

`Fractals` est une application Windows Forms de démonstration qui génère des fractales avec les bibliothèques utilitaires du dépôt.

## Objectif

Permettre un test visuel rapide des algorithmes de calcul/rendu de fractales (Mandelbrot, Julia).

## Exemples

### 1) Lancer l'application

```bash
dotnet run --project Fractals/Fractals.csproj
```

### 2) Construire le projet

```bash
dotnet build Fractals/Fractals.csproj
```

### 3) Explorer les implémentations

- Logique de calcul : `Fractals/ComputeFractal.cs`
- Types de fractales : `Fractals/IFractal.cs`
- Formulaire principal : `Fractals/FactalsForm.cs`

## Projets liés

- [`Utils.Imaging`](../Utils.Imaging/README.md)
- [`Utils`](../Utils/README.md)
