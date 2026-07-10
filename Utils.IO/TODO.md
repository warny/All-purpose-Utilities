# Utils.IO — Audit qualité (2026-07-10)

Premier passage d'audit qualité sur `Utils.IO/` (18 fichiers : infrastructure de sérialisation
binaire `Reader`/`Writer`/`RawReader`/`RawWriter`, consommée intensivement par Utils.Fonts et
d'autres packages). Même méthodologie que les audits précédents. Deux bugs critiques confirmés par
lecture directe du code (écriture et lecture utilisant des formats binaires incompatibles), plus
deux paramètres trompeurs/inertes déjà utilisés (sans effet, par coïncidence) dans Utils.Fonts.
Aucune proposition ci-dessous n'est encore corrigée.

## Bugs fonctionnels critiques

### 1. Sérialisation de `char` totalement cassée — deux bugs indépendants qui se cumulent
`Utils.IO/IO/Serialization/RawWriter.cs` / `RawReader.cs`.

a) **`WriteChar` n'est jamais atteignable via `Writer.Write<char>(...)`** : la liste
   `RawWriter.WriterDelegates` (ligne 21-33) ne contient PAS `WriteChar`, contrairement à
   `RawReader.ReaderDelegates` (ligne 21-31) qui contient bien `ReadChar`. Conséquence : `Writer`
   (`Writer.cs`) ne trouve aucun délégué enregistré pour `typeof(char)` et retombe sur
   `CreateWriterFor(typeof(char))`, le writer générique par réflexion basé sur les membres marqués
   `[Field]` — `char` n'en a aucun, donc la lambda générée est **vide** et `Write<char>(c)` n'écrit
   **zéro octet**, exactement le même mode de défaillance que le bug `GlyphCompound.WriteData`
   corrigé précédemment dans Utils.Fonts pour `byte[]`.
b) **Même si `WriteChar` était atteignable, son format ne correspond pas à celui de `ReadChar`** :
   `WriteChar` (RawWriter.cs) écrit un préfixe de taille sur 1 octet puis les octets encodés
   (UTF-8 par défaut, taille variable 1-4 octets) ; `ReadChar` (RawReader.cs:177-181) ignore
   totalement ce préfixe et lit directement `sizeof(char)` = 2 octets bruts qu'il interprète via
   `BitConverter.ToChar` (un code unit UTF-16). Exemple tracé : écrire `'A'` produit `[0x01, 0x41]`
   (préfixe=1, octet UTF-8) ; lire réinterprète ces 2 octets comme `0x4101` = `'Ą'`, et désynchronise
   la position du flux pour toute lecture suivante.
Aucun test ne couvre `char` en lecture/écriture (`NewReaderWriterTest.TestReadAndWriteNumbersAndDates`
ne teste que byte/short/int/long/float/double/DateTime).
**Fix proposé** : choisir un format canonique unique pour `char` (le plus simple : 2 octets bruts en
UTF-16, sans préfixe de taille, symétrique en lecture/écriture, cohérent avec l'endianness du
writer/reader) ; ajouter `WriteChar` à `WriterDelegates` ; ajouter un test de round-trip.
**Sévérité** : bug fonctionnel critique (perte totale de données + désynchronisation de flux pour
tout `char` sérialisé).

### 2. `TimeSpan` — écriture en `double`, lecture en `long`
`Utils.IO/IO/Serialization/RawWriter.cs` (`WriteTimeSpan`) / `RawReader.cs:165` (`ReadTimeSpan`).
`WriteTimeSpan` sérialise `value.TotalMicroseconds` (un `double`) via `WriteDouble`, qui applique un
retournement d'octets conditionnel à l'endianness (`WriteNumberBytes`). `ReadTimeSpan` désérialise via
`ReadLong` puis `TimeSpan.FromTicks(...)` — `ReadLong` interprète directement les mêmes 8 octets comme
un entier via `IBinaryInteger.ReadBigEndian/ReadLittleEndian`, **sans** appliquer le même retournement
conditionnel que `ReadNumberBytes` (utilisé par les types flottants). Trace concrète : écrire
`TimeSpan.FromSeconds(1)` (`TotalMicroseconds = 1 000 000.0`) produit les octets du double
`1000000.0`, dont la relecture comme `long` donne une valeur totalement différente, puis
`FromTicks(valeur_fausse)` produit un `TimeSpan` n'ayant aucun rapport avec l'original.
Aucun test ne couvre `TimeSpan`.
**Fix proposé** : aligner les deux côtés — le plus simple et le plus dense est
`WriteLong(writer, value.Ticks)` / `TimeSpan.FromTicks(ReadLong(reader))` (comme le fait déjà
`DateTime`/`TimeOnly` avec `.Ticks`), plutôt que de passer par `TotalMicroseconds` en `double`.
**Sévérité** : bug fonctionnel critique (perte totale de données pour tout `TimeSpan` sérialisé).

