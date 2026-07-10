# Utils.Reflection — Propositions d'amélioration (relecture 2026-07-09)

Relecture complète du package (`Reflection/`, `Reflection/Emit/`, `ProcessIsolation/`) axée sécurité,
qualité de code et ergonomie d'interface. Tous les points ont été traités (implémentés, documentés, ou
sciemment écartés avec justification) et couverts par des tests de non-régression.

## Priorité haute — sécurité

### 1. ~~`ProcessContainerPermissions.Default` est un singleton mutable~~ — **implémenté**
`Default` est désormais une propriété qui retourne une **nouvelle instance** à chaque accès, et toutes
les propriétés (`AllowDiskRead`, `AllowDiskWrite`, `AllowNetwork`, `AllowDeviceAccess`,
`AllowProcessDebugging`) sont passées en `init`-only. Il n'est plus possible de muter une instance
après construction — la personnalisation se fait uniquement via l'initialiseur d'objet
(`new ProcessContainerPermissions { AllowNetwork = true }`), ce qui élimine le risque d'affaiblissement
silencieux et partagé de l'isolation par défaut. `Utils.Parser.VisualStudio.Worker.PluginWorkerProcess.
LoadPermissionsFromEnvironment` (seul appelant mutant existant) a été adapté pour construire l'instance
en une seule fois plutôt que par assignations successives.

### 2. ~~`EmitDllMappableClass` construit du C# par concaténation de chaînes non validées~~ — **implémenté**
Plutôt que de valider les identifiants (option initialement envisagée), l'exécution du code généré a
été déplacée dans un **process isolé** :
- `LibraryMapper.Emit<TInterface>` est désormais le chemin **sûr par défaut** : il valide que
  l'interface ne contient que des types représentables en JSON (`CrossProcessMarshaling`), lance une
  copie du process courant en tant que worker (auto-hébergement — voir `LibraryMapper.
  RunWorkerIfRequested`, à appeler en tout premier dans le `Main` de l'application hôte), sous le
  sandbox le plus restrictif disponible (`ProcessContainerFactory`), puis retourne un proxy
  `DispatchProxy` (`EmitWorkerProxy`) qui relaie chaque appel via un tube nommé (JSON ligne par ligne,
  `Reflection/Emit/EmitWorkerMessages.cs`). Le worker (`EmitWorkerHost`) exécute la génération de code
  + le chargement de la DLL native + les appels réels — donc l'injection potentielle décrite ci-dessus
  ne peut s'exécuter qu'avec les permissions du sandbox, jamais avec la confiance totale du process
  appelant.
- Le comportement historique (génération + chargement dans le process courant, sans isolation) reste
  disponible via `LibraryMapper.EmitInProcess<TInterface>`, marqué `[Experimental("UTILSREFL001")]` :
  tout appel non explicitement acquitté (`#pragma warning disable UTILSREFL001` ou `<NoWarn>`) fait
  échouer la compilation de l'appelant (confirmé : c'est une **erreur** de compilation par défaut, pas
  un simple warning — comportement voulu et validé par le mainteneur). `EmitDllMappableClass.
  Emit<TInterface>`/`Emit(Type, CallingConvention)` portent la même annotation, pour les mêmes raisons.
- **Portée du pont inter-process** (choix validé) : primitives, `string`, `enum`, tableaux 1D et structs
  composés uniquement de ces types (support étendu). `IntPtr`/`UIntPtr`/pointeurs bruts et tout type
  référence autre que `string`/tableau sont explicitement rejetés (`CrossProcessMarshaling`), avec un
  message d'erreur renvoyant vers `EmitInProcess<TInterface>`.
- **Limite connue** : le packaging du worker retenu (auto-hébergement) exige que l'application
  consommatrice appelle `LibraryMapper.RunWorkerIfRequested(args)` en tout début de son `Main` ; sans
  cette intégration, `Emit<TInterface>` échoue au lancement du worker. Documenté dans le README.
