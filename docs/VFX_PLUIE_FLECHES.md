# Tuto : une pluie de flèches (VFX Graph)

Objectif : l'effet de **Pluie de flèches** (Archer, spéciale). Gameplay : zone de **5 m de rayon** au point visé, premier
impact **0,4 s** après le tir, puis **8 impacts toutes les 0,25 s** (fin vers 2,15 s).

**Prérequis** (voir `docs/VFX_ECLAIR.md` § 4.0 et `docs/VFX_SOUFFLE.md` § 3.0) :
- sorties **Shader Graph** uniquement (jamais *HDRP*) : `SG_VFX_Additive` (traits lumineux, cercle) et `SG_VFX_Alpha`
  (poussière, flèches plantées) ;
- sur **chaque sortie**, l'entrée `Tint` reliée à *Get Attribute: color* + *alpha* (opérateur *Vector4* ou *Color*) ;
- systèmes en espace **Local** (une vitesse calculée par des opérateurs est sinon lue en coordonnées monde).

---

## 1. Ce qui rend une pluie de flèches lisible

| Temps | Élément | Pourquoi |
| --- | --- | --- |
| 0 → 0,4 s | **Cercle au sol** qui se remplit | Le joueur et ses alliés voient la zone avant l'impact |
| 0,2 → 2,2 s | **Traits** qui tombent du ciel, légèrement inclinés | La pluie, lisible même de loin (des traits, pas des flèches détaillées : elles vont trop vite) |
| Chaque impact | **Petit nuage de poussière** + éclat | Le poids de chaque flèche |
| Impact → +1,5 s | **Flèches plantées** dans le sol, qui s'estompent | La trace : on voit où ça a frappé |

Règle : beaucoup de traits fins et rapides plutôt que peu de grosses flèches.

---

## 2. Préparer le prefab

1. Hierarchy → *Create Empty* → `FX_ArrowRain`. **Pivot = centre de la zone au sol**, +Y vers le ciel.
2. Construis-le **à l'échelle réelle** : rayon **5 m** (le jeu ne le met pas à l'échelle).
3. Glisse le graph `VFXG_ArrowRain` dans le prefab ; *Initial Event Name* = `OnPlay`.
4. Durée totale : **2,8 s** (dernières flèches plantées comprises). Elle est indiquée au jeu côté compétence (§ 5).

Textures (pack Hovl, `Assets/Hovl Studio/Magic effects pack/Textures/`) :

