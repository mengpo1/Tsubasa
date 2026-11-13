# Mécaniques implémentées

Ce document décrit **ce qui fonctionne réellement** dans l'état actuel du projet (lots 0 à 2) et d'où vient
chaque comportement. Il ne décrit pas de fonctions futures.

Légende de provenance :

| Étiquette | Signification |
|---|---|
| **[KO2]** | *Confirmé* : documenté dans le manuel de Kick Off 2 |
| **[FW]** | *Confirmé* : documenté dans le manuel de The Final Whistle |
| **[Reconstitué]** | Repris d'une implémentation de référence (Open Kick-Off) ou déduit de l'inspection des données originales. Non documenté par l'éditeur. Voir [REFERENCE_ANALYSIS.md](REFERENCE_ANALYSIS.md) |
| **[Estimé]** | *Approximatif* : valeur de calibration choisie faute de données, réglable, à ajuster en jouant |
| **[Choix]** | Décision de conception de ce projet (amélioration ou interprétation) |

Les vidéos de référence n'ont **pas** pu être analysées dans l'environnement de développement (pas de lecture
vidéo). Aucun comportement n'est donc présenté comme « observé ». La comparaison visuelle avec la vidéo Amiga
reste à faire sur votre machine.

Les valeurs citées sont celles de `content/config/*.json` au moment de l'écriture.

## Terrain d'entraînement (lot 1)

Un joueur humain, un ballon et deux buts sur un terrain de 76 × 116 yards, soit 69,5 × 106,1 m
[Reconstitué : dimensions du terrain de Kick Off 2 relevées par Open Kick-Off, `content/pitches/*.json`]. Les buts sont comptés et le ballon revient devant le joueur 1,5 s après un but.
**R** replace le ballon. **F3** change de terrain et donc de surface.

## Déplacements des joueurs

- 8 directions de joystick [KO2]. Les sticks analogiques sont ramenés à 8 directions [Choix : mode historique].
- L'orientation du joueur suit le joystick **immédiatement** [Choix : la réactivité d'abord]. La vitesse, elle,
  conserve son élan.
- Vitesse de pointe d'un joueur moyen : 11,4 m/s [Reconstitué : 150 px/s à 12 px/yard dans Open Kick-Off]. C'est
  bien plus rapide qu'un vrai footballeur, et c'est voulu : Kick Off 2 est un jeu très rapide.
- Modèle d'inertie [Estimé] (`config/physics.json › movement`). Open Kick-Off n'a pas d'inertie : ses joueurs
  partent et tournent instantanément.
  - composante dans la direction demandée : accélération 43 m/s² jusqu'à la vitesse de pointe ;
  - composante latérale : annulée par l'« adhérence » (60 m/s²). Les virages sont serrés mais pas instantanés ;
  - joystick au centre ou inversé : freinage à 63 m/s². Un demi-tour est donc plus lent qu'un départ arrêté.
- Mesures (testées) : vitesse de pointe atteinte en environ 0,27 s, arrêt d'un sprint en moins de 0,35 s et 1,5 m.
- **Pace** module la vitesse de pointe (×0,88 à ×1,08) et l'accélération [Estimé ; l'effet de Pace sur la vitesse
  est KO2].
