# Tuto : un éclair convaincant avec le Particle System

Objectif : l'effet de **Appel de la foudre** (druide). Il s'applique à tout éclair : chaîne d'éclairs, pierre de
foudre, élite foudroyant.

**Outils** : uniquement ce qui est déjà dans le projet.
- **Particle System** intégré (pas besoin du VFX Graph, qui n'est pas installé) ;
- les **textures et matériaux du pack Hovl** (`Assets/Hovl Studio/Magic effects pack/`) ;
- le **Bloom** du post-process URP, déjà actif.

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

## 2. Préparer le prefab

1. Hierarchy → *Create Empty* → `FX_LightningStrike` à la position (0, 0, 0).
   - Le **pivot = le point d'impact au sol**, l'axe **+Y vers le ciel**.
2. Le jeu met l'effet à l'échelle du **rayon** de la zone : *Appel de la foudre* a un rayon de 5 m, donc l'effet est
   affiché ×5.
   - **Construis l'effet pour un rayon de 1 m** : onde au sol de 2 m de diamètre, colonne d'environ 4 m de haut
     (20 m en jeu).
   - Sur **chaque** Particle System : *Main → Scaling Mode = Hierarchy*. Sinon l'échelle du jeu est ignorée.
3. Sur chaque Particle System : *Main → Looping* **décoché**, *Play On Awake* coché, *Stop Action* = None.
   - Le jeu détruit l'effet quand toutes les particules ont disparu (4 s au maximum).
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

## 3. Les couches (un Particle System enfant par couche)

Crée chaque couche avec clic droit sur `FX_LightningStrike` → *Effects → Particle System*. Mets **Rotation X = 0** :
par défaut Unity tourne les particules vers +Z (-90°), il faut tout repasser vers le haut.

### 3.1 Arc principal : la technique « particule + Trail + Noise »

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

### 3.2 Branches

Duplique la couche 3.1 (Ctrl+D) et règle-la ainsi :
- **Bursts** : 3 à 5 particules ;
- *Start Lifetime* 0,04 ;
- *Shape* : Sphere de rayon 0,5, à des hauteurs variées (Position Y = 2, avec *Randomize Position*) ;
- *Velocity over Lifetime* : X/Z aléatoires ±20, Y = −30 ;
- *Trails Width* plus fin (multiplicateur 0,4) ;
- couleur plus bleue, intensité +2.

Les branches partent de l'arc principal et s'éteignent plus vite.

### 3.3 Halo de la colonne (volume)

Un **Stretched Billboard** donne de l'épaisseur à l'arc vu de loin.
- Main : Lifetime 0,12 · Size 0,8 · Speed 0 · couleur bleu clair, intensité +1, **alpha 0,5** ;
- Emission : Burst de 1 ;
- Shape : désactivé (au centre) ;
- Renderer : Render Mode **Mesh** = Quad, ou Billboard avec *Start Size 3D* (X 0,8 · Y 4) et *Render Alignment = Local* ;
- matériau `GlowFree1` additif ;
- Color over Lifetime : 1 → 0 rapide.

### 3.4 Flash d'impact

- Main : Lifetime **0,1** · Size **3** · Speed 0 · blanc, intensité +3 ;
- Emission : Burst 1 au temps 0 ;
- Size over Lifetime : 0,5 → 1 ;
- Color over Lifetime : alpha 1 → 0 ;
- matériau `Flash` ou `FlashFree2` ;
- position Y = 0,3.

### 3.5 Lumière (le vrai « wow »)

Module **Lights** sur le Flash (3.4) :
1. Crée un prefab avec une **Point Light** :
   - couleur bleu-blanc, *Range* 8 ;
   - **Shadow Type None** (performance), *Render Mode Auto*.
2. Sur le Flash, module **Lights** : *Light* = ce prefab, *Ratio* 1, *Use Particle Color* décoché,
   **Intensity Multiplier** 8, *Range Multiplier* 1.
3. *Intensity over Lifetime* : courbe 1 → 0 sur 0,1 s.

Le décor s'illumine d'un coup : c'est ce qui vend l'effet.

### 3.6 Étincelles

- Main : Lifetime 0,2-0,5 · Speed **6-14** · Size 0,05-0,12 · Gravity Modifier **1** · blanc → bleu ;
- Emission : Burst **25-40** au temps 0 ;
- Shape : **Hemisphere** de rayon 0,2, orientée vers le haut (Rotation X = -90) ;
- Renderer : **Stretched Billboard**, Speed Scale 0,05 (traits) ;
- matériau `Point` additif, intensité +2 ;
- Collision : facultatif (World, Bounce 0,3) pour qu'elles rebondissent au sol.

### 3.7 Onde au sol

- Main : Lifetime 0,35 · Size **0,5** · Speed 0 · bleu, alpha 0,8 ;
- Emission : Burst 1 ;
- Renderer : **Render Alignment = World** et Rotation X = 90, pour qu'elle soit **à plat** au sol ;
- Size over Lifetime : 0,3 → **2** (2 m = le diamètre de la zone pour un rayon de 1) ;
- Color over Lifetime : alpha 1 → 0 ;
- matériau `Circle2` ;
- position Y = 0,05, pour éviter qu'elle clignote au contact du sol.

### 3.8 Brûlure au sol

- Main : Lifetime **1,2** · Size 1,5 · Speed 0 · bleu, intensité +1,5 ;
- Emission : Burst 1 à 0,02 s ;
- Renderer : Alignment World, à plat, comme l'onde ;
- Color over Lifetime : bleu lumineux → gris sombre → transparent ;
- Rotation : Start Rotation aléatoire 0-360 (chaque impact est différent) ;
- matériau `Crack`.

### 3.9 Fumée

- Main : Lifetime 0,8-1,2 · Speed 0,5-1,5 · Size 0,6-1,2 · gris foncé, **alpha 0,35** ;
- Emission : Burst 4-6 ;
- Shape : Sphere de rayon 0,4 ;
- Size over Lifetime : 0,6 → 1,4 ;
- Rotation over Lifetime : ±30°/s ;
- matériau `Smoke26`. Le *Blend* doit être **Alpha**, pas additif : sinon la fumée brille.

---

## 4. Brancher l'effet dans le jeu

1. Glisse `FX_LightningStrike` dans `Assets/_Project/Art/VFX/` pour créer le prefab.
2. Ouvre `Resources/ArtLibrary` → liste **Vfx** → entrée **`VFX_LightningStrike`**.
   - *Model* = ton prefab ;
   - *Keep Placeholder* **décoché** (la colonne grise disparaît) ;
   - *Scale* = 1 (ajuste si l'effet est globalement trop grand ou trop petit) ;
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

## 5. Régler en direct

- Sélectionne le prefab dans la scène : la petite fenêtre **Particle Effect** (en bas à droite de la Scene) permet de
  faire **Restart** et de ralentir avec **Playback Speed 0,1**. Indispensable pour un effet de 0,1 s.
- Regarde-le **dans une carte sombre, avec le post-process** : en Play, ou avec la Scene View en mode « Post
  Processing » coché. Sans Bloom, un éclair HDR paraît terne.
- Compare de loin (30 m) et de près (3 m) : de loin, c'est le halo (3.3) et la lumière (3.5) qui comptent ; de près,
  ce sont les branches et les étincelles.

---

## 6. Checklist

**Structure**
- [ ] Pivot au point d'impact, +Y vers le ciel, construit pour un **rayon de 1 m**
- [ ] *Scaling Mode = Hierarchy* sur **tous** les Particle Systems
- [ ] *Looping* décoché partout, rien ne dure plus de ~1,5 s (fumée et brûlure comprises)
- [ ] Matériaux **dupliqués** dans `_Project/Art/VFX/Materials`, pack Hovl intact

**Lisibilité**
- [ ] Arc principal visible en **< 0,1 s**, avec **2-3 scintillements** (bursts décalés)
- [ ] Arc **dentelé** (Noise + Minimum Vertex Distance petit), pas une ligne droite
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
- [ ] Testé en Play sur une carte sombre **et** une carte claire
- [ ] Testé à 30 m et à 3 m
- [ ] Pas plus de **~150 particules** à l'instant le plus chargé (fenêtre Particle Effect → *Particles*) : 4 joueurs
      peuvent en lancer en même temps
- [ ] Ombres désactivées sur la lumière
- [ ] Tremblement de caméra (*Owner Camera Trauma*) et son réglés sur la compétence
