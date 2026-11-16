# Intégrer un modèle animé (personnage ou ennemi)

Ce guide décrit comment brancher un modèle 3D d'un pack (Asset Store, Mixamo…) sur un personnage jouable ou un
ennemi, avec ses animations. Exemple de référence : **l'Archer** avec le pack *LOWPO Mercenary* (Standout7), recette
`Assets/_Project/Configs/Models/Model_Archer.asset`.

Tout passe par une **recette de modèle** (`CharacterModelRecipe`). Un clic sur « Construire le modèle » génère :
- le **prefab du modèle** (armes attachées aux os, mis à la bonne taille) ;
- son **Animator Controller** (paramètres du jeu, états et transitions déjà câblés) ;
- puis branche ce prefab sur le personnage ou l'ennemi visé.

On ne modifie jamais les fichiers générés (`Prefabs/Models/*.prefab` et `*.controller`) : on change la recette et on
reconstruit.

---

## 0. Vocabulaire rapide

| Terme | Sens |
|---|---|
| **Rig Humanoid** | Squelette reconnu par Unity (hanches, jambes, bras…). Permet de partager les animations entre modèles. |
| **Avatar** | La correspondance « os du modèle ↔ os humains » créée par Unity à l'import en mode Humanoid. |
| **Clip** | Une animation (Idle, Walk, Attack…). Souvent rangée *dans* un fichier FBX. |
| **Animator Controller** | Le graphe d'états qui choisit quel clip jouer selon les paramètres envoyés par le jeu. |

---

## 1. Préparer le modèle

1. Importer le pack (le dossier du pack reste tel quel, ex. `Assets/Standout7/...`).
2. Sélectionner le **FBX du personnage** → Inspector → onglet **Rig** :
   - *Animation Type* : **Humanoid** ;
   - *Avatar Definition* : **Create From This Model** ;
   - **Apply**, puis **Configure…** : tous les os obligatoires doivent être verts. Sinon, les assigner à la main
     (Hips, Spine, Head, UpperLeg/LowerLeg/Foot, UpperArm/LowerArm/Hand).
