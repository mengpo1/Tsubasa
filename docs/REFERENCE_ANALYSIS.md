# Analyse des références techniques

Ce document récapitule ce que les sources sur Kick Off 2 et The Final Whistle permettent réellement de reprendre,
et ce que le projet en a fait.
- **Confirmé** : documenté par un manuel ou par Dino Dini.
- **Reconstitué** : repris d'une implémentation de référence ou d'un désassemblage publié, ou déduit des données
  originales.
- **Approximatif** : valeur de calibration choisie faute de données.

## Sources étudiées

| Source | Nature | Licence / réutilisation |
|---|---|---|
| **A. Open Kick-Off** (`ssenegas/kickoff`, commit `b5f7f87`, 2024) | Réécriture Java/libGDX inachevée : 34 fichiers, 2 800 lignes | Code sous MIT : constantes et modèles repris avec attribution (`THIRD_PARTY_NOTICES.md`). Ses sprites, cartes et tactiques viennent du jeu commercial : non repris |
| **B. Tactical Editor** (`ssenegas/tacticaleditor`) | Lecteur et éditeur des fichiers `.tac` originaux | **Aucune licence** : code non réutilisé. Seul le format de fichier, un fait technique, sert de référence. Ses `.tac` sont des fichiers originaux : non repris |
| **C. KO2 Competition Version** (wiki.kickoff2.com, KOA, ko2wc.com) | Notes de version, règles de tournoi, extraits de désassemblage publiés par la communauté | Informations sur des comportements ; aucun binaire utilisé |
| **D. Manuels** KO2 et Final Whistle, **blog de Dino Dini** | Règles et commandes ; déclarations de l'auteur | Les sources originales retrouvées par Dini (2011) ne sont pas publiques : traitées comme propriétaires |
| **E. KONEY** (`KONEY/ko2-gfx-restyle-adf-amiga`) | Refonte graphique de KO2 : fichiers du jeu modifiés et notes | **Aucune licence**, et le dépôt contient des fichiers du jeu : seules les notes ont été lues (palette, organisation). Rien n'est repris |

Sources injoignables : forum ko-gathering (403), web.archive.org, transcription de la conférence GDC 2013. La
vidéo Amiga n'a pas pu être visionnée dans cet environnement.

## Synthèse par mécanique

