# Tuto : un éclair convaincant (VFX Graph + Particle System)

Objectif : l'effet de **Appel de la foudre** (druide). Il s'applique à tout éclair : chaîne d'éclairs, pierre de
foudre, élite foudroyant.

**Outils** (tous dans le projet) :
- le **VFX Graph** 17.5, effets calculés sur la carte graphique ;
- le **Particle System** intégré ;
- les **textures et matériaux du pack Hovl** (`Assets/Hovl Studio/Magic effects pack/`) ;
- le **Bloom** du post-process URP, déjà actif.

> Le VFX Graph est arrivé avec le package **HDRP** (dont il est une dépendance), mais le jeu rend toujours en **URP**
> (`URP_Default` dans *Graphics / Quality*). Le VFX Graph fonctionne en URP. Si un jour tu retires HDRP, ajoute
> d'abord le VFX Graph directement : *Package Manager → Unity Registry → Visual Effect Graph → Install*. Sinon il partira
> avec HDRP.

---

## 1. Ce qui rend un éclair crédible

Un éclair « fait vrai » quand il combine, dans cet ordre et en **moins d'une demi-seconde** :

| Temps | Élément | Pourquoi |
|---|---|---|
| 0 | **Arc principal** dentelé, très lumineux, du ciel au sol | La forme reconnaissable |
| 0 → 0,1 s | **Scintillement** : l'arc clignote 2-3 fois, avec des **branches** fines | La foudre n'est jamais stable |
| 0 | **Flash** blanc à l'impact + **lumière** qui éclaire le décor | Le cerveau associe éclair = lumière soudaine |
| 0 → 0,3 s | **Étincelles** projetées et **onde au sol** | Le poids de l'impact |
| 0,1 → 1 s | **Brûlure au sol** (fissure lumineuse) + **fumée** | La trace qui reste |

Règle d'or : **très court, très lumineux, très contrasté**. Un éclair qui dure 1 s ressemble à un néon.

---

## 2. Quel outil pour quoi

| Couche | Outil conseillé | Pourquoi |
|---|---|---|
| **Arc principal + branches** | **VFX Graph** (Particle Strip) | Un « strip » relie des points en un ruban continu : on place 20-30 points du ciel au sol, on les décale au hasard, et l'arc est parfaitement dentelé. Il est recalculé à chaque scintillement. Bien plus net que le Trail + Noise du Particle System. |
| Flash, étincelles, onde, brûlure, fumée | VFX Graph **ou** Particle System | Les deux conviennent. Si l'arc est en VFX Graph, tout faire dans le **même graph** simplifie le prefab. |
| **Lumière** qui éclaire le décor | **Particle System** (module Lights) ou un composant **Light** | Le VFX Graph **ne crée pas de lumières en URP**. Garde un petit Particle System enfant dédié à la lumière. |

Recommandation : **un graph `VFXG_LightningStrike`** (arc, branches, flash, étincelles, sol, fumée) + **un Particle
System enfant** pour la lumière. La variante 100 % Particle System reste décrite à la section 5.

---

## 3. Préparer le prefab

1. Hierarchy → *Create Empty* → `FX_LightningStrike` à la position (0, 0, 0).
   - Le **pivot = le point d'impact au sol**, l'axe **+Y vers le ciel**.
2. Le jeu met l'effet à l'échelle du **rayon** de la zone : *Appel de la foudre* a un rayon de 5 m, donc l'effet est
   affiché ×5.
   - **Construis l'effet pour un rayon de 1 m** : onde au sol de 2 m de diamètre, colonne d'environ 4 m de haut
     (20 m en jeu).
   - **VFX Graph** : chaque système en espace **Local** (bouton *Local / World* en haut à droite de chaque contexte).
     Les positions suivent alors l'échelle du GameObject.
   - **Particle System** : *Main → Scaling Mode = Hierarchy* sur **chacun**. Sinon l'échelle du jeu est ignorée.
3. **Durée** :
   - Particle System : *Looping* **décoché**, *Play On Awake* coché, *Stop Action* = None. Le jeu attend la fin des
     particules (4 s au maximum) avant de recycler l'effet.
   - VFX Graph : le jeu ne peut pas lire sa durée. Indique-la dans l'ArtLibrary, champ **Lifetime** de l'entrée
     (ex. 1,3 s si la brûlure dure 1,2 s). Sinon l'effet est coupé après la durée du placeholder (0,3 s).
