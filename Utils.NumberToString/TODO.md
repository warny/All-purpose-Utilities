# Utils.NumberToString — Améliorations à venir

## Ambitieux

### 1. Ordinaux ZU (Zoulou) via `IOrdinalLanguageSpecifics`
Les ordinaux zoulou dépendent de la classe nominale du substantif (morphologie agglutinante),
ce qui rend l'approche XML insuffisante. Nécessite :
- Recherche linguistique (classes 1–17, préfixes de classe)
- Implémentation d'une classe `ZuluOrdinalLanguageSpecifics : IOrdinalLanguageSpecifics`

### 2. Ordinaux PL complets — accord genre × cas via `IOrdinalLanguageSpecifics`
L'implémentation actuelle couvre le nominatif masculin singulier.
Pour les formes complètes (féminin/neutre + 7 cas), une classe `PolishOrdinalLanguageSpecifics`
serait plus appropriée que de multiplier les `<OrdinalVariants>`.

### 3. `ConvertOrdinal` avec accord complet pour AR via `IOrdinalLanguageSpecifics`
L'arabe inverse le genre de l'ordinal par rapport au cardinal pour 3-10 (règle de polarité),
et possède une forme duelle. Nécessite une classe `ArabicOrdinalLanguageSpecifics`.

### 4. `onScale` — valeurs négatives (groupes décimaux)
L'attribut `onScale` sur `<Replacement>` ne couvre actuellement que les groupes entiers
(0 = unités, 1 = milliers, 2 = millions, …). Les groupes décimaux (dixièmes, centièmes, …)
passent par un chemin séparé (`Fractions` + digit-by-digit dans `Convert(decimal)`) et ne
traversent pas `ConvertRaw`. Les valeurs négatives (`onScale="-1"` etc.) sont donc réservées
pour un usage futur si ce chemin est un jour unifié avec `ConvertRaw`.

### 5. `ConvertYear` — extension à d'autres langues
Langues candidates pour un format split an :
- **RU** : « тысяча девятьсот восемьдесят четыре » (pas de split — fallback OK)
- **FR** : « mille neuf cent quatre-vingt-quatre » (pas de split — fallback OK)
- **IT** : 1984 → « millenovecentottantaquattro » (un seul mot en IT — pas de split)
- Conclusion : le split en deux moitiés est surtout pertinent pour EN, DE et NL.
