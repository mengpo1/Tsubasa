# Football arcade modulaire

Jeu de football 2D vu de dessus, en C# / MonoGame, qui vise les **mécaniques et les sensations** de
*Kick Off 2* et de *The Final Whistle* (Dino Dini, 1990-1991), sans en reprendre le code ni les ressources.
Le ballon n'est jamais collé au pied : on le pousse, on le perd, on le contrôle, on le frappe et on le
courbe avec l'aftertouch, avec un seul bouton dont le sens dépend du timing.

**État actuel : lots 0 à 2.** On peut jouer un **match complet contre l'ordinateur** :
- deux équipes de onze, avec sélection automatique du joueur le plus proche du ballon ;
- une IA élémentaire et un gardien de base ;
- score, chronomètre, mi-temps avec changement de côté ;
- coups d'envoi, touches, corners et six mètres.

Deux **apparences** partagent exactement le même jeu : *Custom*, avec les graphismes originaux du projet, et
*Classic Amiga*, construite sur votre machine à partir de **votre propre copie** du jeu par un importeur local.
Aucune ressource originale n'est fournie (voir [Apparence Classic Amiga](#apparence-classic-amiga)).

Il existe aussi un terrain d'entraînement pour travailler les gestes. Pas encore de tacles, de têtes, de fautes,
de hors-jeu ni de menus (lots 3 à 5). Le détail de ce qui est implémenté est dans [docs/GAMEPLAY.md](docs/GAMEPLAY.md).