| Texture | Usage |
| --- | --- |
| `Trail67.png` | Traits de chute |
| `Arrow1.png` | Flèches plantées (la flèche pointe vers la gauche de l'image) |
| `Circle2.png` ou `MagicCircle.png` | Cercle au sol |
| `Smoke26.png` | Poussière |
| `Point1.png` | Éclats |

---

## 3. Le graph

### 3.1 Créer le graph et le Blackboard

`Assets/_Project/Art/VFX/` → *Create → Visual Effects → Visual Effect Graph* → `VFXG_ArrowRain`, supprime le système
par défaut. *Blackboard* (**+**), *Exposed* coché, **valeur par défaut renseignée** :

| Property | Type | Valeur | Rôle |
| --- | --- | --- | --- |
| `Radius` | *Float* | 5 | Rayon de la zone (m) |
| `Height` | *Float* | 14 | Hauteur d'apparition des flèches |
| `FallSpeed` | *Float* | 70 | Vitesse de chute (m/s) : 14 m en 0,2 s |
| `ArrowColor` | *Color* HDR | blanc chaud (1 ; 0,95 ; 0,85), *Intensity* +1,5 | Traits |
| `ZoneColor` | *Color* HDR | ambre (1 ; 0,75 ; 0,3), *Intensity* +1 | Cercle au sol |

### 3.2 Cercle au sol (annonce de la zone)

*Spawn* → *Initialize Particle* → *Update Particle* → *Output Particle Shader Graph Quad* (`SG_VFX_Additive`,
`MainTex` = `Circle2.png`).

- **Spawn** : *Single Burst*, *Count* 1, *Delay* 0.
- **Initialize** : *Set Lifetime* **2,4** ; *Set Position* (0 ; 0,05 ; 0) ; *Set Size* = `Radius` × 2 ;
  *Set Color* = `ZoneColor`.
- **Output** :
  - *Orient* : **Fixed Axis**, *Axis* (0, 1, 0) → à plat au sol ;
  - *Set Alpha over Life* : 0 (0 %) → 0,9 (15 %) → 0,5 (30 %) → 0,5 (85 %) → 0 (100 %) : il apparaît vite, reste
    discret pendant la pluie, s'efface à la fin ;
  - *Set Size over Life* : 0,6 (0 %) → 1 (15 %) → 1 (100 %) : il « s'ouvre » au tir.

### 3.3 Traits de chute (le cœur de l'effet)

*Spawn* → *Initialize Particle* → *Update Particle* → *Output Particle Shader Graph Quad* (`SG_VFX_Additive`,
`MainTex` = `Trail67.png`).

**Spawn**
- *Constant Spawn Rate* : *Rate* **60**.
- Réglages du contexte *Spawn* (Inspector du contexte) :
  - *Loop Duration* : **Constant**, 2 ;
  - *Loop Count* : **Constant**, 1 ;
  - *Delay Before Loop* : **Constant**, 0,2 (les premières flèches touchent le sol vers 0,4 s, avec les dégâts).

**Initialize Particle** (*Capacity* 160, *Bounds Mode* Manual : *Center* (0 ; 7 ; 0), *Size* (14 ; 16 ; 14))

| Bloc | Valeur |
| --- | --- |
| *Set Lifetime* | 0,5 (la collision la tue avant) |
| *Set Position (Shape: Circle)* | *Radius* = `Radius`, *Arc* 360, mode **Volume** (disque plein), puis *Set Position* Y = `Height` (composition **Add** ou bloc *Add Position* (0, `Height`, 0)) |
| *Set Velocity* | (X, −`FallSpeed`, Z) avec X et Z = *Random Number* (*Min* −6, *Max* 6, *Constant* décoché) : légère inclinaison, chaque flèche différente |
| *Set Size* | 0,08 (largeur du trait) |
| *Set Scale* | Y = 3 (trait allongé) |
| *Set Color* | `ArrowColor` |

> Le point d'apparition est décalé en sens inverse de l'inclinaison pour que la flèche retombe dans le disque : avec
> ±6 m/s sur 0,2 s, l'écart n'est que de ±1,2 m, on peut l'ignorer.

**Update Particle**
- *Collision Shape* : *Shape* **Plane**, position (0, 0, 0), normale (0, 1, 0), *Lifetime Loss* **1** (la flèche meurt au
  contact du sol).
- *Trigger Event On Die* : *Count* 1 → relier sa sortie (le petit port vert *Evt*) aux contextes **GPU Event** des §§ 3.4
  et 3.5.

**Output**
- *Orient* : **Along Velocity** (le trait suit la chute) ;
- *Set Alpha over Life* : 0,9 → 0,9 ; pas de fondu (elles vivent 0,2 s).

### 3.4 Impacts : poussière + éclats (GPU Event)

1. Espace → *Context* → **GPU Event**. Relie le port *Evt* du *Trigger Event On Die* (§ 3.3) à ce contexte.
2. Sous lui : *Initialize Particle* → *Update Particle* → *Output Particle Shader Graph Quad*.

**Poussière** (sortie `SG_VFX_Alpha`, `MainTex` = `Smoke26.png`)
- *Inherit Source Position* (le point d'impact) ;
- *Set Lifetime* *Random* 0,4 → 0,6 ; *Set Size* *Random* 0,4 → 0,7 ; *Set Angle* Z *Random* 0 → 360 ;
- *Set Velocity* *Random* : (−0,5 ; 0,3 ; −0,5) → (0,5 ; 1 ; 0,5) ;
- *Set Color* gris-beige (0,45 ; 0,4 ; 0,35) ; *Set Alpha over Life* 0,6 → 0 ; *Set Size over Life* 1 → 2.

**Éclats** (deuxième *GPU Event* relié au même port, ou un *Trigger Event On Die* de *Count* 3) : sortie
`SG_VFX_Additive`, `MainTex` = `Point1.png`, *Inherit Source Position*, *Set Lifetime* 0,15 → 0,3,
*Set Velocity* *Random* (−3 ; 2 ; −3) → (3 ; 5 ; 3), *Gravity* (0 ; −15 ; 0), *Orient* **Along Velocity**,
*Set Size* 0,05, *Set Scale* Y 3, *Set Color* `ArrowColor`.

### 3.5 Flèches plantées (GPU Event)

Un troisième *GPU Event* relié au même port (*Count* 1).

- *Initialize* : *Inherit Source Position* ; *Inherit Source Velocity* (pour garder l'inclinaison de la chute) ;
  *Set Lifetime* **1,5** ; *Set Size* 0,6 ; *Set Scale* X = 0,25 (flèche fine).
- *Update* : *Linear Drag*, *Drag Coefficient* **1000** : la flèche s'arrête en une image mais garde la direction de
  sa chute, utilisée par l'*Orient*. (Ne remets pas la vitesse à 0 : l'orientation serait perdue.)
- Enfonce-la un peu : *Add Position* (0 ; −0,2 ; 0) dans l'*Initialize*.
- Sortie `SG_VFX_Alpha`, `MainTex` = `Arrow1.png`, *Orient* **Along Velocity**, *Set Angle* Z = **90** (la flèche de l'image pointe vers la gauche :
  on la tourne pour qu'elle pointe vers le sol ; essaie −90 si elle est à l'envers) ;
  *Set Alpha over Life* : 1 (0 %) → 1 (70 %) → 0 (100 %) ; *Set Color* gris-brun.

### 3.6 Lumière (facultatif)

Un *Particle System* enfant (renderer désactivé, module *Lights*, voir `VFX_ECLAIR.md` § 4.8) : quelques lumières
ambrées brèves sur la zone pendant 2 s, *Range* 6, *Intensity* 2. Coûteux si plusieurs archers : à garder pour la fin.

### 3.7 Astuces

- *Play Controls* → **Play Rate 0,1** pour vérifier que les premiers impacts arrivent vers **0,4 s** (comme les
  dégâts). Ajuste *Delay Before Loop* sinon.
- Trop de flèches pour les FPS ? Baisse *Rate* (40) avant de baisser la durée.
- Les traits sont invisibles de face ? Vérifie *Orient* **Along Velocity** et *Set Scale* Y.
- Rien n'apparaît : valeurs par défaut du *Blackboard* (pas 0), lien `Tint`, *Bounds*, sorties non HDRP.

---

## 4. Checklist

- [ ] Premier impact vers 0,4 s, dernières flèches vers 2,2 s, tout a disparu à 2,8 s.
- [ ] Le cercle couvre exactement la zone de dégâts (rayon 5 m).
- [ ] `Tint` relié sur **toutes** les sorties, systèmes en **Local**, aucune sortie HDRP.
- [ ] Poussière, éclats et flèches plantées apparaissent bien au point d'impact (*Inherit Source Position*).

## 5. Brancher dans le jeu

Le builder génère un prefab **`Prefabs/Vfx/VFX_ArrowRain`** (placeholder : un disque) et son entrée d'ArtLibrary.

1. *Build Prototype Scene*.
2. ArtLibrary (`Resources/ArtLibrary`) → entrée **`VFX_ArrowRain`** → *Model* = `FX_ArrowRain` (ton prefab, jamais celui
   de `Prefabs/Vfx`).
3. Compétence `Configs/Skills/Archer_ArrowRain` (déjà existante : ces trois champs ne sont pas écrasés par le builder,
   à régler une fois à la main) :
   - *Cast Vfx* = `Prefabs/Vfx/VFX_ArrowRain` ;
   - *Cast Vfx Anchor* = **Target** (au point visé, centre de la zone) ;
   - *Cast Vfx Duration* = **2,8**.
4. *Tick Vfx* : garde `VFX_Area` pour un anneau à chaque impact, ou vide-le si la pluie suffit.

L'effet est joué une fois au tir, localement chez chaque joueur, au point visé.
