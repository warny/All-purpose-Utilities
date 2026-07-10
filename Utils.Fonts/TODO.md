# Utils.Fonts — Audit qualité (2026-07-10)

Audit complet du package : conformité AGENTS.md, bugs, dette technique, couverture de tests.
Package : parsing/écriture de polices PostScript (`Utils.Fonts/PostScript/`) et TrueType/OpenType
(`Utils.Fonts/TTF/`).

**État (2026-07-10, suite) :** les 8 bugs fonctionnels (items 1-8) sont corrigés, chacun avec un
commit dédié et un test de régression dans `UtilsTest.Functional/Fonts/`. Les items 9-20 (dette
technique, cosmétique, couverture de tests) restent des propositions non traitées, sauf mention
contraire.

## Bugs fonctionnels (priorité haute)

### 1. `CmapTable.ReadData` — décalage d'indice dans le calcul des longueurs de sous-table
`Utils.Fonts/TTF/Tables/CmapTable.cs:171-181`
Le calcul `subTables[i] = (platformID, platformSpecificID, offset, offset - lastOffset)`, avec
`lastOffset` mis à jour *après* l'assignation, attribue à la sous-table `i` la longueur
`offset[i] - offset[i-1]` — c'est-à-dire la taille de la sous-table `i-1`, pas celle de `i`. Pour
toute police avec plus d'une sous-table cmap (cas très courant : Windows-Unicode + Mac-Roman, ou
symbol + BMP), les slices passées à `CMapFormatBase.GetMap` ont la mauvaise longueur, ce qui peut
tronquer la dernière sous-table ou décaler la lecture des autres.
**Fix proposé** : calculer `length[i] = offset[i+1] - offset[i]` (ou `dataLength_de_la_table - offset[i]`
pour la dernière sous-table), avec un offset "suivant" plutôt que "précédent".
**Corrigé.** Test de régression : `CmapTableTests.MultipleSubtables_AllSurviveRoundTrip`.

### 2. `CMapFormat4.WriteData` — troncature des indices de glyphe sur 1 octet au lieu de 2
`Utils.Fonts/TTF/Tables/Cmap/CMapFormat4.cs:376-379`
Dans la boucle qui écrit le tableau de correspondance d'un `TableMap`, `data.Write<Byte>((byte)index)`
écrit un seul octet alors que le format cmap 4 stocke des `uint16` par entrée (et `TableMap.Length`
déclare bien `Map.Count * sizeof(short)`). Toute police avec plus de 255 glyphes et une sous-table à
correspondance explicite produira un flux cmap corrompu en écriture (glyphIdArray tronqué +
désalignement de tout ce qui suit).
**Fix proposé** : `data.Write<Int16>(index)`.
**Corrigé**, avec deux bugs supplémentaires trouvés en écrivant les tests d'aller-retour : l'ordre
d'écriture endCode[]/startCode[] était inversé par rapport à `ReadData` (et le spec), et
`segCountX2` était écrit/lu avec le décalage de bits inversé (`>> 1` au lieu de `<< 1` à l'écriture,
et vice-versa à la lecture — les deux bugs se compensaient pour un nombre de segments pair, d'où
l'absence de détection sur la police Arial réelle utilisée par `TrueTypeFontTests`). Voir le détail
dans le message du commit. Tests : `CMapFormat4Tests.cs` (nouveau fichier).

### 3. `CMapFormat4.WriteData` — calcul de `glyphArrayOffset` incohérent avec l'écriture réelle
`Utils.Fonts/TTF/Tables/Cmap/CMapFormat4.cs:367-382`
`glyphArrayOffset += tableMap.Map.Count * 2` suppose 2 octets par entrée alors que la boucle
d'écriture correspondante (item 2) n'en écrit qu'un actuellement. À corriger conjointement avec
l'item 2, puis revalider avec un test d'aller-retour (plusieurs `TableMap` consécutifs).
**Corrigé** en même temps que l'item 2 (devient cohérent automatiquement une fois l'écriture en
`Int16` corrigée). Un bug de double-comptage annexe a aussi été corrigé dans
`TableMap.Length`/`DeltaMap.Length` (voir le commit).

### 4. `GlyphSimple.WriteData` — tailles d'octets inversées par rapport à `ReadData`/`GetFlags`
`Utils.Fonts/TTF/Tables/Glyf/GlyphSimple.cs:237-259`
La fonction locale `Write` fait : si `isByte` est posé, écrit un `Int16` (2 octets) quand `isSame`
est aussi posé, et rien sinon ; si `isByte` n'est pas posé, écrit un seul octet. C'est l'inverse de
la logique de `ReadData`/`GetFlags`, où `XIsByte`/`YIsByte` signifie "1 octet" et son absence
signifie "2 octets (`Int16`)". Toute écriture de glyphe simple produit donc un flux `glyf` binaire
cassé (tailles ne correspondant pas aux flags écrits), rendant `WriteFont()` invalide pour toute
police TrueType contenant des contours. Aucun test ne couvre `GlyphSimple.WriteData`/round-trip,
d'où le passage inaperçu.
**Fix proposé** : inverser la logique — `isByte` posé ⇒ écrire un octet (signe selon `isSame`),
sinon ⇒ écrire `Int16`.
**Corrigé**, avec deux bugs supplémentaires trouvés en écrivant les tests d'aller-retour :
`GetFlags` calculait les flags `IsByte`/`IsSame` sur la coordonnée absolue au lieu du delta par
rapport au point précédent (les coordonnées `glyf` sont toujours des deltas), et inversait la
polarité du bit `IsSame` pour les deltas encodés sur un octet (le spec dit : bit posé = positif,
absent = négatif — c'était l'inverse) ; `WriteData` écrivait aussi le nombre de points par contour
au lieu de l'index du dernier point (`count` au lieu de `count - 1`) attendu par `ReadData`. Tests :
`GlyphSimpleTests.cs` (nouveau fichier).
**Relecture PR (Codex)** : ce dernier point était encore faux pour les glyphes à plusieurs contours —
`count - 1` donne l'index local au contour, pas l'index cumulatif dans le tableau de points aplati du
glyphe entier qu'exige réellement `endPtsOfContours` (correct par coïncidence pour le premier contour
seulement). Corrigé avec un total courant (`cumulativePointIndex += contours[i].Length`). Test ajouté :
`GlyphSimpleTests.ReadThenWrite_TwoContours_ProducesCumulativeEndPoints`.

