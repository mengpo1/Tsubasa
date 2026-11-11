# Créer un mod

Un mod modifie l'apparence et les données du jeu **sans toucher au code** : terrains, sprites, équipes,
paramètres physiques, contrôles, traductions. Un changement profond de règles nécessitera du code C#. Il n'existe
volontairement pas de langage de script.

## Arborescence

```text
mods/
└── mon-mod/                    le nom du dossier = l'id du mod
    ├── manifest.json           obligatoire
    ├── assets.json             ids logiques → fichiers (fusionné avec celui de base)
    ├── assets/textures/*.png   nouvelles images
    ├── config/physics.json     seulement les valeurs modifiées
    ├── pitches/normal.json     modifie un terrain existant
    ├── pitches/stade-nuit.json ajoute un terrain
    ├── surfaces/*.json
    ├── teams/*.json
    └── locales/fr.json, locales/de.json ...
```

Les fichiers et dossiers dont le nom commence par `.` (par exemple `.DS_Store`) sont ignorés.

## Manifeste

```json
{
  "id": "mon-mod",
  "name": "Mon mod",
  "version": "1.0.0",
  "formatVersion": 1,
  "description": "Optionnel",
  "authors": ["Moi"]
}
```

- `id` : lettres minuscules, chiffres, `-`, `_`, `.`. Il doit être identique au nom du dossier.
- `formatVersion` : version du format de données. Un mod qui déclare une version plus récente que celle comprise
  par le jeu (actuellement `1`) est refusé avec un message.
- Le contenu du mod est défini par ses fichiers, pas par le manifeste. Au chargement, le journal liste chaque
  fichier du mod et indique s'il **remplace** (`overrides`) ou **ajoute** (`adds`) un fichier de base.

## Activer, ordonner, retirer

- Ligne de commande : `--mod mon-mod` (répétable), `--no-mods`.
- Fichier `settings.json` à la racine du jeu : `{ "mods": ["mon-mod", "autre-mod"] }`.
- En jeu : **F6** active ou désactive les mods. Sans liste configurée, F6 active tous les mods installés.
  **F5** recharge les données.

**Priorité** : les données de base d'abord, puis les mods dans l'ordre de la liste. En cas de conflit, le dernier
gagne. Pour retirer un mod, enlevez-le de la liste ou supprimez son dossier. Rien n'est modifié dans `content/`.

## Règles de fusion

Une seule règle, appliquée à tous les fichiers :

- **JSON** : les fichiers de même chemin sont fusionnés (JSON Merge Patch, RFC 7386). Les objets sont fusionnés
  clé par clé, récursivement. Une valeur simple ou un tableau **remplace** la précédente. `null` **supprime** la
  clé. Il suffit donc d'écrire les valeurs modifiées :

  ```json
  // mods/mon-mod/config/physics.json
  { "ball": { "rollingResistance": 0.9 } }
  ```

- **Binaires** (PNG, TTF) : le fichier du pack le plus prioritaire remplace les autres.
- **Collections** (`teams/`, `pitches/`, `surfaces/`, `locales/`) : chaque fichier est un élément. Un nouveau nom
  de fichier ajoute un élément ; un nom existant le modifie par fusion. L'`id` déclaré dans le fichier doit être
  égal au nom du fichier.

Les commentaires `//` et les virgules finales sont acceptés dans tous les JSON.

## Ressources graphiques

Le code n'utilise que des **identifiants logiques**. `assets.json` les relie aux fichiers :

```json
{
  "textures": {
    "pitch.grass.normal": "assets/textures/grass_neon.png",
    "ball.default": "assets/textures/ball_gold.png"
  }
}
```

Identifiants utilisés actuellement :

| Id | Rôle |
|---|---|
| `player.sheet` | Planche des joueurs (animations `player.*`) |
| `ball.default` | Planche du ballon (animation `ball.roll`) |
| `shadow.default` | Ombre (joueurs et ballon) |
| `goal.net` | Motif de filet, répété |
| `ui.selection` | Marqueur du joueur contrôlé (teinté en jaune) |
| `pitch.surround` | Sol autour du terrain, répété |
| `pitch.grass.<terrain>` | Herbe de chaque terrain, répétée (référencée par `pitches/*.json › visual.grassTexture`) |
| Polices `ui.default`, `ui.bold` | Polices TrueType (TTF/OTF), Unicode complet |

Un fichier absent ou un id inconnu est signalé dans le journal et remplacé par un damier magenta et noir, bien visible.

### Animations

```json
"animations": {
  "player.run": {
    "texture": "player.sheet",
    "frameWidth": 32, "frameHeight": 32,
    "directions": 8,
    "frames": [1, 2, 3, 4, 5, 6],
    "frameDuration": 0.065,
    "referenceSpeed": 8.0,
    "anchor": [16, 29],
    "offset": [0, 0],
    "palette": [ { "marker": "#FF0000", "slot": "shirt" }, { "marker": "#C00000", "slot": "shirt", "brightness": 0.72 } ]
  }
}
```

- La planche est une grille de `frameWidth × frameHeight`. `frames` donne les colonnes jouées.
- `directions: 8` : la ligne `r` correspond à la direction `r`. L'ordre est haut, haut-droite, droite, bas-droite,
  bas, bas-gauche, gauche, haut-gauche. Avec `directions: 1`, seule la première ligne est utilisée.
- `anchor` : le pixel de la frame posé sur la position au sol de l'entité (entre les pieds). `offset` décale
  l'affichage. Les sprites peuvent avoir **n'importe quelle taille** : la collision ne dépend pas du sprite.