- **Fatigue** : l'énergie baisse en course rapide et remonte en marchant. Une énergie basse réduit la vitesse
  (jusqu'à ×0,82) et l'accélération [Estimé]. **Stamina** ralentit la baisse [KO2 : « players with lower stamina
  … lose pace »]. La fatigue est désactivable (`config/players.json › fatigue.enabled`). L'effet de la fatigue sur
  la précision [KO2] n'est **pas encore** implémenté.

## Le ballon

Objet physique indépendant : il n'est jamais attaché au joueur [KO2 : « the ball travels in front of the player »].

- Position 3D, vitesse 3D, gravité 9,81 m/s².
- Rebonds : le ballon garde ¾ de sa vitesse verticale et 31/32 de sa vitesse horizontale [Reconstitué : Open
  Kick-Off, `Ball.update`. Ces fractions, ¼ et 1/32, correspondent à des décalages binaires typiques du 68000, ce
  qui laisse penser qu'elles viennent du jeu ; ce n'est pas prouvé].
- Traînée de l'air et roulement : décélération constante de 1,4 m/s² plus 0,35/s proportionnelle à la vitesse,
  arrêt sous 0,1 m/s [Estimé]. Le modèle d'Open Kick-Off (une traînée unique de 0,81/s, aussi en l'air, avec une
  gravité appliquée deux fois) n'est pas cohérent et n'a pas été repris.
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
| Dribble | Courir dans le ballon sans bouton : il part dans la direction du joueur à `1,125 × vitesse + 2,29` m/s. Il s'éloigne puis le joueur le rattrape. Contact à moins de 0,61 m. | Principe [KO2]. Formule [Reconstitué, deux sources concordantes : Open Kick-Off et désassemblage publié par la communauté, `1,125 × vitesse + 30 px/s`]. Rayon de 8 px [Reconstitué] |
| Dribble dans la surface (PBD) | Dans les 18 yards (16,46 m) avant le but attaqué, la poussée perd 30 + 10 px/s (3,05 m/s) pour les joueurs de l'ordinateur : ils gardent le ballon très près. Les humains ne sont pas concernés, comme dans KO2 v1.4. Le comportement de World Cup 90 et de Player Manager (tous les joueurs, −30 px/s) se règle par `controls.json › dribble.penaltyBoxDribble`. | [Reconstitué : désassemblage publié sur le wiki KOA, page *PBD*]. Le choix du but attaqué est une interprétation |
| Perte de balle | Un virage serré ou un ballon trop rapide (écart > 13 m/s) font perdre le ballon : il rebondit sur le joueur. Un ballon derrière le joueur est dévié, pas poussé. | Conséquence du modèle [Choix] |
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
- **Écart connu avec l'original** : ici, un appui sur le bouton sans ballon à portée **arme le contrôle**. Dans Kick
  Off 2, sur un ballon libre, le bouton déclenche un **tacle glissé**, ou une **tête** si le ballon est en l'air ;
  l'amorti n'est possible qu'en possession [Confirmé : Dino Dini, cité sur le wiki KOA]. Cette règle sera
  appliquée au lot 3, avec les tacles et l'option *Trapfix* de la Competition Version.

## Match (lot 2)

### Déroulement

- Deux équipes de onze : les onze premiers joueurs du fichier d'équipe, dans l'ordre des postes de la tactique.
  Pas encore de remplaçants.
- Durée : 2 × 3 minutes réelles par défaut (`rulesets/classic.json`, option `--half-minutes`), affichées de 0 à 90'
  [KO2 : mi-temps de 3, 5, 10 ou 20 minutes]. Le chronomètre s'arrête pendant la célébration d'un but, la mise en
  place des coups d'envoi et la mi-temps [Choix].
- Tirage au sort de l'équipe qui donne le coup d'envoi (graine du match). L'autre équipe engage en seconde période.
  Changement de côté à la mi-temps. Après un but, l'équipe qui l'a encaissé engage.
- Coup d'envoi : tous les joueurs dans leur moitié, les adversaires hors du rond central (9,15 m). Les joueurs ont
  3 s pour rejoindre leur place ; les retardataires y sont ensuite placés directement [Choix].
- Fin du temps réglementaire au coup de sifflet, même pendant une action [Choix]. Pas encore de prolongations ni
  de tirs au but (lot 5).

### Sorties et remises en jeu de base

- Ballon entièrement sorti par une ligne de touche : **touche** pour l'adversaire du dernier joueur à l'avoir
  touché, au point de sortie.
- Ballon sorti par la ligne de but hors du but : **six mètres** si un attaquant l'a touché en dernier, **corner**
  du côté de la sortie si c'est un défenseur.
- Remises en jeu de l'humain (coup d'envoi, touche, corner) : le tireur est désigné et devient le joueur contrôlé.
  Le joystick vise ; maintenir le bouton charge la puissance et la hauteur, relâcher frappe [Choix, fondé sur les
  touches de FW : « la durée d'appui détermine la distance » et « seules les cinq positions avant » sont
  utilisables]. Sans appui pendant 4 s, l'ordinateur joue la remise en jeu [KO2/FW : touches automatiques sauf
  si le joueur agit].