| Mécanique | Source étudiée | État de la référence | Adaptation C# |
|---|---|---|---|
| Déplacements | A : `entities/Player.java` (`SPEED = 150f`, `update()`), wiki *Pitch* (12 px/yard) | **Partiel**. Vitesse constante de 150 px/s, soit 11,4 m/s, sans accélération ni inertie | Vitesse de pointe 11,4 m/s **[Reconstitué]**. Inertie, adhérence et freinage **[Approximatif]**. Fatigue, Pace : manuel **[Confirmé]**, effets **[Approximatif]** |
| Physique du ballon | A : `entities/Ball.java` (`update()`) ; D : blog de Dini, « A ball with air friction » | **Partiel**. Rebond : vitesse verticale −¼, horizontale −1/32. Traînée unique de 0,81/s et gravité appliquée deux fois, donc incohérent. Dini confirme l'intégration d'Euler avec traînée linéaire | Rebond ×0,75 / ×31/32 **[Reconstitué]**. Traînée linéaire **[Confirmé, principe]**, valeurs **[Approximatif]**. Roulement et surfaces **[Approximatif]**, effets qualitatifs du manuel **[Confirmé]** |
| Dribble | A : `screens/MatchScreen.java` (`checkCollisions()`, `speed()*1.125f + 30.0f`) ; C : wiki *PBD*, désassemblage `0x0000A416` | **Confirmé par deux sources indépendantes**. Ballon poussé à `1,125 × vitesse + 30 px/s` dans la direction du joueur. **PBD** : dans les 18 yards du but, `+30` est retiré (KO2 World Cup 90 et Player Manager : tous les joueurs ; v1.2e et suivantes : ordinateur seul, `−40`) | Formule `1,125 × v + 2,29 m/s` **[Reconstitué]**. PBD implémenté et paramétrable : ordinateur seul par défaut, comme la v1.4 (`controls.json › dribble.penaltyBoxDribble`). Ballon jamais collé **[Confirmé, Dini]** |
| Tir et passe | D : manuels, tutoriel de Dini (wiki *Kick Off 2 Tutorial*) ; A : rien (touches de debug S/D/E/X) | **Confirmé (règles), absent (valeurs)**. Bouton après la touche = tir ; avant = amorti ; relâcher en visant = passe ; relâcher au centre = sortie de l'amorti. Ballon libre : bouton = **tacle glissé**, ou **tête** si le ballon est en l'air. Impossible de glisser quand on a le ballon | Règles d'amorti, tir et passe implémentées. La règle « ballon libre » (glissade ou tête au lieu de l'amorti) arrive au **lot 3** avec les tacles ; pour l'instant, un appui sur ballon libre arme l'amorti (écart signalé dans GAMEPLAY.md). Puissances **[Approximatif]** |
| Aftertouch | D : manuel KO2 (positions avant, « very short time »), tableau des coups francs ; Dini : « reading the post kick joystick movements » ; A : absent ; C : « Aftertouch modifications » non détaillées | **Confirmé (principe)**, durée et intensité non documentées | Courbe latérale par les positions avant, fenêtre courte, positions arrière sans effet **[Confirmé]**. 0,55 s et 11 m/s² **[Approximatif]**. Plongée et lift paramétrés, nuls en jeu courant (réservés aux coups francs du lot 4) |
| Collisions | A : `Player.getBounds()` (cercle de 8 px), test de hauteur `z < taille` ; pas de collision entre joueurs | **Partiel** | Rayon de contact au pied 0,61 m **[Reconstitué]**. Collisions entre joueurs, filets, poteaux, sous-pas anti-traversée **[Approximatif / Choix]** |
| Sélection des joueurs | D : manuel (« the player nearest to the ball ») ; A : `Team.calculateClosestPlayer()` (distance au carré, sans hystérésis, utilisé seulement pour les touches) ; C : astuce communautaire (le bouton fait passer au joueur plus proche) | **Partiel**. Le règle exacte n'est pas documentée | Joueur de champ le plus proche **[Confirmé]**. Anticipation de 0,25 s et marge de 1,5 m contre le scintillement **[Choix]** |
| IA | D : Dini (22 machines à états de joueurs à environ 55 états, plus un contrôleur de match à environ 45 états, 50 images/s, 68000 en virgule fixe) ; C : les coéquipiers non contrôlés glissent et jouent de la tête automatiquement (*Autofix* les en empêche), l'ordinateur dribble avec PBD ; A : absent | **Architecture confirmée, comportements absents** | Machines à états par joueur et contrôleur de match **[Confirmé, principe]**. Décisions **[Approximatif]**. Glissades et têtes automatiques des coéquipiers, options Autofix et Trapfix : **lot 3** |
| Tactiques | A : `tactics/Tactic.java` (`getRegionIndex()`, `update()`), `Team.setupKickoff()` ; B : `model/Tactic.java` (`read()`), `model/PitchZone.java`, `utils/PitchConstants.java` ; D : manuel KO2 (positions selon le ballon), FW (selon le ballon **et** la phase attaque/défense), Dini (éditeur sur grille) | **Confirmé (modèle et format)**. 12 zones du ballon (3 colonnes × 4 rangées, numérotées par colonne), plus 8 situations ; 10 joueurs de champ, gardien à part ; `.tac` = 10 × 20 × (x, y) en int16 big-endian, 912 × 1392 à 12 par yard ; aucune interpolation | Modèle repris tel quel **[Reconstitué]** (`Tactics/TacticDefinition.cs`, `MatchTeam.TacticTarget`). Jeu `attack` optionnel pour FW **[Confirmé, principe]**. Lecteur `.tac` (`KickOff2TacticFile`) pour les fichiers que le joueur possède. Tactiques fournies : positions originales **[Approximatif]** |

### Points de détail vérifiés

