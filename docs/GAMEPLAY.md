# Mécaniques implémentées

Ce document décrit **ce qui fonctionne réellement** dans l'état actuel du projet (lots 0 et 1) et d'où vient
chaque comportement. Il ne décrit pas de fonctions futures.

Légende de provenance :

| Étiquette | Signification |
|---|---|
| **[KO2]** | Documenté dans le manuel de Kick Off 2 |
| **[FW]** | Documenté dans le manuel de The Final Whistle |
| **[Estimé]** | Non documenté. Valeur estimée, réglable dans les données, à calibrer en jouant |
| **[Choix]** | Décision de conception de ce projet (amélioration ou interprétation) |

Les vidéos de référence n'ont **pas** pu être analysées dans l'environnement de développement (pas de lecture
vidéo). Aucun comportement n'est donc présenté comme « observé ». La comparaison visuelle avec la vidéo Amiga
reste à faire sur votre machine.

Les valeurs citées sont celles de `content/config/*.json` au moment de l'écriture.

## Terrain d'entraînement (lot 1)

Un joueur humain, un ballon et deux buts sur un terrain de 105 × 68 m [Choix : dimensions réglementaires,
dans `content/pitches/*.json`]. Les buts sont comptés et le ballon revient devant le joueur 1,5 s après un but.
**R** replace le ballon. **F3** change de terrain et donc de surface.

## Déplacements des joueurs

- 8 directions de joystick [KO2]. Les sticks analogiques sont ramenés à 8 directions [Choix : mode historique].
- L'orientation du joueur suit le joystick **immédiatement** [Choix : la réactivité d'abord]. La vitesse, elle,
  conserve son élan.
- Modèle de vitesse [Estimé] (`config/physics.json › movement`) :
  - composante dans la direction demandée : accélération 34 m/s² jusqu'à la vitesse de pointe (9 m/s pour un
    joueur moyen) ;
  - composante latérale : annulée par l'« adhérence » (48 m/s²). Les virages sont serrés mais pas instantanés ;
  - joystick au centre ou inversé : freinage à 50 m/s². Un demi-tour est donc plus lent qu'un départ arrêté.
- Mesures (testées) : vitesse de pointe atteinte en 0,1–0,45 s, arrêt d'un sprint en moins de 0,35 s et 1,5 m.
- **Pace** module la vitesse de pointe (×0,88 à ×1,08) et l'accélération [Estimé ; l'effet de Pace sur la vitesse
  est KO2].
