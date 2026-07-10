# Utils.Reflection — Propositions d'amélioration (relecture 2026-07-09)

Relecture complète du package (`Reflection/`, `Reflection/Emit/`, `ProcessIsolation/`) axée sécurité,
qualité de code et ergonomie d'interface. Tous les points de la relecture du 2026-07-09 (items 1 à 25)
ont été traités (implémentés, documentés, ou sciemment écartés avec justification) et couverts par des
tests de non-régression. Voir la section « Nouvelles propositions (relecture 2026-07-10) » en fin de
fichier pour les points identifiés lors d'une seconde relecture, pas encore traités.

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

## Corrections post-revue (PR #428, revue automatisée Codex)

Trois bugs réels signalés par la revue automatisée sur `EmitWorkerProcess.cs`, tous corrigés :

- **Worker isolé injoignable sous `bwrap` (Linux)** : `EmitWorkerProcess.Start` lançait le worker avec
  `AllowDiskWrite = false`. Sur Linux, `NamedPipeServerStream` est adossé à une socket Unix créée sous le
  répertoire temporaire de l'OS ; `LinuxBubblewrapContainer` monte `/tmp` en `tmpfs` neuf quand
  `AllowDiskWrite` est faux, rendant la socket invisible pour l'enfant sandboxé → `Emit<TInterface>`
  échouait systématiquement après le timeout de connexion de 10s. Même problème sur macOS
  (`MacOsSandboxExecContainer` refuse `file-write*` par défaut). **Fix** :
  `EmitWorkerProcess.CreateWorkerPermissions()` positionne désormais `AllowDiskWrite =
  !OperatingSystem.IsWindows()`. **Piège évité** : mettre `AllowDiskWrite = true` inconditionnellement
  (y compris sous Windows) aurait désactivé le sandbox Windows tout entier —
  `ProcessContainerFactory.TryCreate` traite `AllowDiskWrite` comme une demande de permissions élargies
  et renvoie `null` (pas d'AppContainer) dès que ce flag est vrai. Le flag reste donc `false` sous
  Windows, où les tubes nommés sont des objets noyau hors du système de fichiers de toute façon.
- **Perte de données des structs à champs publics lors du marshaling JSON** : `EmitWorkerProcess`/
  `EmitWorkerHost` appelaient `JsonSerializer.Serialize`/`Deserialize` avec les options par défaut, qui
  n'incluent que les propriétés. Or `CrossProcessMarshaling.IsSupportedType` accepte les structs en
  inspectant leurs **champs** (idiome P/Invoke typique, ex. `public int X;` sans propriété) — un tel
  argument/valeur de retour/valeur by-ref était donc silencieusement sérialisé en `{}`. **Fix** : options
  partagées `CrossProcessMarshaling.JsonOptions` (`IncludeFields = true`), utilisées à tous les points de
  sérialisation/désérialisation de données applicatives (arguments, retours, by-ref) des deux côtés du
  tube ; le protocole d'enveloppe (`WorkerRequest`/`WorkerResponse`, propriétés uniquement) reste
  inchangé.
- **Relance impossible sous le muxer `dotnet`** : quand le process hôte est lancé via `dotnet MonApp.dll`
  (ou un déploiement `UseAppHost=false`), `Environment.ProcessPath` résout vers l'exécutable `dotnet`
  (le muxer), pas vers l'assembly managée. Le worker relancé recevait alors `dotnet
  --utils-reflection-emit-worker <pipe>`, que le muxer tente de parser comme sa propre ligne de commande
  au lieu de la transmettre au `Main` de l'application → échec silencieux (timeout de connexion). **Fix** :
  `EmitWorkerProcess.BuildWorkerArguments` détecte l'écart entre le nom de fichier de l'exécutable relancé
  et celui de `Assembly.GetEntryAssembly().Location` ; en cas d'écart, le chemin de l'assembly d'entrée est
  inséré avant le marqueur (`dotnet MonApp.dll --utils-reflection-emit-worker <pipe>`), reproduisant la
  syntaxe d'invocation normale de `dotnet` pour un déploiement dépendant du framework.

Tests : `UtilsTest/Reflection/EmitWorkerProcessTests.cs` (permissions/arguments, sans lancer de vrai
second process), `UtilsTest/Reflection/EmitWorkerProtocolTests.cs` (round-trip JSON d'une struct à champs
publics, et test documentant explicitement le comportement par défaut cassé de `System.Text.Json`).

## Nouvelles propositions (relecture 2026-07-10)

Seconde relecture du package une fois les items 1-25 et les corrections post-revue de la PR #428 en
place. **Marqués par l'utilisateur comme prioritaires : items 26, 28 et 32.** Items 26-36 tous
implémentés — voir chaque section pour le détail.

### Priorité haute — sécurité

#### 26. [PRIORITAIRE] ~~Le worker Windows (AppContainer) hérite de tout l'environnement du process hôte~~ — **implémenté**
`AppContainerSandbox.StartProcessInternal` appelait `CreateProcess` avec `lpEnvironment = IntPtr.Zero`,
ce qui faisait hériter du bloc d'environnement complet du process parent. À l'inverse,
`LinuxBubblewrapContainer`/`MacOsSandboxExecContainer` appellent tous deux
`SandboxedProcessEnvironment.ApplyMinimalEnvironment(psi)` (liste blanche `PATH`/`HOME`/`DOTNET_*`/etc.,
voir item 5 de la relecture précédente) avant de lancer le process. **Fix** : nouvelle méthode
`SandboxedProcessEnvironment.BuildWindowsEnvironmentBlock()` qui construit le même filtrage sous forme
de bloc natif `"NAME=VALUE\0"` trié par nom et terminé par un `\0` supplémentaire ; `AppContainerSandbox.
StartProcessInternal` le marshale via `Marshal.StringToHGlobalUni` et le passe en `lpEnvironment` avec le
flag `CREATE_UNICODE_ENVIRONMENT` (nouvelle constante dans `WindowsNativeMethods`), libéré dans le
`finally` existant. Tests : `UtilsTest/Reflection/SandboxedProcessEnvironmentTests.cs` (exclusion d'une
variable arbitraire, présence de `PATH`, terminaison double `\0`, tri alphabétique des entrées).

#### 27. ~~`Platform.NativeULongSize` / `StructPackingSize` sont des setters statiques mutables globaux~~ — **documenté**
Même famille de problème que `ProcessContainerPermissions.Default` avant sa correction (item 1 de la
relecture précédente). N'importe quel composant qui fait `Platform.NativeULongSize = 8` change
silencieusement le comportement pour tous les autres consommateurs du même process — problématique en
particulier si plusieurs bibliothèques d'interop PKCS#11 cohabitent. **Choix retenu** : documentation
explicite plutôt que refonte de l'API (option « à minima » de la proposition initiale) — les deux
propriétés sont publiques et déjà documentées comme modifiables par setter depuis la publication du
package (`omy.Utils.Reflection`), donc les remplacer par un mécanisme scindé par appelant serait un
breaking change non demandé. XML doc de `NativeULongSize`/`StructPackingSize` complétée d'un avertissement
explicite : état process-wide non synchronisé, à ne positionner qu'une seule fois au tout début du
démarrage, avant toute lecture concurrente possible. Pas de nouveau test — changement purement
documentaire, aucun comportement modifié.

### Priorité haute — robustesse

#### 28. [PRIORITAIRE] ~~Aucun timeout sur les échanges Load/Call/Shutdown avec le worker isolé~~ — **implémenté**
Seul le handshake de connexion initial (`EmitWorkerProcess.Start`, `WaitForConnectionAsync`) avait un
`CancellationTokenSource(10s)`. `InvokeMethod`/`Load`/`Dispose` appelaient ensuite `SendAndReceive` →
`reader.ReadLine()` sans aucune limite de temps — un appel natif bloqué à l'intérieur du worker gelait le
thread hôte indéfiniment, y compris dans `Dispose()`. **Fix** : `SendAndReceive` utilise désormais
`reader.ReadLineAsync(CancellationToken)` sous un `CancellationTokenSource` par requête
(`DefaultLoadTimeout`/`DefaultCallTimeout` = 30s, `ShutdownTimeout` = 5s pour `Dispose()`). En cas de
dépassement : `TimeoutException`, et l'instance est « empoisonnée » (`PoisonAfterTimeout`) — le worker est
tué et toute réutilisation ultérieure échoue immédiatement (pas de tentative de resynchronisation du
protocole après un `ReadLine` abandonné en plein vol). `LibraryMapper.Emit<TInterface>` expose désormais
`loadTimeout`/`callTimeout` optionnels (compatibles avec les appelants existants). Tests :
`UtilsTest/Reflection/EmitWorkerProcessTests.cs` (valeurs par défaut),
`UtilsTest.Functional/Reflection/EmitWorkerProcessTimeoutTests.cs` (valide que
`StreamReader.ReadLineAsync(CancellationToken)` sur un vrai `NamedPipeServerStream` observe bien
l'annulation — le mécanisme exact dont dépend le fix — sans passer par un vrai second process, limite
déjà acceptée pour ce fichier).

### Priorité moyenne — qualité de code

#### 29. ~~Pas de validation explicite des interfaces génériques dans `EmitDllMappableClass`~~ — **implémenté**
`CompileMappingType` utilise `type.FullName`/`methodInfo.ReturnType.FullName` tels quels pour générer le
C#. Une interface générique ou une méthode générique produirait un `FullName` avec la syntaxe de
métadonnées CLR (backticks, arity) invalide en C# source — l'échec arrivait sous forme de diagnostic
Roslyn cryptique plutôt qu'un message clair. **Fix** : nouvelle méthode `EnsureNotGeneric`, appelée dans
`Emit(Type, CallingConvention)` juste après le contrôle « doit être une interface », qui rejette
explicitement `type.IsGenericType`/`IsGenericTypeDefinition` ainsi que toute méthode
`IsGenericMethodDefinition`, avec un message clair expliquant la cause (syntaxe de métadonnées CLR non
valide en C# source). Couvre à la fois `EmitInProcess<TInterface>` et le chemin isolé (`EmitCore`, appelé
par `EmitWorkerHost` à l'intérieur du worker) puisque les deux passent par `EmitDllMappableClass.Emit`.
Tests : `UtilsTest/Reflection/EmitDllMappableClassRobustnessTests.cs`
(`Emit_GenericInterface_ThrowsNotSupportedException`, `Emit_InterfaceWithGenericMethod_ThrowsNotSupportedException`).

#### 30. ~~Pas de bump de version / changelog pour le breaking change de `Emit<TInterface>`~~ — **implémenté**
Déjà signalé lors de la relecture précédente (« flagged to the user, no version bump made
unilaterally ») mais resté sans suite jusqu'ici — le package est publié sur NuGet et `Emit<TInterface>`
change de comportement à signature identique (isolation par défaut, exigence de `RunWorkerIfRequested`,
`NotSupportedException` immédiate pour les types non JSON-représentables, coût par appel). **Fix** :
`Utils.Reflection.csproj` : `<Version>1.2.1</Version>` → `<Version>2.0.0</Version>` (bump majeur, cohérent
avec la politique semver du dépôt documentée dans `docs/getting-started.md` — un changement de
comportement à signature identique n'est pas un patch/minor « non-breaking »). Entrées ajoutées dans
`CHANGELOG.md` sous `## [Unreleased]` : section « Changed — omy.Utils.Reflection (BREAKING, v1.2.1 →
2.0.0) » détaillant le changement de comportement de `Emit<TInterface>` et l'immutabilité de
`ProcessContainerPermissions.Default`, section « Added — omy.Utils.Reflection » listant les items
26-29/32 de cette relecture (timeouts, `EmitWorkerPool`, durcissement `ProcessIsolation`, validation
interfaces génériques, alias `Platform.IsMacOS`).