## Prérequis

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) (macOS Apple Silicon : choisir l'installeur Arm64).
- Rien d'autre : MonoGame DesktopGL et ses bibliothèques natives (SDL2, OpenAL) arrivent par NuGet.
  Le pipeline de contenu MonoGame (MGCB) n'est **pas** utilisé : textures PNG et polices TTF sont lues à l'exécution.

## Compiler et lancer

```bash
dotnet build Tsubasa.slnx
```

```bash
dotnet run --project src/Arcade.Desktop
```

Par défaut, le jeu lance un **match amical** : vous dirigez l'équipe à domicile contre l'ordinateur.

En développement, le jeu lit `content/` et `mods/` directement dans le dépôt : modifiez un fichier JSON
(par exemple `content/config/controls.json`) puis appuyez sur **F5** pour recharger sans recompiler.

Options utiles (`dotnet run --project src/Arcade.Desktop -- --help`) :

| Option | Effet |
|---|---|
| `--mode training` | Terrain d'entraînement au lieu du match |
| `--players 0` | Ordinateur contre ordinateur (pour observer l'IA) |
| `--players 2` | Deux joueurs : flèches + Espace contre W/A/S/D + Maj gauche (ou deux manettes) |
| `--home reds --away blues` | Équipes (dossier `content/teams/`) |
| `--level easy` | Niveau de l'ordinateur : `easy`, `normal` ou `hard` |
| `--half-minutes 5` | Durée réelle d'une mi-temps (3 par défaut) |
| `--debug` | Démarre avec l'overlay debug (états et cibles de l'IA) |
| `--lang fr` | Langue (sinon `settings.json`, sinon anglais) |
| `--mod example-mod` | Active un mod (répétable ; l'ordre donne la priorité) |
| `--no-mods` | Désactive tous les mods |
| `--pitch icy` | Terrain : `normal`, `wet`, `soggy`, `artificial`, `icy`, `muddy`, `bumpy` |
| `--scale 3` | Taille de fenêtre (multiple de la résolution virtuelle 640×360) |
| `--fullscreen` | Plein écran |

### Version autonome par plateforme

```bash
dotnet publish src/Arcade.Desktop -c Release -r osx-arm64 --self-contained -o publish/macos
```

Remplacez `osx-arm64` par `osx-x64`, `win-x64` ou `linux-x64`. Les dossiers `content/` et `mods/` sont
copiés à côté de l'exécutable. Sur macOS, un exécutable non signé peut demander une autorisation
(Réglages Système › Confidentialité et sécurité).

## Commandes

| Action | Clavier (joueur 1) | Manette |
|---|---|---|
| Courir (8 directions) | Flèches | Stick gauche ou croix |
| Bouton d'action | Espace ou Ctrl droit | A ou B |
| Joueur 2 (`--players 2`) | W/A/S/D + Maj gauche ou Q (sur AZERTY, remappez en Z/Q/S/D dans `content/config/input.json`) | Deuxième manette |

Le bouton unique est contextuel :

- **Dribbler** : courir dans le ballon (sans bouton) le pousse devant soi ; plus on court vite, plus il part loin.
- **Tirer** : appuyer **juste après** avoir touché le ballon. Le tir part dans la direction du joueur.
- **Contrôler** : appuyer **avant** le contact et maintenir. Le joueur arrête le ballon.
- **Passer** : ballon contrôlé, bouton maintenu, viser avec le joystick puis relâcher. Plus on vise longtemps, plus la passe est forte.
- **Garder le ballon** : relâcher le bouton joystick au centre. Une direction repart en dribble, le bouton tire.
- **Ballon piqué** : inverser le joystick quand le ballon est au pied.
- **Aftertouch** : juste après une frappe, pousser le joystick sur le côté (ou en diagonale avant) courbe le ballon.

En match, vous contrôlez automatiquement le joueur de champ le plus proche du ballon. Le gardien est géré par
l'ordinateur, comme dans Kick Off 2. Pour une remise en jeu (coup d'envoi, touche, corner), visez avec le joystick,
maintenez le bouton pour doser et relâchez pour frapper. Sans appui pendant 4 s, l'ordinateur la joue pour vous.
Après le coup de sifflet final, le bouton relance un match.

Touches de l'entraînement : **R** replacer le ballon · **T** aftertouch on/off · **F3** terrain suivant ·
**F7** langue suivante · **X** radar · **[ / ]** zoom · **P** pause · **F1** debug · **F5** recharger les données ·
**F6** activer/désactiver les mods · **F8** apparence suivante · **F11** plein écran · **Échap** quitter.
Toutes les touches sont remappables dans `content/config/input.json`.

## Tests

```bash
dotnet test Tsubasa.slnx
```

Les tests n'ouvrent aucune fenêtre. Ils couvrent la simulation, les contrôles, le déroulement des matchs
(arbitre, remises en jeu, mi-temps), l'IA, les données réelles du jeu, les mods et les traductions.

### Exécution scriptée et captures d'écran

Le jeu peut rejouer une séquence d'entrées (60 Hz fixes) et enregistrer des captures, utile pour vérifier le rendu
sans clavier (par exemple sous Linux avec `xvfb-run`) :

```bash
dotnet run --project src/Arcade.Desktop -- --mode training --script tools/scripts/dribble-shot-goal.json --capture-ticks 300,320,345 --exit-tick 350
```

Les images sont écrites dans `artifacts/screenshots/`. Autres scripts : `trap-and-pass.json` et `chip.json`
(entraînement, avec `--mode training`), ainsi que `match-human.json`.

Pour observer un match entier sans intervenir :

```bash
dotnet run --project src/Arcade.Desktop -- --players 0 --debug
```

## Apparence Classic Amiga

Si vous possédez Kick Off 2 ou The Final Whistle sur Amiga, convertissez vos fichiers une fois :

```bash
dotnet run --project tools/Arcade.Importer -- --adf "KickOff2.adf" --set-default
```

L'importeur accepte aussi un second disque (`--adf` répété : Kick Off 2 puis The Final Whistle) ou des fichiers
déjà extraits (`--files <dossier>`). Il écrit les images dans `content/themes/classic-amiga/imported/`, dossier
exclu de Git, avec un rapport `IMPORT_REPORT.md` : ressources installées, manquantes et hypothèses à vérifier.

Ensuite, `--theme classic-amiga`, `"theme"` dans `settings.json` ou **F8** en jeu. Sans import, l'apparence
reste utilisable avec des remplacements temporaires signalés à l'écran. Détails, correspondance des animations
et limites : [docs/THEMES.md](docs/THEMES.md).

## Mods

Un mod est un dossier `mods/<id>/` avec un `manifest.json` et des fichiers qui remplacent ou complètent ceux de `content/`.
Le mod de démonstration change le terrain, le ballon, le nom et les couleurs d'une équipe, un paramètre physique
et des textes :

```bash
dotnet run --project src/Arcade.Desktop -- --mod example-mod
```

Pour l'activer en permanence, créez un fichier `settings.json` à la racine (non versionné) :

```json
{ "language": "fr", "mods": ["example-mod"] }
```

Pour le retirer, supprimez l'entrée (ou le dossier). En jeu, **F6** active ou désactive les mods à la volée.
Le format complet est décrit dans [docs/MODDING.md](docs/MODDING.md).

## Documentation

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) : structure du code, boucle de simulation, chargement des données.
- [docs/GAMEPLAY.md](docs/GAMEPLAY.md) : mécaniques implémentées, ce qui est documenté, estimé ou choisi.
- [docs/MODDING.md](docs/MODDING.md) : créer un mod.
- [docs/THEMES.md](docs/THEMES.md) : apparences, import de votre copie du jeu, animations, cadrage.
- [docs/RESEARCH.md](docs/RESEARCH.md) : synthèse des manuels originaux et points à calibrer.
- [docs/REFERENCE_ANALYSIS.md](docs/REFERENCE_ANALYSIS.md) : ce qui est repris d'Open Kick-Off, du format des
  tactiques, de la Competition Version et des écrits de Dino Dini.
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) : licences et attributions.

## Ressources et licences

Tous les graphismes du dépôt sont des ressources temporaires originales, générées par
`tools/generate_placeholder_art.py`. La police DejaVu Sans est distribuée sous sa licence
(`content/assets/fonts/LICENSE-DejaVu.txt`). Le dépôt ne contient aucun sprite, son, logo ni code du jeu
commercial. L'apparence Classic Amiga n'utilise que des fichiers convertis sur votre machine, à partir de votre
copie, pour un usage personnel. Ils sont exclus de Git et ne doivent pas être partagés.