## Incohérences trompeuses (paramètres inertes)

### 3. `WriteVariableLengthString` — le paramètre `bigEndian` n'est jamais utilisé
`Utils.IO/IO/Serialization/ReaderWriterExtensions.cs:178-194`. La méthode déclare et documente un
paramètre `bigEndian` ("Whether the length is stored in big-endian order"), mais ne le lit jamais
dans son corps : l'endianness réellement appliquée est entièrement déterminée par la configuration
propre du `Writer` passé en paramètre (son `RawWriter.BigEndian`), pas par cet argument.
**Déjà utilisé en production** : `Utils.Fonts/TTF/Tables/PostTable.cs:211` appelle
`data.WriteVariableLengthString(glyphNames[i], Encoding.Default, bigEndian: true, sizeLength: 1)` —
cela "fonctionne" uniquement parce que le `Writer` utilisé par Utils.Fonts est déjà configuré en
big-endian par ailleurs ; le paramètre lui-même n'a aucun effet. Un futur appelant qui aurait besoin
d'un flux à endianness mixte (certains champs big-endian, d'autres little-endian, cas réel dans
certains formats binaires) serait silencieusement ignoré.
**Fix proposé** : soit implémenter réellement le paramètre (construire un writer temporaire avec
l'endianness demandée pour la taille), soit le supprimer et documenter clairement que l'endianness
suit celle du `Writer` fourni.
**Sévérité** : incohérence trompeuse (piège silencieux, même famille que les pièges déjà documentés
dans l'audit Utils.Fonts).

### 4. `ReadArray<T>` — le paramètre `bigEndian` ne change rien pour tous les types listés
`Utils.IO/IO/Serialization/ReaderWriterExtensions.cs:49-72`. Quand `bigEndian = true`, la méthode
bascule sur un `switch` qui appelle explicitement `reader.Read<Int16>()`, `reader.Read<Int32>()`,
etc. Mais `Int16` **est** `short`, `Int32` **est** `int`, `Int64` **est** `long` (alias du langage
C#, pas des types distincts) — donc pour chacun des types listés, la branche `bigEndian == true`
appelle exactement le même code que la branche `else` (`reader.Read<T>()`). Le paramètre ne change
absolument rien au comportement ; l'endianness réelle vient uniquement de la configuration du
`Reader` fourni. Même remarque que l'item 3 : déjà utilisé (avec `true`) dans
`Utils.Fonts/TTF/Tables/Cmap/CMapFormat4.cs:331`, `GlyphSimple.cs:125`, `LocaTable.cs:158,163` — ça
"fonctionne" uniquement parce que ces `Reader` sont déjà configurés en big-endian par ailleurs.
**Fix proposé** : même remarque que l'item 3 — implémenter réellement le comportement ou supprimer
le paramètre et documenter que l'endianness suit celle du `Reader` fourni.
**Sévérité** : incohérence trompeuse (paramètre mort, aucun effet fonctionnel actuellement mais
risque silencieux si l'endianness ambiante du Reader diffère un jour de ce qui est demandé).

## Incohérence mineure

### 5. `RawWriter.WriteString` — appel indirect par réflexion au lieu d'un appel direct
`Utils.IO/IO/Serialization/RawWriter.cs` (`WriteString`). Écrit `writer.Write(data.Length)` — un
appel à `IWriter.Write(object)`, qui effectue une résolution dynamique de writer par réflexion à
chaque appel — alors que `WriteBigInteger` (juste au-dessus dans le même fichier), qui suit le même
schéma "préfixe de taille + octets", appelle directement `WriteInt(writer, bytes.Length)`. Ce n'est
pas un bug fonctionnel (le writer pour `int` est bien trouvé), mais une incohérence de style et un
coût de performance évitable sur le chemin le plus commun (écriture de chaînes).
**Fix proposé** : remplacer par `WriteInt(writer, data.Length)`, comme dans `WriteBigInteger`.
**Sévérité** : cosmétique / performance mineure.
