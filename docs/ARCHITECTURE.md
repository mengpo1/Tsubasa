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
│       ├── Control/      Contrôle contextuel à un bouton (humain)
│       ├── Teams/        Équipes, tenues, effectifs
│       └── Training/     Session d'entraînement (lot 1)
├── src/Arcade.Desktop    Application MonoGame DesktopGL
│   ├── Hosting/          Options de lancement, dossier du jeu, chargement des données avec repli
│   ├── Input/            Clavier/manette → ControlInput, bindings JSON, entrées scriptées
│   ├── Camera/           Caméra (lit l'état, ne le modifie jamais)
│   ├── Rendering/        Textures (ids logiques, palette des tenues), terrain, buts, sprites
│   ├── UI/               HUD, radar, overlay debug, polices TTF
│   └── Scenes/           Scène d'entraînement
├── tests/Arcade.Tests    Tests xUnit (simulation, contrôles, données, mods, i18n, CLI)
├── content/              Données et ressources de base (pack « base »)
├── mods/                 Mods installés (dont example-mod)
├── tools/                Générateur d'art temporaire, scripts d'entrées
└── docs/
```

Dépendances : `Arcade.Desktop → Arcade.Core`. Le noyau ne référence aucune bibliothèque graphique.
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

Le contrôleur humain (`ContextualController`) ne déplace jamais le ballon lui-même. Il produit :

- des **intentions** sur le joueur (`PlayerIntent` : direction, orientation, prêt à contrôler, garde du ballon) ;
- des **actions** vérifiées par le monde (`FootballWorld.Kick`, `HoldBall`, `ApplyBallAcceleration` pour l'aftertouch).

L'IA des lots suivants utilisera les mêmes intentions et actions, donc la même physique et les mêmes portées.

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
affichage et entrées sont des données. Les rares constantes du code sont visuelles (durée de l'animation de
frappe, bannière de but) ou propres à l'entraînement (délai de remise en jeu après un but).

## Rendu

- Le monde est dessiné dans une cible 640×360 (pixel art), agrandie à l'entier le plus proche puis
  centrée. Le HUD est dessiné à la résolution de la fenêtre pour que le texte reste net.
- Projection oblique : `écran = (x, y) × échelle − z × échelle × heightScale`. Toutes les faces des buts
  deviennent des rectangles alignés, ce qui simplifie le dessin. Les objets sont triés par Y au sol.
- Les sprites sont décrits par des données (`assets.json` : grille, ancrage, directions, durée des frames).
  Les tenues sont obtenues par substitution de couleurs repères, une fois par équipe, et mises en cache.
  Une ressource manquante est signalée et remplacée par un damier magenta.
- L'overlay debug (F1) affiche les rayons de contact, les vecteurs vitesse, la trajectoire prédite du ballon
  (calculée sur une copie de l'état), l'état du contrôleur, les temps de simulation et les derniers événements.
  Il ne modifie rien.

## Points d'extension prévus (non implémentés)

Ombres et effets de terrain, météo, particules, sons, célébrations : le rendu lit l'état et les événements, donc
ces ajouts ne touchent pas aux règles. La machine à états du match, les équipes complètes, l'IA, les gardiens et
les règles arriveront aux lots 2 à 4 au-dessus de `FootballWorld`.
