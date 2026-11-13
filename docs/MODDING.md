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
    ├── tactics/*.json          formations
    ├── rulesets/*.json         déroulement des matchs
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
  "tactic": "4-4-2",
  "kit": { "shirt": "#C8202C", "shorts": "#F2F2F2", "socks": "#C8202C" },
  "players": [
    { "number": 9, "name": "I. Ingram", "role": "forward",
      "attributes": { "pace": 70, "stamina": 60, "aggression": 55, "resilience": 60,
                      "passing": 58, "shooting": 75, "tackling": 40, "flair": 72 } }
  ]
}
```

- `role` : `goalkeeper`, `defender`, `midfielder` ou `forward`.
- `tactic` : id d'un fichier de `tactics/` (`.json` ou `.tac`). Les onze premiers joueurs de la liste sont
  titulaires. Celui qui a le rôle `goalkeeper` garde les buts ; les dix autres, dans l'ordre, occupent les dix
  postes de la tactique.
- Il faut au moins onze joueurs pour jouer un match.
- Les attributs sont sur l'échelle de `config/players.json › ratingScale` (0 à 100 par défaut). Leurs effets
  (multiplicateurs entre la pire et la meilleure note) se règlent dans le même fichier.
- Pour renommer ou recolorer une équipe existante, il suffit d'écrire les clés modifiées (voir `example-mod`).

## Tactiques

Les tactiques suivent le modèle de **Kick Off 2**. Pour chacun des 10 joueurs de champ (les joueurs 2 à 11 de
l'équipe ; le gardien est géré à part), le fichier donne une position par **zone du ballon**, plus 8 situations
particulières. Les joueurs vont à la position de la zone où se trouve le ballon, sans interpolation.

- Zones : le terrain est découpé en 3 colonnes × 4 rangées, vues depuis l'équipe. Elles sont numérotées par
  colonne : 0 à 3 pour la colonne gauche (de son propre but vers le but adverse), 4 à 7 pour le centre, 8 à 11
  pour la droite.
- Coordonnées en unités de tactique (`units`, 912 × 1392 dans Kick Off 2, soit 12 par yard). L'origine est le
  poteau de corner gauche du camp de l'équipe ; x va vers la touche droite, y vers le but adverse.

```json
{
  "id": "4-4-2",
  "nameKey": "tactic.442",
  "units": { "width": 912, "length": 1392 },
  "players": [
    {
      "defend": [[27, 56], [27, 205], "... 12 positions ..."],
      "attack": [[27, 115], "... 12 positions (facultatif) ..."],
      "kickOffOwn": [182, 313],
      "kickOffOpponent": [182, 313],
      "goalKickOwn": [146, 63],
      "goalKickOpponent": [146, 606],
      "cornerOwnLeft": [27, 56],
      "cornerOwnRight": [328, 56],
      "cornerOpponentLeft": [27, 724],
      "cornerOpponentRight": [328, 724]
    }
  ]
}
```

- `defend` : les positions de Kick Off 2, qui ne dépendent pas de la possession. `attack`, facultatif, est
  utilisé quand l'équipe a le ballon, comme dans Final Whistle.
- Coup d'envoi : le joueur dont `kickOffOwn` est le plus proche du rond central engage. Le jeu replace dans leur
  moitié les joueurs mal placés, et sort du rond central l'équipe qui n'engage pas.
- Les tactiques fournies (`4-4-2`, `4-3-3`) sont générées par `tools/generate_default_tactics.py`. Ce sont des
  positions originales, pas celles du jeu.

### Utiliser les fichiers `.tac` originaux

Si vous possédez Kick Off 2 ou Player Manager, vous pouvez déposer leurs fichiers de tactique dans un mod :
`mods/mes-tactiques/tactics/zbreak.tac`. Ils sont lus tels quels : 10 joueurs × 20 positions en entiers 16 bits
big-endian, les données suivantes étant ignorées. L'identifiant est le nom du fichier ; une équipe l'utilise avec
`"tactic": "zbreak"`. Aucun fichier original n'est fourni avec le jeu.

## Règlement et IA

- `rulesets/<id>.json` : durée réelle d'une mi-temps, minutes affichées, délais des célébrations, des remises en
  jeu et de la mi-temps, distances imposées aux adversaires, délai avant qu'un humain inactif laisse l'ordinateur
  tirer la remise en jeu, changement de côté.
- `config/ai.json` : niveaux de difficulté (réaction, intervalle de décision, nombre de poursuivants, portée et
  précision des tirs et des passes, temps de garde du ballon, délai des remises en jeu), placement (rayons,
  distances de passe, seuil de décision) et gardien (sortie, ramassage, temps de garde, dégagement).
- `config/controls.json` : `keeper` (mains du gardien), `selection` (choix du joueur contrôlé), `setPieces`
  (puissance et hauteur des remises en jeu de base selon la durée d'appui).

## Terrains et surfaces

- `pitches/<id>.json` : dimensions en mètres, surface utilisée et apparence (`visual` : texture d'herbe, texture
  des abords, largeur et teinte des bandes de tonte, couleur des lignes et des buts, texture de filet).
- `surfaces/<id>.json` : multiplicateurs physiques (roulement, rebond, traînée, déviations aléatoires, fatigue,
  adhérence des joueurs). Une nouvelle surface fonctionne sans code : la physique ne connaît que ces nombres.

## Paramètres de jeu

| Fichier | Contenu |
|---|---|
| `config/physics.json` | Gravité, ballon, buts, panneaux, locomotion des joueurs |
| `config/controls.json` | Portées, fenêtres de timing, puissances de frappe, aftertouch, gardien, sélection, remises en jeu |
| `config/ai.json` | Niveaux et comportements de l'ordinateur |
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
