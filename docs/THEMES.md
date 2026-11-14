# Apparences (thèmes) et import Classic Amiga

Le jeu propose deux apparences qui partagent exactement le même jeu : mêmes règles, même physique, mêmes données.
Seuls l'image et le cadrage changent.

| Apparence | Dossier | Contenu |
|---|---|---|
| **Custom** (par défaut) | `content/themes/custom/` | Graphismes originaux du projet. Point de départ pour une apparence personnelle |
| **Classic Amiga** | `content/themes/classic-amiga/` | Aspect de Kick Off 2 / The Final Whistle sur Amiga, construit **localement** à partir de **votre** copie du jeu |

Choix de l'apparence : `--theme classic-amiga` en ligne de commande, `"theme": "classic-amiga"` dans
`settings.json`, ou **F8** en jeu. F8 change l'apparence sans recommencer le match. `--list-themes` liste les
apparences installées.

## Ressources originales : ce que le projet fournit ou non

- Le dépôt **ne contient aucune** ressource du jeu commercial : ni sprite, ni terrain, ni police, ni son. Les
  dépôts publics étudiés ne sont pas considérés comme libres de droits (voir REFERENCE_ANALYSIS.md).
- L'importeur lit **vos** fichiers et écrit les images dans `content/themes/classic-amiga/imported/`. Ce dossier
  est exclu par `.gitignore`, comme les images disque (`*.adf`, `*.adz`, `*.dms`, `*.ipf`) et les fichiers
  `*.iff` et `*.dat`. Ne le partagez pas.
- Sans import, l'apparence Classic Amiga fonctionne avec des **remplacements temporaires clairement signalés** :
  un bandeau rouge indique le nombre de ressources manquantes, et le journal liste chaque identifiant.
  - Sprites : ceux de l'apparence Custom, ramenés à l'échelle du terrain classique.
  - Terrain : dessiné par le programme.
  - Ressource introuvable partout : damier magenta.

## Importer votre copie

```bash
dotnet run --project tools/Arcade.Importer -- --adf "KickOff2.adf"
```

Options :
- `--adf <fichier>` : image disque `.adf`, ou `.adz` compressée. Option répétable. Donnez Kick Off 2 puis The
  Final Whistle : un disque donné après remplace les fichiers de même nom, et les archives `.dat` sont fusionnées
  membre par membre.
- `--files <dossier>` : fichiers déjà extraits du disque (`sprites.dat`, `pitches.dat`, `title`…). Utile si
  votre disque n'utilise pas le système de fichiers de Dino Dini : l'importeur le signale précisément.
- `--set-default` : sélectionne aussi l'apparence dans `settings.json`.
- `--root <dossier>` : dossier du jeu, détecté automatiquement sinon.

L'importeur affiche le nombre de ressources du thème installées (par exemple `10 / 10`) et écrit
`imported/IMPORT_REPORT.md`. Ce rapport contient :
- les sources lues, avec la taille et l'empreinte SHA-256 de chaque fichier, pour comparer les versions ;
- les fichiers produits ;
- ce qui manque ;
- les palettes et l'usage des couleurs de chaque planche ;
- les hypothèses à vérifier.

| Fichier original | Contenu | Produit |
|---|---|---|
| `sprites.dat › style1a.iff … style6b.iff` | Planches joueurs 320×161, 3 plans, tuiles 16×16 | `sprites/players-style*.png`, tenues en couleurs repères |
| `sprites.dat › keeper.iff`, `ball.iff` | Gardien 320×177, ballon 320×17 | `sprites/keeper.png`, `sprites/ball.png` |
| `pitches.dat › pdat.*` + `fbgraph4.*` | Carte 80×96 tuiles + jeu de tuiles | `pitches/<terrain>.png` (1280×1536) |
| `cornsel1.iff`, `cornsel2.iff`, `title` | Écrans d'interface | `ui/*.png` (non utilisés pour l'instant) |
| `fonts.bl3`, `fonts.dat` | Polices bitmap | `fonts/`, planches d'examen (non utilisées : ordre des caractères inconnu) |

Correspondance des terrains : `nor` donne `normal`, `wet` donne `wet`, `sog` donne `soggy`, `art` donne
`artificial`, `mud` donne `muddy` et `sno` donne `icy`. `non` donne `bumpy`, ce qui n'est **pas vérifié**.
`wem` (Wembley) est conservé, mais aucun terrain des données ne l'utilise encore.

Les **planches contact** (`imported/contact/*.png`) agrandissent chaque planche et numérotent les tuiles. Ces numéros
sont ceux du champ `frames` de `assets.json`. Elles servent à compléter les animations à partir de vos fichiers.

## Correspondance des états d'animation

Le moteur de rendu interprète la simulation en **états** et `assets.json › states` associe chaque état à des
animations, par ordre de préférence. Si une animation manque ou si sa texture n'est pas installée, l'état suivant
de la chaîne est essayé, d'abord dans l'apparence active pour garder une image cohérente, puis dans l'apparence
par défaut. Chaque substitution est écrite dans le journal (niveau Debug).

