# Architecture

## Vue d'ensemble

```text
Tsubasa.slnx
├── src/Arcade.Core       Simulation et règles, sans MonoGame (testable sans fenêtre)
│   ├── Common/           Maths vectorielles, journal structuré (GameLog)
│   ├── Simulation/       Horloge à pas fixe, générateur pseudo-aléatoire déterministe
│   ├── Input/            Entrées logiques : joystick 8 directions + bouton (ControlInput)
│   ├── Content/          Packs de contenu, mods, fusion JSON, validation, catalogue d'assets
│   ├── Localization/     Traductions, repli, paramètres nommés, pluriels
│   └── Football/         Module football
│       ├── Ball/         État du ballon, physique, collisions buts/panneaux
│       ├── Players/      Joueur, attributs, locomotion, fatigue
│       ├── Pitch/        Terrains, surfaces, géométrie
│       ├── Control/      Actions de jeu partagées (BallActions) et contrôle contextuel humain
│       ├── Teams/        Équipes, tenues, effectifs
│       ├── Tactics/      Tactiques à zones (modèle KO2), lecteur des fichiers .tac
│       ├── Match/        Machine à états du match, arbitre, équipes en jeu, réglages
│       ├── AI/           IA d'équipe, gardien, prévision de trajectoire du ballon
│       └── Training/     Session d'entraînement (lot 1)
├── src/Arcade.Desktop    Application MonoGame DesktopGL
│   ├── Hosting/          Options de lancement, dossier du jeu, chargement des données avec repli
│   ├── Input/            Clavier/manette → ControlInput, bindings JSON, entrées scriptées
│   ├── Camera/           Caméra (lit l'état, ne le modifie jamais)
│   ├── Rendering/        Textures (ids logiques, palette des tenues), terrain, buts, sprites
│   ├── UI/               HUD, radar, overlay debug, polices TTF
│   └── Scenes/           Scènes de match et d'entraînement (interface IGameScene)
├── tests/Arcade.Tests    Tests xUnit (simulation, contrôles, données, mods, thèmes, importeur, i18n, CLI)
├── content/              Données et ressources de base (pack « base »)
│   └── themes/           Apparences : custom, classic-amiga (images importées localement, hors Git)
├── mods/                 Mods installés (dont example-mod)
├── tools/Arcade.Importer Importeur local des fichiers Amiga de l'utilisateur (sans MonoGame)
├── tools/                Générateur d'art temporaire, scripts d'entrées, captures comparatives
└── docs/
```

Dépendances : `Arcade.Desktop → Arcade.Core`. L'importeur ne dépend de rien. Le noyau ne référence aucune
bibliothèque graphique.
Un test d'architecture (`ArchitectureTests`) vérifie que le noyau ne dépend pas de MonoGame et que les espaces
génériques (`Common`, `Simulation`, `Input`, `Content`, `Localization`) n'utilisent pas le module `Football`.
Le module football est un dossier du noyau plutôt qu'un projet séparé : une seule frontière vérifiée suffit pour
l'instant. On en fera un projet le jour où un deuxième jeu partagera le noyau.

Bibliothèques externes : MonoGame.Framework.DesktopGL 3.8.6.1, FontStashSharp.MonoGame 1.6.2 pour les polices
TTF et l'Unicode sans pipeline de contenu, et xUnit pour les tests.

## Unités et coordonnées

Mètres et secondes. L'origine est au point central. +X va vers la ligne de touche droite, +Y vers le but du bas
de l'écran et Z vers le haut. Le grand axe du terrain est vertical à l'écran, comme dans Kick Off 2. La hauteur
du ballon est celle de son point le plus bas (0 = au sol).

## Boucle de simulation

```text
frame (fréquence d'affichage libre)
 ├─ InputSampler.Update()            lit clavier/manette, mémorise les appuis brefs
 ├─ FixedStepClock.Advance(dt réel)  → n pas de 1/60 s (plafonné à 0,25 s par frame)
 │   └─ n fois : ContextualController.Update(entrée) → FootballWorld.Step()
 │        1. joueurs : intention → locomotion (vitesse, inertie, adhérence, fatigue)
 │        2. ballon : k sous-pas (déplacement < 8 cm) : physique libre, poteaux/barre/filets/panneaux,
 │                    contacts avec les joueurs (positions interpolées sur le sous-pas)
 │        3. détection de but, événements
 ├─ Camera2D.Update(dt réel)          lissage exponentiel, ne touche pas à la simulation
 └─ Draw(alpha)                        rendu interpolé entre l'état précédent et l'état courant
```

- La simulation ne lit jamais l'horloge réelle : le temps de jeu est `Tick × 1/60 s`.
- Toutes les sources d'aléa (rebonds sur terrain irrégulier) passent par `GameRandom`, dont la graine est fixée.
  À entrées identiques, le résultat est identique (testé).