#### 31. ~~`EmitWorkerInvocationException` ne transporte pas la stack trace distante~~ — **implémenté**
Seuls `message` et `RemoteExceptionTypeName` (nom de type) étaient propagés. **Fix** : `WorkerResponse`
porte un nouveau champ texte `ErrorStackTrace`, rempli par `EmitWorkerHost.Run` (`effective.StackTrace`)
dans le `catch` générique qui construit déjà la réponse d'échec ; `EmitWorkerInvocationException` expose
`RemoteStackTrace` (texte brut — reconstruire une vraie exception cross-process n'a pas de sens, le
worker et l'hôte ne partagent pas le même tas). `ToString()` surchargé pour ajouter la stack distante
après la sortie standard de `Exception.ToString()`, étiquetée explicitement pour ne pas la confondre avec
la stack trace de cette exception elle-même (qui ne montre que l'appel hôte→worker, pas la chaîne
d'appels côté worker). Tests : `UtilsTest/Reflection/EmitWorkerInvocationExceptionTests.cs` (valeur par
défaut `null`, `ToString()` avec/sans stack distante), round-trip JSON du nouveau champ dans
`EmitWorkerProtocolTests.WorkerResponse_Failure_CarriesErrorDetails`.

### Priorité basse — perf / architecture (plus structurant, à discuter avant d'engager)

#### 32. [PRIORITAIRE] ~~Un process worker complet est relancé par interface mappée~~ — **implémenté**
Chaque appel à `Emit<TInterface>` relançait l'exécutable hôte en entier (démarrage CLR complet) ; mapper 5
interfaces natives distinctes créait 5 processus. **Fix** : nouvelle classe publique
`EmitWorkerPool` (opt-in, `LibraryMapper.Emit<TInterface>` reste inchangé par défaut — un worker par
interface, isolation maximale) qui démarre un worker partagé au premier `Emit<TInterface>` et le réutilise
pour tous les appels suivants sur la même instance de pool.
- Protocole étendu : `WorkerRequest`/`WorkerResponse` portent désormais un champ `Handle` (int) ; chaque
  `Load` alloue un nouveau handle (compteur local à `EmitWorkerHost.Run`, jamais persistant entre deux
  process), retourné dans la réponse, et chaque `Call`/nouveau `Unload` cible ce handle. `EmitWorkerHost`
  garde une `Dictionary<int, LoadedInterface>` au lieu d'un unique couple `(instance, interfaceType)`.
  Nouveau `WorkerRequestKind.Unload` : libère (Dispose) l'instance visée sans arrêter le worker — appelé
  automatiquement quand un proxy issu du pool est disposé, alors que `Emit<TInterface>.Dispose()` continue
  d'arrêter tout le worker (chemin exclusif, `ownsWorker: true` sur `EmitWorkerProxy`).
- `EmitWorkerProcess.Start` scindé en `Start(TimeSpan? callTimeout)` (démarre + connecte, sans charger
  d'interface — utilisé par le pool) et `Start(Type, string, CallingConvention, ..., out int handle)`
  (démarre + charge une seule interface — chemin historique de `Emit<TInterface>`) ; `LoadInterface`/
  `UnloadInterface`/`InvokeMethod(handle, ...)` protégés par le `callLock` existant (plusieurs interfaces
  sur un même worker doivent sérialiser leurs appels sur le même tube, comme un seul appel le faisait déjà).
  **Note (mise à jour par l'item 34)** : `callLock` a depuis été retiré — le protocole hôte↔worker
  supporte désormais plusieurs requêtes en vol simultanément, corrélées par id ; la description
  ci-dessus reflète l'état du code au moment où l'item 32 a été implémenté, pas l'état actuel.
- **Trade-off explicitement documenté** (XML doc de `EmitWorkerPool`) : partager un worker réduit
  l'isolation entre les interfaces qui y sont chargées (un crash/comportement hostile sur l'une peut
  affecter les autres) — opt-in réservé aux interfaces d'une même frontière de confiance, à ne pas utiliser
  quand l'isolation mutuelle entre interfaces est requise en plus de l'isolation vis-à-vis du process
  appelant.
- Bug évité en cours d'implémentation : la validation `CrossProcessMarshaling.EnsureInterfaceIsSupported`
  avait été déplacée trop tard (à l'intérieur de `LoadInterface`, donc après le lancement du process pour
  le chemin `Emit<TInterface>`) — une interface non supportée aurait fait démarrer puis tuer un worker
  complet avant de lever `NotSupportedException` au lieu d'échouer immédiatement. Revalidée en amont dans
  l'overload `Start(Type, ...)`.

Tests : `UtilsTest.Functional/Reflection/EmitWorkerHostLoopTests.cs`
(`Run_RoutesCallsByHandle_WhenTwoInterfacesAreLoadedOnTheSameWorker` — deux interfaces différentes
chargées sur le même worker simulé, appels routés par handle, `Unload` d'une interface sans perturber
l'autre, appel sur un handle déchargé rejeté), `UtilsTest/Reflection/EmitWorkerPoolTests.cs` (sémantique
de disposal du pool sans lancer de vrai process).

#### 33. ~~Coût par appel non documenté~~ — **documenté**
Chaque appel round-trip en JSON + pipe nommé, sérialisé par un seul `SemaphoreSlim` (`callLock`) —
au moment où cet item a été traité, aucun appel concurrent n'était possible sur un même worker. **Fix**
initial : encart « Performance note » ajouté dans le README juste après la description de
`Emit<TInterface>`, expliquant le coût de sérialisation/IPC par appel et recommandant
`EmitInProcess<TInterface>` pour les boucles d'appels haute fréquence ; mention des paramètres
`loadTimeout`/`callTimeout` (item 28) à cet endroit également. Changement purement documentaire à
l'origine, aucun nouveau test. **Mis à jour par l'item 34** : le protocole ne sérialise plus les appels
(le `callLock` a été retiré) ; l'encart README a été réécrit en conséquence pour refléter la concurrence
intra-worker désormais réelle et son prérequis (thread-safety de la bibliothèque native appelée).

#### 34. ~~Aucun parallélisme des appels à l'intérieur d'un même worker (threads)~~ — **implémenté**
Le protocole hôte↔worker était strictement mono-appel-en-vol des deux côtés : côté hôte,
`EmitWorkerProcess.InvokeMethod` sérialisait tous les appels via un unique `SemaphoreSlim(1,1)`
(`callLock`) ; côté worker, `EmitWorkerHost.Run` était une boucle mono-thread traitant une requête à la
fois. **Fix** : protocole étendu pour supporter plusieurs requêtes en vol simultanément, corrélées par
`WorkerRequest.Id`/`WorkerResponse.Id` (déjà présents) :
- **Côté hôte** (`EmitWorkerProcess`) : `callLock` (`SemaphoreSlim`) retiré, remplacé par
  `ConcurrentDictionary<int, TaskCompletionSource<WorkerResponse>> pending` + une boucle de lecture en
  arrière-plan (`RunReaderLoop`, tâche démarrée dans le constructeur) qui lit les réponses en continu et
  complète le `TaskCompletionSource` correspondant. `SendAndReceive` enregistre son `TaskCompletionSource`
  puis écrit la requête sous un simple verrou d'écriture (`writeLock`, un `object`, pas un `SemaphoreSlim`
  — la section critique ne fait qu'écrire une ligne, jamais attendre une réponse) sans jamais bloquer les
  autres appels en vol.
- **Côté worker** (`EmitWorkerHost.Run`) : chaque requête lue est dispatchée via `Task.Run` au lieu d'être
  traitée en ligne ; `loaded` devient un `ConcurrentDictionary`, l'allocation de handle utilise
  `Interlocked.Increment` sur un compteur boxé (`int[]`, un `ref int` local n'étant pas capturable dans
  une lambda) ; l'écriture des réponses est protégée par un verrou dédié pour éviter l'entrelacement de
  lignes JSON concurrentes.
- **Sémantique de timeout revue** (supersède la description de l'item 28) : grâce à la corrélation par id,
  un timeout n'a plus besoin de « empoisonner » tout le worker (`PoisonAfterTimeout` supprimé) — seule la
  requête concernée échoue (`TaskCompletionSource.TrySetCanceled`, retiré de `pending`) ; une réponse
  tardive pour une requête déjà abandonnée ne trouve simplement plus d'entrée correspondante et est
  ignorée, au lieu d'être prise à tort pour la réponse d'une autre requête. Le worker et les autres appels
  en vol restent intacts. Le pipe cassé (fin de `RunReaderLoop`, EOF ou exception) reste, lui, traité comme
  fatal pour tout le worker (`FailAllPending` + arrêt du process si ce n'est pas déjà un `Dispose()`
  délibéré).
- **Bug réel découvert en écrivant le test de concurrence** (`Sleep`, retour `void`) : `EmitDllMappableClass`
  écrivait `System.Void` (nom CLR de `Type.FullName` pour `void`) au lieu du mot-clé `void` dans le type de
  retour du délégué et de la méthode générés — syntaxe C# invalide, échec de compilation Roslyn pour
  **toute** interface avec une méthode sans valeur de retour (bug préexistant, jamais détecté faute d'un
  test exerçant ce cas). **Fix** : `EmitDllMappableClass.GetReturnTypeName` retourne `"void"` littéralement
  pour `typeof(void)`, `Type.FullName` sinon.
- Rien n'empêchait techniquement un process sandboxé (AppContainer/bwrap/sandbox-exec) de créer des
  threads — confirmé, les trois mécanismes d'isolation du projet restreignent des ressources OS externes
  mais aucun ne limite la création de threads, purement intra-process.
- **Prérequis documenté** (XML doc `InvokeMethod`, README) : la bibliothèque native appelée via P/Invoke
  doit elle-même être thread-safe pour supporter des appels concurrents — responsabilité de l'appelant,
  indépendante de ce changement.

Tests : `UtilsTest.Functional/Reflection/EmitWorkerHostLoopTests.cs` réécrit pour piloter
`EmitWorkerHost.Run` de façon interactive (`LineQueueTextReader`/`LineQueueTextWriter`, une file
bloquante en mémoire) plutôt qu'avec un script `StringReader` figé — nécessaire car sous dispatch
concurrent, l'ordre des réponses n'est plus garanti égal à l'ordre des requêtes, et un script figé ne
peut pas exprimer « attendre la réponse du Load avant d'envoyer le Call qui dépend de son handle » (ce
qu'un hôte réel fait toujours). Nouveau test
`Run_ExecutesTwoSlowCallsConcurrently_NotSequentially` : deux appels `Sleep(800ms)` envoyés sans attendre
la réponse du premier doivent se terminer en ~800ms, pas ~1600ms. `UtilsTest/Reflection/EmitDllMappableClassRobustnessTests.cs` :
`Emit_InterfaceWithVoidReturningMethod_GeneratesAndInstantiatesWithoutError` (régression dédiée pour le
bug `System.Void`).

#### 35. ~~Round-robin sur plusieurs process workers pour la parallélisation~~ — **implémenté**
Alternative à l'item 34 (parallélisme intra-worker par threads, déjà implémenté) qui ne dépend pas de la
thread-safety de la bibliothèque native appelée, au prix d'un coût de démarrage plus élevé : ouvrir
plusieurs process workers indépendants et répartir les appels entre eux en round-robin, chaque process
ayant son propre chargement séparé de la DLL native (donc jamais de risque de course entre deux appels
qui tomberaient dans le même worker). **Fix** : `LibraryMapper.EmitRoundRobin<TInterface>(dllPath,
callingConvention, workerCount, loadTimeout, callTimeout)` — démarre `workerCount` workers isolés
indépendants (chacun via `EmitWorkerProcess.Start(Type, ...)`, le même chemin que `Emit<TInterface>`),
retourne un proxy unique. Nouvelle classe `EmitWorkerRoundRobinProxy : DispatchProxy` (non scellée —
requis par `DispatchProxy.Create`, comme `EmitWorkerProxy`) qui conserve `(EmitWorkerProcess, int
handle)[]` et sélectionne le membre suivant via `Interlocked.Increment` sur un compteur `int` (caste en
`uint` avant le modulo pour rester dans les bornes même après un débordement du compteur après ~2^31
appels). `Dispose()` sur le proxy dispose tous les workers du jeu. Si le démarrage d'un worker échoue en
cours de boucle, les workers déjà démarrés sont disposés avant de relancer l'exception (cohérent avec les
autres chemins de démarrage `EmitWorkerProcess`). Le jeu de workers est figé pour la durée de vie du
proxy — pas de remplacement dynamique d'un worker mort ; ses tours de round-robin échouent simplement
avec l'exception que lève `InvokeMethod` pour une connexion cassée. Complémentaire de l'item 32
(`EmitWorkerPool`, qui réduit le nombre de process pour plusieurs *interfaces différentes*) : celui-ci
distribue les appels d'une *même* interface sur plusieurs process pour paralléliser.
Tests : `UtilsTest/Reflection/EmitWorkerRoundRobinProxyTests.cs` (génération du proxy, comportement avant
attachement des workers, validation `workerCount < 1`) — sans lancer de vrais process, même limite déjà
acceptée pour `EmitWorkerProxyTests`/`EmitWorkerPoolTests`.

#### 36. ~~Documentation détaillée, avec exemples, du fonctionnement du système de process/threads~~ — **implémenté**
Le README documente l'usage de haut niveau (`Emit<TInterface>`, `EmitInProcess<TInterface>`,
`ProcessContainerFactory`) mais pas le *fonctionnement interne* des mécanismes de process/threads eux-
mêmes — utile pour quiconque doit déboguer, étendre le protocole, ou décider entre les options
(worker exclusif vs pool vs, une fois implémentés, threads/round-robin). Proposition : une doc dédiée
(par exemple `Utils.Reflection/docs/process-model.md`, ou une section étoffée du README) couvrant, à
chaque fois avec un exemple de code concret et éventuellement un schéma texte du flux de messages :
1. **Comment on émet plusieurs interfaces dans le même process** : `EmitWorkerPool` (item 32) — cycle de
   vie du worker partagé, allocation de `Handle` par `Load`, routage des `Call` par handle,
   `Unload`/`Dispose` d'une interface individuelle vs `Dispose` du pool entier. Exemple : deux interfaces
   mappées sur `pool.Emit<T>()`, avec le détail des requêtes `WorkerRequest`/réponses `WorkerResponse`
   échangées.
2. **Comment on charge une DLL dans un process** : différence entre `LibraryMapper.Create<T>` (sous-
   classe statique, `[External]` sur champs/propriétés) et `EmitDllMappableClass`/`Emit<TInterface>`
   (génération dynamique d'une classe à partir d'une interface) — avec le détail de
   `NativeLibrary.Load`/`GetExport`/`GetDelegateForFunctionPointer` sous-jacent aux deux chemins.
3. **Comment on lance une commande dans un process** : `IProcessContainer.StartProcess` (implémentations
   `AppContainerSandbox`/`LinuxBubblewrapContainer`/`MacOsSandboxExecContainer`) vs le lancement plus bas
   niveau du worker Emit lui-même (`EmitWorkerProcess.StartWorkerProcess`, `BuildWorkerArguments`,
   `CreateWorkerPermissions`) — en clarifiant que ce sont deux mécanismes distincts (sandbox générique
   réutilisable vs lancement spécifique du worker auto-hébergé).
4. **Comment on lance une commande dans un thread** : dépend de l'item 34 (parallélisme intra-worker par
   threads), pas encore implémenté — documenter soit le design cible une fois fait, soit, en attendant,
   expliquer explicitement l'absence actuelle de parallélisme intra-worker (`callLock`, boucle mono-thread
   de `EmitWorkerHost.Run`) pour que ce ne soit pas une surprise silencieuse.