- **Ordre des 8 situations d'un `.tac`** : 12 = coup d'envoi pour soi, 13 = coup d'envoi adverse, 14 = six mètres
  adverse, 15 = six mètres pour soi, 16 à 19 = corners propre-gauche, adverse-gauche, propre-droite,
  adverse-droite. Les deux dépôts ne nomment pas les six mètres dans le même ordre ; cet ordre a été déduit en
  regardant si les joueurs montent ou reculent dans les fichiers.
- **Repère des tactiques** : x part de la touche gauche, y de la ligne de but de l'équipe. Les données le
  confirment : défenseurs à y faible, positions de coup d'envoi toutes sous 696, le milieu de terrain.
- **Zone PBD** : le désassemblage compare la position du ballon à 216 et 1176, soit 1392 − 216. 216 unités
  valent 18 yards, la profondeur de la surface : la zone couvre toute la largeur du terrain sur cette profondeur.
  Un indicateur « haut ou bas » choisit l'extrémité. Nous l'interprétons comme le but attaqué ; ce point est
  **ambigu**.
- **Caméra** : Open Kick-Off la centre sur le ballon pendant le jeu (`MatchState.INPLAY.update()`) et la fait
  glisser vers une cible pendant les arrêts (`CameraHelper.update()`). Le projet suit le ballon avec un lissage
  court. Le cadrage d'origine (environ 24 × 15 m visibles) est plus serré que le nôtre (40 × 22 m), choisi pour le
  16:9.
- **Hauteur à l'écran** : `Ball.render()` monte le ballon de la moitié de son altitude ; nous projetons les
  hauteurs à 0,5.

## Graphismes (apparence Classic Amiga)

| Élément | Source étudiée | État de la référence | Adaptation C# |
|---|---|---|---|
| Disque et archives | C : wiki KOA, *Dino Dini File System* ; structure vérifiée sur les en-têtes | **Reconstitué** : table à l'octet 0x400 ; entrées de 16 octets (nom de 12 octets, 12 bits de bloc et 20 bits de taille). Les archives `.dat` ont une table de 16 octets par membre, avec des décalages relatifs | `tools/Arcade.Importer` : `DdfsDisk`, `DatArchive`. Un disque d'un autre format est signalé, et l'option `--files` le contourne |
| Images | Spécification IFF ILBM (EA 85, Commodore) | **Confirmé** (format public) : BMHD, CMAP, BODY en plans entrelacés, ByteRun1, masque | `IlbmImage`. Palettes 4 bits étendues à 8 bits. HAM et EHB refusés explicitement |
| Planches de sprites | A : `Player.java`, `Ball.java` ; en-têtes des fichiers du joueur | **Reconstitué** : tuiles 16×16 sur 20 colonnes, sprite centré sur la position. Planches de 320×161 (une ligne de plus que la grille) | Disposition indexée (`directionStride`, `sheetColumns`). La grille est recadrée et sa position notée dans le rapport |
| Course | A : `Player.java` (`runningFrameAnimationSequence`, `maxFrameTime = 5 / SPEED`) | **Reconstitué** : tuile `séquence[i] + 8 × direction`, directions N, NE, E… NW, une image tous les 5 px | `classic.run`, pilotée par la distance : 11,4 m/s × 0,0333 s |
| Ballon et ombre | A : `Ball.render()` | **Reconstitué** : ballon dessiné à h = z/2 au-dessus du sol. Au ras du sol, tuiles 0–3, ombre comprise. En l'air, tuiles 4–7 (`min(3, z/32)`) et ombre (tuile 8) décalée de (+h, −h) | Pilotage par la hauteur affichée (`driver: height`) ; `ballShadowShift` |
| Palette des sprites | E : notes de KONEY | **Approximatif** : 16 couleurs, 8 pour le terrain et 8 pour les sprites (0 transparent, 1 cheveux, 2 peau, 3 et 4 couleurs principales des équipes A et B, 5 secondaire et ballon, 6 gardien, 7 ombre) | Indices 3 et 4 → repère maillot, 5 → repère short, recoloriés par équipe |
| Terrains | C : wiki KOA ; A : `MatchScreen` (image de 1280×1536 px) ; structure des fichiers `pdat.*` | **Reconstitué** : carte de 80×96 tuiles de 16 px (en-tête 0x0050 0x0060 puis un octet par tuile) et jeux de tuiles `fbgraph4.*` en 8 couleurs. Lignes sur 912×1392 px à partir de (176, 64) | `PitchComposer`. Image placée par `pitchBitmap` (centre en (632, 760), 13,1234 px/m), alignée sur la géométrie du jeu |
| Résolution | En-tête de l'image `title` (320×256, 5 plans) | **Reconstitué** : mode PAL basse résolution. La zone de jeu en match n'est pas vérifiée | Apparence en 320×256, agrandissement entier, `pixelAspect` facultatif |
| Autres animations, polices, tableau de score, menus | — | **Non établi** | Remplacements signalés ; planches contact numérotées pour les identifier à partir des fichiers de l'utilisateur |

