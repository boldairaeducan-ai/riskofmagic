# Risk of Magic — liste des assets à produire

Chaque ligne indique **où brancher** l'asset. Rien n'est écrasé par les builders aux emplacements indiqués.
Ne jamais modifier directement un prefab généré : passer par le champ de la définition (`icon`, `modelPrefab`…) ou par
**`Resources/ArtLibrary.asset`** (*Tools → Risk of Magic → Select Art Library*), dont les emplacements sont créés par
*Build Prototype Scene* et appliqués à chaque génération.

---

## 1. Audio (~206 emplacements, tous en `SoundEvent` : un ou plusieurs clips tirés au hasard)

Emplacements générés vides dans `Assets/_Project/Audio/Sounds/` par *Create Audio Slots* (ou *Build Prototype Scene*).

### 1.1 Sons globaux — `Resources/AudioConfig.asset` (22)
| Catégorie | Sons |
|---|---|
| Interface | clic, survol |
| Combat (joueur local) | coup confirmé, coup critique confirmé, kill confirmé, joueur touché, mis à terre, réanimé, mort, passage de niveau (discret) |
| Économie & objets | or gagné, achat, ramassage d'objet ×5 (commun, inhabituel, rare, légendaire, boss) |
| Partie | téléporteur activé, téléporteur chargé, téléporteur prêt, game over, victoire |

