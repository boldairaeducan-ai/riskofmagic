# Tuto : un souffle de feu continu (VFX Graph)

Objectif : l'effet de **Souffle enflammé** (druide) : un **jet de flammes continu** devant le joueur pendant toute la
canalisation (2,5 s, cône de 10 m, demi-angle 30°). La même méthode sert à tout lance-flammes (élite, ennemi, objet).

**Outils** : les mêmes que pour l'éclair (`docs/VFX_ECLAIR.md`) :
- **VFX Graph** 17.5 (en URP, sorties *Shader Graph* uniquement, jamais *HDRP*) ;
- les Shader Graphs **`SG_VFX_Additive`** et **`SG_VFX_Alpha`** de la section 4.0 du tuto de l'éclair ;
- les textures du pack Hovl (`Assets/Hovl Studio/Magic effects pack/Textures/`) ;
- un **Particle System** enfant pour la lumière (le VFX Graph n'en crée pas en URP).

> ⚠️ **Côté jeu** : aujourd'hui le souffle n'affiche **pas** d'effet continu. Il fait apparaître `tickVfx`
> (`VFX_FireBurst`) en 3 bouffées le long du cône à chaque tick (toutes les 0,25 s). Pour un jet continu, il faut un
> petit ajout de code (section 7), **pas encore fait**. Construis l'effet d'abord, on branche ensuite.

---

## 1. Ce qui rend un souffle crédible

Un jet de flammes, c'est **beaucoup de particules douces qui naissent petites et brûlantes, puis grossissent,
ralentissent, montent et refroidissent**. L'œil lit la couleur comme une température.

| Zone du jet | Ce qu'on voit | Couche |
|---|---|---|
| Bouche (0-1 m) | Cœur **blanc-jaune** très lumineux, étroit, rapide | Cœur |
| Milieu (1-6 m) | Flammes **orange** qui tourbillonnent et s'élargissent | Flammes |
| Bout (6-10 m) | Flammes **rouge sombre** qui s'éteignent, **fumée** qui monte | Flammes + fumée |
| Partout | **Braises** projetées, plus loin que les flammes | Braises |
| Décor | **Lumière orange vacillante** | Lumière |

Règles :
- **la couleur change avec l'âge** de la particule (blanc → jaune → orange → rouge → noir), jamais une couleur fixe ;
- **ça monte** : les gaz chauds s'élèvent, le jet se courbe légèrement vers le haut au bout ;
- **ça ralentit** : très rapide à la bouche, presque arrêté au bout ;
- **ça tourbillonne** : rotation aléatoire + turbulence, sinon on voit des billes qui glissent.

---

## 2. Préparer le prefab

1. Hierarchy → *Create Empty* → `FX_FlameBreath`.
   - **Pivot = la bouche** (point de départ du jet), **axe +Z = direction du jet**.
   - Construis-le **à l'échelle réelle** : le jet atteint ~10 m. Une propriété `Range` permet de l'ajuster.
2. Glisse le graph (section 3) dans le prefab : un enfant avec le composant *Visual Effect*.
   *Initial Event Name* = `OnPlay`.
3. Espace des systèmes : **Local** (bouton *Local / World* en haut à droite de chaque contexte).
   - En **Local**, tout le jet tourne d'un bloc avec le lanceur : il part toujours dans la bonne direction, en vue à
     la première personne comme vu par un allié.
   - **World** courberait le jet quand le joueur tourne, mais une vitesse calculée par des opérateurs (D × S) y est
     lue **en coordonnées monde** : le bouton L de l'entrée *Velocity* est ignoré dès qu'elle est branchée, et le jet
     part toujours vers le +Z du monde. Pour l'utiliser quand même, insère un opérateur *Vector* (*Inline*, pas
     *Vector3*) réglé sur **L** juste avant chaque *Set Velocity*.
   - Mets *Bounds Mode* = **Automatic** sur chaque *Initialize*.
4. Durée : l'effet **boucle** tant qu'on ne l'arrête pas. Le code l'arrêtera avec l'événement `OnStop` à la fin de
   la canalisation : l'émission cesse, les particules déjà émises finissent leur vie (~1,5 s), puis l'objet est recyclé.

Textures utiles :

| Texture | Usage |
|---|---|
| `Smoke26.png` | **Flammes** (bouffée douce, teintée en additif) et **fumée** (en alpha) |
| `SmokeFree1.png` | Variante **stylisée / cartoon** des flammes (contours marqués) |
| `GlowFree1.png` | Cœur lumineux à la bouche |
| `Point1.png` | Braises |