4. Matériaux : pars des matériaux du pack Hovl, qui sont additifs et compatibles URP, et **duplique-les** (Ctrl+D) dans
   `Assets/_Project/Art/VFX/Materials/`. Ne modifie jamais les originaux du pack.
5. Couleur **HDR** : dans le sélecteur de couleur, monte **Intensity entre +1 et +3**. C'est ce qui fait briller le
   Bloom (les effets du jeu sont à ~×1,8).

Textures utiles du pack (dossier `Textures/`) :

| Texture | Usage |
|---|---|
| `Electro.png` | Arcs électriques (tranche d'éclair) |
| `Trail67.png` | Traînée lumineuse pour les Trails |
| `Flash.png`, `FlashFree1-3.png` | Flash d'impact |
| `Point1.png`, `GlowFree1.png` | Lueur ronde, étincelles |
| `Circle.png`, `Circle2.png` | Onde au sol |
| `Crack.png` | Fissure brûlée au sol |
| `Smoke26.png`, `SmokeFree1.png` | Fumée |

---

## 4. L'éclair en VFX Graph

> Les noms entre *italiques* sont ceux de l'interface Unity (en anglais). Dans le graph, **Espace** ou clic droit →
> *Create Node* ouvre la recherche : tape le nom du bloc ou de l'opérateur (ex. `Single Burst`, `Lerp`).

### 4.0 Deux Shader Graphs pour les sorties (à faire une fois)

Dans ce projet, les sorties proposées par le VFX Graph sont des variantes **Shader Graph** (et des variantes
**HDRP**). Les sorties **HDRP** (*HDRP Distortion*, *HDRP Lit*…) **ne s'affichent pas** : le jeu rend en URP.
Il faut donc prendre la variante **Shader Graph** et lui donner un Shader Graph compatible VFX.

1. Project → `Assets/_Project/Art/VFX/Shaders/` → clic droit → *Create → Shader Graph → URP → Unlit Shader Graph*
   → `SG_VFX_Additive`.
2. Ouvre-le. *Graph Inspector* → onglet *Graph Settings* :
   - *Material* : Unlit ;
   - *Surface Type* : **Transparent** ;
   - *Blending Mode* : **Additive** ;
   - *Render Face* : Both ;
   - coche **Support VFX Graph**.
3. *Blackboard* (**+**) :
   - `MainTex` : *Texture2D* ;
   - `Tint` : *Color*, *Mode* **HDR**, valeur blanche.
4. Graph :
   - *Sample Texture 2D* (Texture = `MainTex`) ;
   - *Multiply* : sortie RGBA du sample × `Tint` ;
   - sortie RGB → *Base Color* du *Fragment* ; composante A (nœud *Split*) → *Alpha*.
5. *Save Asset*. Duplique-le en `SG_VFX_Alpha` et passe son *Blending Mode* à **Alpha** : il servira à la fumée.

Dans le VFX Graph, chaque sortie *Shader Graph* reçoit ce Shader Graph dans son champ **Shader Graph**. Ses
propriétés apparaissent alors comme des entrées du contexte :
- `MainTex` : la texture du pack Hovl ;
- `Tint` : à relier à *Get Attribute: color* et *Get Attribute: alpha*, assemblés en *Vector4* (opérateur
  *Vector4*, ou *Combine*). Les couleurs et fondus réglés par les blocs *Set Color*, *Set Alpha over Life*… arrivent
  ainsi dans le shader.

### 4.1 Créer le graph
1. Project → `Assets/_Project/Art/VFX/` → clic droit → *Create → Visual Effects → Visual Effect Graph* →
   `VFXG_LightningStrike`. Supprime le système par défaut.
2. Ouvre le graph. Dans le *Blackboard* (bouton **+**), crée ces propriétés et coche *Exposed* sur chacune. Elles
   apparaissent ensuite dans l'Inspector du composant *Visual Effect*.
   **Renseigne la valeur de chacune** : déplie la propriété (▸) dans le *Blackboard*, champ *Default Value*.
   Une propriété créée vaut 0, ou noir transparent pour une couleur : avec `Points` = 0 aucune particule n'apparaît,
   et une couleur d'alpha 0 est invisible.

| Property | Type | Valeur |
|---|---|---|
| `Height` | *Float* | 4 (hauteur de la colonne, rayon 1) |
| `Jaggedness` | *Float* | 0,35 (amplitude des zigzags) |
| `BoltColor` | *Color* (HDR) | blanc-bleu, *Intensity* +3 |
| `GlowColor` | *Color* (HDR) | bleu, *Intensity* +1,5 |
| `Points` | *Int* | 24 (points par arc) |

3. Glisse le `.vfx` dans le prefab `FX_LightningStrike` : un enfant avec le composant *Visual Effect* est créé.
   Laisse *Initial Event Name* = `OnPlay` : l'effet se joue à chaque apparition, recyclage compris.

### 4.2 Arc principal (Particle Strip)

Assemble 4 contextes (*Create Node → Context*), reliés de haut en bas :
*Spawn* → *Initialize Particle Strip* → *Update Particle Strip* → *Output ParticleStrip Shader Graph Quad*.
Dans chaque contexte, **Espace** ajoute un bloc.

**Spawn**
- 3 blocs *Single Burst* : *Delay* 0, 0,08 et 0,16 s ; *Count* = `Points`. Chaque burst = un arc complet = un
  scintillement.

**Initialize Particle Strip** : les champs du contexte lui-même.

| Champ | Valeur |
|---|---|
| Bouton d'espace (en haut à droite) | **Local** |
| *Strip Capacity* | 1 |
| *Particle Per Strip Count* | 32 (≥ `Points`) |
| *Bounds Mode* | **Manual** |
| *Bounds* (déplier ▸) | *Center* (0, 2, 0), *Size* (3, 5, 3) |
| *Bounds Padding* | 0 |
| *Strip Index* | 0 |

*Bounds Mode* : *Recorded* (défaut) demande d'enregistrer les bounds dans l'éditeur, *Automatic* les recalcule
chaque frame et coûte plus cher, *Manual* est fixe et sûr. Sans bounds corrects, l'effet disparaît quand le centre
sort de l'écran.

Blocs du contexte Initialize :
- *Set Lifetime* = 0,07. Chaque arc meurt avant le suivant (0,08 s plus tard) : le strip se reconstruit et le
  scintillement est net. Plus court, l'arc ne dure qu'une ou deux images et passe inaperçu.
- *Set Position*, dont l'entrée *Position* est calculée par ces opérateurs :
  1. `t` = *Get Attribute: spawnIndex* ÷ (`Points` − 1), avec *Divide* et *Subtract*. Il vaut 0 au premier point et 1
     au dernier.
  2. base = *Lerp*(A = (0, `Height`, 0), B = (0, 0, 0), S = `t`) : du ciel au sol.
  3. **courbe** (la grande forme, comme un vrai éclair qui serpente) :
     - opérateur *Total Time* (temps de l'effet, identique pour tous les points d'un même scintillement) ;
     - coordonnée = `t` × 2 + *Total Time* × 13 ;
     - X = *Perlin Noise* (*Type* 1D, *Coordinate* = coordonnée, *Frequency* 1, *Octaves* 2) ;
     - Z = le même *Perlin Noise* avec coordonnée + 100 ;
     - *Vector3*(X, 0, Z) × `Jaggedness` × (1 − `t`) : la courbe est large en haut et ramène l'arc **exactement** sur
       la cible en bas.
  4. **éclats** (les petits angles vifs) : deux *Random Number* (*Min* −1, *Max* 1, *Constant* décoché) en X et Z
     d'un *Vector3* (Y = 0), × 0,05 × *Sine*(*Pi* × `t`).
  5. *Position* = base + courbe + éclats (deux *Add*).

  > Avec un simple *Random Number* par point (ancienne version), l'arc fait des allers-retours de 35 cm tous les
  > 17 cm : on obtient des traits presque horizontaux, pas un éclair. La forme vient de la **courbe** lente ; le
  > hasard par point ne doit ajouter que de petits angles.
- *Set Size* = 0,12 × *Lerp*(0,6 ; 1,4 ; `t`) : l'arc s'épaissit vers l'impact.
- *Set Color* = `BoltColor`.

**Update Particle Strip** : rien, ou *Set Size over Life* pour amincir l'arc en fin de vie.

**Output ParticleStrip Shader Graph Quad** (pas la variante *HDRP*)
- *Shader Graph* : `SG_VFX_Additive` ;
- *Tiling Mode* : **Stretch** (la texture est étirée sur toute la longueur de l'arc) ;
- *Uv Mode* : Default ;
- `MainTex` : **`Trail67.png`** (trait net au centre, bords doux). Évite `Electro.png` sur le ruban : sa texture
  déjà dentelée, étirée sur l'arc, le découpe en traits parallèles ;
- `Tint` : couleur + alpha de la particule (section 4.0) ;
- bloc *Set Alpha over Life* : courbe 1 → 0.

### 4.2 bis Halo de l'arc

Le cœur blanc et fin ne suffit pas : l'image de référence a un **halo bleu large** autour de l'arc.
1. Duplique les 4 contextes de l'arc.
2. Dans ce double, **retire les éclats** (étape 4) : garde base + courbe. *Total Time* et le *Perlin Noise* donnent
   alors exactement le même tracé que l'arc, puisque les deux systèmes démarrent ensemble.
3. *Set Size* 0,5 ; *Set Color* = `GlowColor` ; *Set Alpha* 0,35.
4. Sortie : `SG_VFX_Additive`, `MainTex` = `GlowFree1.png` ou `Trail67.png`.

### 4.3 Branches

Les branches forment un **deuxième système, indépendant**, dans le même graph : il n'est relié à l'arc par
**aucun lien**. Au lancement de l'effet (`OnPlay`), chaque bloc *Spawn* du graph démarre tout seul, donc l'arc et les
branches partent ensemble. Les seuls liens sont internes au système :
- les liens de flux, verticaux, entre ses 4 contextes ;
- les opérateurs, branchés sur les entrées des contextes et des blocs.

1. Sélectionne les 4 contextes de l'arc, Ctrl+D, et pose la copie à côté. Elle garde ses liens de flux
   *Spawn* → *Initialize* → *Update* → *Output* ; supprime les opérateurs copiés qui pendent.
Sur l'image de référence, les branches partent **du point d'impact**, au ras du sol, et rayonnent vers l'extérieur
sur toute la zone : 6 branches de 10 points.

2. **Spawn** : les 3 *Single Burst* passent à *Count* = 60 (6 branches × 10 points).
3. **Initialize Particle Strip** : *Strip Capacity* 6, *Particle Per Strip Count* 10, mêmes *Bounds*.
4. Les opérateurs (Espace dans une zone vide → recherche), puis les liens :

| # | Opérateur | Entrées | Sortie branchée sur |
|---|---|---|---|
| A | *Get Attribute: spawnIndex* | — | B et C |
| B | *Divide* → *Floor* | A ÷ 10 | **entrée *Strip Index* du contexte *Initialize Particle Strip*** (le petit rond à gauche du champ), et les *Seed* de D et E. C'est le numéro de la branche (0 à 5) |
| C | *Modulo* → *Divide* | (A modulo 10) ÷ 9 | entrée *S* de G : `t`, de 0 à 1 le long de la branche |
| D | *Random Number* | *Min* 0, *Max* 0,3, *Constant* coché, *Seed* = B | *Y* d'un *Vector3* (X = 0, Z = 0) = **départ** de la branche, au ras du sol |
| E | 3 × *Random Number* (*Constant* coché) | X et Z : *Min* −1, *Max* 1, *Seed* = B + 10 et B + 20 ; Y : *Min* −0,1, *Max* 0,3, *Seed* = B + 40 | *Vector3*(X, Y, Z) → *Normalize* → × longueur = un 4e *Random Number* (*Min* 1,2, *Max* 2,2, *Constant*, *Seed* = B + 30) → **direction** (rayonne à plat sur toute la zone) |
| F | *Add* | départ + direction | entrée *B* de G = **bout** de la branche |
| G | *Lerp* | *A* = départ, *B* = F, *S* = C | entrée *A* de I |
| H | zigzag | 2 × *Random Number* (*Min* −1, *Max* 1, *Constant* **décoché**) en X/Z d'un *Vector3* (Y = 0), × 0,12, × *Sine*(*Pi* × `t`) | entrée *B* de I |
| I | *Add* | G + H | entrée *Position* du bloc *Set Position* |

5. Blocs du contexte Initialize :
   - *Set Lifetime* 0,12 : les branches restent un peu après l'arc ;
   - *Set Size* = 0,04 × (1 − `t`) : fines, pointues au bout ;
   - *Set Color* = `GlowColor` (glisse la propriété depuis le *Blackboard* sur l'entrée).
6. **Output** : le même Shader Graph que l'arc (`SG_VFX_Additive`), `MainTex` = `Trail67.png`.
7. Optionnel : pour que chaque scintillement ait des branches différentes, *Seed* = B + *Floor*(*Total Time* × 100) × 7
   au lieu de B (même chose pour les +10, +20, +30, +40).

*Constant* coché sur D et E : la valeur aléatoire est la même pour tous les points qui ont le même *Seed*,
c'est-à-dire pour toute une branche. Les 8 points partent donc du même endroit, dans la même direction. Décoché sur
H : chaque point a son propre décalage, d'où le zigzag.

### 4.4 Flash d'impact
Système classique : *Spawn* → *Initialize Particle* → *Update Particle* → *Output Particle Shader Graph Quad*
(ou *Create Node → System → Simple Particle System*, puis on vide ses blocs).
- *Single Burst* : *Count* 1, *Delay* 0 ;
- *Set Lifetime* 0,1 ; *Set Position* (0, 0,3, 0) ; *Set Size* 3 ;
- *Set Size over Life* : 0,5 → 1 ; *Set Alpha over Life* : 1 → 0 ;
- sortie *Shader Graph* `SG_VFX_Additive`, `MainTex` = `Flash.png`, *Set Color* blanc HDR (+3).

### 4.5 Étincelles
- *Single Burst* : *Count* 30 ;
- *Set Lifetime* en mode *Random* (*Uniform*) : 0,2 à 0,5 ;
- *Set Velocity* en mode *Random* (*Uniform*) : *A* (−6, 6, −6), *B* (6, 14, 6) ;
- *Update Particle* :
  - *Gravity* : (0, −9,8, 0) ;
  - *Collision Shape* : *Shape* Plane, position (0, 0, 0), normale (0, 1, 0), *Bounce* 0,3 ;
- sortie *Shader Graph* (`SG_VFX_Additive`) :
  - bloc *Orient* : mode **Along Velocity** ;
  - *Set Scale* : Y = 4 (traits allongés) ; *Set Size* 0,06 ;
  - `MainTex` = `Point1.png`, *Set Color* HDR +2.

### 4.6 Onde au sol et brûlure
Deux petits systèmes (sortie *Output Particle Shader Graph Quad*, `SG_VFX_Additive`), un *Single Burst* de 1 chacun. Pour les poser **à plat** au sol :
bloc *Orient*, mode **Fixed Axis** (*Axis* (0, 1, 0)), ou **Advanced** avec *Front* = (0, 1, 0).

- **Onde** :
  - *Set Lifetime* 0,35 ;
  - *Set Size over Life* 0,3 → 2 (2 m = diamètre de la zone au rayon 1) ;
  - *Set Alpha over Life* 1 → 0 ;
  - `MainTex` = `Circle2.png` ;
  - *Set Position* Y = 0,05.
- **Brûlure** :
  - *Set Lifetime* 1,2 ; *Set Size* 1,5 ;
  - *Set Angle* : Z en mode *Random* 0 à 360 ;
  - *Set Color over Life* : bleu lumineux → gris → transparent ;
  - `MainTex` = `Crack.png`.

### 4.7 Fumée
- *Single Burst* : *Count* 5 ;
- *Set Lifetime* *Random* 0,8 à 1,2 ;
- *Set Velocity* *Random* : Y de 0,5 à 1,5 (vers le haut, faible) ;
- *Set Size* *Random* 0,6 à 1,2, puis *Set Size over Life* 0,6 → 1,4 ;
- *Set Angle* *Random* et *Set Angular Velocity* *Random* ±30 ;
- sortie *Shader Graph* avec **`SG_VFX_Alpha`** (pas l'additif), *Set Color* gris foncé, alpha 0,35, `MainTex` =
  `Smoke26.png`.

### 4.8 La lumière (Particle System enfant)
Le VFX Graph ne crée pas de lumières en URP. Ajoute au prefab un *Particle System* dédié :
- *Duration* 0,2, *Looping* décoché, *Emission → Bursts* 1, *Start Lifetime* 0,1 ;
- module *Renderer* **désactivé** : on ne veut que la lumière ;
- module *Lights* réglé comme à la section 5.5.

### 4.9 Astuces VFX Graph
- *Capacity* (dans l'Inspector du contexte *Initialize Particle*, ou *Strip Capacity* × *Particle Per Strip Count*
  pour un strip) au plus juste : c'est de la mémoire réservée par instance.
- Le graph se recompile à chaque sauvegarde (Ctrl+S) : garde la fenêtre ouverte et regarde la *Scene*.
- *Play Controls* (overlay de la *Scene View* quand l'objet est sélectionné) : *Play*, *Restart*, *Play Rate* 0,1
  pour voir l'éclair au ralenti.
- Shader Graphs **Unlit** : un Shader Graph *Lit* coûte plus cher et n'apporte rien à un effet additif lumineux.
- Jamais de sortie **HDRP** : rien ne s'affiche en URP. Si tu retires le package HDRP (le jeu n'en a pas besoin),
  installe d'abord *Visual Effect Graph* depuis le *Package Manager* : il partirait avec HDRP, et ces sorties
  disparaîtront du menu.
- Un bloc qui ne s'applique pas est souvent un bloc désactivé : la case à cocher à gauche de son nom doit être
  cochée.

---

## 5. Variante 100 % Particle System

À utiliser pour un effet simple, ou pour la lumière (5.5), que le VFX Graph ne sait pas faire en URP.
Une couche = un Particle System enfant.


Crée chaque couche avec clic droit sur `FX_LightningStrike` → *Effects → Particle System*. Mets **Rotation X = 0** :
par défaut Unity tourne les particules vers +Z (-90°), il faut tout repasser vers le haut.

### 5.1 Arc principal : la technique « particule + Trail + Noise »

Une particule part du ciel et descend très vite. Le module **Noise** la fait zigzaguer, et son **Trail** (traînée)
dessine le chemin dentelé.

| Module | Réglage |
|---|---|
| Main | Duration 0,2 · Start Lifetime **0,06** · Start Speed **0** · Start Size 0,1 · Simulation Space **Local** |
| Emission | Rate 0 · **Bursts** : 3 bursts (temps 0 / 0,05 / 0,1) de **1** particule (le scintillement) |
| Shape | **Box** ou Sphère de rayon 0,3, position **Y = 4** (haut de la colonne) |
| Velocity over Lifetime | Linear Y = **−70** (traverse 4 m en ~0,06 s) |
| Noise | Strength **1,5 à 3** · Frequency **4** · Scroll Speed 0 · Damping coché · Octaves 2 · **Separate Axes** : Y = 0 (on ne veut zigzaguer qu'en X/Z) |
| Trails | Coché · Mode Particles · Ratio 1 · Lifetime **0,15** · Minimum Vertex Distance **0,15** (petit = plus d'angles) · Width over Trail : courbe de 1 vers 0,3 · Color over Trail : blanc → bleu pâle · *Die With Particles* décoché (le trait reste un instant) |
| Renderer | Render Mode : None ou une Material quelconque (on ne voit que le Trail) · **Trail Material** : copie de `Electro.mat` ou `Trail67.mat` en blanc-bleu HDR intensité +3 |

Résultat : 3 traits blancs dentelés en 0,1 s, chacun légèrement différent. Ça donne le scintillement.

### 5.2 Branches

Duplique la couche 5.1 (Ctrl+D) et règle-la ainsi :
- **Bursts** : 3 à 5 particules ;
- *Start Lifetime* 0,04 ;
- *Shape* : Sphere de rayon 0,5, à des hauteurs variées (Position Y = 2, avec *Randomize Position*) ;
- *Velocity over Lifetime* : X/Z aléatoires ±20, Y = −30 ;
- *Trails Width* plus fin (multiplicateur 0,4) ;
- couleur plus bleue, intensité +2.

Les branches partent de l'arc principal et s'éteignent plus vite.

### 5.3 Halo de la colonne (volume)

Un **Stretched Billboard** donne de l'épaisseur à l'arc vu de loin.
- Main : Lifetime 0,12 · Size 0,8 · Speed 0 · couleur bleu clair, intensité +1, **alpha 0,5** ;
- Emission : Burst de 1 ;
- Shape : désactivé (au centre) ;
- Renderer : Render Mode **Mesh** = Quad, ou Billboard avec *Start Size 3D* (X 0,8 · Y 4) et *Render Alignment = Local* ;
- matériau `GlowFree1` additif ;
- Color over Lifetime : 1 → 0 rapide.

### 5.4 Flash d'impact

- Main : Lifetime **0,1** · Size **3** · Speed 0 · blanc, intensité +3 ;
- Emission : Burst 1 au temps 0 ;
- Size over Lifetime : 0,5 → 1 ;
- Color over Lifetime : alpha 1 → 0 ;
- matériau `Flash` ou `FlashFree2` ;
- position Y = 0,3.

### 5.5 Lumière (le vrai « wow »)

Module **Lights** sur le Flash (5.4), ou sur le Particle System dédié (4.8) :
1. Crée un prefab avec une **Point Light** :
   - couleur bleu-blanc, *Range* 8 ;
   - **Shadow Type None** (performance), *Render Mode Auto*.
2. Sur le Flash, module **Lights** : *Light* = ce prefab, *Ratio* 1, *Use Particle Color* décoché,
   **Intensity Multiplier** 8, *Range Multiplier* 1.
3. *Intensity over Lifetime* : courbe 1 → 0 sur 0,1 s.

Le décor s'illumine d'un coup : c'est ce qui vend l'effet.

### 5.6 Étincelles

- Main : Lifetime 0,2-0,5 · Speed **6-14** · Size 0,05-0,12 · Gravity Modifier **1** · blanc → bleu ;
- Emission : Burst **25-40** au temps 0 ;
- Shape : **Hemisphere** de rayon 0,2, orientée vers le haut (Rotation X = -90) ;
- Renderer : **Stretched Billboard**, Speed Scale 0,05 (traits) ;
- matériau `Point` additif, intensité +2 ;
- Collision : facultatif (World, Bounce 0,3) pour qu'elles rebondissent au sol.

### 5.7 Onde au sol

- Main : Lifetime 0,35 · Size **0,5** · Speed 0 · bleu, alpha 0,8 ;
- Emission : Burst 1 ;
- Renderer : **Render Alignment = World** et Rotation X = 90, pour qu'elle soit **à plat** au sol ;
- Size over Lifetime : 0,3 → **2** (2 m = le diamètre de la zone pour un rayon de 1) ;
- Color over Lifetime : alpha 1 → 0 ;
- matériau `Circle2` ;
- position Y = 0,05, pour éviter qu'elle clignote au contact du sol.

### 5.8 Brûlure au sol

- Main : Lifetime **1,2** · Size 1,5 · Speed 0 · bleu, intensité +1,5 ;
- Emission : Burst 1 à 0,02 s ;
- Renderer : Alignment World, à plat, comme l'onde ;
- Color over Lifetime : bleu lumineux → gris sombre → transparent ;
- Rotation : Start Rotation aléatoire 0-360 (chaque impact est différent) ;
- matériau `Crack`.

### 5.9 Fumée

- Main : Lifetime 0,8-1,2 · Speed 0,5-1,5 · Size 0,6-1,2 · gris foncé, **alpha 0,35** ;
- Emission : Burst 4-6 ;
- Shape : Sphere de rayon 0,4 ;
- Size over Lifetime : 0,6 → 1,4 ;
- Rotation over Lifetime : ±30°/s ;
- matériau `Smoke26`. Le *Blend* doit être **Alpha**, pas additif : sinon la fumée brille.

---

## 6. Brancher l'effet dans le jeu

1. Glisse `FX_LightningStrike` dans `Assets/_Project/Art/VFX/` pour créer **ton propre prefab**.
   - **Ne modifie jamais** les prefabs générés de `Prefabs/Vfx/` : *Build Prototype Scene* les réécrit.
   - **Ne les mets jamais** dans le champ *Model* : ils contiennent un *ArtSlot*, et le jeu les instancierait en
     boucle. Unity se figerait ; depuis le correctif, la console affiche une erreur à la place.
2. Ouvre `Resources/ArtLibrary` → liste **Vfx** → entrée **`VFX_LightningStrike`**.
   - *Model* = ton prefab ;
   - *Keep Placeholder* **décoché** (la colonne grise disparaît) ;
   - *Scale* = 1 (ajuste si l'effet est globalement trop grand ou trop petit) ;
   - **Lifetime** = durée de l'effet complet si tu utilises le VFX Graph (ex. 1,3). Laisse 0 pour un effet
     100 % Particle System, sa durée est lue automatiquement ;
   - *Offset* / *Rotation* = 0.
3. Pas de rebuild : un simple Play suffit.
4. Pour que la **caméra tremble** à l'impact : fiche `Configs/Skills/Druid_CallLightning` → *Owner Camera Trauma*
   ≈ 0,3. Seul le lanceur le ressent.
5. Pour le **son** : fiche de la compétence → *Cast Sound* (au lancement). Tu peux aussi ajouter un son au prefab de
   l'effet.

> Le même prefab peut servir à d'autres entrées de l'ArtLibrary, par exemple `VFX_Lightning` (Chaîne d'éclairs,
> élites). Mais ces effets-là sont des **lignes** tracées par le code entre deux points : pour eux, garder
> *Keep Placeholder* coché et n'ajouter que le flash et les étincelles.

---

## 7. Régler en direct

- Sélectionne le prefab dans la scène. Pour les ralentir (indispensable pour un effet de 0,1 s) :
  - Particle System : fenêtre **Particle Effect** (en bas à droite de la Scene), **Restart** et **Playback Speed 0,1** ;
  - VFX Graph : **Play Controls**, *Play Rate* 0,1.
- Dans la fenêtre du VFX Graph, bouton **Attach** (en haut) : relie le graph à l'objet sélectionné, pour voir les
  modifications en direct sur l'instance de la scène.
- Regarde-le **dans une carte sombre, avec le post-process** : en Play, ou avec la Scene View en mode « Post
  Processing » coché. Sans Bloom, un éclair HDR paraît terne.
- Compare de loin (30 m) et de près (3 m) : de loin, c'est le halo (5.3) et la lumière (5.5 ou 4.8) qui comptent ; de près,
  ce sont les branches et les étincelles.

---

## 8. Checklist

**Structure**
- [ ] Pivot au point d'impact, +Y vers le ciel, construit pour un **rayon de 1 m**
- [ ] *Scaling Mode = Hierarchy* sur **tous** les Particle Systems
- [ ] VFX Graph : **tous** les systèmes en espace **Local**
- [ ] VFX Graph : *Bounds Mode* = **Manual** et *Bounds* qui englobent toute la colonne (sinon l'effet disparaît en
      bord d'écran)
- [ ] VFX Graph : *Capacity* au plus juste, paramètres exposés (couleur, hauteur, zigzag)
- [ ] VFX Graph : sorties **Shader Graph** avec `SG_VFX_Additive` / `SG_VFX_Alpha` (*Support VFX Graph* coché),
      jamais **HDRP**
- [ ] *Looping* décoché partout, rien ne dure plus de ~1,5 s (fumée et brûlure comprises)
- [ ] Matériaux **dupliqués** dans `_Project/Art/VFX/Materials`, pack Hovl intact

**Lisibilité**
- [ ] Arc principal visible en **< 0,1 s**, avec **2-3 scintillements** (bursts décalés)
- [ ] Arc **dentelé**, pas une ligne droite : strip avec décalage aléatoire × sin(π t) (VFX Graph), ou Noise +
      petite *Minimum Vertex Distance* (Particle System)
- [ ] **Branches** plus fines et plus courtes que l'arc
- [ ] **Flash** d'impact blanc, plus gros que la zone pendant 0,1 s
- [ ] **Lumière** (module Lights) qui éclaire le décor, intensité décroissante
- [ ] Couleurs **HDR** (intensité +1 à +3) : l'arc fait du Bloom
- [ ] Palette cohérente : cœur **blanc**, bords **bleu/violet pâle**
- [ ] L'**onde au sol** atteint le bord de la zone de dégâts (diamètre 2 au rayon 1)

**Finition**
- [ ] **Brûlure** au sol qui refroidit (bleu → gris → transparent)
- [ ] **Fumée** en blend Alpha (pas additive), discrète
- [ ] **Étincelles** en Stretched Billboard avec gravité
- [ ] Rotation aléatoire de la brûlure (deux impacts ne sont jamais identiques)

**Jeu**
- [ ] Branché dans l'ArtLibrary (`VFX_LightningStrike`), *Keep Placeholder* décoché
- [ ] VFX Graph : champ **Lifetime** de l'entrée renseigné (durée totale de l'effet)
- [ ] La **lumière** est un Particle System (ou une Light) : le VFX Graph n'en crée pas en URP
- [ ] Testé en Play sur une carte sombre **et** une carte claire
- [ ] Testé à 30 m et à 3 m
- [ ] Pas plus de **~150 particules** à l'instant le plus chargé (Particle Effect → *Particles*, ou le compteur du
      VFX Graph) : 4 joueurs peuvent en lancer en même temps
- [ ] Ombres désactivées sur la lumière
- [ ] Tremblement de caméra (*Owner Camera Trauma*) et son réglés sur la compétence