- Les sous-pas du ballon empêchent les traversées à haute vitesse : à 40 m/s, un pas de 1/60 s fait 67 cm,
  découpés en sous-pas de moins de 8 cm (rayon du ballon : 11 cm).

## Séparation contrôle / physique

Ni le contrôleur humain (`ContextualController`) ni l'IA (`TeamAi`, `KeeperAi`) ne déplacent le ballon
eux-mêmes. Ils produisent :

- des **intentions** sur le joueur (`PlayerIntent` : direction, orientation, prêt à contrôler, garde du ballon) ;
- des **actions** de `BallActions` (tir, passe, ballon piqué, dribble, remise en jeu). Ces actions vérifient les
  mêmes conditions de portée et de timing pour tous, puis appellent `FootballWorld.Kick`, `HoldBall` ou
  `ApplyBallAcceleration` (aftertouch).

L'IA joue donc avec la même physique et les mêmes règles que l'humain. Elle prévoit la trajectoire du ballon
(`BallForecast`, même physique sans aléa) mais ne lit aucune information cachée. Seule différence assumée : ses
frappes ne sont pas limitées aux 8 directions du joystick. En contrepartie, elle a une erreur de visée qui dépend
du niveau et des attributs.

## Match (lot 2)

`FootballMatch` pilote un `FootballWorld` avec une machine à états :

```text
KickOffSetup → RestartReady(coup d'envoi) → Playing ─┬─ but ──────→ GoalCelebration → KickOffSetup
                                                     ├─ sortie ───→ BallOut → RestartSetup → RestartReady → Playing
                                                     └─ fin du temps → HalfTime → (changement de côté) → KickOffSetup
                                                                       → FullTime
```

- `Referee` décide des sorties : touche, corner ou six mètres, avec l'équipe et le point de remise en jeu. Ce sont
  des fonctions pures, testées isolément.
- Le chronomètre tourne pendant le jeu et les arrêts ordinaires. Il s'arrête pendant les célébrations, les mises en
  place des coups d'envoi et la mi-temps. Le temps affiché (0–90') est une mise à l'échelle de la durée réelle.
- Le ballon a trois états : `Live` (physique), `Dead` (posé pour une remise en jeu, ni physique ni contact) et
  `Carried` (dans les mains du gardien ou du lanceur de touche).