> Pas de flipbook de feu dans le pack : ce n'est pas nécessaire. 150-250 bouffées douces qui tournent, grossissent et
> changent de couleur donnent un feu convaincant. Si tu ajoutes un jour une texture de feu animée (planche 4×4 ou
> 8×8), utilise le bloc *Flipbook Player* et la *UV Mode* **Flipbook** de la sortie.

---

## 3. Le graph

> Les noms entre *italiques* sont ceux de l'interface Unity (en anglais). **Espace** ou clic droit → *Create Node*
> ouvre la recherche.

### 3.0 Shader Graphs et entrée `Tint`

Réutilise les Shader Graphs du tuto de l'éclair (§ 4.0) :
- `SG_VFX_Additive` pour les flammes, le cœur et les braises ;
- `SG_VFX_Alpha` pour la fumée.

**D'où vient `Tint`** : ces Shader Graphs ont deux propriétés dans leur *Blackboard*, `MainTex` (texture) et `Tint`
(couleur HDR). Quand tu choisis le Shader Graph dans le champ **Shader Graph** d'une sortie *Output Particle Shader
Graph Quad*, ces deux propriétés deviennent des **entrées du contexte de sortie**. Elles apparaissent en haut du
contexte, sous les réglages, avec un petit rond de connexion à gauche.

- `MainTex` : glisse la texture (ex. `Smoke26.png`) dans le champ.
- `Tint` : **ne mets pas une couleur fixe, relie-la à la couleur de la particule** :
  1. Espace → *Get Attribute: color* (sortie Vector3) ;
  2. Espace → *Get Attribute: alpha* (sortie Float) ;
  3. Espace → opérateur *Vector4* (catégorie *Inline*), déplie son entrée (▸) : branche color sur **x, y, z**
     (déplie aussi la sortie de *Get Attribute: color* pour relier x → x, y → y, z → z) et alpha sur **w** ;
  4. relie la sortie du *Vector4* au rond de l'entrée `Tint` de la sortie.

  Le type de `Tint` est *Color* : si le fil refuse de se brancher, passe par un opérateur *Color* (*Inline*) à la
  place du *Vector4*, mêmes branchements (r, g, b, a).

Fais-le **sur chaque sortie** : un même groupe *Get Attribute* + *Vector4* peut être relié à toutes les sorties
(plusieurs fils depuis la même sortie).

**Pourquoi c'est indispensable** : un Shader Graph ne lit pas tout seul la couleur de la particule. Sans ce lien,
les blocs *Set Color*, *Set Color over Life* et *Set Alpha over Life* de ce tuto n'ont **aucun effet visible** :
les flammes restent blanches et ne s'estompent pas.

### 3.1 Créer le graph

1. `Assets/_Project/Art/VFX/` → *Create → Visual Effects → Visual Effect Graph* → `VFXG_FlameBreath`.
   Supprime le système par défaut.
2. *Blackboard* (**+**), *Exposed* coché, **valeur par défaut renseignée** (déplier ▸ → *Default Value* ; une
   propriété vaut 0 à sa création) :

| Property | Type | Valeur | Rôle |
|---|---|---|---|
| `Range` | *Float* | 10 | Portée visuelle du jet (m) |
| `Spread` | *Float* | 0,35 | Ouverture du cône (0,35 ≈ 20° de demi-angle) |
| `Intensity` | *Float* | 1 | Multiplie le nombre de particules |
| `CoreColor` | *Color* HDR | blanc-jaune (1 ; 0,9 ; 0,6), *Intensity* +3 | Cœur |
| `EmberColor` | *Color* HDR | orange (1 ; 0,45 ; 0,1), *Intensity* +3 | Braises |

3. Opérateurs **partagés** (posés une fois, branchés dans plusieurs systèmes) :

| # | Opérateur | Calcul | Rôle |
|---|---|---|---|
| S | *Multiply* | `Range` × 2,3 | **Vitesse de départ** (m/s). 2,3 compense le freinage (*Linear Drag* 1,5) sur ~0,7 s de vie : le jet s'arrête vers `Range` |
| D | voir ci-dessous | — | **Direction** aléatoire dans le cône |