- Tests : `UtilsTest/Reflection/CrossProcessMarshalingTests.cs`, `EmitWorkerProtocolTests.cs`,
  `EmitWorkerProxyTests.cs`, `UtilsTest.Functional/Reflection/EmitWorkerHostLoopTests.cs`,
  `ProcessContainerPermissionsTests.cs`. Le lancement réel d'un second process OS via
  `EmitWorkerProcess`/`LibraryMapper.Emit<TInterface>` n'est pas couvert par un test automatisé — même
  limite déjà acceptée pour `PluginWorkerProcess` dans ce dépôt (le test host `dotnet test` ne peut pas
  jouer le rôle d'exécutable auto-hébergeant un `Main` personnalisé).

### 3. ~~`MacOsSandboxExecContainer.BuildProfile` autorise toujours `(allow process*)` et `(allow file-read*)`~~ — **documenté**
Option retenue : documentation explicite plutôt que restriction (une restriction correcte demanderait
de suivre les répertoires accordés et de générer des clauses `subpath`/`literal` ciblées — non fait
faute de pouvoir tester sur macOS réel dans cet environnement). La XML doc de la classe et un
commentaire inline dans `BuildProfile` expliquent maintenant clairement que `file-read*`/`process*`
sont toujours actifs, que `GrantDirectoryReadAccess` est un no-op pour cette raison, et que
`AllowProcessDebugging` a un effet limité puisque `process*` couvre déjà l'essentiel de `process-info*`.
Le README (section « Process isolation ») documente aussi l'asymétrie de portée de lecture de fichiers
entre Windows et Linux/macOS. `BuildProfile` est devenu `internal static` (prend les permissions en
paramètre plutôt que de lire un champ d'instance) pour être testable sans macOS — voir item 24.

### 4. ~~`LinuxBubblewrapContainer` monte tout `/` en lecture seule dans le bac à sable~~ — **documenté**
Même choix que le point 3 : documentation explicite (XML doc de classe + `GrantDirectoryReadAccess`)
plutôt que binding restreint (qui exigerait de connaître à l'avance tous les répertoires nécessaires au
runtime .NET — risque de casser des déploiements réels sans pouvoir tester sur Linux dans cet
environnement). `LinuxBubblewrapContainer.BuildArguments` est devenu `internal static` (testable sans
Linux, voir item 24).

### 5. ~~Aucun nettoyage de l'environnement du process enfant (Linux/macOS)~~ — **implémenté**
Nouvelle classe `ProcessIsolation/SandboxedProcessEnvironment.cs` : `ApplyMinimalEnvironment(psi)` vide
`ProcessStartInfo.EnvironmentVariables` puis ne réinjecte qu'une liste blanche (`PATH`, `HOME`,
`TMPDIR`, `LANG`, `LC_ALL`, `USER`, `LOGNAME`, `TERM`, plus tout préfixe `DOTNET_`/`CORECLR_` pour ne
pas casser la résolution du runtime .NET — important puisque le worker Emit isolé du point 2 est un
process .NET auto-hébergé). Branché dans `LinuxBubblewrapContainer.StartProcess` et
`MacOsSandboxExecContainer.StartProcess`. Tests : `SandboxedProcessEnvironmentTests.cs`.

### 6. ~~`HasValidAuthenticodeSignature` retourne `true` sans vérification sur macOS/Linux~~ — **implémenté**
Lève désormais `PlatformNotSupportedException` sur les plateformes non-Windows au lieu de retourner
`true` silencieusement. Vérifié sans risque de régression : le seul appelant du dépôt
(`Utils.Parser.VisualStudio.Worker.PluginAssemblyVerifier.Filter`) court-circuite déjà tout appel sur
non-Windows via `if (!OperatingSystem.IsWindows()) return paths;` avant d'atteindre cette méthode.

### 7. ~~`IsExpectedNamedPipeClient` retourne toujours `true` sur non-Windows~~ — **documenté**
Implémenter la vérification Unix (`SO_PEERCRED`/`getpeereid` via P/Invoke libc) a été jugé disproportionné
sans environnement Linux/macOS pour tester le P/Invoke correspondant. La XML doc documente maintenant
explicitement cette limitation : sur non-Windows, la méthode ne vérifie rien et retourne toujours `true`,
ce qui doit être pris en compte par tout appelant s'appuyant dessus pour un durcissement IPC.

### 8. ~~`AppContainerSandbox.TryCreate` ignore l'échec de création du Job Object~~ — **implémenté**
Si `CreateConfiguredJobObject()` échoue (`IntPtr.Zero`), le SID déjà créé est libéré
(`WindowsNativeMethods.FreeSid`) et `TryCreate` retourne `null` au lieu de construire un sandbox sans
garantie `KillOnJobClose`, cohérent avec l'échec de création du profil AppContainer.

### 9. ~~`AssignProcessToJobObject` : valeur de retour ignorée~~ — **implémenté**
`StartProcessInternal` vérifie maintenant le retour booléen. En cas d'échec, les handles sont fermés, le
process fraîchement créé est **terminé** (nouvelle méthode `TerminateOrphanedProcess`, best-effort) pour
ne jamais renvoyer à l'appelant un process qu'il croit contenu mais qui ne l'est pas, et une
`InvalidOperationException` explicite est levée avec le code d'erreur Win32.

## Priorité moyenne — qualité de code

### 10. ~~`EmitDllMappableClass.emittedLibraries` n'est pas thread-safe~~ — **implémenté**
`Dictionary<(Type, CallingConvention), Type>` → `ConcurrentDictionary<(Type, CallingConvention),
Lazy<Type>>`. Une première tentative avec `ConcurrentDictionary<..., Type>.GetOrAdd` (factory pouvant
s'exécuter deux fois sous contention) s'est révélée **insuffisante** : le test de concurrence ajouté
(item 25) a démontré que deux compilations concurrentes pour la même clé produisent deux assemblies
distinctes portant le **même nom simple** (dérivé du nom de l'interface), et
`AssemblyLoadContext.Default.LoadFromStream` lève `FileLoadException` (« Assembly with same name is
already loaded ») au second chargement. Le `Lazy<Type>` avec `LazyThreadSafetyMode.
ExecutionAndPublication` garantit que la compilation + le chargement ne s'exécutent **qu'une seule
fois** par clé, même sous contention concurrente.

### 11. ~~Type émis introuvable mis en cache tel quel (`null`)~~ — **implémenté**
`CompileMappingType` (extrait de l'ancien corps d'`Emit`) lève désormais une `InvalidOperationException`
explicite si le type généré est introuvable après compilation, plutôt que de retourner `null`. Combiné
au fix de l'item 10 (`Lazy<Type>`), un échec de compilation est capturé une seule fois et relevé de
façon cohérente à chaque appel ultérieur pour la même clé (comportement standard de `Lazy<T>` :
l'exception est mise en cache et rejetée à chaque accès à `.Value`).

### 12. ~~Résolution des références d'assembly fragile dans `Compile`~~ — **implémenté**
`GenerateCode`/nouvelle méthode `BuildReferences` : préfère désormais la liste
`AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")` (peuplée par le runtime .NET pour les déploiements
standards) plutôt que `Assembly.Load("System.Runtime")`/`Assembly.Load("netstandard")` par nom court,
avec repli sur l'ancien comportement si cette donnée n'est pas peuplée (hosts non standards). Dédoublonnage
des chemins pour éviter les références en double. Vérifié par un test de fumée réel (compilation +
chargement + appel natif effectif via `kernel32.dll`, avant et après le changement).

## Priorité moyenne — qualité de code (suite)

### 13. ~~`AppContainerSandbox.GrantDirectoryReadAccess` avale toute exception silencieusement~~ — **implémenté**
Le `catch` nu est remplacé par `catch (Exception ex) when (ex is IOException or
UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)`, avec
`Trace.TraceWarning` journalisant le chemin et l'exception complète avant de continuer (best-effort,
mais plus silencieux).

### 14. ~~Fuite mémoire non managée dans `VerifyAuthenticode`~~ — **implémenté**
Ajout de `Marshal.DestroyStructure<WindowsNativeMethods.WINTRUST_FILE_INFO>(fileInfoPtr)` avant
`Marshal.FreeHGlobal(fileInfoPtr)`, pour libérer le bloc marshalé séparément pour `pcwszFilePath`.

### 15. ~~`AppContainerSandbox` n'a pas de finaliseur~~ — **implémenté**
Pattern `Dispose(bool)` standard ajouté avec `~AppContainerSandbox() => Dispose(false)`, cohérent avec
`LibraryMapper`.

### 16. ~~Incohérence documentation/API sur `Platform.NativeULongSize`~~ — **implémenté**
Setter passé de `private` à `public`, cohérent avec `StructPackingSize` et avec ce que la documentation
affirmait déjà.

### 17. ~~Erreurs peu explicites dans `LibraryMapper.MapLibraryToInstance`~~ — **implémenté**
Trois validations explicites ajoutées avant l'appel natif : le type du membre `[External]` doit être un
type délégué (`InvalidOperationException` nommant le membre sinon), `Marshal.
GetDelegateForFunctionPointer` est enveloppé pour ré-emballer une `ArgumentException` avec le nom du
membre/de la fonction native, et une propriété `[External]` sans setter lève désormais un message
explicite plutôt que l'exception générique de `PropertyInfo.SetValue`. Vérifié par test de fumée réel
(chemin `EmitInProcess` + `kernel32.dll`, avant/après).

## Priorité basse — API / ergonomie

### 18. ~~`CommandAvailability.Exists` ignore `PATHEXT` sous Windows~~ — **implémenté**
Nouvelle méthode privée `ExistsWithOptionalExtensions` : sous Windows, si le chemin candidat n'a pas déjà
d'extension, chaque extension de `PATHEXT` est essayée en plus du nom nu. S'applique aussi bien au
chemin absolu (`Exists("C:\...\ffmpeg")`) qu'à la résolution via `PATH`. Tests :
`UtilsTest.Functional/Reflection/CommandAvailabilityTests.cs`.

### 19. ~~Nommage incohérent des paramètres de type génériques~~ — **implémenté**
`LibraryMapper.Emit<I>`/`EmitInProcess<I>` et `EmitDllMappableClass.Emit<T>` renommés en `<TInterface>`
de façon cohérente (renommage de paramètre de type générique, sans impact sur les appelants existants —
la syntaxe `LibraryMapper.Emit<IMonInterface>(...)` ne référence jamais le nom du paramètre générique).

### 20. `Philosophies d'erreur différentes au sein du même package` — **écarté (à la discrétion de l'équipe)**
Point explicitement marqué « pas nécessairement prioritaire » dans la proposition initiale. Décision :
ne pas ajouter de nouvelle surface d'API (`TryCreate`/`TryEmit` non levants pour `LibraryMapper`) sans
demande explicite — cohérent avec le principe « ne pas ajouter d'abstraction au-delà du besoin ». À
reconsidérer si un besoin concret se présente.

### 21. ~~`Platform.IsMacOsX` détonne du reste des propriétés~~ — **implémenté**
Ajout de `Platform.IsMacOS => IsMacOsX` (alias en lecture seule), `IsMacOsX` conservé pour compatibilité
source/binaire.

### 22. ~~`sandbox-exec` est un outil déprécié par Apple~~ — **documenté**
Paragraphe dédié ajouté dans la XML doc de classe de `MacOsSandboxExecContainer` (dépréciation depuis
macOS 10.12, pas de date de retrait annoncée, migration future vers l'App Sandbox entitlements si
nécessaire).

### 23. ~~Cache statique non borné dans `EmitDllMappableClass`~~ — **documenté**
Commentaire dédié ajouté au-dessus de `emittedLibraries` expliquant explicitement la limite (pas
d'éviction, acceptable pour un nombre fixe d'interfaces connues à l'avance, à surveiller si `Emit` est
appelé avec un grand nombre d'interfaces générées dynamiquement).

## Priorité basse — couverture de tests

### 24. ~~`ProcessIsolation` est très peu testé~~ — **implémenté**
Constructions d'arguments/profils extraites en méthodes `internal static` pures (sans dépendance OS),
testables sur n'importe quelle plateforme :
- `LinuxBubblewrapContainer.BuildArguments(executablePath, arguments, permissions)` — testé par
  `LinuxBubblewrapContainerTests.cs` (7 tests : posture par défaut, chaque permission individuellement,
  ordre des arguments, `--ro-bind / /` toujours présent).
- `MacOsSandboxExecContainer.BuildProfile(permissions)` — testé par `MacOsSandboxExecContainerTests.cs`
  (6 tests : clauses toujours actives, chaque permission individuellement).
- `AppContainerSandbox.QuoteArgument`/`BuildArgumentString` passés de `private` à `internal` — testés
  directement (sans réflexion) par `AppContainerSandboxQuotingTests.cs` (7 tests : chaîne vide, valeur
  simple, espace, chemin Windows avec espaces, guillemet imbriqué, backslashes finaux avec/sans
  guillemet, jointure de plusieurs arguments).
- `SandboxedProcessEnvironmentTests.cs` (item 5) et `CommandAvailabilityTests.cs` (item 18) complètent
  la couverture `ProcessIsolation`.

### 25. ~~`EmitDllMappableClass`/`LibraryMapper.Emit<I>` sans test de robustesse~~ — **implémenté**
`UtilsTest/Reflection/EmitDllMappableClassRobustnessTests.cs` : rejet d'un type non-interface,
génération pour une interface avec paramètres `ref`/`out`, génération pour un paramètre non-byref avec
attribut `[Out]` explicite (idiome `StringBuilder`), et appels `Emit` concurrents (16 tâches) pour la
même interface.

**Ces tests ont mis au jour deux bugs réels, corrigés dans la foulée :**
- **Ordre des attributs `[In]`/`[Out]` invalide en C#** (`WriteFunctionParameters`) : le code générait
  `out [System.Runtime.InteropServices.OutAttribute]System.Int32 doubled` (attribut après le modificateur
  `out`) au lieu de `[OutAttribute] out System.Int32 doubled` — syntaxe invalide, échec de compilation
  Roslyn pour **toute** interface avec un paramètre `out` (bug préexistant, non lié aux changements des
  points 10-12). Cause racine : `ParameterInfo.GetCustomAttribute<OutAttribute>()` renvoie une instance
  synthétisée non-null aussi bien pour un vrai paramètre `out` que pour un attribut `[Out]` explicite —
  impossible de distinguer les deux via cette API. **Fix** : les attributs `[In]`/`[Out]` explicites ne
  sont plus ré-émis pour les paramètres byref (`ref`/`out`), seulement pour les paramètres non-byref
  (ex. `[Out] StringBuilder buffer`), et l'ordre respecte désormais la grammaire C#
  (`[attributs] modificateur type nom`).
- **Chargement concurrent en double** : détaillé à l'item 10 ci-dessus (découvert par le test de
  concurrence de ce point, corrigé par le passage à `Lazy<Type>`).
