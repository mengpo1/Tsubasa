# Synthèse des références historiques

Synthèse établie avant l'implémentation pour séparer ce qui est **documenté** de ce qui doit être **estimé**.

## Sources consultées

| Source | Statut |
|---|---|
| Manuel Amiga de Kick Off 2, transcription du wiki `ssenegas/kickoff` | Lu. Transcription de fan, avec quelques coquilles (une durée « 2x2 », une phrase contradictoire sur la hauteur des corners) |
| Manuel de The Final Whistle (PDF openretro, en/de/fr/it) | Lu (scan sans texte, lu visuellement, section anglaise) |
| Manuel ZX Spectrum de Kick Off 2 (World of Spectrum) | Lu, source secondaire proche (version 8 bits) |
| Remake Java `ssenegas/kickoff` | Consulté. **Œuvre tierce et inachevée** : ses constantes sont celles de son auteur, pas des données originales |
| Vidéo Amiga (YouTube) | **Non analysée** : pas de lecture vidéo dans l'environnement de développement. À comparer sur votre machine |

## Mécaniques confirmées par les manuels

### Contrôles (un joystick 8 directions, un bouton)

- **Dribble** : « The ball travels in front of the player. The distance it travels depends on the speed of the
  player at the time of contact. » [KO2]
- **Tir** : « Press the F.B AFTER touching the ball will shoot the ball in the direction the player is facing. » [KO2]
- **Arrêt** : « Press F.B BEFORE touching the ball will stop the ball and give the player total control. » [KO2]
- **Passe** : arrêter le ballon, maintenir le bouton, orienter le joystick, relâcher. Relâcher joystick au centre
  revient au dribble : le joueur part dans la direction du joystick et tire si le bouton est pressé. [KO2]
- **Ballon piqué** : « If the ball is on the ground, reversing the joystick direction will chip the ball. » [KO2]
- **Aftertouch** (option oui/non) : modifier légèrement la direction « immediately after shooting », « only effective
  for a very short time », avec les **positions avant** du joystick seulement. [KO2]
- **Coups francs**, table complète avant et après contact : haut = plongeant, diagonales hautes = plongeant et
  brossé, gauche/droite = brossé, arrière = rien. [KO2]
- **Tête** : ballon en l'air, bouton = saut ; le joystick oriente la tête. [KO2]
- **Retourné / ciseau** : inverser le joystick quand le ballon est en l'air. [KO2] [FW]
- **Flick** : bouton avant le contact (ballon contrôlé), joystick vers l'avant puis inversé en relâchant le
  bouton : le ballon est levé pour une tête ou un retourné. [FW]
- **Tacles** : tacle en bloquant (de face ou en dépassant) ; tacle glissé avec le bouton quand le ballon est au sol.
  Tacler par derrière est une faute. Le manuel ne dit pas comment le jeu distingue tacle glissé et arrêt du ballon.
- **Gardien** : contrôlé par l'ordinateur sauf sur les penalties et les six mètres [KO2]. Dans FW, un second
  joueur peut le contrôler, confiné à la surface. L'ordinateur gère l'amplitude du plongeon, le joueur le déclenche.

### Coups de pied arrêtés

- **Corners FW** : 9 puissances (choix au joystick), hauteur selon la durée d'appui, biais latéral selon la durée
  de poussée du joystick pendant l'élan, aftertouch. Dans **KO2**, les 9 cases combinaient hauteur et effet, et la
  force dépendait de l'appui.
- **Touches** : automatiques sauf si le bouton est pressé avant ; la durée d'appui donne la distance ; seules les
  5 positions avant servent. [FW]
- **Penalties** : un curseur va et vient pour la direction ; la durée d'appui donne la hauteur. [KO2]
- **Six mètres** : 9 types (fort, moyen ou faible × gauche, centre ou droite). Dans FW, la longueur dépend de
  l'attribut Shooting du gardien.

### Joueurs, tactiques, règles

- Attributs : Pace, Stamina, Aggression, Resilience ; compétences : Passing, Shooting, Tackling [KO2] ; Flair
  (jeu individuel) [FW]. **Aucune échelle numérique documentée.** L'effet de Resilience n'est pas décrit.
- Tactiques : dans KO2, les positions dépendent de la position du ballon seulement. Dans FW, elles dépendent du
  ballon **et** de l'attaque ou de la défense. Les noms des tactiques ne sont pas documentés. Le remake utilise une
  grille de 12 zones (3 × 4), source tierce.
- Durées 2×3, 2×5, 2×10, 2×20 ; prolongations optionnelles ; tirs au but 5 + mort subite ; hors-jeu optionnel
  (FW, modes 2 à 4 joueurs) ; 24 arbitres de tempéraments différents ; 2 remplacements ; vent (off, léger, moyen,
  fort, effet non décrit).
- Surfaces : normal, humide, détrempé, synthétique [KO2] ; gelé, boueux, irrégulier [FW]. Leurs effets
  qualitatifs sont repris dans [GAMEPLAY.md](GAMEPLAY.md).
- Modes : match simple, championnat (8 équipes), coupe, amical international, entraînement (1 ou 2 joueurs),
  penalties ; 1 à 4 joueurs ; contrôle du joueur le plus proche ou d'un poste fixe ; ordinateur contre
  ordinateur [FW] ; action replay des buts.

## Non documenté : à calibrer expérimentalement

- Vitesses, accélérations et virages selon Pace ; courbe de fatigue.
- Frottements, traînée, rebonds du ballon ; multiplicateurs exacts des surfaces et du vent.
- Distance du ballon après une touche de dribble, rayon de contact, fenêtres « avant » et « après » contact.
- Puissance et angle des tirs et des passes, hauteur du ballon piqué, vitesse d'inversion requise.
- Durée et intensité de l'aftertouch.
- Courbes durée d'appui → puissance des coups de pied arrêtés.
- Portée du tacle glissé et sa distinction avec l'arrêt ; probabilités de faute ; sévérité des arbitres.
- Timing des têtes, retournés et flicks.
- Effets précis des attributs, dont Resilience et Flair.
- Délai avant les remises en jeu automatiques ; changement de joueur contrôlé.
- Placement dans les zones tactiques ; IA des gardiens.
- Durée réelle d'une minute de jeu ; comportement exact de la caméra.

Les valeurs choisies pour les lots 0 et 1 et leur justification sont dans [GAMEPLAY.md](GAMEPLAY.md).
