# Football arcade modulaire

Jeu de football 2D vu de dessus, en C# / MonoGame, qui vise les **mécaniques et les sensations** de
*Kick Off 2* et de *The Final Whistle* (Dino Dini, 1990-1991), sans en reprendre le code ni les ressources.
Le ballon n'est jamais collé au pied : on le pousse, on le perd, on le contrôle, on le frappe et on le
courbe avec l'aftertouch, avec un seul bouton dont le sens dépend du timing.

**État actuel : lots 0 et 1** (base technique et sensations fondamentales). Un joueur, un ballon et deux buts
sur un terrain d'entraînement. Pas encore d'adversaires, d'IA ni de match : voir
[docs/GAMEPLAY.md](docs/GAMEPLAY.md) pour ce qui est réellement implémenté.

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

En développement, le jeu lit `content/` et `mods/` directement dans le dépôt : modifiez un fichier JSON
(par exemple `content/config/controls.json`) puis appuyez sur **F5** pour recharger sans recompiler.

Options utiles (`dotnet run --project src/Arcade.Desktop -- --help`) :

| Option | Effet |
|---|---|
| `--lang fr` | Langue (sinon `settings.json`, sinon anglais) |
| `--mod example-mod` | Active un mod (répétable ; l'ordre donne la priorité) |
| `--no-mods` | Désactive tous les mods |
| `--pitch icy` | Terrain d'entraînement : `normal`, `wet`, `soggy`, `artificial`, `icy`, `muddy`, `bumpy` |
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

Le bouton unique est contextuel :

- **Dribbler** : courir dans le ballon (sans bouton) le pousse devant soi ; plus on court vite, plus il part loin.
- **Tirer** : appuyer **juste après** avoir touché le ballon. Le tir part dans la direction du joueur.
- **Contrôler** : appuyer **avant** le contact et maintenir. Le joueur arrête le ballon.
- **Passer** : ballon contrôlé, bouton maintenu, viser avec le joystick puis relâcher. Plus on vise longtemps, plus la passe est forte.
- **Garder le ballon** : relâcher le bouton joystick au centre. Une direction repart en dribble, le bouton tire.
- **Ballon piqué** : inverser le joystick quand le ballon est au pied.
- **Aftertouch** : juste après une frappe, pousser le joystick sur le côté (ou en diagonale avant) courbe le ballon.

Touches de l'entraînement : **R** replacer le ballon · **T** aftertouch on/off · **F3** terrain suivant ·
**F7** langue suivante · **X** radar · **[ / ]** zoom · **P** pause · **F1** debug · **F5** recharger les données ·
**F6** activer/désactiver les mods · **F11** plein écran · **Échap** quitter.
Toutes les touches sont remappables dans `content/config/input.json`.

## Tests

```bash
dotnet test Tsubasa.slnx
```

Les tests n'ouvrent aucune fenêtre. Ils couvrent la simulation, les contrôles, les données réelles du jeu,
les mods et les traductions.

### Exécution scriptée et captures d'écran

Le jeu peut rejouer une séquence d'entrées (60 Hz fixes) et enregistrer des captures, utile pour vérifier le rendu
sans clavier (par exemple sous Linux avec `xvfb-run`) :

```bash
dotnet run --project src/Arcade.Desktop -- --script tools/scripts/dribble-shot-goal.json --capture-ticks 300,384,400 --exit-tick 410
```

Les images sont écrites dans `artifacts/screenshots/`. Autres scripts : `trap-and-pass.json` et `chip.json`.

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
- [docs/RESEARCH.md](docs/RESEARCH.md) : synthèse des manuels originaux et points à calibrer.

## Ressources et licences

Tous les graphismes sont des ressources temporaires originales, générées par `tools/generate_placeholder_art.py`.
La police DejaVu Sans est distribuée sous sa licence (`content/assets/fonts/LICENSE-DejaVu.txt`).
Aucun sprite, son, logo ni code du jeu commercial n'est utilisé.