### 5. `PostMapFormat0.stdNames[63]` — nom de glyphe erroné
`Utils.Fonts/TTF/Tables/PostTable.cs:73` : `"ackslash"` au lieu de `"backslash"`.
Casse la correspondance nom↔index standard Macintosh pour l'index 63 (`getGlyphNameIndex("backslash")`
retourne 0/`.notdef` au lieu de 63). **Fix proposé** : corriger la faute de frappe.
**Corrigé.** Test : `PostTableTests.Format0_BackslashResolvesToIndex63`.

### 6. `PostMapFormat2.WriteData` — `Seek(0)` parasite en fin d'écriture
`Utils.Fonts/TTF/Tables/PostTable.cs:203-215`
Après avoir écrit `glyphNameIndex` et les noms de glyphes, la méthode fait
`data.Seek(0, SeekOrigin.Begin)`, repositionnant le flux au tout début. Si `PostTable.WriteData`
(ou l'appelant `TrueTypeFont.WriteFont`) écrit autre chose après cet appel dans le même flux, les
données précédentes seront écrasées. Ressemble à un artefact de debug oublié.
**Fix proposé** : supprimer cette ligne.
**Corrigé.** Test : `PostTableTests.Format2_WriteData_LeavesStreamPositionedAfterItsOwnData`.

### 7. `TtfEncoderFactory.GetEncoding` — confusion entre `TtfLanguageID` et code page Windows
`Utils.Fonts/TTF/TtfEncoderFactory.cs:23-47`
Pour les plateformes Macintosh et Microsoft, le code appelle `Encoding.GetEncoding((int)languageID)`
en traitant l'identifiant de langue TrueType (ex. `MS_English_United_States = 1033`, un LCID
Windows) comme un *code page* .NET. Ce comportement erroné est déjà documenté par le test
`TtfEncoderFactoryTests.Microsoft_InvalidCodePage_FallsBackToAscii` ("1033 is a Windows LCID, not a
valid code page"), qui se contente de vérifier le repli sur ASCII plutôt que de corriger la
logique. En pratique, quasiment tous les enregistrements `name`/`post` avec un LanguageID Microsoft
réaliste tombent en ASCII, corrompant silencieusement les métadonnées non-ASCII (accents, CJK, etc.)
lues via `NameTable.ReadData`/`WriteData`.
**Fix proposé** : construire une vraie table de correspondance LanguageID→Encoding (ou a minima
PlatformID/PlatformSpecificID→Encoding, le triplet réellement pertinent selon la spec OpenType — le
languageID seul ne détermine pas l'encodage).
**Corrigé** : `GetEncoding` bascule désormais sur `PlatformSpecificID` (Roman → MacRoman pour
Macintosh, toujours UTF-16BE pour Microsoft/Unicode) et n'utilise plus `languageID` pour choisir
l'encodage. Tests réécrits dans `TtfEncoderFactoryTests.cs` (les anciens asseraient le comportement
buggy comme voulu).
**Relecture PR (Codex)** : le "toujours UTF-16BE" pour Microsoft était incomplet — les IDs
d'encodage legacy CJK (2=ShiftJIS, 3=PRC, 4=Big5, 5=Wansung, 6=Johab) doivent utiliser leurs propres
code pages (932/936/950/949/1361), pas UTF-16BE. `TtfPlatformSpecificID` ne déclare aucun nom pour
ces valeurs (et sa valeur 3 s'appelle `UNICODE_V2`, un sens Macintosh/versioning Unicode sans rapport
qui partage la même valeur numérique que l'ID Microsoft PRC — piège de nommage). Ajouté
`GetMicrosoftEncoding` qui dispatche sur la valeur numérique brute. Test :
`TtfEncoderFactoryTests.Microsoft_LegacyCjkEncodingIds_ReturnTheirOwnCodePage`.

### 8. `TtfHinting.MovePoint`/`ScalePoints` — troncature des coordonnées de points en `short`
`Utils.Fonts/TTF/TtfHinting.cs:61-83`
`TTFPoint` stocke `X`/`Y` en `float`, mais `MovePoint` et `ScalePoints` reconstruisent le point avec
`(short)(pt.Y + delta)` / `(short)(pt.X * scale)`. Toute fraction de pixel introduite par le hinting
est perdue immédiatement, à l'encontre de l'objet même du hinting (ajustements sous-pixel). Portée
limitée : `TtfHinting` semble être une infrastructure expérimentale sans appelant de production
identifié.
**Fix proposé** : conserver le résultat en `float`, sans cast intermédiaire vers `short`.
**Corrigé.** Tests (item 20 traité au passage) : `TtfHintingTests.cs` (nouveau fichier, `TtfHinting`
n'avait aucun test avant).

## Dette technique / non-conformité AGENTS.md

### 9. `using static System.Net.Mime.MediaTypeNames;` inutilisé et trompeur
`Utils.Fonts/TTF/TrueTypeFont.cs:11`
Directive non utilisée dans le fichier (probablement un artefact d'auto-complétion IDE important
`Text`/`Application` depuis un espace de noms sans rapport). **Fix proposé** : supprimer.

### 10. Code mort — implémentation ACNT parallèle et non branchée
`Utils.Fonts/TTF/Tables/Acnt/AcntFormatBase.cs`, `AcntFormat0.cs`, `AcntFormat1.cs`
Ces trois classes forment une implémentation alternative, simplifiée et non testée du format ACNT,
distincte de `AcntTable` (hiérarchie `AccentDescription.Single`/`Multiple`, testée par
`AcntTableTests.cs`). Aucune n'est référencée en dehors du dossier `Acnt/` — ni `[TTFTable]`, ni
appelée par `TrueTypeFont`.
**Fix proposé** : supprimer le dossier `TTF/Tables/Acnt/` (3 fichiers), ou documenter explicitement
pourquoi il est conservé.
**Corrigé.** Dossier `TTF/Tables/Acnt/` supprimé (3 fichiers).

**Relecture PR #434 (Codex + utilisateur)** : Codex a signalé que ces classes étaient `public` dans
le package NuGet publié `omy.Utils.Fonts` (v1.2.1) et que leur suppression casse la compilation de
tout consommateur externe qui les référencerait — une préoccupation de compatibilité API, pas
architecturale. L'utilisateur a soulevé séparément un point légitime : la table `acnt` peut avoir
plusieurs formats par glyphe (format 0 et format 1), et disposer d'une base polymorphe pour les
représenter est effectivement indispensable. Vérification faite : **ce besoin est déjà satisfait par
`AcntTable`**, pas par le code supprimé.
- `AcntTable.AccentDescription` (classe abstraite) avec `Single` (format 0) et `Multiple` (format 1)
  est exactement cette base polymorphe, déjà en place, testée, et conforme à la spec Apple
  (https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6acnt.html).
- Le code supprimé (`AcntFormatBase.GetActn(Reader)`) n'analysait qu'un seul enregistrement de
  description isolé (pas l'en-tête de la table, pas la plage de glyphes, pas les sous-tables
  d'extension/secondaire que `AcntTable` gère) — un premier jet incomplet du même format, abandonné.
- `AcntFormatBase` n'hérite pas de `TrueTypeTable` : le mécanisme d'enregistrement de
  `TrueTypeFont` (`TablesType.Add(tag, ...)`, dictionnaire à clé unique par tag) ne scanne que les
  sous-classes de `TrueTypeTable`, et `AcntTable` possède déjà `[TTFTable(TableTypes.Tags.ACNT)]`.
  Même restructuré pour s'y greffer, ce code entrerait directement en conflit avec `AcntTable` sur le
  même tag — il ne peut pas coexister comme implémentation alternative utilisable.

**Décision** : suppression maintenue. La table `acnt` elle-même n'est pas obsolète et reste
pleinement supportée (par `AcntTable`) ; seuls ces trois fichiers précis, qui dupliquaient
partiellement et incorrectement la même fonctionnalité sans jamais être branchés, étaient à
supprimer. Le risque de compatibilité API signalé par Codex est jugé acceptable : ce code n'était de
toute façon jamais atteignable via le flux normal de parsing de police, et `Utils.Fonts` (comme les
autres packages `Utils.*`) doit de toute façon migrer vers une version 2.0.0 groupée qui absorbera ce
type de changement cassant.

### 11. `NameTable.WriteData` — positionnement du flux non garanti en fin d'écriture
`Utils.Fonts/TTF/Tables/NameTable.cs:200-225`
Chaque enregistrement de nom est écrit via `Push()`/`Seek(6+12*Count+offset)`/écriture/`Pop()`, mais
après la boucle, la position du flux reste juste après le dernier enregistrement d'en-tête (pas à la
fin des données de chaîne). Ça fonctionne actuellement par accident de l'implémentation de
`MemoryStream` (qui s'étend correctement via `Seek`+écritures), mais c'est fragile et non documenté.
**Fix proposé** : documenter l'invariant, ou repositionner explicitement `data.Position` à la fin du
bloc de chaînes avant de retourner.
**Corrigé** (en écrivant les tests de l'item 16) : `WriteData` repositionne désormais explicitement
le flux à la fin de la zone de chaînes avant de retourner. Un second bug, plus grave, a été trouvé au
passage — voir la note sur l'item 16.

### 12. Style — tableau non-bracket dans `PostMapFormat0.stdNames`
`Utils.Fonts/TTF/Tables/PostTable.cs:65` : `protected internal string[] stdNames = { ... }` utilise
l'initialiseur `{ }` au lieu de la syntaxe crochets `[ ]` requise par AGENTS.md.
**Fix proposé** : `= [ ... ]`.

### 13. `params (float x, float y)[] points` / `params TableTypes.Tags[] dependsOn` non conformes
`Utils.Fonts/IFont.cs:90`, `Utils.Fonts/TTF/TTFTableAttribute.cs:19`
Ces deux paramètres sont lus séquentiellement sans accès indexé nécessaire ; AGENTS.md demande de
préférer `params IEnumerable<T>` dans ce cas. Impact réel très faible (API publique déjà stable),
signalé pour cohérence avec les changements déjà appliqués ailleurs dans `Utils/`
(`EnumerableEx`/`Bytes`).
**Non applicable, les deux après vérification :**
- `TTFTableAttribute(TableTypes.Tags tableTag, params TableTypes.Tags[] dependsOn)` est le
  constructeur d'un `Attribute` — le C# restreint les types de paramètres de constructeur
  d'attribut à un ensemble fixe (types primitifs, `string`, `Type`, enums, et tableaux
  unidimensionnels de ceux-ci) ; `IEnumerable<T>` n'en fait pas partie. Convertir ce paramètre
  casserait la compilation de tout usage `[TTFTable(tag, dep1, dep2)]` (CS0181). Conservé en
  `params T[]`.
- `IGraphicConverter.BezierTo(params (float x, float y)[] points)` : l'audit affirmait qu'aucun
  accès indexé n'était nécessaire, mais `BitmapGraphicConverter.BezierTo` (implémentation réelle,
  `DrawTest/BitmapGraphicConverter.cs`) utilise `points[^1]` pour retrouver le dernier point de
  contrôle — exactement le cas d'exception documenté pour `ArrayUtils`/`MathEx` (accès indexé natif
  plus performant qu'`IEnumerable<T>`). Conservé en `params T[]`.

### 14. Duplication de constantes de recherche binaire AAT (`searchRange`/`entrySelector`/`rangeShift`)
Répétée quasi identiquement dans `KernTable.WriteData`, `BslnTable.WriteLookupTableFormat6`,
`LcarTable.WriteData`, `OpbdTable.WriteData`, `PropTable.WriteLookupTableFormat6` (5 occurrences du
même calcul `BitOperations.Log2`/`searchRange`/`entrySelector`/`rangeShift`).
**Fix proposé** : factoriser dans une méthode statique partagée (ex.
`AatBinarySearchHeader.Compute(int unitCount, int unitSize)`), pour éviter qu'un futur correctif ne
soit appliqué qu'à un sous-ensemble des tables.
**Corrigé** : `AatBinarySearchHeader.Compute(unitCount, unitSize)` ajouté
(`Utils.Fonts/TTF/Tables/AatBinarySearchHeader.cs`), les 5 tables l'utilisent désormais. Changement
sans impact comportemental (les tests de round-trip existants de chaque table passent inchangés).

### 15. Documentation XML manquante sur certains membres internes de collections
Ex. `LocaTable` : les champs privés `headTable`/`maxpTable`/`offsets` n'ont pas de doc XML,
contrairement à d'autres tables du même fichier (`HmtxTable.advanceWidths`/`leftSideBearings`,
`VmtxTable.advanceHeights`/`topSideBearings`, qui en ont). Application inconsistante plutôt
qu'absente partout — suggère un passage de conformité partiel déjà fait.

## Manque de test (pas un bug, mais racine de plusieurs bugs ci-dessus)

### 16. Aucun test de round-trip sur les tables TTF "cœur" — priorité haute
Tables concernées : `Cmap`, `Glyf`, `Hmtx`, `Kern`, `Name`, `Loca`, `Maxp`, `Hhea`, `Vmtx`, `Post`.
Seul `TrueTypeFontTests.LoadFontGlyphTest` exerce indirectement `CmapTable`/`GlyfTable`/`HmtxTable`
en lecture (via `TrueTypeFont.GetGlyph('a')` sur une police Arial embarquée), sans jamais vérifier
`WriteData`/round-trip, ni les cas à plusieurs sous-tables cmap, ni `KernTable.GetSpacingCorrection`,
ni `NameTable`/`PostTable`. C'est précisément la zone où se cachaient les bugs 1 à 6 : un test de
round-trip (`ParseFont` → `WriteFont` → `ParseFont`, comparer les valeurs) sur une police réelle
multi-glyphes aurait très probablement détecté les bugs 2 et 4.
**Fix proposé** : ajouter des tests dédiés par table dans `UtilsTest.Functional/Fonts/`, en
particulier un test d'aller-retour bit-exact sur `TrueTypeFont.WriteFont()`.
**Corrigé** : les 10 tables listées ont désormais chacune un fichier de test dédié
(`CmapTableTests`, `CMapFormat4Tests`, `GlyphSimpleTests`, `HmtxTableTests`, `KernTableTests`,
`NameTableTests`, `LocaTableTests`, `MaxpTableTests`, `HheaTableTests`, `VmtxTableTests`,
`PostTableTests`). Écrire le test de `NameTable` a révélé un second bug réel (`WriteData` passait un
nombre de caractères à `WriteFixedLengthString` au lieu du nombre d'octets déjà calculé — invisible
tant que `TtfEncoderFactory` retombait toujours sur ASCII avant l'item 7, où 1 caractère = 1 octet ;
exposé dès que l'encodage UTF-16BE correct a été utilisé), corrigé dans le même commit que le fix du
point 11 ci-dessous (les deux bugs cohabitaient dans `NameTable.WriteData`). Pas encore fait : un
test d'aller-retour bit-exact sur `TrueTypeFont.WriteFont()` complet (toutes les tables d'une police
réelle) — les tests ajoutés valident chaque table isolément.

### 17. Aucun test pour `Type3Font`, `Type42Font`, `CidKeyedFont`
`Utils.Fonts/PostScript/Type3Font.cs`, `Type42Font.cs`, `CidKeyedFont.cs`
Seul `PostScriptFont` est couvert (`UtilsTest.Functional/Fonts/PostScriptFontTests.cs`). Les trois
autres classes du même répertoire — dont deux (`Type3Font`, `CidKeyedFont`) contiennent une logique
de parsing regex+charstring non triviale largement dupliquée depuis `PostScriptFont` — n'ont aucune
couverture.
**Correction du constat** : en y regardant de plus près, `PostScriptFontTests.cs` couvrait déjà
`Type3Font`/`Type42Font`/`CidKeyedFont` par des tests de fumée (`LoadType3Font`, `LoadType42Font`,
`ParseCidKeyedFont`, `Type3FontParsesFontMatrixAndFontBBox`, etc., 14 tests au total) — l'audit
initial avait raté ce fichier. **Complété** : `Type3FontTests.cs` ajouté pour vérifier les commandes
de tracé réellement produites par `moveto`/`lineto`/`curveto` (jamais vérifiées avant, seule la
présence du glyphe l'était) et le fallback `MapName` sur un nom multi-caractères inconnu. Aucun bug
trouvé.

### 18. Aucun test pour les tables AAT restantes
Tables concernées : `Feat`, `Fdsc`, `Fmtx`, `Lcar`, `Opbd`, `Prop`, `Trak`, `Hdmx`. Toutes bien
conçues et cohérentes à la lecture, mais sans fichier de test dédié, contrairement à leurs pairs déjà
testés (`AcntTableTests`, `AvarTableTests`, `BslnTableTests`, `CvarTableTests`, `FvarTableTests`,
`GaspTableTests`, `LtshTableTests`, `Os2TableTests`, `PcltTableTests`, `VheaTableTests`,
`DsigTableTests`). Vu la cohérence de style avec les tables déjà testées, probablement un oubli
plutôt qu'un choix — bonne opportunité de compléter la suite existante par symétrie.
**Corrigé** : les 8 tables ont désormais un fichier de test dédié (`FeatTableTests`, `FdscTableTests`,
`FmtxTableTests`, `LcarTableTests`, `OpbdTableTests`, `PropTableTests`, `TrakTableTests`,
`HdmxTableTests`). Aucun bug trouvé.

### 19. `CvtTable`/`FpgmTable`/`PrepTable` sans test
Ces trois tables (bytecode brut) sont fonctionnellement triviales et sans bug détecté, mais sans
aucun test dédié ni indirect. Risque faible vu la simplicité du code.
**Corrigé** : `CvtTableTests`, `FpgmTableTests`, `PrepTableTests` ajoutés. Aucun bug trouvé.

### 20. `TtfHinting.cs` (`TtfHintingContext`/`TtfHintingProcessor`) sans aucun test
**Traité** en corrigeant l'item 8 : `TtfHintingTests.cs` couvre désormais `MovePoint` et
`ScalePoints`. `NOP` reste non testé (trivial, pas de comportement à vérifier).

## Résumé priorisé

| # | Sévérité | Fichier | État |
|---|---|---|---|
| 4 | Bug fonctionnel majeur | `GlyphSimple.cs` (WriteData round-trip cassé + endPtsOfContours cumulatif) | Corrigé |
| 1 | Bug fonctionnel | `CmapTable.cs` (longueurs de sous-table décalées) | Corrigé |
| 2, 3 | Bug fonctionnel | `CMapFormat4.cs` (troncature glyph index en écriture) | Corrigé |
| 7 | Bug fonctionnel connu/documenté | `TtfEncoderFactory.cs` (LCID confondu avec code page + CJK legacy) | Corrigé |
| 6 | Bug fonctionnel potentiel | `PostTable.cs` (`Seek(0)` parasite) | Corrigé |
| 5 | Bug fonctionnel mineur | `PostTable.cs` (typo "ackslash") | Corrigé |
| 8 | Bug fonctionnel (portée limitée) | `TtfHinting.cs` (troncature `short`) | Corrigé |
| 16 | Manque de test (racine des bugs 1-6) | Cmap/Glyf/Hmtx/Kern/Name/Loca/Maxp/Hhea/Vmtx/Post | Corrigé (tests par table ; reste : round-trip `WriteFont()` complet) |
| 17 | Manque de test (constat partiellement erroné) | Type3Font/Type42Font/CidKeyedFont | Corrigé |
| 10 | Dette technique | `Acnt/` (code mort) | Corrigé |
| 9, 12, 13 | Cosmétique | using inutile, initialiseur de tableau, params | Corrigé (13 : non applicable, voir détail) |
| 14, 15 | Dette technique mineure | duplication AAT header, docs incomplètes | Corrigé |
| 11 | Bug fonctionnel (trouvé via l'item 16) | `NameTable.cs` (position flux + overflow `WriteFixedLengthString`) | Corrigé |
| 18, 19 | Manque de test | tables AAT restantes, Cvt/Fpgm/Prep | Corrigé |
| 20 | Manque de test | TtfHinting | Corrigé (via item 8) |

## Bilan (2026-07-10, fin de session)
Tous les items de test (16, 17, 18, 19, 20) et tous les bugs fonctionnels (1-8, plus le 11 et le bug
`NameTable.WriteData`/`WriteFixedLengthString` trouvé en cours de route) sont corrigés, chacun avec
son propre commit et son test de régression.

**Suite (même jour) — items de dette technique/cosmétique traités** : les items restants (9, 10, 12,
13, 14, 15) ont tous été traités sur la branche `claude/fonts-todo-cleanup` :
- 9 : `using` inutile supprimé de `TrueTypeFont.cs`.
- 10 : dossier mort `TTF/Tables/Acnt/` supprimé.
- 12 : `PostMapFormat0.stdNames` passé en syntaxe crochets `[ ]`.
- 13 : les deux candidats (`TTFTableAttribute.dependsOn`, `IGraphicConverter.BezierTo`) se sont
  révélés **non applicables** à l'examen — un constructeur d'attribut ne peut pas prendre
  `IEnumerable<T>` (CS0181), et `BitmapGraphicConverter.BezierTo` utilise un accès indexé
  (`points[^1]`) qui justifie de garder `params T[]`. Documenté, aucun changement de code.
- 14 : calcul d'en-tête de recherche binaire AAT factorisé dans
  `AatBinarySearchHeader.Compute(unitCount, unitSize)`, utilisé par les 5 tables concernées.
- 15 : docs XML ajoutées sur les champs privés de `LocaTable`.

Tous les items du TODO sont désormais traités (corrigés, ou documentés comme non applicables avec
justification).

**Relecture de la PR #432 (Codex)** : deux corrections supplémentaires apportées suite aux
commentaires de revue, chacune avec son propre commit + test — voir le détail dans les items 4 et 7
ci-dessus :
- `GlyphSimple.WriteData` : `endPtsOfContours` doit être cumulatif sur l'ensemble du glyphe, pas
  local à chaque contour (mon premier fix ne marchait que pour un glyphe à un seul contour).
- `TtfEncoderFactory.GetMicrosoftEncoding` : les IDs d'encodage Microsoft legacy CJK (2-6) utilisent
  des code pages spécifiques (932/936/950/949/1361), pas UTF-16BE comme je l'avais généralisé.

Ces deux corrections ont aussi nécessité de fusionner `origin/master` dans la branche
(`claude/fonts-fix-write-bugs`) après que la PR #431 (doc-only) a été squash-mergée entre-temps —
conflit add/add sur ce même fichier `TODO.md`, résolu en conservant la version de cette branche (sur-
ensemble strict de celle de master).

---

# Second audit qualité (2026-07-10, suite)

Deuxième passage, après que tous les items ci-dessus ont été corrigés (PRs #432, #434). Objectif :
trouver des problèmes **nouveaux**, non identifiés au premier passage, en particulier dans les
fichiers que celui-ci n'avait pas examinés en détail (`GlyphCompound`, PostScript, `Enums.cs`,
tables AAT non encore relues ligne à ligne). Aucune proposition ci-dessous n'est encore corrigée.
Numérotation reprise à 1 (indépendante de la section précédente).

## Bugs fonctionnels (priorité haute)

### 1. `TrueTypeFont.GetSpacingCorrection` — kerning indexé par code caractère au lieu de glyph index
`Utils.Fonts/TTF/TrueTypeFont.cs:458-468`, `Utils.Fonts/TTF/Tables/KernTable.cs:37-46`.
La table `kern` stocke des paires **d'index de glyphes**, jamais de codes caractères. Or
`TrueTypeFont.GetSpacingCorrection(char before, char after)` transmet directement les `char` à
`KernTable.GetSpacingCorrection`, qui les caste en `ushort` et les utilise tels quels comme clés —
sans jamais passer par `cmap` pour résoudre le glyph index, contrairement à `GetGlyph(char c)`
(lignes 433-449) qui le fait correctement via `cmap.CMaps`/`map.Map(c)`. Pour toute police réelle où
code caractère ≠ glyph index (la quasi-totalité des polices Unicode), le kerning renvoyé sera
systématiquement faux ou nul. `KernTableTests.RoundTrip_PreservesKerningPairs` masque ce bug car il
choisit des paires où le code caractère ASCII coïncide par construction avec le glyph index testé.
**Fix proposé** : `TrueTypeFont.GetSpacingCorrection` doit résoudre `before`/`after` en glyph index
via `cmap` avant d'appeler `kernTable.GetSpacingCorrection` ; `KernTable.GetSpacingCorrection`
devrait accepter des `ushort` (glyph index) plutôt que des `char`.
**Sévérité** : bug fonctionnel (kerning silencieusement incorrect sur toute police réelle non triviale).

### 2. `GlyphCompound.ComputeTransform` — mauvaise paire de coefficients pour `AdjustY`
`Utils.Fonts/TTF/Tables/Glyf/GlyphCompound.cs:84-97`.
Selon la spec (Apple TrueType Reference Manual, glyphes composites) : l'ajustement en X compare
`|a|` vs `|c|` (M11 vs M21), l'ajustement en Y compare `|d|` vs `|b|` (M22 vs M21 — même paire
"cisaillement", pas M12/M22). Le code calcule correctement `AdjustX`
(`Math.Abs(M11) - Math.Abs(M12)`)... mais calcule `AdjustY` avec `Math.Abs(M12) - Math.Abs(M22)`,
soit la paire (M12, M22) plutôt que (M22, M21). Cette paire n'est correcte que par coïncidence
quand la matrice est une similitude sans cisaillement (M12 == M21, le cas courant/testé
actuellement). Dès qu'un composant a un cisaillement asymétrique (M12 ≠ M21), le seuil de
doublement de `AdjustY` (33/65535) se déclenche sur la mauvaise condition.
**Fix proposé** : `Math.Abs(Math.Abs(M22) - Math.Abs(M21)) < limit` pour la branche `AdjustY`.
**Sévérité** : bug fonctionnel (portée limitée aux glyphes composites avec cisaillement asymétrique
— rare mais réel). *À revalider contre la spec primaire avant application, la formule exacte des
indices matriciels est subtile.*

### 3. `GlyphCompound.Transform` — mise à l'échelle inconditionnelle de l'offset de translation
`Utils.Fonts/TTF/Tables/Glyf/GlyphCompound.cs:113-118`.
`TranslateX`/`TranslateY` sont systématiquement multipliés par `AdjustX`/`AdjustY`. Or selon la spec
OpenType/TrueType, cette mise à l'échelle de l'offset ne doit s'appliquer que si le composant porte
le flag `SCALED_COMPONENT_OFFSET` (0x0800) ; si `UNSCALED_COMPONENT_OFFSET` (0x1000) est posé, ou si
aucun des deux n'est présent (cas le plus courant pour les polices produites avec la convention
Microsoft/FreeType), l'offset doit être utilisé brut. Ces deux flags sont absents de
`CompoundGlyfFlags` (item 4) et il n'y a aucune branche conditionnelle : le code applique toujours
le comportement "Apple historique". Pour un composant avec échelle non unitaire et un offset
`ArgsAreXY`, la position sera visiblement décalée par rapport aux rendus de référence (FreeType,
navigateurs).
**Fix proposé** : ajouter les flags à `CompoundGlyfFlags` (item 4), les lire dans `ReadData`, et
n'appliquer `AdjustX`/`AdjustY` à la translation que si `ScaledComponentOffset` est explicitement
posé (ne pas les appliquer par défaut, pour matcher le comportement de facto standard).
**Sévérité** : bug fonctionnel (positionnement incorrect des glyphes composites accentués/composés
dès qu'une échelle est présente — cas courant pour les caractères accentués Latin).

### 4. `CompoundGlyfFlags` — flags `SCALED_COMPONENT_OFFSET`/`UNSCALED_COMPONENT_OFFSET` manquants
`Utils.Fonts/Enums.cs:216-267`. Conséquence directe de l'item 3 : l'énumération ne déclare que 10
des 12 flags du spec glyf composite (il manque les bits 0x0800 et 0x1000).
**Fix proposé** : ajouter `ScaledComponentOffset = 0x0800` et `UnscaledComponentOffset = 0x1000`.
**Sévérité** : dette technique (prérequis du fix de l'item 3).

## Dette technique / non-conformité AGENTS.md

### 5. `TtfPlatformSpecificID` — `MAC_ROMAN` et `UNICODE_DEFAULT` partagent la valeur 0
`Utils.Fonts/Enums.cs:284-292`. Même famille de piège de nommage que celui déjà corrigé pour
`UNICODE_V2`/PRC (voir item 7 de la première section) : `MAC_ROMAN = 0` (sens Macintosh) et
`UNICODE_DEFAULT = 0` (sens Microsoft/Unicode) partagent la même valeur numérique dans une énumération
partagée entre plateformes. Fonctionne aujourd'hui car `TtfEncoderFactory.GetEncoding` ne compare
`platformSpecificID` à `MAC_ROMAN` que sous la garde `platformID == Macintosh`, mais toute
comparaison future à `UNICODE_DEFAULT` sans vérifier `platformID` en premier serait un bug latent.
**Fix proposé** : scinder en deux enums distincts par plateforme (`MacintoshEncodingID`,
`MicrosoftEncodingID`), ou documenter le piège par un commentaire XML sur chaque valeur partagée.
**Sévérité** : dette technique / cosmétique (latent, pas encore exploité par un bug réel).

### 6. `GaspTable` — commentaire XML dupliqué sur le constructeur
`Utils.Fonts/TTF/Tables/GaspTable.cs:46-50` : deux blocs `<summary>Initializes a new instance...`
consécutifs précèdent le même constructeur — artefact de copier-coller.
**Fix proposé** : supprimer le premier bloc dupliqué.
**Sévérité** : cosmétique.

### 7. `DsigTable` — doc XML de classe incohérente avec le code ("12-byte" vs 8 octets réels)
`Utils.Fonts/TTF/Tables/DsigTable.cs:14-21`. Le résumé de la classe affirme un en-tête de 12 octets
alors que `ReadData`/`WriteData`/`Length` (`8 + SignatureData.Length`) utilisent bien 8 octets
(`version` UInt32 + `numSigs` UInt16 + `flags` UInt16), la valeur correcte selon la spec OpenType
DSIG réelle. Le code est correct ; c'est la documentation qui ment.
**Fix proposé** : corriger "12-byte" en "8-byte" dans le commentaire XML.
**Sévérité** : cosmétique (doc trompeuse, AGENTS.md exige une doc XML exacte).

### 8. `PropTable` format 2 — incohérence mineure de convention de sentinel avec `BslnTable`
`Utils.Fonts/TTF/Tables/PropTable.cs:133` teste `if (firstGlyph == 0xFFFF) break;` alors que
l'équivalent dans `BslnTable.ReadLookupTable` (case 2) teste `if (lastGlyph == 0xFFFF) break;`. Les
deux fonctionnent en pratique (l'enregistrement sentinelle a ses deux champs à 0xFFFF), mais
l'incohérence entre deux implémentations quasi identiques du même format AAT suggère un copier-coller
partiellement adapté — augmente le risque qu'une future modification n'en corrige qu'une des deux.
**Fix proposé** : uniformiser sur un seul champ de test, éventuellement en factorisant le lecteur de
format 2 commun aux tables AAT (même esprit que `AatBinarySearchHeader`).
**Sévérité** : cosmétique / dette technique mineure (aucun bug actif).

### 9. `CidKeyedFont.GetGlyph(char c)` — limite CID > 0xFFFF non documentée
`Utils.Fonts/PostScript/CidKeyedFont.cs:18-44`. Le glyph est stocké par CID (`int`, pouvant dépasser
0xFFFF) mais `IFont.GetGlyph(char c)` ne peut représenter que des CID ≤ 0xFFFF — limitation de
l'interface `IFont`, pas de cette classe spécifiquement, mais non documentée sur cette méthode.
**Fix proposé** : étoffer le commentaire `<remarks>` pour documenter explicitement la limite de
plage (CID > 0xFFFF inatteignables via cette API).
**Sévérité** : cosmétique / documentation.

## Manque de test

### 10. Aucun test ne couvre `GlyphCompound` (glyphes composites)
`Utils.Fonts/TTF/Tables/Glyf/GlyphCompound.cs` — contrairement à `GlyphSimple` (fortement testé après
le premier audit), aucun `GlyphCompoundTests.cs` n'existe. C'est précisément la classe où se cachent
les bugs 2 et 3 : un test de round-trip (composant mis à l'échelle + cisaillement + offset, position
rendue vérifiée) les aurait détectés.
**Fix proposé** : ajouter `GlyphCompoundTests.cs` avec au minimum : (a) composant à échelle uniforme
+ offset non scellé, (b) composant `HasTwoByTwo` avec cisaillement asymétrique (M12 ≠ M21) pour
l'item 2, (c) test du flag `SCALED_COMPONENT_OFFSET`/`UNSCALED_COMPONENT_OFFSET` une fois l'item 3
corrigé.
**Sévérité** : manque de test (racine des bugs 2 et 3).

### 11. Aucun test ne couvre `KernTable.GetSpacingCorrection` avec un glyph index ≠ code caractère
`UtilsTest.Functional/Fonts/KernTableTests.cs` — le test existant choisit des valeurs où code
caractère == glyph index par construction, masquant le bug de l'item 1 plutôt que de le détecter.
**Fix proposé** : ajouter un cas où le glyph index (obtenu via un faux `cmap`) diffère du code
caractère, pour vérifier que `TrueTypeFont.GetSpacingCorrection` résout bien l'index avant
d'interroger `KernTable`.
**Sévérité** : manque de test (aurait révélé le bug 1 immédiatement).

### 12. Aucun test de round-trip bit-exact sur `TrueTypeFont.WriteFont()` complet
Toujours pas traité malgré la mention explicite dans la section précédente (item 16, "reste : round-
trip `WriteFont()` complet"). Tous les tests actuels valident chaque table isolément ; aucun ne
charge une police réelle complète, appelle `WriteFont()`, puis reparse le résultat pour vérifier
(bit-à-bit ou au moins glyphe-à-glyphe/table-à-table) l'identité avec l'original. C'est le seul type
de test qui pourrait détecter une interaction entre tables (décalages d'offset `loca`/`glyf`,
checksum global) qu'aucun test par-table ne peut voir.
**Fix proposé** : dans `TrueTypeFontTests.cs`, ajouter un test qui charge la police Arial embarquée
existante, appelle `WriteFont()`, reparse, et compare au minimum les métriques de plusieurs glyphes
(avance, contours, points) avant/après.
**Sévérité** : manque de test (déjà signalé au premier passage, toujours pas comblé).

## Résumé priorisé

| # | Sévérité | Fichier | Constat |
|---|---|---|---|
| 1 | Bug fonctionnel | `TrueTypeFont.cs` / `KernTable.cs` | Kerning indexé par char au lieu de glyph index — cassé sur toute police réelle |
| 3 | Bug fonctionnel | `GlyphCompound.cs` (Transform) | Offset de composant toujours mis à l'échelle, jamais conditionné aux flags SCALED/UNSCALED |
| 2 | Bug fonctionnel (portée limitée) | `GlyphCompound.cs` (ComputeTransform) | Mauvaise paire de coefficients pour AdjustY (cisaillement asymétrique) |
| 4 | Dette technique | `Enums.cs` | Flags SCALED/UNSCALED_COMPONENT_OFFSET manquants (prérequis de l'item 3) |
| 10 | Manque de test | `GlyphCompound.cs` | Aucun test — racine des bugs 2 et 3 |
| 11 | Manque de test | `KernTable.cs` | Test actuel masque le bug 1 par construction |
| 12 | Manque de test | `TrueTypeFont.cs` | Round-trip `WriteFont()` complet toujours absent |
| 5 | Dette technique | `Enums.cs` | Collision de valeur `MAC_ROMAN`/`UNICODE_DEFAULT` = 0 |
| 8 | Cosmétique/dette | `PropTable.cs` | Incohérence de convention de sentinel vs `BslnTable` |
| 6 | Cosmétique | `GaspTable.cs` | Doc XML dupliquée sur le constructeur |
| 7 | Cosmétique | `DsigTable.cs` | Doc de classe fausse ("12-byte" vs 8 réels) |
| 9 | Cosmétique | `CidKeyedFont.cs` | Doc incomplète sur la limite CID > 0xFFFF |

## Points vérifiés sans anomalie trouvée (second passage)
`Type3Font.cs`, `Type42Font.cs`, `PostScriptFont.cs`, `PostScriptGlyph.cs`, `CMapFormat0.cs`,
`CMapFormatBase.cs`, `Tag.cs`, `TrueTypeTable.cs`, `TrueTypeGlyph.cs`, `TableTypes.cs`,
`TTFTableAttribute.cs`, `Records.cs`, `HeadTable.cs`, `Os2Table.cs`, `HmtxTable.cs`, `VmtxTable.cs`,
`HheaTable.cs`, `VheaTable.cs`, `MaxpTable.cs`, `LocaTable.cs`, `NameTable.cs` (au-delà du fix
existant), `PostTable.cs` (au-delà du fix existant), `AvarTable.cs`, `FvarTable.cs`, `CvarTable.cs`,
`DsigTable.cs` (code, hors doc), `LtshTable.cs`, `PcltTable.cs`, `BslnTable.cs`, `LcarTable.cs`,
`OpbdTable.cs`, `PropTable.cs` (code, hors item 8), `TrakTable.cs`, `FeatTable.cs`, `FdscTable.cs`,
`FmtxTable.cs`, `HdmxTable.cs`, `CvtTable.cs`, `AcntTable.cs`, `TtfEncoderFactory.cs`,
`TtfHinting.cs`, `FontSupport.cs`, `IFont.cs`.
