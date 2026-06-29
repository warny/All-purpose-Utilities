# Utils.NumberToString — Améliorations à venir

## Rapides (faible effort)

### 1. Mettre à jour README.md — tableau des cultures
Les langues PL, RU, AR, HI, EL, WO ont reçu le support ordinal (PRs #391 et #393) mais le tableau les indique encore comme « not yet implemented ».

| Code | Ordinals | Variants |
|---|---|---|
| RU | ✓ | — |
| AR | ✓ (1-10 masculin) | — |
| HI | ✓ | — |
| EL | ✓ | gender (αρσενικό/θηλυκό/ουδέτερο) pour 1-12 |
| FI | ✓ | sijamuoto (nominatiivi/partitiivi/genetiivi) |
| PL | ✓ | — |
| WO | ✓ | — |

### 2. `ConvertOrdinal(long)` — surcharge manquante
L'interface `INumberToStringConverter` expose seulement `ConvertOrdinal(int)`.
Un overload `ConvertOrdinal(long)` et `ConvertOrdinal(long, params string[])` permettrait les grands ordinaux (> 2,1 milliards), cohérent avec `Convert(long)`.

---

## Moyens

### 3. `<YearFormat>` pour DE et NL — lecture scindée des années
En allemand et en néerlandais, les années 1100-1999 sont communément lues en deux moitiés :
- DE : 1984 → « neunzehnhundertvierundachtzig » (19 × hundert + 84)
  `hundredWord="hundert"`, `SplitRange from="1100" to="1999"`
- NL : 1984 → « negentienhonderd vierennachtig » (19 × honderd + 84)
  `hundredWord="honderd"`, `SplitRange from="1100" to="1999"`

Implémentation : ajouter `<YearFormat>` dans `DE.xml` et `NL.xml` (même pattern que `EN.xml`).

### 4. Variantes de genre pour les ordinaux HI
Les ordinaux hindi ont une forme masculine et une forme féminine :

| n | Masculin (défaut) | Féminin |
|---|---|---|
| 1 | पहला | पहली |
| 2 | दूसरा | दूसरी |
| 3 | तीसरा | तीसरी |
| 4 | चौथा | चौथी |
| 5 | पांचवाँ | पांचवीं |
| 6 | छठा | छठी |

Implémentation : `<OrdinalVariants>` dans `HI.xml` avec `<Variant type="gender" variant="strī">`.

### 5. Variantes de genre pour les ordinaux AR
Les ordinaux arabes ont une forme féminine distincte pour 1-10 :

| n | Masculin (défaut) | Féminin |
|---|---|---|
| 1 | أول | أولى |
| 2 | ثانٍ | ثانية |
| 3 | ثالث | ثالثة |
| 4-10 | (suffixe ة) | |

Implémentation : `<OrdinalVariants>` dans `AR.xml` avec `<Variant type="gender" variant="muʾannath">`.

### 6. Support des `<Ordinal>` (word rules) dans les blocs `<Variant>`
Actuellement, `<OrdinalVariants>` n'accepte que des `<OrdinalException>` (valeurs fixes).
Permettre aussi `<Ordinal from="..." to="...">` à l'intérieur d'un `<Variant>` rendrait possible
la déclinaison systématique pour EL (masculin → féminin par règle plutôt qu'exception par exception)
et pour de futurs langues.

Implémentation :
- Étendre `OrdinalVariantType` dans le XSD pour accepter `<Ordinal>`.
- Ajouter `List<OrdinalRuleType> Rules` sur `OrdinalVariantRule` (C#).
- Modifier `ApplyOrdinalTransform` pour appliquer les rules du variant après les word rules globales.

---

## Ambitieux

### 7. Ordinaux ZU (Zoulou) via `IOrdinalLanguageSpecifics`
Les ordinaux zoulou dépendent de la classe nominale du substantif (morphologie agglutinante),
ce qui rend l'approche XML insuffisante. Nécessite :
- Recherche linguistique (classes 1–17, préfixes de classe)
- Implémentation d'une classe `ZuluOrdinalLanguageSpecifics : IOrdinalLanguageSpecifics`

### 8. Ordinaux PL complets — accord genre × cas via `IOrdinalLanguageSpecifics`
L'implémentation actuelle couvre le nominatif masculin singulier.
Pour les formes complètes (féminin/neutre + 7 cas), une classe `PolishOrdinalLanguageSpecifics`
serait plus appropriée que de multiplier les `<OrdinalVariants>`.

### 9. `ConvertOrdinal` avec accord complet pour AR via `IOrdinalLanguageSpecifics`
L'arabe inverse le genre de l'ordinal par rapport au cardinal pour 3-10 (règle de polarité),
et possède une forme duelle. Nécessite une classe `ArabicOrdinalLanguageSpecifics`.

### 10. `ConvertYear` — extension à d'autres langues
Langues candidates pour un format split an :
- **RU** : « тысяча девятьсот восемьдесят четыре » (pas de split — fallback OK)
- **FR** : « mille neuf cent quatre-vingt-quatre » (pas de split — fallback OK)
- **IT** : 1984 → « millenovecentottantaquattro » (un seul mot en IT — pas de split)
- Conclusion : le split en deux moitiés est surtout pertinent pour EN, DE et NL.
