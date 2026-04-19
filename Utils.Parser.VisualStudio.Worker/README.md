# Utils.Parser.VisualStudio.Worker

`Utils.Parser.VisualStudio.Worker` est un exécutable worker utilisé par l'intégration Visual Studio pour la classification de tokens via un protocole JSON sur pipe nommé.

## Objectif

Découpler la classification/parsing du processus principal Visual Studio en exécutant la logique dans un processus dédié.

## Exemples

### 1) Exécuter le worker avec un nom de pipe

```bash
dotnet run --project Utils.Parser.VisualStudio.Worker/Utils.Parser.VisualStudio.Worker.csproj -- my-pipe-name
```

### 2) Construire le worker

```bash
dotnet build Utils.Parser.VisualStudio.Worker/Utils.Parser.VisualStudio.Worker.csproj
```

### 3) Format de message (JSON line)

Le worker lit des lignes JSON et répond en JSON (voir `WorkerProtocol.cs`) :

```json
{"assemblyPath":"...","typeName":"...","tokens":["if","var","customId"]}
```

Le point d'entrée et la gestion pipe se trouvent dans `Program.cs`.

## Projets liés

- [`Utils.Parser`](../Utils.Parser/README.md)
- [`Utils.Parser.VisualStudio`](../Utils.Parser.VisualStudio/README.md)