- `referenceSpeed` (optionnel) : l'animation avance avec la distance parcourue, les pieds suivent donc la vitesse.
  À 0, elle avance avec le temps.
- `palette` (optionnel) : couleurs repères remplacées par les couleurs de la tenue de l'équipe (`shirt`, `shorts`,
  `socks`), avec un facteur de luminosité. Une seule planche sert à toutes les équipes.
- Animations utilisées : `player.idle`, `player.run`, `player.kick`, `ball.roll`.

## Équipes

```json
{
  "id": "reds",
  "name": "Redcliff Rovers",
  "shortName": "RED",
  "kit": { "shirt": "#C8202C", "shorts": "#F2F2F2", "socks": "#C8202C" },
  "players": [
    { "number": 9, "name": "I. Ingram", "role": "forward",
      "attributes": { "pace": 70, "stamina": 60, "aggression": 55, "resilience": 60,
                      "passing": 58, "shooting": 75, "tackling": 40, "flair": 72 } }
  ]
}
```

- `role` : `goalkeeper`, `defender`, `midfielder` ou `forward`.
- Les attributs sont sur l'échelle de `config/players.json › ratingScale` (0 à 100 par défaut). Leurs effets
  (multiplicateurs entre la pire et la meilleure note) se règlent dans le même fichier.
- Pour renommer ou recolorer une équipe existante, il suffit d'écrire les clés modifiées (voir `example-mod`).

## Terrains et surfaces

- `pitches/<id>.json` : dimensions en mètres, surface utilisée et apparence (`visual` : texture d'herbe, texture
  des abords, largeur et teinte des bandes de tonte, couleur des lignes et des buts, texture de filet).
- `surfaces/<id>.json` : multiplicateurs physiques (roulement, rebond, traînée, déviations aléatoires, fatigue,
  adhérence des joueurs). Une nouvelle surface fonctionne sans code : la physique ne connaît que ces nombres.

## Paramètres de jeu

| Fichier | Contenu |
|---|---|
| `config/physics.json` | Gravité, ballon, buts, panneaux, locomotion des joueurs |
| `config/controls.json` | Portées, fenêtres de timing, puissances de frappe, aftertouch |
| `config/players.json` | Échelle et effets des attributs, fatigue |
| `config/display.json` | Résolution virtuelle, échelle, caméra, radar (affichage seulement) |
| `config/input.json` | Touches et boutons (noms des énumérations MonoGame `Keys` et `Buttons`) |

Chaque valeur est bornée. Une valeur hors limites ou une **clé inconnue** (faute de frappe) produit une erreur
qui nomme le fichier, la clé et les packs concernés.

## Traductions

`locales/<code>.json` contient des paires clé → texte :

```json
{
  "@name": "Deutsch",
  "@plural": "one",
  "hud.training": "Training",
  "hud.goals.one": "{count} Tor",
  "hud.goals.other": "{count} Tore"
}
```

- Un mod peut modifier quelques chaînes d'une langue existante ou **ajouter une langue** avec un nouveau fichier.
  **F7** fait défiler les langues disponibles.
- Les paramètres sont nommés (`{count}`, `{name}`), ce qui permet à chaque langue de placer les mots dans son
  propre ordre.
- `@plural` choisit la règle des pluriels : `one` (anglais, allemand...), `zero-one` (français : 0 et 1 au
  singulier) ou `none` (japonais, chinois...). Les formes sont les clés `….one` et `….other`.
- Une clé absente utilise l'anglais. Si elle manque aussi en anglais, le jeu affiche `[clé]` et la journalise.
- La police fournie (DejaVu Sans) couvre le latin, le grec et le cyrillique. Pour d'autres écritures, remplacez
  `ui.default` et `ui.bold` par une police adaptée.

## Validation et erreurs courantes

| Message | Cause |
|---|---|
| `Mod rejected: manifest.json: id ... must contain only lowercase` | Id invalide |
| `manifest id 'x' does not match the folder name 'y'` | Le dossier et l'id diffèrent |
| `formatVersion 2 is newer than the supported version 1` | Mod prévu pour une version plus récente du jeu |
| `'teams/x.json' is malformed JSON: ...` | Erreur de syntaxe : le mod entier est refusé |
| `Invalid data in 'config/physics.json' (packs: base > mon-mod): ... 'rollingResistanse' ...` | Clé inconnue (faute de frappe) |
| `Invalid values in '...': ball.radius must be between ...` | Valeur hors limites |
| `Mods disabled: ...` | Les mods produisent des données incohérentes : le jeu repart avec les données de base |
| `Texture file not found, using placeholder` | Chemin d'image erroné dans `assets.json` |
| `Invalid content path '../x.png'` | Les chemins absolus ou contenant `..` sont interdits |

Les problèmes sont écrits dans la console et résumés en haut à droite de l'écran.

## Mod de démonstration

`mods/example-mod` (« Neon Night ») modifie :

- le **terrain** : herbe néon (`assets/textures/grass_neon.png`), lignes cyan ;
- le **ballon** : sprite doré ;
- une **équipe** : `reds` devient « Neon Rangers » (NEO), nouvelles couleurs, effectif conservé ;
- un **paramètre physique** : `ball.rollingResistance` passe de 1,4 à 0,9 (le ballon roule plus loin) ;
- des **chaînes traduites** : le titre de l'entraînement en anglais et en français.

```bash
dotnet run --project src/Arcade.Desktop -- --mod example-mod --lang fr
```

Les tests `GameDataTests.Example_mod_changes_pitch_team_physics_and_textures` et
`LocalizerTests.A_mod_can_override_a_single_translated_string` vérifient que ces fichiers viennent bien du mod.