5. **Comment on construit un round-robin de process** et **comment on y lance des commandes** : dépend de
   l'item 35, pas encore implémenté — documenter le design une fois fait (répartition des appels entre N
   workers, stratégie de sélection, gestion du cycle de vie de chacun).
Pour les points 4 et 5, la documentation ne peut être écrite « avec exemples » qu'une fois les items 34/35
eux-mêmes implémentés (ou, à défaut, présentée explicitement comme un design proposé et non un
comportement existant) — à séquencer après ces deux items si l'objectif est une doc reflétant un système
réellement livré.

**Fix** : items 34 et 35 ayant été implémentés d'abord, les 5 points sont documentés avec des exemples de
code réels (pas de design proposé) dans `docs/reflection/ProcessAndThreadModel.md`, suivant la convention
déjà en place pour les docs approfondies (`docs/parser/*.md`, entrée dans `docs/toc.yml`) plutôt qu'un
fichier isolé sous `Utils.Reflection/`. Chaque section inclut un schéma texte du flux de messages
`WorkerRequest`/`WorkerResponse` pertinent. Lien ajouté depuis `Utils.Reflection/README.md` (section
« Further reading »). Changement purement documentaire, aucun nouveau test (cohérent avec les autres
items « documenté »/« implémenté-doc » de cette liste).
