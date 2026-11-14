# Notices de tiers

## Open Kick-Off (MIT)

Plusieurs valeurs et modèles de ce projet ont été repris ou adaptés d'**Open Kick-Off**
(https://github.com/ssenegas/kickoff), une réécriture en Java de Kick Off 2. Le code n'a pas été traduit : les
mécanismes ont été réécrits en C#, et le détail figure dans `docs/REFERENCE_ANALYSIS.md`.

Éléments repris :
- l'échelle de 12 pixels par yard et le terrain de 76 × 116 yards ;
- la vitesse de course des joueurs (150 px/s) ;
- la formule de la poussée de dribble (1,125 × vitesse + 30 px/s) ;
- les coefficients de rebond du ballon (perte de ¼ de la vitesse verticale et de 1/32 de la vitesse
  horizontale) ;
- le rayon de contact joueur–ballon (8 px) ;
- le modèle tactique à 12 zones et 8 situations, avec la numérotation des zones et la disposition des
  fichiers `.tac` ;
- pour l'apparence Classic Amiga, des faits d'affichage, réécrits sous forme de données dans
  `content/themes/classic-amiga/` :
  - planches de tuiles 16×16 sur 20 colonnes ;
  - image de course `séquence[i] + 8 × direction` avec sa séquence et sa cadence ;
  - sprites centrés sur la position ;
  - images du ballon choisies selon la hauteur, avec l'ombre décalée de (+h, −h).

```
MIT License

Copyright (c) 2019 Sébastien Sénégas

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Ne sont **pas** repris :
- les sprites, cartes de terrain et fichiers de tactiques fournis avec Open Kick-Off et le Tactical Editor : ils
  proviennent du jeu commercial et la licence MIT ne peut pas les couvrir ;
- le code du Tactical Editor (https://github.com/ssenegas/tacticaleditor), publié sans licence. Seule la
  description du format de fichier, un fait technique, a été utilisée ;
- le contenu de `KONEY/ko2-gfx-restyle-adf-amiga` : ce dépôt contient des fichiers du jeu, modifiés, sans
  licence. Seules sa documentation et ses descriptions (répartition de la palette, organisation des fichiers)
  ont été lues. Aucun fichier n'en est tiré.

## DejaVu Sans

Police distribuée selon sa licence : voir `content/assets/fonts/LICENSE-DejaVu.txt`.