**D, direction dans le cône** :
1. deux *Random Number* (*Min* −1, *Max* 1, *Constant* **décoché**) → X et Y ;
2. *Vector3*(X × `Spread`, Y × `Spread`, 1) ;
3. *Normalize*.

D × S donne une vitesse vers l'avant (+Z), dispersée dans le cône. Chaque système ci-dessous la réutilise avec un
facteur (ex. « vitesse = D × S × 0,8 »).

> **Espace des entrées** : sur *Set Velocity* et *Set Position*, le petit bouton **L / W** à gauche de l'entrée doit
> être sur **L** (Local). Attention : une entrée **branchée** sur des opérateurs sans espace (*Vector3*, *Multiply*…)
> prend l'espace du système, quel que soit ce bouton. D'où les systèmes en Local (§ 2).

### 3.2 Flammes (le corps du jet)

*Spawn* → *Initialize Particle* → *Update Particle* → *Output Particle Shader Graph Quad*
(ou *Create Node → System → Simple Particle System*, puis vide ses blocs).

**Spawn**
- *Constant Spawn Rate* : *Rate* = 220 × `Intensity`.
- Ne touche pas aux entrées *Start* / *Stop* du contexte : par défaut `OnPlay` démarre, `OnStop` arrête.

**Initialize Particle** (*Capacity* 200, *Bounds Mode* Automatic, espace Local)

| Bloc | Valeur |
|---|---|
| *Set Lifetime* (*Random*, *Uniform*) | 0,55 → 0,75 |
| *Set Position (Shape: Sphere)* | *Radius* 0,1, *Position Mode* Volume, centre (0, 0, 0) **en L** |
| *Set Velocity* | D × S (entrée **en L**) |
| *Set Size* (*Random*) | 0,25 → 0,4 |
| *Set Angle* | Z *Random* 0 → 360 |
| *Set Angular Velocity* | Z *Random* −180 → 180 |

**Update Particle**

| Bloc | Valeur | Pourquoi |
|---|---|---|
| *Linear Drag* | *Drag Coefficient* 1,5, *Use Particle Size* décoché | Rapide à la bouche, lent au bout |
| *Turbulence* | *Intensity* 3, *Drag* 1, *Frequency* 1,5, *Octaves* 2 | Tourbillons |
| *Gravity* | (0, **+3**, 0) | La flamme monte (gravité inversée) |

**Output Particle Shader Graph Quad** (`SG_VFX_Additive`, `MainTex` = `Smoke26.png`)

| Bloc | Valeur |
|---|---|
| *Orient* | *Face Camera Plane* |
| *Set Size over Life* | courbe 0,15 (0 %) → 1 (40 %) → 2,4 (100 %) |
| *Set Color over Life* | dégradé (§ ci-dessous) |
| *Set Alpha over Life* | 0 (0 %) → 1 (5 %) → 1 (55 %) → 0 (100 %) |

**Dégradé de couleur** (*Gradient*, mode **HDR** : clique la clé de couleur, monte *Intensity*) :

| Position | Couleur | Intensité |
|---|---|---|
| 0 % | blanc-jaune (1 ; 0,9 ; 0,6) | +3 |
| 15 % | jaune-orange (1 ; 0,65 ; 0,2) | +2 |
| 45 % | orange (1 ; 0,35 ; 0,05) | +1,5 |
| 75 % | rouge sombre (0,6 ; 0,08 ; 0,02) | +0,5 |
| 100 % | noir | 0 |

En additif, le noir est invisible : la flamme s'éteint d'elle-même au bout, sans trou net.

### 3.3 Cœur (bouche du jet)

Copie le système des flammes (Ctrl+D), puis :
- *Rate* 120 × `Intensity` ; *Capacity* 40 ;
- *Set Lifetime* *Random* 0,12 → 0,2 ;
- *Set Velocity* = D × S × 0,8, avec un `Spread` réduit : branche `Spread` × 0,4 dans la copie de D ;
- retire *Turbulence* et *Gravity* ;
- *Set Size over Life* 0,15 → 0,5 ; pas de *Set Angular Velocity* ;
- sortie : `MainTex` = `GlowFree1.png` ; remplace *Set Color over Life* par *Set Color* = `CoreColor` ;
  *Set Alpha over Life* 1 → 0.

C'est la « buse » éblouissante qui fait briller le Bloom.

### 3.4 Braises