- La touche part des mains, au-dessus de la tête (2 m). Les six mètres sont toujours tirés par le gardien de
  l'ordinateur [KO2 : « the keeper takes them automatically unless you act quickly » ; le choix manuel viendra au
  lot 4].
- Adversaires à 9,15 m lors des corners, six mètres et coups d'envoi, à 3 m lors des touches.
- Les corners à neuf puissances, la hauteur, le biais et l'aftertouch de Final Whistle viendront au lot 4. Ici, la
  puissance et la hauteur augmentent ensemble avec la durée d'appui (`controls.json › setPieces`).

### Joueur contrôlé

- Mode « jouer en équipe » [KO2] : vous contrôlez le joueur de champ le plus proche de l'endroit où sera le ballon
  dans 0,25 s, avec 1,5 m de marge et au plus un changement toutes les 0,3 s (`controls.json › selection`) [Estimé].
- Pas de changement pendant que votre joueur garde le ballon ou tire une remise en jeu.
- Bouton maintenu pendant qu'une passe arrive : le receveur, qui devient votre joueur, amortit le ballon [Choix].
- L'aftertouch reste actif après une passe même si le contrôle passe au receveur [Choix].
- Le gardien n'est jamais contrôlé par l'humain [KO2 : « YOU CAN NOT PLAY AS A KEEPER »].

### Contacts entre joueurs

- Les joueurs sont des disques de 0,4 m de rayon : ils se repoussent et perdent leur vitesse de rapprochement
  [Choix, `physics.json › movement.bodyRadius`]. Pas encore de chute ni de faute (lots 3–4).
- Un joueur qui court dans un ballon tenu par un adversaire le lui prend par le contact : le ballon part dans sa
  direction. C'est le « blocking tackle » de KO2, sans bouton. Le tacle glissé viendra au lot 3.
- Personne ne peut entrer dans un but : la bouche du but est un mur pour les joueurs [Choix]. Un gardien ne peut
  donc pas emporter le ballon derrière sa ligne par accident.

### Gardien élémentaire

- Mains dans sa surface : portée 0,9 m, jusqu'à 2,4 m de haut. Il capte les ballons arrivant à moins de 21 m/s ;
  au-delà, il repousse (restitution 0,45) [Estimé, `controls.json › keeper`].
- Placement entre le ballon et le centre du but, de 1 à 4 m de sa ligne selon la distance du ballon.
- Après son temps de réaction, il court vers le point où le tir va croiser sa ligne. **Pas de plongeon** : il ne
  rattrape que ce que sa course et ses bras permettent, donc pas d'arrêt impossible ni de téléportation.
- Il ramasse les ballons lents de sa surface quand il y arrive avant les attaquants. Il garde le ballon 1,2 s puis
  relance vers le coéquipier libre le plus avancé, sinon vers l'aile la moins occupée.

### IA élémentaire

- Le porteur dribble dans la meilleure direction parmi cinq. Il tire à moins de 22 m (niveau normal) si l'angle
  le permet, en visant le côté opposé au gardien, avec une erreur de visée. Quand il est pressé (adversaire à
  moins de 4 m) ou qu'un partenaire est nettement mieux placé, il amortit puis passe.
- Les notes de passe tiennent compte de la progression, de l'espace autour du receveur et de la ligne de passe
  dégagée. **Flair** : plus il est élevé, plus le joueur garde le ballon [FW : « individual game »].