| État | Émis quand | Repli |
|---|---|---|
| `idle`, `run`, `dribble`, `kick`, `control` | Arrêt, course, course avec touche de balle récente (< 1,5 s), frappe, amorti | `dribble`→`run`→`idle`, `kick`→`idle`, `control`→`idle` |
| `keeper.idle`, `keeper.run`, `keeper.kick`, `keeper.hold` | Mêmes actions pour le gardien, ballon en main | état de joueur correspondant, `keeper.hold`→`keeper.idle` |
| `ball`, `ball.high`, `ball.shadow`, `player.shadow` | Ballon bas, ballon haut, ombres | facultatifs (`ball.high` absent : `ball` à toute hauteur) |
| `slide`, `header`, `volley`, `keeper.dive`, `celebrate` | **Réservés** : émis quand le lot 3 ajoutera ces actions | `kick`, `kick`, `kick`, `keeper.idle`, `run` |

État de l'apparence Classic Amiga :

| État | Animation | Provenance |
|---|---|---|
| Course | tuiles `séquence[i] + 8 × direction`, séquence `0,3,2,1,1,2,3,4,7,6,5,5,6,7`, une image tous les 5 px à 150 px/s, soit 0,38 m | [Reconstitué : `Player.java` d'Open Kick-Off] |
| Arrêt | tuile `0 + 8 × direction` | [Approximatif] |
| Frappe, amorti, tacle, tête, plongeon, célébration | non identifiés : l'image d'arrêt du thème est affichée | à compléter avec les planches contact |
| Gardien | même disposition supposée que les joueurs | [Approximatif] |
| Ballon | tuiles 0–3 au ras du sol, ombre dans l'image, choisies par hauteur affichée (1 px par tuile) ; tuiles 4–7 en l'air, `min(3, h/16)` ; tuile 8 = ombre, décalée de (+h, −h) | [Reconstitué : `Ball.java` d'Open Kick-Off] |
| Tenues | indices de palette 3 et 4 → maillot, 5 → short ; les autres couleurs restent celles du fichier | [Approximatif : KONEY décrit 3 et 4 comme couleurs principales des équipes A et B, 5 comme secondaire] |
| Directions | ordre N, NE, E, SE, S, SW, W, NW, identique à celui du moteur | [Reconstitué] |
| Ancrage | centre de la tuile 16×16 sur la position | [Reconstitué : Open Kick-Off] |

## Cadrage et affichage

Réglages de `content/themes/classic-amiga/config/display.json` :
- **320×256 pixels virtuels** [Reconstitué : l'image titre du jeu est en 320×256, mode PAL basse résolution]. La
  zone de jeu exacte (lignes masquées par le tableau de score, overscan) n'est **pas vérifiée**.
- **13,1234 px/m**, soit 912 px pour les 69,49 m de largeur, comme l'image de terrain originale (lignes sur
  912×1392 px, drapeau de coin en haut à gauche en (176, 64)). Pour l'image composée, le point central est
  (632, 760). La vue couvre environ 24 × 19,5 m.
- Hauteurs affichées à 0,5 [Reconstitué], comme dans l'apparence Custom.
- `pixelAspect` à 1 (pixels carrés). Un Amiga PAL sur un écran 4:3 affiche des pixels environ 1,07 fois plus
  larges : `1.0667` permet de l'essayer, au prix de colonnes de largeur inégale d'un pixel.
- Agrandissement **entier** au plus proche voisin, image centrée (bandes noires). Le plein écran (F11) est de type
  « fenêtre sans bord » : pas de changement de mode vidéo, ce qui convient à macOS.
- `drawGoals` reste activé : on ne sait pas encore si les tuiles du terrain contiennent les buts. Si votre terrain
  importé les montre, mettez `false`.
- `playerShadows` est désactivé : la palette des sprites a une couleur d'ombre (indice 7), donc les ombres sont
  supposées faire partie des images. **À vérifier.**
- Le tableau de score, le chronomètre, le radar et les bandeaux restent ceux du projet : leur mise en page
  originale et les polices bitmap ne sont pas encore reconstituées.

## Comparer avec l'original

Captures reproductibles d'un même scénario scripté dans les deux apparences :

```bash
tools/capture_looks.sh
```

Les images vont dans `artifacts/screenshots/looks/<apparence>/` et se comparent côte à côte avec des captures
de l'original lancé dans un émulateur (WinUAE, FS-UAE) sur la même scène. Le projet ne revendique **aucune**
fidélité au pixel près tant que cette comparaison n'a pas été faite et documentée.

## Créer sa propre apparence

Copiez `content/themes/custom/` sous un autre nom. Changez l'`id` du manifeste, qui doit être le nom du dossier,
puis ajoutez :
- des images, à n'importe quel emplacement ;
- `assets.json`, pour les textures, les animations et les états ;
- `config/display.json`, pour la résolution, l'échelle, les ombres et l'image de terrain.

Tout autre fichier JSON, par exemple la physique, les équipes ou les règles, **fait rejeter le thème entier** :
une apparence ne peut pas changer le jeu.
