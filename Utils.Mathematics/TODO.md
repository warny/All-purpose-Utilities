# Utils.Mathematics — Audit qualité (2026-07-10)

Premier passage d'audit qualité sur `Utils.Mathematics/` (24 fichiers : algèbre linéaire, dérivation
symbolique, statistiques, etc.). Même méthodologie que les audits précédents (Utils.Geography,
Utils.Reflection, Utils.Fonts, Utils core) : exploration large puis vérification manuelle de chaque
piste par un calcul/trace concret avant de la retenir. Aucune proposition ci-dessous n'est encore
corrigée.

## Bugs fonctionnels

### 1. `Statistics.Median<T>` — mauvaise moitié retournée pour un nombre pair d'éléments
`Utils.Mathematics/Statistics.cs:133-141`. La doc affirme "For an even number of elements, returns
the lower of the two middle values", mais l'implémentation retourne `sorted[sorted.Length / 2]`. Pour
`[1, 3, 5, 7]` (trié), les deux valeurs du milieu sont aux index 1 et 2 (valeurs 3 et 5) ; la "plus
basse" est 3 (index 1), mais `sorted.Length / 2 = 4/2 = 2` retourne `sorted[2] = 5`, la valeur **haute**.
Le cas impair n'est pas affecté (`sorted.Length/2` coïncide avec l'unique index du milieu).
Le test existant `StatisticsTests.Median_EvenLength_ReturnsLowerMiddle` (data=`[1,3,5,7]`, attend
`5.0`) valide en réalité le comportement actuel (buggé) sous un nom qui prétend le contraire — il
n'a donc jamais pu détecter le problème.
**Fix proposé** : `sorted[(sorted.Length - 1) / 2]` (équivalent à l'actuel pour n impair, corrige le
cas pair). Corriger aussi la valeur attendue du test existant (qui doit passer à `3.0`, pas `5.0`).
**Sévérité** : bug fonctionnel (résultat statistique faux pour toute séquence de taille paire).

### 2. `MatrixTransformations.Skew<T>` — logique cassée à plusieurs niveaux, jamais testée
`Utils.Mathematics/LinearAlgebra/MatrixTransformations.cs:108-140`. Deux problèmes distincts vérifiés
par trace manuelle, sur une méthode sans aucun test (`Skew` n'apparaît dans aucun fichier de test) :

a) **Formule dimension↔nombre d'angles incohérente avec la boucle réelle** (ligne 112) :
   `dimension = (Math.Sqrt(4 * anglesArray.Length + 1) + 1) / 2`. Pour comparaison, `Rotation<T>`
   (ligne 152, juste en dessous) utilise la formule `(1 + Math.Sqrt(8 * n + 1)) / 2` pour une relation
   combinatoire différente (angles = C(dimension,2), correcte pour une matrice de rotation
   anti-symétrique). Avec 1 seul angle : `Skew` calcule `dimension ≈ 1.618` (n'est pas un entier →
   lève `ArgumentException`), alors que la double boucle de la méthode (voir point b) consomme en
   réalité `baseDimension²` angles pour une dimension `baseDimension` donnée — ni la formule de
   `Skew` (qui résout `n = m·(m-1)`) ni celle de `Rotation` (qui résout `n = m·(m-1)/2`) ne
   correspond à ce que la boucle consomme réellement.
b) **Calcul de colonne pour éviter la diagonale inversé** (ligne 133) :
   `int column = y >= x ? y : y + 1;`. Tracé à la main pour `baseDimension=3`, ligne `x=0` : pour
   `y=0,1,2`, la condition `y >= x` (`y >= 0`) est **toujours vraie**, donc `column` vaut toujours `y`
   sans jamais appliquer le décalage `+1` — la diagonale (colonne 0) est donc écrasée par
   `tan(angle)` au lieu d'être préservée à `T.One`, et la dernière colonne (colonne 3, la colonne de
   translation de la matrice homogène) n'est jamais atteinte pour cette ligne. La condition correcte
   pour "sauter" la colonne `x` est `column = y < x ? y : y + 1`.
**Fix proposé** : retraiter entièrement la méthode à partir de la définition mathématique voulue pour
une matrice de cisaillement homogène (nombre de paramètres libres, mapping ligne/colonne), avec un
test de round-trip/valeurs numériques calculé à la main avant implémentation (comme pour les bugs de
projection cartographique de Utils.Geography — ce type d'erreur ne se voit pas à la simple relecture).
**Sévérité** : bug fonctionnel majeur (la méthode ne peut actuellement construire aucune matrice de
cisaillement correcte dès que plus d'une dimension est en jeu ; lève une exception pour la plupart
des tailles d'entrée, et produit une matrice fausse pour les rares tailles qui passent la validation).

## Piste rejetée après vérification (ne pas re-signaler)
- **`params IEnumerable<T>` sur les factory methods de `MatrixTransformations`/`Polynomial`/`Vector`**
  (ex. `Diagonal<T>(params IEnumerable<T> values)`) — un balayage initial a signalé ceci comme un
  "anti-pattern" au motif que "`params` ne fonctionne qu'avec les tableaux en C#". C'est une
  information obsolète : ce projet cible C# 13/.NET 9, qui introduit les *params collections*
  (support de `params` pour toute collection constructible par expression de collection, dont
  `IEnumerable<T>`) — `Diagonal(1, 2, 3)` fonctionne bien avec expansion des arguments. Ce pattern est
  d'ailleurs déjà la convention délibérée et établie du projet (voir historique des changements
  "Cat 4" sur `Utils/`, `Utils.Fonts/`, etc.).

## Points vérifiés sans anomalie trouvée
Dérivation/simplification symbolique d'expressions (`ExpressionDerivation`, `ExpressionSimplifier`),
intégration numérique (Simpson), FFT, opérations matricielles de base (`Matrix<T>` : produit, inverse,
déterminant), `AffineSubspace<T>` (déjà couvert par 37 tests dédiés), `Line<T>` — aucune incohérence
trouvée après vérification manuelle sur des exemples numériques concrets.
