Ce projet utilise dotnet 9.
Toutes les méthodes et les classes doivent être documentées en anglais, même si elles sont privées.
Les méthodes qui manipulent des flux de données doivent être commentées.
Dans la mesure du possible, je préfère m'appuyer sur des classes contenant des données d'un côté et des classes contenant leur traitement de l'autre. Je préfère quand le traitement s'appuie sur une interface, y compris sur une interface générique si nécessaire.

Il faut systématiquement écrire un test.

Les tableaux doivent utiliser la syntaxe avec des crochets.
Si une méthode utilise des params, et si la lecture des éléments est séquentielle, alors elle doit utiliser params IEnumerable<T>

Si une méthode doit lire le contenu d'un fichier, alors elle doit seulement ouvrir le fichier appeler une méthode qui traite le contenu lui-même

Si on recontre un switch avec un grand nombre de cas différents (le code dépasse 10 cas ou 30 lignes au total), celui-ci doit être remplacé par dictionnaire <cas, méthode> ou chaque méthode traite un cas particulier.