- **Fatigue** : l'énergie baisse en course rapide et remonte en marchant. Une énergie basse réduit la vitesse
  (jusqu'à ×0,82) et l'accélération [Estimé]. **Stamina** ralentit la baisse [KO2 : « players with lower stamina
  … lose pace »]. La fatigue est désactivable (`config/players.json › fatigue.enabled`). L'effet de la fatigue sur
  la précision [KO2] n'est **pas encore** implémenté.

## Le ballon

Objet physique indépendant : il n'est jamais attaché au joueur [KO2 : « the ball travels in front of the player »].

- Position 3D, vitesse 3D, gravité 9,81 m/s², traînée de l'air, rebonds (restitution 0,55, frottement 0,85),
  roulement (décélération constante 1,4 m/s² plus 0,35/s proportionnelle à la vitesse), arrêt sous 0,1 m/s [Estimé].
- Vent : poussée constante sur le ballon en l'air (0,12 m/s² par m/s de vent), très faible au sol [Choix ; KO2 a
  une option de vent sans en décrire l'effet]. Le moteur le gère, mais aucune option de jeu ne l'active encore.
- Collisions : poteaux (cylindres verticaux), barre (cylindre horizontal), filets (boîte ouverte côté terrain,
  très amortissante de l'intérieur, rebond faible de l'extérieur), panneaux autour du terrain.
- Anti-traversée : sous-pas de moins de 8 cm. Un ballon à 40 m/s rebondit sur un poteau ou sur un joueur au
  lieu de le traverser (testé).
- But : le ballon a entièrement franchi la ligne entre les poteaux et sous la barre.

### Surfaces

Chaque surface est un fichier de multiplicateurs (`content/surfaces/*.json`) appliqués par la physique
générique. Aucun nom de surface n'est testé dans le code.

| Surface | Manuel | Réglage actuel [Estimé] |
|---|---|---|
| normal | « normal bounce and travel » [KO2] | ×1 partout |
| wet (humide) | « more ball travel and speed » [KO2] | roulement ×0,7, glisse au rebond, adhérence joueurs ×0,9 |
| soggy (détrempé) | « bounce and travel reduced, stamina reduced » [KO2] | roulement ×1,8, rebond ×0,6, fatigue ×1,3 |
| artificial | « high bounce, more speed and travel, reduced stamina » [KO2] | rebond ×1,35, roulement ×0,75, fatigue ×1,25 |
| icy (gelé) | « more ball pace and travel » [FW] | roulement ×0,35 ; adhérence joueurs ×0,55 [Choix] |
| muddy (boueux) | « travel reduced drastically both in the air and on the ground, stamina greatly reduced » [FW] | roulement ×3, traînée ×2,5, rebond ×0,4, fatigue ×1,8 |
| bumpy (irrégulier) | « the ball is likely to fly off in any direction » [FW] | déviation aléatoire au rebond (±25°) et en roulant |

Que la glace fasse glisser les joueurs n'est pas documenté : c'est un [Choix], réglable par `playerGripScale`.

## Contrôles contextuels à un bouton

Toutes les durées et portées sont dans `content/config/controls.json` [Estimé]. Seul l'enchaînement des actions
est documenté.

| Geste | Comment | Provenance |
|---|---|---|
| Dribble | Courir dans le ballon sans bouton : il part dans la direction du joueur à `1,2 + 1,12 × vitesse` m/s. Il s'éloigne puis le joueur le rattrape. | Principe [KO2] ; valeurs [Estimé] |
| Perte de balle | Un virage serré ou un ballon trop rapide (écart > 9,5 m/s) font perdre le ballon : il rebondit sur le joueur. Un ballon derrière le joueur est dévié, pas poussé. | Conséquence du modèle [Choix] |
| Tir | Bouton pressé dans les 0,3 s **après** une touche, ballon à moins de 1,7 m et à moins de 0,8 m de haut. Vitesse 22 m/s × (0,85 à 1,1 selon Shooting) + 0,5 × la vitesse de course, élévation 4 m/s, dans la direction du joystick. | Règle [KO2] ; fenêtre, portée et puissance [Estimé] |
| Contrôle (amorti) | Bouton pressé **avant** le contact et maintenu : au contact (ballon à moins de 1,3 m de haut), le ballon est arrêté. Le joueur s'immobilise dessus et le ballon reste physique, ramené vers ses pieds. | Règle [KO2] |
| Passe | Ballon contrôlé, bouton maintenu : le joystick vise, relâcher passe. Puissance de 9 à 24 m/s selon le temps de visée (0,6 s pour la pleine puissance), × Passing. | Règle [KO2] ; puissance selon la visée [Choix : non documenté] |
| Garder le ballon | Relâcher avec le joystick au centre : ballon au pied. Une direction repart en dribble dans cette direction, le bouton tire. | [KO2] |
| Ballon piqué | Inverser le joystick (directement ou en passant par le centre en moins de 0,12 s) avec le ballon au sol, au pied (moins de 1,6 m, touché depuis moins de 0,7 s) : le ballon s'élève (7 m/s vers le haut, 9 m/s vers l'avant) dans la direction d'origine. | Règle [KO2] ; valeurs [Estimé] |
| Aftertouch | Dans les 0,55 s après une frappe du joueur : la composante latérale du joystick, par rapport à la direction de frappe, courbe le ballon (jusqu'à 11 m/s², décroissant à zéro sur la fenêtre). Joystick vers l'avant : neutre. Positions arrière : aucun effet. Annulé si un autre joueur touche le ballon. Option on/off (touche **T**). | Déviation latérale, positions avant, fenêtre très courte [KO2] ; intensité et durée [Estimé] |

Garanties testées :

- un bouton pressé avant le contact et maintenu ne déclenche **jamais** de tir ;
- un tir n'a lieu que juste après une touche ;
- inverser le joystick loin du ballon ne pique pas.

Choix d'interprétation à noter :

- Le manuel KO2 ne précise pas la hauteur des tirs. Ici, chaque tir décolle (4 m/s vers le haut). Le « dip » vers
  l'avant n'est documenté que pour les coups francs : il existe dans le moteur (`dipAcceleration`) mais vaut 0 en
  jeu courant. La mise en œuvre de la table complète des coups francs viendra au lot 4.
- Un appui sur le bouton sans ballon à portée **arme le contrôle**. Le tacle glissé de KO2 (« fire with the ball on
  the ground ») arrivera au lot 3. Il faudra alors trancher la distinction tacle / contrôle, que le manuel ne
  documente pas.

## Caméra et affichage

- La caméra suit le ballon avec une anticipation de 0,3 s sur sa vitesse et un lissage exponentiel. Elle reste
  dans l'enceinte du terrain. Elle est indépendante de la simulation [Choix ; le comportement exact de KO2 n'est
  pas documenté].
- Vue : 640×360 pixels virtuels à 16 px/m, soit environ 40 × 22 m visibles, à peu près le cadrage serré de
  l'original. Zoom réglable avec **[ ]**.
- Radar (scanner) : terrain miniature avec joueurs, ballon et zone visible [KO2 : scanner].
- Tableau : équipe, buts, chronomètre de la séance.

## Paramètres à calibrer en priorité

Ces valeurs font l'essentiel des sensations. Toutes se règlent dans les données et se rechargent avec F5 :

1. `controls.json › dribble` : distance à laquelle le ballon court devant le joueur. Actuellement environ 1 m
   d'avance à pleine vitesse, une touche toutes les 0,95 s.
2. `controls.json › shot.window` et `shot.reach` : difficulté du timing du tir.
3. `physics.json › movement` : nervosité (accélération, adhérence, freinage).
4. `physics.json › ball.rollingResistance` et `rollingDrag` : portée des passes et des tirs au sol.
5. `controls.json › aftertouch` : intensité et durée de la courbe.
6. `controls.json › pass` : puissances et temps de charge.

## Limitations connues

- Pas encore d'adversaire, de coéquipier, d'IA, de gardien, de règles de match, de tacle, de tête ni de reprise
  de volée (lots 2 et 3). Un ballon haut est amorti s'il est contrôlé, sinon il rebondit sur le corps.
- Le vent est simulé mais n'a pas d'option de jeu.
- La fatigue n'affecte pas encore la précision.
- Pas de son.
- Graphismes temporaires générés.