Nouveau système (*Capacity* 60) :
- *Constant Spawn Rate* 35 × `Intensity` ;
- *Set Lifetime* *Random* 0,6 → 1,2 ;
- *Set Velocity* = D × S × 0,6, avec `Spread` × 1,3 (elles s'écartent plus que les flammes) ;
- *Set Size* *Random* 0,03 → 0,06 ; *Set Color* = `EmberColor` ;
- Update : *Linear Drag* 1 ; *Turbulence* *Intensity* 5 (elles virevoltent) ; *Gravity* (0, 1,5, 0) ;
- sortie (`SG_VFX_Additive`, `MainTex` = `Point1.png`) :
  - *Orient* **Along Velocity** ; *Set Scale* Y = 3 (traits allongés) ;
  - *Set Color over Life* : `EmberColor` → rouge sombre ; *Set Alpha over Life* 1 → 0.

### 3.5 Fumée

Nouveau système (*Capacity* 50) :
- *Constant Spawn Rate* 30 × `Intensity` ;
- *Set Lifetime* *Random* 1,2 → 1,6 ;
- *Set Velocity* = D × S × 0,8 ;
- *Set Size* *Random* 0,6 → 1 ; *Set Angle* *Random* ; *Set Angular Velocity* *Random* ±40 ;
- Update : *Linear Drag* 1,5 ; *Turbulence* *Intensity* 2 ; *Gravity* (0, 1,2, 0) ;
- sortie **`SG_VFX_Alpha`** (pas l'additif), `MainTex` = `Smoke26.png` :
  - *Set Color* gris très sombre (0,12 ; 0,1 ; 0,09) ;
  - *Set Size over Life* 1 → 3,5 ;
  - *Set Alpha over Life* : 0 (0 %) → 0 (30 %) → 0,35 (55 %) → 0 (100 %). La fumée n'apparaît qu'**au bout** du jet,
    là où les flammes s'éteignent.
- Ordre d'affichage : dans l'Inspector de la sortie, *Sorting Priority* = −1 (fumée dessinée **derrière** les
  flammes). Les flammes, le cœur et les braises gardent 0.

### 3.6 Lumière (Particle System enfant)

Enfant `Light` du prefab, position (0, 0, 2,5) (milieu du jet) :
1. *Particle System* :
   - *Duration* 1, **Looping coché**, *Start Lifetime* *Random Between Two Constants* 0,1 → 0,25, *Start Speed* 0 ;
   - *Emission* : *Rate over Time* 12 ; *Shape* décoché ;
   - module *Renderer* **désactivé** (on ne veut que la lumière).
2. Module **Lights** :
   - *Light* = un prefab de **Point Light** orange (1 ; 0,55 ; 0,2), *Range* 8, **Shadow Type None** ;
   - *Ratio* 1, *Use Particle Color* décoché, *Intensity Multiplier* 3, *Maximum Lights* 3 ;
   - *Intensity over Lifetime* : courbe 0 → 1 → 0.

Plusieurs lumières courtes qui se chevauchent donnent un **vacillement** naturel, sans script.

### 3.7 Optionnel : distorsion de chaleur

L'air qui ondule au-dessus du jet est très efficace, mais en URP il faut :
- activer **Opaque Texture** dans l'asset URP (*Rendering → Opaque Texture*) ;
- un Shader Graph qui décale *Scene Color* avec un bruit.

C'est coûteux et facultatif : à garder pour la fin.

### 3.8 Astuces

- *Play Controls* (overlay de la *Scene View*) → **Play Rate 0,1** pour régler le dégradé au ralenti ; **Stop** pour
  vérifier que le jet s'éteint proprement (fin de canalisation).
- Pour tester l'orientation, tourne l'objet dans la scène pendant que l'effet joue : le jet doit suivre son +Z.
- Total : ~450 particules par joueur, 4 joueurs = ~1 800. C'est négligeable pour le VFX Graph.
- Tout est **trop blanc** ? Le lien `Tint` (§ 3.0) manque, ou les intensités HDR sont trop hautes : baisse d'abord
  celle du cœur.
- **Vue à la première personne** : le jet part juste sous la caméra. De grosses bouffées additives devant l'objectif
  remplissent l'écran. Si c'est le cas, baisse la taille de départ des flammes (*Set Size over Life* à 0 %) plutôt
  que leur nombre.

---

## 4. Variante 100 % Particle System (rapide)

Une couche = un Particle System enfant, **Looping coché**, *Simulation Space* **World**, *Scaling Mode*
**Hierarchy**, Rotation X = 0 (Unity tourne par défaut les particules vers +Z de −90°, on garde +Z = direction du jet).

| Couche | Réglages clés |
|---|---|
| **Flammes** | *Start Lifetime* 0,55-0,75 · *Start Speed* 23 · *Start Size* 0,25-0,4 · *Start Rotation* 0-360 · *Rate* 220 · *Shape* **Cone** (*Angle* 20, *Radius* 0,1) · *Limit Velocity over Lifetime* (*Drag* 1,5) · *Force over Lifetime* Y = +3 · *Noise* (*Strength* 1,5, *Frequency* 1,5) · *Size over Lifetime* 0,15 → 2,4 · *Color over Lifetime* = le dégradé du § 3.2 · *Rotation over Lifetime* ±180 · matériau additif `Smoke26` |
| **Cœur** | *Lifetime* 0,12-0,2 · *Speed* 18 · *Cone Angle* 8 · *Size* 0,15 → 0,5 · *Rate* 120 · couleur `CoreColor` · matériau `GlowFree1` |
| **Braises** | *Lifetime* 0,6-1,2 · *Speed* 14 · *Cone Angle* 26 · *Size* 0,03-0,06 · *Rate* 35 · *Noise* fort (3) · *Renderer* **Stretched Billboard** (*Speed Scale* 0,05) · matériau `Point1` |
| **Fumée** | *Lifetime* 1,2-1,6 · *Speed* 18 · *Rate* 30 · *Size* 1 → 3,5 · alpha 0 → 0,35 (55 %) → 0 · matériau **alpha** `Smoke26` gris sombre · *Order in Layer* −1 |
| **Lumière** | identique au § 3.6 |

Arrêt : `ParticleSystem.Stop()` sur la racine (*Stop Emitting*) laisse les particules finir, comme `OnStop`.

---

## 5. Brancher dans le jeu

1. ArtLibrary (`Resources/ArtLibrary`) → entrée **`VFX_FlameBreath`** → *Model* = ton prefab `FX_FlameBreath`.
   Le builder la remplit avec `VX_FireBreath` s'il trouve ce prefab dans `Assets/_Project/Art`.
   - Ne mets **jamais** un prefab généré (`VFX_*`) comme *Model* : ArtSlot le refuse (récursion).
2. *Rotation* / *Offset* de l'entrée : à ajuster si le jet ne part pas pile de la bouche ou ne pointe pas vers +Z.

## 6. Checklist

- [ ] `Tint` relié à *color* + *alpha* sur **toutes** les sorties.
- [ ] Systèmes en **Local**, entrées *Position* / *Velocity* en **L**, *Bounds Mode* **Automatic**.
- [ ] Les propriétés du *Blackboard* ont leur valeur par défaut (pas 0).
- [ ] *Stop* dans les *Play Controls* : le jet s'éteint en ~1,5 s sans coupure.
- [ ] La fumée est derrière les flammes (*Sorting Priority* −1).
- [ ] Aucune sortie **HDRP**.

## 7. Côté code (fait)

- `ConeChannelSkill.channelVfx` = prefab généré **`VFX_FlameBreath`** (placeholder : un pavé orange de 9 m). Son entrée
  de l'ArtLibrary reçoit `VX_FireBreath` comme *Model* au premier build (*Lifetime* 4,5 s) ; tu peux la changer.
- Au début de la canalisation, **chaque client** fait apparaître le jet localement, sans réseau (composant
  `ChannelEffect`, ajouté à l'exécution) :
  - pour le joueur lui-même : il suit la caméra, visée verticale comprise, décalé de *First Person Offset*
    (0,15 ; −0,35 ; 0,6) ;
  - pour les autres joueurs : il suit la tête et l'orientation du corps, avec l'inclinaison de la visée au moment
    du lancer (*Third Person Offset*).
- Fin des 2,5 s, ou druide à terre : `Stop()` sur le *Visual Effect* (événement `OnStop`) et les Particle Systems
  (arrêt de l'émission). Les particules finissent leur vie et l'effet retourne au pool 4,5 s après son apparition.
- `tickVfx` est vidé : le jet remplace les bouffées par tick. Remets un effet si tu veux un petit impact en plus.
- Le prefab de l'effet doit avoir sa racine et son *Visual Effect* à la position (0, 0, 0), jet vers +Z.