3. Vérifier le **prefab** du pack (ex. `Mercenary_Archer.prefab`) : le personnage doit regarder vers **+Z**
   (flèche bleue de l'outil de déplacement en mode *Local*). Sinon, régler plus tard le champ **Rotation** de la recette.

> Le pack LOWPO Mercenary est déjà en Humanoid (os Rigify mappés).

---

## 2. Récupérer des animations

Le pack Mercenary (et le Frog Marauder) **ne contiennent aucune animation**. Sources gratuites :

### Mixamo (recommandé, gratuit, compte Adobe)
1. Sur mixamo.com → **Upload Character** : envoyer le FBX du personnage, valider l'auto-rig.
   Ou utiliser n'importe quel personnage Mixamo : en Humanoid, les clips se partagent entre modèles.
2. Choisir une animation. Pour les déplacements, cocher **In Place**.
3. **Download** :
   - Format : **FBX for Unity (.fbx)** ;
   - Skin : **Without Skin** (seulement l'animation) ;
   - 30 fps.
4. Ranger les fichiers dans `Assets/_Project/Animations/<Personnage>/` (ex. `Archer_Idle.fbx`).
5. Pour **chaque FBX d'animation** :
   - onglet **Rig** : *Humanoid*, *Avatar Definition* = **Copy From Other Avatar** → choisir l'avatar du modèle → Apply ;
   - onglet **Animation** :
     - cocher **Loop Time** pour les boucles (Idle, Walk, Run, Fall, Downed) ;
     - pour les boucles, cocher aussi **Loop Pose** et *Root Transform Rotation / Position (Y)* → **Bake Into Pose** ;
     - Apply.

Liste conseillée (noms Mixamo indicatifs) :

| Rôle | Exemple Mixamo | Boucle |
|---|---|---|
| Idle | *Standing Idle* / *Bow Idle* | oui |
| Walk | *Walking* | oui |
| Run | *Running* / *Standing Run Forward* | oui |
| Jump / Fall / Land | *Jump*, *Falling Idle*, *Falling To Landing* | Fall seulement |
| Hit | *Hit Reaction* | non |
| Death | *Dying* | non |
| Downed / Revive | *Wounded Idle* / *Getting Up* | Downed seulement |
| Attaques | *Standing Draw Arrow* (arc), *Sword Slash*, *Magic Attack*… | non |

---

## 3. Créer la recette

1. Project → clic droit dans `Assets/_Project/Configs/Models/` → **Create → Risk of Magic → Character Model Recipe**.
   Le nom du fichier devient le nom du prefab et du controller générés (ex. `Model_Archer`).
2. Remplir :

| Champ | Rôle |
|---|---|
| **Source Model** | Prefab du pack (ex. `Mercenary_Archer.prefab`). |
| **Height** | Taille finale en mètres (joueurs ≈ 1,8 ; 0 = taille d'origine). |
| **Rotation** | Correction si le modèle ne regarde pas vers +Z (ex. `0, 180, 0`). |
| **Attachments** | Armes / accessoires : prefab + **os humanoïde** (`LeftHand`, `RightHand`, `Spine`…) + position, rotation et échelle locales. |
| **Output Folder** | Dossier des fichiers générés (par défaut `Assets/_Project/Prefabs/Models`). |
| **Target Character** / **Target Enemy** | La fiche qui recevra le modèle (`Configs/Characters/Character_X` ou `Configs/Enemies/Enemy_X`). |
| **Idle / Walk / Run** + vitesses | Locomotion, mélangée selon la vitesse réelle en m/s (`Walk Speed` / `Run Speed` = vitesse à laquelle le clip semble naturel). |
| **Jump / Fall / Land** | Sauts (joueurs). Optionnels. |
| **Hit / Death / Downed / Revive / Use Equipment** | Réactions. Optionnels. |
| **Actions** | Une ligne par attaque (voir ci-dessous). |
| **Actions Upper Body Only** | Les attaques ne jouent que sur le haut du corps : on peut tirer en courant. |

### Les actions (attaques, sorts)

Chaque ligne associe un clip à un **déclencheur** envoyé par le jeu :

- **Joueurs** : chaque compétence envoie son `animatorTrigger` (champ de la `SkillDefinition` : `Cast` ou `Attack`)
  et l'emplacement dans `SkillSlot` (0 clic gauche, 1 clic droit, 2 utilitaire, 3 spéciale).
  - Exemple Archer : `Cast` / index `0` → tir de base ; `Cast` / index `-1` → toutes les autres compétences.
- **Ennemis** : chaque attaque envoie `Attack` (champ *Animator Trigger* de la donnée d'attaque) et son rang dans
  `AttackIndex` (0 = premier composant `EnemyAttack` du prefab).
- Index `-1` = n'importe quel emplacement. Mettre les lignes précises **avant** la ligne générique.
- **Speed** accélère ou ralentit le clip (ex. 1,5 pour qu'une attaque colle à un sort rapide).

---

## 4. Construire et tester

1. Sélectionner la recette → bouton **« Construire le modèle »**.
2. Vérifier `Prefabs/Models/<Recette>.prefab` :
   - double-clic pour l'ouvrir ;
   - contrôler que l'arme est bien en main ;
   - sinon, ajuster *Local Position / Euler Angles* dans la recette et reconstruire.
   - **Astuce** pour trouver les bons angles :
     1. dans le prefab ouvert, déplacer l'arme à la main jusqu'à ce qu'elle soit bien placée ;
     2. recopier ses valeurs *Position / Rotation* dans la recette ;
     3. reconstruire.
3. Play depuis `Scenes/MainMenu`, choisir le personnage. Le modèle n'est visible **que des autres joueurs** et en
   ombre pour soi (vue à la première personne) : tester avec deux instances (*Multiplayer Play Mode*), ou en
   regardant son ombre. Pour un ennemi, il suffit de le croiser.
4. Les modifications de clips → reconstruire. La fiche du personnage garde le nouveau prefab.

**Sans clip Idle**, aucun controller n'est créé : le modèle est **animé par code** (`ProceduralHumanoidMotion`).
C'est l'état actuel de l'Archer et de la Goule, en attendant des animations :
- respiration au repos ;
- marche et course ;
- attaque : coup de hache en mêlée, tir à l'arc pour les sorts à projectiles ;
- recul quand il est touché, chute à la mort.

Dès qu'un vrai controller existe, il prend le relais automatiquement.

---

## 5. Paramètres envoyés par le jeu (référence)

Générés automatiquement dans chaque controller (définis dans `Scripts/Animation/AnimatorBridge.cs`, `AnimParams`) :

| Type | Paramètres |
|---|---|
| Float | `Speed` (m/s), `MoveX` / `MoveY` (déplacement local), `VerticalSpeed`, `AimPitch` (joueur local) |
| Bool | `Grounded`, `Sprinting`, `Downed`, `Dead`, `Frozen` |
| Int | `SkillSlot`, `AttackIndex` |
| Trigger | `Cast`, `Attack`, `Hit`, `Jump`, `Land`, `UseEquipment`, `Revive`, `Spawn`, `Dodge`, `Leap` |

Le graphe généré (modifiable à la main dans *Window → Animation → Animator*, mais écrasé à chaque reconstruction) :

- **Couche Base**
  - `Locomotion` : blend 1D sur `Speed` ;
  - `Jump → Fall → Land` : saut, chute, réception ;
  - `Hit` : depuis n'importe quel état ;
  - `Downed → Revive` : à terre, puis relevé ;
  - `Death` : définitif.
- **Couche Actions** (haut du corps)
  - `Empty` : état par défaut ;
  - un état par ligne d'action, qui revient à `Empty` à la fin du clip.

Pour un graphe plus poussé (strafe 2D avec `MoveX` / `MoveY`, visée avec `AimPitch`…), dupliquer le controller
généré, le modifier, puis :
1. retirer la cible de la recette ;
2. assigner le controller à la main sur l'Animator du prefab.

---

## 6. Dépannage

| Problème | Cause probable |
|---|---|
| Modèle en T-pose qui glisse | Controller sans clips valides, ou avatar non Humanoid. Vérifier l'étape 1. |
| Personnage tordu / bras retournés | Les clips n'utilisent pas le même avatar : *Copy From Other Avatar* dans l'onglet Rig des FBX d'animation. |
| Il avance en marchant sur place puis « recule » | *Root Transform Position (XZ)* non baké, ou clip sans **In Place**. |
| Il regarde de côté | Champ **Rotation** de la recette (ex. `0, 90, 0`). |
| L'arme flotte ou est géante | Mauvais os ou échelle : vérifier `Bone` et `Scale` de l'attachement. |
| Rose / magenta | Matériau non URP : *Window → Rendering → Render Pipeline Converter*, ou assigner un matériau URP/Lit. |
| « bone … not found » dans la console | Le Rig n'est pas en Humanoid ou l'os n'est pas mappé (Configure… dans l'onglet Rig). |