Aucune comparaison au pixel près avec l'original n'a été faite : la fidélité au pixel n'est pas revendiquée.

## Différences entre KO2, Final Whistle et Competition Version

| Sujet | KO2 (v1.4) | Final Whistle | Competition Version et tournois | Dans le projet |
|---|---|---|---|---|
| Tactiques | Positions selon le ballon seulement | Selon le ballon et la phase attaque/défense | — | Les deux : jeu `attack` facultatif |
| PBD | Ordinateur seul (v1.2e et suivantes) ; tout le monde sur World Cup 90 et l'Oracle | Inconnu | Option par joueur, « PBD Madness » | Réglable, ordinateur seul par défaut |
| Glissades automatiques des coéquipiers | Oui | Inconnu | *Autofix* les supprime (option par défaut en tournoi) | Lot 3, avec option |
| Amorti changé en glissade près d'un adversaire | Oui | Inconnu | *Trapfix* l'empêche (désactivé par défaut en tournoi) | Lot 3, avec option |
| Gardien et lobs | Faible sur les lobs | Meilleur | Option « gardiens FW » | Lot 3 |
| Avantages cachés | Joueurs humains plus rapides que l'ordinateur ; équipe A plus forte (environ 10 %) | Inconnu | Corrigés | Non reproduits pour l'instant ; options possibles au lot 4 |
| Vent | Agit aussi au sol | — | Air seulement, ±2/4/6 m/s | Air et un peu au sol ; niveaux à brancher au lot 4 |
| Passe au gardien | Interception impossible | — | Corrigée | Non reproduit (interception possible) |
| Contrôle du gardien | Penalties et six mètres | Possible en mode équipe à deux, limité à la surface | — | Lots 3–5 |
| Cadence | 50 images/s | 50 images/s | — | 60 pas/s : les unités SI rendent la cadence neutre |

## Effets sur le projet

Déjà appliqué :
1. Tactiques au modèle de Kick Off 2 (12 zones et 8 situations, sans interpolation), jeu `attack` de FW, import
   des `.tac`.
2. Constantes reconstituées : vitesse de course, formule de dribble, rebonds, rayon de contact, terrain de
   76 × 116 yards, caméra centrée sur le ballon, projection des hauteurs à 0,5.
3. PBD, ordinateur seul par défaut.
4. Apparence Classic Amiga, construite par un importeur local à partir de la copie du joueur (THEMES.md).

Programme du **lot 3**, orienté par ces conclusions :
1. Règle d'origine du bouton sur ballon libre : glissade au sol, tête en l'air. L'amorti n'est possible qu'en
   possession, et la glissade est impossible avec le ballon. Option *Trapfix*.
2. Tacle glissé et tacle en bloc ; fautes par-derrière (manuel KO2).
3. Glissades et têtes automatiques des coéquipiers non contrôlés, avec option *Autofix*.
4. Têtes, retourné et flick (manuels), puis plongeons du gardien, avec l'option « gardiens KO2 / FW » pour les lobs.

Restent à calibrer, faute de source : inertie, roulement, durée et force de l'aftertouch, puissances des tirs et
des passes, règle exacte de changement de joueur, comportement fin de l'IA et du gardien.