### 1.2 Musiques — `AudioConfig` (6)
Menu principal · étape (par défaut ; surchargeable par carte avec `SceneMusic`) · boss (événement du téléporteur) · boss final (Avatar) · victoire · game over.
Musique par carte : `Audio/Music/<Carte>_Music.asset` (musique + musique du boss), créé avec la carte.
**À prévoir (pas encore d'emplacement)** : ambiances sonores de carte (vent, forêt, lave, crypte).

### 1.3 Compétences — `castSound` sur chaque compétence (48)
| Personnage | De base | Alternatives |
|---|---|---|
| Archer | Tir rapide, Flèche perforante, Roulade, Pluie de flèches | Flèches enflammées, Flèche de givre, Bond arrière, Volée explosive |
| Guerrier | Combo d'épée, Tourbillon, Charge, Frappe sismique | Marteau de guerre, Hache de jet, Parade, Cri de guerre |
| Arcaniste | Projectile magique, Explosion arcanique, Bouclier arcanique, Blocage temporel | Éclats arcaniques, Orbe de givre, Transfert, Rayon du néant |
| Druide | Griffes toxiques, Nuage toxique, Bond sylvestre, Floraison fatale | Épines, Ronces, Enracinement, Épidémie |
| Paladin | Marteau béni, Jugement, Égide, Sol consacré | Lame radieuse, Bouclier projeté, Charge du croisé, Intervention divine |
| Ombre | Dagues jumelles, Dagues de lancer, Voile d'ombre, Marque de mort | Lames saignantes, Fumigène, Pas de l'ombre, Danse des lames |

### 1.4 Projectiles — `fireSound` + `impactSound` (22 × 2 = 44)
Flèche, flèche perforante, trait de feu, orbe de givre, trait ennemi, missile arcanique, bombe de feu, trait arcanique, dard empoisonné,
crachat d'araignée, rocher, trait du chaos, météore, flèche enflammée, flèche de givre, flèche explosive, hache, éclat arcanique,
orbe de givre arcanique, marteau sacré, onde radieuse, dague de lancer.

### 1.5 Ennemis — `spawnSound`, `attackSound`, `hurtSound`, `deathSound` (11 × 4 = 44)
Goule, Loup spectral, Lame d'ombre, Ogre, Pyromancien, Squelette archer, Araignée, Colosse d'os, Reine araignée, Drake de braise, Avatar du chaos.

### 1.6 Personnages — `footstepSound`, `jumpSound`, `landSound` (6 × 3 = 18)
Archer, Guerrier, Arcaniste, Druide, Paladin, Ombre.

### 1.7 Équipements — `activateSound` (3)
Corne de ralliement, Tome sombre, Pierre de foudre.

### 1.8 Effets visuels — `EffectSound` sur chaque prefab de `Prefabs/Vfx` (21)
Impact, explosion de givre, zone, coup d'arme, téléportation, éclair, explosion, explosion de feu, explosion arcanique, nuage de poison,
éclat de poison, ruée, impulsion de bouclier, impulsion de malédiction, impulsion de ralliement, onde de choc, impulsion temporelle,
marqueur au sol, onde sismique, rayon du néant, flaque de feu.

### 1.9 Optionnels (vides = son par défaut)
`pickupSound` par objet (26), `purchaseSound` par objet interactif, sons d'interface surchargés par bouton (`UiSoundTrigger`).

---

## 2. Sprites & interface

### 2.1 Thème du HUD — `UI/HudTheme.asset`
| Type | Éléments |
|---|---|
| Polices (TMP) | corps de texte, titres, chiffres |
| Sprites (9-slice si bordures) | panneau, cadre, fond de barre, remplissage de barre, cadre d'emplacement, voile de recharge, bouton, champ de saisie |
| Icônes | réticule, or, icône de compétence par défaut, icône d'objet par défaut, portrait par défaut, marqueur de verrouillage de cible |
| Couleurs | déjà réglables dans le thème (dont couleurs des dégâts par type, sacré compris) |

### 2.2 Icônes de jeu
| Quoi | Nombre | Où |
|---|---|---|
| Compétences | 48 | `icon` de chaque compétence (`Configs/Skills`) |
| Objets | 26 | `icon` de chaque objet (`Configs/Items`) — 11 communs, 6 inhabituels, 5 rares, 4 légendaires, 2 boss |
| Équipements | 3 | `icon` de chaque équipement (`Configs/Equipment`) |
| Portraits de personnages | 6 | `portrait` de chaque personnage (`Configs/Characters`) — HUD + cartes du salon |

### 2.3 Décor du menu principal — `Prefabs/Menu/MenuBackdrop.prefab`
Créé une fois (ruines, feu de camp, obélisque en placeholders), jamais écrasé : à habiller librement.
Caméra, point visé, ciel, brouillard et lumière ambiante se règlent sur le composant `MenuBackdrop`.

### 2.4 `ArtLibrary`
| Quoi | Champ |
|---|---|
| Logo (remplace le titre de l'accueil), fond du menu, fond de l'écran de chargement | `logo`, `menuBackground`, `loadingBackground` |
| Curseur de souris (texture importée en *Cursor*) | `cursor`, `cursorHotspot` |
| Icônes des statuts (barre de statuts du HUD, au-dessus du portrait) | `statuses` (13) |
| Icônes de rareté (icône de secours des objets sans icône) | `rarities` (5) |
| Icônes des élites (au-dessus des ennemis élites) | `elites` (4) |
| Icônes des DoT : poison, saignement, brûlure (au-dessus des ennemis + barre de statuts) | `dots` (3) |

### 2.5 À prévoir (pas encore d'emplacement ni d'affichage)
Icônes des défis du journal.

---

## 3. Modèles 3D

### 3.1 Personnages — `modelPrefab` (vue 3e personne) + `viewModelPrefab` (bras/arme en vue FPS) (6 × 2 = 12)
Archer, Guerrier, Arcaniste, Druide, Paladin, Ombre.

### 3.2 Ennemis — `modelPrefab` de chaque définition (11)
Goule, Loup spectral, Lame d'ombre, Ogre, Pyromancien, Squelette archer, Araignée · Boss : Colosse d'os, Reine araignée, Drake de braise, Avatar du chaos.
Échelle 1, pieds au pivot ; les hitbox (tête = point faible) restent celles du prefab généré.

### 3.3 Objets interactifs — `ArtLibrary.interactables` (modèle imbriqué à la génération, remplace les primitives)
Petit coffre, grand coffre, coffre du choix, coffre maudit, tonneau d'équipement, autel de soin, marchand, téléporteur,
sceau de victoire (obélisque), tombe à purifier, autel de défi, objet au sol (orbe flottant générique).

### 3.4 Projectiles — `ArtLibrary.projectiles` (22, liste en 1.4), ou directement `visualPrefab` de la définition (conservé)

### 3.5 Cartes (level design, à ta charge) — `Scenes/Maps`
Grim Forest, Necropolis, Mountain Peak, Volcano Tomb, Chaos Sanctum : terrains et textures, arbres, ruines, rochers, bâtiments,
temple, statues, lave, skybox, éclairage, post-process.

---

## 4. Animations (Animator, paramètres standards : voir README « Plugging in models & animations »)

| Pour | États / clips à prévoir |
|---|---|
| Chaque personnage (3e pers.) | idle, locomotion (blend `MoveX`/`MoveY`/`Speed`), sprint, saut, chute, atterrissage, touché, à terre, mort, réanimation, équipement, **1 clip par compétence** (8 par perso, `animatorTrigger`) |
| Chaque personnage (bras FPS) | idle, marche/sprint, attaque/lancement par compétence (8), équipement, touché |
| Chaque ennemi | apparition, idle, locomotion, **1 clip par attaque** (`AttackIndex` : 1 pour les ennemis, 3 pour les boss), touché, esquive, bond, gelé, mort |
| Boss en plus | Colosse : frappe sismique, lancer, charge · Reine : crachat, invocation, bond · Drake : souffle, charge, boule de feu · Avatar : salve, météores, invocation, séparation |
| Évènements d'animation (optionnel) | `PlaySound`, `PlayVfx`, `Footstep` via `AnimationEventRelay` |

---

## 5. Effets visuels (VFX) — `ArtLibrary.vfx` (un emplacement par prefab de `Prefabs/Vfx` ; cocher `keepPlaceholder` pour les rayons et anneaux dessinés par le code)

| Groupe | Prefabs |
|---|---|
| Combat | Impact, Swing (coup), Explosion, FireBurst, FrostBurst, ArcaneBurst, PoisonBurst, Shockwave, Lightning (trait), VoidBeam (rayon) |
| Zones & marqueurs | Area, PoisonCloud, Telegraph (marqueur au sol), FirePatch (traînée de feu), GroundWave (onde du Colosse) |
| Compétences & buffs | Blink, DashBurst, ShieldPulse, CursePulse, RallyPulse, TimePulse |
| Auras d'élites | Aura_Burning, Aura_Blizzard, Aura_Storm, Aura_Spectral |
| Météo | neige du blizzard (Mountain Peak) |
| À prévoir | traînées des projectiles (22), effet de passage de niveau (discret), effet d'invisibilité (Ombre), feedback de mise à terre/réanimation, lumière du téléporteur, effet de ramassage d'objet par rareté |

---

## 6. Matériaux & shaders (à prévoir)
Matériaux des personnages et ennemis, teintes compatibles (`_Color` / `_BaseColor` pour gel, poison, malédiction, élites),
matériau de lave, eau/brume, effets d'invisibilité et de bouclier, matériaux des cartes, shader de contour (optionnel).

---

## 7. Textes & divers
Textes FR déjà en place (noms et descriptions). À prévoir : traduction EN (si sortie internationale), crédits,
textes de tutoriel / premier lancement, page Steam (capsules, captures, trailer).
