# Neon Night — mod de démonstration

Montre qu'un mod remplace réellement des ressources et des données sans toucher au code :

| Fichier | Effet |
|---|---|
| `assets.json` | Relie `pitch.grass.normal` et `ball.default` aux images de ce dossier |
| `assets/textures/*.png` | Herbe néon, ballon doré |
| `pitches/normal.json` | Lignes cyan, bandes plus contrastées (fusion avec le terrain de base) |
| `teams/reds.json` | Nom « Neon Rangers », abréviation NEO, nouvelles couleurs |
| `config/physics.json` | Ballon plus vif (`rollingResistance` 0,9 au lieu de 1,4) |
| `locales/en.json`, `locales/fr.json` | Titre de l'entraînement |

Activation : `--mod example-mod`, ou `"mods": ["example-mod"]` dans `settings.json`, ou **F6** en jeu.