- Les joueurs sont des corps solides : ils se repoussent entre eux et n'entrent pas dans les buts.
- Tactiques (`content/tactics/`, modèle de Kick Off 2) : chaque joueur de champ a une position par zone du
  ballon (12 zones) et par situation (coups d'envoi, six mètres, corners). Les jeux de positions `attack` de Final
  Whistle sont pris en charge, et `KickOff2TacticFile` lit les fichiers `.tac` originaux.
- Sélection humaine : le joueur de champ le plus proche du point où va le ballon, avec une marge et un délai
  minimum entre deux changements. Pas de changement pendant que le joueur garde le ballon ou tire une remise en
  jeu. Un amorti armé (bouton maintenu pendant qu'une passe arrive) passe au receveur.

## IA élémentaire (lot 2)

`TeamAi` met à jour chaque joueur non humain :

- **porteur** : il tire s'il est à portée (distance et angle), sinon il dribble dans la meilleure direction parmi
  cinq, selon la progression, l'espace libre et la distance aux touches. Quand il est pressé, ou qu'un coéquipier
  est nettement mieux placé, il amortit puis passe ;
- **poursuivants** (1 ou 2 selon le niveau) : les joueurs qui atteindront le ballon le plus tôt, d'après la
  prévision de trajectoire. Ils amortissent un ballon libre et courent dans un ballon tenu par un adversaire ;
- **les autres** : placement à la position tactique de la zone du ballon, réévalué à intervalle régulier, ce qui
  donne le temps de réaction ;
- **gardien** (`KeeperAi`) : placé entre le ballon et le centre du but. Après son temps de réaction, il se déplace
  vers le point où un tir croisera sa ligne. Il ramasse les ballons lents de sa surface et relance vers un
  coéquipier ;
- les passes sont notées : progression, espace autour du receveur, ligne de passe dégagée. L'attribut Flair relève
  le seuil, donc les joueurs créatifs gardent davantage le ballon.

Les niveaux (`config/ai.json`) changent le temps de réaction, la fréquence de décision, la précision, la portée de
tir et le nombre de poursuivants. Ils ne touchent jamais à la vitesse ni à la physique.

En mode debug, chaque joueur de l'ordinateur affiche son état et sa décision, et une ligne indique sa cible.

## Événements

`FootballWorld.Events` est un tampon de `MatchEvent` (struct, sans allocation) : touche, frappe, rebond, poteau,
barre, filet, panneau, but. Le bureau le vide à chaque frame pour le HUD et l'overlay debug.
Les sons, les effets et les ralentis s'y brancheront.

## Chargement des données

1. `ContentLoader` construit une `ContentRepository` : d'abord le pack `content/` (id `base`), puis les mods
   activés, dans l'ordre. Chaque pack est validé : manifeste, noms de fichiers et JSON bien formé. Un mod invalide
   est rejeté entièrement, et le problème est journalisé puis affiché à l'écran.
2. Les fichiers JSON de même chemin sont **fusionnés** dans l'ordre des packs (JSON Merge Patch). Les fichiers
   binaires (PNG, TTF) sont **remplacés** par le pack de plus haute priorité.
3. Les fichiers fusionnés sont désérialisés en `record` typés. Les propriétés inconnues sont refusées, ce qui
   détecte les fautes de frappe, et chaque type valide ses bornes (`IContentData.Validate`). Les erreurs citent
   le fichier et les packs concernés.
4. Côté bureau, `GameData.Load` réunit football, assets, affichage, entrées et traductions. Si les mods rendent
   les données invalides, le jeu revient aux données de base et l'affiche. Une erreur dans les données de base
   est fatale, avec un message explicite.

Aucune constante de gameplay n'est codée en dur : physique (`config/physics.json`), contrôles
(`config/controls.json`), attributs et fatigue (`config/players.json`), surfaces, terrains, équipes,
affichage et entrées sont des données. Le code garde quelques constantes nommées :
- des constantes visuelles (durée de l'animation de frappe, bannière de but) ;
- des constantes propres à l'entraînement (délai de remise en jeu après un but) ;
- des seuils internes des heuristiques de l'IA et de l'arbitre, par exemple la distance à laquelle un ballon
  « appartient » à un joueur ou le retrait des points de corner. Chacun est documenté à sa déclaration.

Les réglages à calibrer (niveaux, portées, timings, puissances) sont tous dans les données.

## Rendu

- Le monde est dessiné dans une cible de la résolution virtuelle de l'apparence (640×360 en Custom, 320×256 en
  Classic Amiga), agrandie d'un facteur entier au plus proche voisin puis centrée (`pixelAspect` facultatif). Le
  HUD est dessiné à la résolution de la fenêtre pour que le texte reste net, avec une mise en page de référence
  de 360 lignes.
- Projection oblique : `écran = (x, y) × échelle − z × échelle × heightScale`. Toutes les faces des buts
  deviennent des rectangles alignés, ce qui simplifie le dessin. Les objets sont triés par Y au sol.
- Les sprites sont décrits par des données (`assets.json` : grille, ancrage, directions, durée des frames).
  Les tenues sont obtenues par substitution de couleurs repères, une fois par équipe, et mises en cache.
  Une ressource manquante est signalée et remplacée par un damier magenta.
- **Apparences** (`content/themes/<id>/`) : une couche de contenu chargée entre la base et les mods
  (`PackKind.Theme`). Elle ne peut fournir que `assets.json`, `config/display.json` et des fichiers binaires.
  Tout autre JSON fait rejeter le thème : il ne peut pas changer le jeu. `ContentRepository.Without(theme)`
  donne l'apparence par défaut, utilisée pour tout ce que le thème ne fournit pas (`TextureStore.Fallback`).
- **États d'animation** (`AnimationStateMap`, dans Core, sans dépendance graphique) : le rendu traduit l'état
  d'un joueur en nom d'état (`run`, `keeper.hold`…), et `assets.json › states` le traduit en animation, avec une
  chaîne de repli et un rapport des substitutions. `AnimationFrames` choisit l'image : par le temps, la distance
  parcourue ou la hauteur affichée (ballon). Il gère aussi les planches indexées de l'original.
- F8 recrée seulement les objets d'affichage (`ArcadeGame.ApplyVisuals`) : la scène et la simulation
  continuent.
- **Importeur** (`tools/Arcade.Importer`, sans MonoGame) : il lit les images disque Amiga (système de fichiers
  de Dino Dini), les archives `.dat`, les images IFF ILBM et les cartes de terrain. Il écrit des PNG dans
  `content/themes/classic-amiga/imported/`, un dossier ignoré par Git. Voir THEMES.md.
- L'overlay debug (F1) affiche les rayons de contact, les vecteurs vitesse, la trajectoire prédite du ballon
  (calculée sur une copie de l'état), l'état du contrôleur, les temps de simulation et les derniers événements.
  Il ne modifie rien.

## Scènes du bureau

`ArcadeGame` ne connaît qu'une `IGameScene` : `MatchScene` ou `TrainingScene`. La scène avance sa simulation,
reçoit les entrées de chaque jeu de commandes, gère ses touches propres et dessine son HUD. Le rendu du monde, la
caméra, le radar et l'overlay debug sont communs.

## Points d'extension prévus (non implémentés)

Ombres et effets de terrain, météo, particules, sons, célébrations : le rendu lit l'état et les événements, donc
ces ajouts ne touchent pas aux règles. Les tacles, fautes, têtes et plongeons (lot 3), puis les tactiques
attaque/défense de Final Whistle, le hors-jeu et les coups de pied arrêtés avancés (lot 4) s'ajouteront dans
`Match/`, `AI/` et `BallActions`.