- Sans le ballon : un ou deux poursuivants (ceux qui l'atteignent le plus tôt), les autres tiennent leur poste
  (formation décalée avec le ballon, plus haute en possession). Un joueur qui vient de perdre le ballon ne repart
  pas aussitôt à sa poursuite.
- Niveaux `easy`, `normal`, `hard` (`config/ai.json`) : réaction, fréquence de décision, précision, portée de tir,
  nombre de poursuivants. Aucun bonus de vitesse ni de physique.
- Les passes et tirs de l'IA ne sont pas limités aux 8 directions du joystick [Choix], compensé par l'erreur de
  visée. Ils obéissent aux mêmes conditions de portée et de timing que ceux de l'humain.
- Mesure : sur six matchs ordinateur contre ordinateur de 2 × 3 minutes, scores de 0-0 à 3-0, des passes, des
  tirs, des parades, des touches, des corners et des six mètres, aucun blocage.

### Tactiques

- Modèle de Kick Off 2 : chaque joueur de champ a une position pour chacune des **12 zones du ballon** (3 colonnes
  × 4 rangées), plus 8 situations (coups d'envoi, six mètres, corners). Le gardien est géré à part [KO2 : « the
  players are positioned depending on the position of the ball » ; format Reconstitué : Open Kick-Off
  `Tactic.java` et Tactical Editor].
- Pas d'interpolation : quand le ballon change de zone, les joueurs courent vers leur nouvelle position
  [Reconstitué : comportement des deux implémentations de référence].
- Final Whistle : les positions dépendent aussi de l'attaque ou de la défense [FW]. Une tactique peut fournir un
  jeu de positions `attack`, utilisé quand l'équipe a le ballon.
- Tactiques fournies : 4-4-2 et 4-3-3, avec des positions originales [Estimé]. Les fichiers `.tac` du jeu
  original peuvent être utilisés via un mod (voir MODDING.md).

## Caméra et affichage

- La caméra reste centrée sur le ballon, avec un lissage très court et sans anticipation. Elle reste dans
  l'enceinte du terrain [Reconstitué : Open Kick-Off centre la caméra sur le ballon pendant le jeu]. Elle est
  indépendante de la simulation.
- Hauteur affichée : le ballon monte à l'écran de la moitié de son altitude réelle [Reconstitué : `Ball.render`
  d'Open Kick-Off]. Les buts sont projetés de la même façon.
- Vue : 640×360 pixels virtuels à 16 px/m, soit environ 40 × 22 m visibles. L'original, en 320 pixels de large à
  12 px/yard, montre environ 24 × 15 m. Le cadrage plus large est un [Choix] pour l'écran 16:9 ; le zoom se règle
  avec **[ ]**.
- Radar (scanner) : terrain miniature avec joueurs, ballon et zone visible [KO2 : scanner].
- Tableau : score et minute de jeu en match ; équipe, buts et chronomètre en entraînement.
- Bandeaux : coup d'envoi, touche, corner, six mètres, but (avec le buteur), mi-temps, fin du match.

## Paramètres à calibrer en priorité

Ces valeurs font l'essentiel des sensations. Toutes se règlent dans les données et se rechargent avec F5 :

1. `controls.json › dribble` et `physics.json › ball.rolling*` : distance à laquelle le ballon court devant le
   joueur. Actuellement le centre du ballon s'éloigne jusqu'à 1,7 m du joueur à pleine vitesse, avec une touche
   toutes les 1,27 s environ.
2. `controls.json › shot.window` et `shot.reach` : difficulté du timing du tir.
3. `physics.json › movement` : nervosité (accélération, adhérence, freinage).
4. `physics.json › ball.rollingResistance` et `rollingDrag` : portée des passes et des tirs au sol.
5. `controls.json › aftertouch` : intensité et durée de la courbe.
6. `controls.json › pass` : puissances et temps de charge.
7. `controls.json › keeper` et `ai.json › keeper` : difficulté de marquer (portée, vitesse captée, sortie).
8. `ai.json › levels` : réaction et précision de l'ordinateur pour chaque niveau.
9. `rulesets/classic.json` : durée des mi-temps et des arrêts de jeu.

## Limitations connues

- Pas encore de tacle glissé, de faute, de carton, de tête, de reprise de volée, de retourné, de flick ni de
  plongeon du gardien (lot 3). Un ballon haut est amorti s'il est contrôlé, sinon il rebondit sur le corps.
- Pas de hors-jeu, de coup franc, de penalty, de remplacement, de prolongation ni de tirs au but (lots 4–5).
- Les six mètres de l'équipe humaine sont toujours tirés par l'ordinateur.
- IA élémentaire : pas de marquage, de couverture ni d'appels de balle construits. Les mêlées autour du ballon
  restent fréquentes.
- Le vent est simulé mais n'a pas d'option de jeu.
- La fatigue n'affecte pas encore la précision.
- Pas de menus (lot 5) : le mode de jeu se choisit en ligne de commande.
- Pas de son. Graphismes temporaires générés.
