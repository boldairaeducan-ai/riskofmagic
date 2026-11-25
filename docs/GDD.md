# Risk of Magic — Game Design Document

17 novembre 2026 · version en ligne : https://claude.ai/artifact/VBF4KcUwjzwePz5bmtqaEw

## Pitch

Roguelite coopératif à la première personne dans l'esprit de *Risk of Rain*, en univers médiéval fantastique low-poly coloré.

| Élément | Valeur |
| --- | --- |
| Genre | Action roguelite, FPS, runs de 30 à 60 min |
| Joueurs | 1 à 4, coop en ligne (Steam, code de partie) |
| Plateforme | PC (Unity 6, URP) |
| Personnages | 6 classes jouables, 8 compétences chacune (4 de base + 4 alternatives) |
| Contenu | 44 objets, 3 équipements, 17 ennemis dont 4 boss et 5 boss secrets |

**Piliers** : la difficulté monte avec le temps ; les objets se cumulent sans plafond ; chaque classe a deux styles de jeu ; la coop est au cœur du jeu (soins, relèves, buffs de zone).

## Boucle de jeu

Une run enchaîne 5 étapes : chaque carte se termine en chargeant un téléporteur gardé par un boss, puis la run boucle avec un boss final à l'étape 5.

1. Arrivée sur la carte : ennemis déjà en place, coffres, tonneaux d'équipement, marchand, secrets.
2. Exploration et farm : l'or achète des coffres, les kills donnent de l'expérience (niveaux).
3. Téléporteur : 90 s de charge dans un rayon de 30 m, vagues continues, boss à vaincre.
4. Récompense du boss, étape suivante.

| Étape | Cartes |
| --- | --- |
| 1 | Forêt sombre |
| 2 | Nécropole ou Pic montagneux |
| 3 | Tombeau volcanique |
| 4 | (à définir) |
| 5 | Sanctuaire du chaos (boss final) |

**Difficulté** : un coefficient démarre à 1,25, monte de 0,1 par minute, est multiplié par 1,08 à chaque étape et par 1 + 0,3 par joueur supplémentaire. Il fixe le niveau des ennemis (1 niveau tous les 0,33) et le budget du directeur d'ennemis.

| Mode | Différences |
| --- | --- |
| Normal | Mode de base |
| Difficile (débloqué après une victoire) | Montée 50 % plus rapide, budget du directeur ×1,3, détection des ennemis ×1,25, ennemis variés débloqués plus tôt (×1,2) |

**Directeur d'ennemis** : 110 crédits de population par carte, puis une vague toutes les 8 à 15 s (4 ennemis max par vague, 22 vivants max). Élites possibles dès un coefficient de 1,5, 35 % des vagues.
- **Étape 1** (première boucle) : population et crédits de départ ×0,8, vagues 25 % moins fréquentes (10 à 19 s).
- **Étapes suivantes** : population et crédits de départ +3 % par objet détenu (moyenne par joueur), plafonné à ×2
  (10 objets = ×1,3 ; 33 objets et plus = ×2). Les vagues au fil du temps ne changent pas.

## Personnages

6 classes, toutes à 10 de dégâts de base et 1 % de critique ; les pourcentages des compétences s'appliquent à ces dégâts. Chaque emplacement a une compétence de base et une alternative débloquée par un défi, choisie dans le salon.

| Classe | Rôle | PV | Vitesse (m/s) | Sauts | Vit. d'attaque |
| --- | --- | --- | --- | --- | --- |
| Archer | Distance agile | 80 | 6,4 | 2 | 0,8 |
| Guerrier | Mêlée robuste | 130 | 5,95 | 1 | 0,64 |
| Arcaniste | Projectiles chercheurs, contrôle | 90 | 5,95 | 1 | 0,8 |
| Druide | Distance, seul à infliger des dégâts sur la durée | 100 | 6,1 | 1 | 0,64 |
| Paladin | Soutien de première ligne, sceaux | 120 | 5,6 | 1 | 0,8 |
| Ombre | Assassin, critiques dans le dos | 85 | 6,8 | 2 | 0,8 |

Emplacements : clic gauche (principale), clic droit (secondaire), A (utilitaire), R (spéciale). Rechargement entre crochets.

### Archer

| Emplacement | Base | Alternative |
| --- | --- | --- |
| Clic gauche | Tir rapide : 100 % en continu | Tir en éventail : 3 × 50 % |
| Clic droit | Flèche perforante [6 s] : chargée, 150-500 %, traverse | Bolas [7 s] : chargés, 1 à 4 cibles en cône de 18 m, 150 %, −50 % de vitesse 3 s |
| Utilitaire | Roulade [5 s] | Bond arrière [6 s] : 12 m, repousse |
| Spéciale | Pluie de flèches [15 s] : 8 × 75 % en zone | Volée explosive [12 s] : 3 × 250 % dans 4 m |

### Guerrier

| Emplacement | Base | Alternative |
| --- | --- | --- |
| Clic gauche | Combo d'épée : 100/100/200 % | Marteau de guerre : 2 × 250 % en zone de 3 m, repousse |
| Clic droit | Tourbillon [7 s] : 3 × 120 % | Hache de jet [6 s] : 300 %, perforante |
| Utilitaire | Charge [8 s] : 200 % aux ennemis percutés | Parade [8 s] : bloque 100 % 1 s, contre-attaque 200 % |
| Spéciale | Frappe sismique [15 s] : bond + onde 600 % | Cri de guerre [20 s] : alliés à 15 m, +40 % vitesses 6 s, +20 PV |

### Arcaniste

| Emplacement | Base | Alternative |
| --- | --- | --- |
| Clic gauche | Projectile magique : chargé, jusqu'à 6 cibles, 110 % chacune | Éclats arcaniques : 3 × 60 % |
| Clic droit | Explosion arcanique [6 s] : 200 % autour, repousse | Orbe de givre [7 s] : 300 % dans 4 m, gèle 2 s |
| Utilitaire | Bouclier arcanique [10 s] : bloque 95 % 3 s | Transfert [7 s] : téléportation 15 m |
| Spéciale | Blocage temporel [14 s] : fige les ennemis devant soi 4 s | Rayon du néant [12 s] : 3 s, 8 × 45 %/s (× vitesse d'attaque), +25 %/s de montée, traverse |

### Druide

Kit Nature en base (saignement, foudre, flammes), kit Poison en alternative.

| Emplacement | Base | Alternative |
| --- | --- | --- |
| Clic gauche | Fouet de liane : 14 m, 80 % + saignement | Épines : 70 % + poison |
| Clic droit | Appel de la foudre [7 s] : 320 % dans 5 m, étourdit 0,8 s | Nuage toxique [8 s] : 5 s, 30 %/tick + poison |
| Utilitaire | Envol de corbeaux [9 s] : 1,5 s invulnérable, +60 % de vitesse, flotte | Racines vénéneuses [10 s] : enracine 2 s à 8 m, 50 % + poison |
| Spéciale | Souffle enflammé [12 s] : 2,5 s, cône 10 m, 10 × 45 % + brûlure | Floraison fatale [10 s] : fait éclater les poisons à 25 m, 150 % du reste |

### Paladin

Mécanique : **Sceaux**. Chaque coup de clic gauche pose un sceau sur l'ennemi touché (max 3, 8 s). Le Jugement (ou le
Bouclier projeté) les fait éclater : +80 % de dégâts par sceau et soin de 5 % des PV max par sceau aux alliés à 8 m.
Pas de régénération passive.

| Emplacement | Base | Alternative |
| --- | --- | --- |
| Clic gauche | Marteau béni : 120/180 %, pose un sceau | Lame radieuse : 90/90/150 %, onde perforante 100 %, pose un sceau |
| Clic droit | Jugement [6 s] : 300 %, étourdit 1 s, fait éclater les sceaux | Bouclier projeté [6 s] : ricoche sur 4 ennemis, 200 %, fait éclater leurs sceaux |
| Utilitaire | Égide [12 s] : −50 % de dégâts subis, alliés à 10 m, 4 s | Charge du croisé [8 s] : 12 m, 200 %, soigne 10 % les alliés |
| Spéciale | Sol consacré [18 s] : 8 m, 6 s, alliés +5 % PV/s, ennemis 60 %/s | Intervention divine [60 s] : relève les alliés à 30 m (50 % PV) |

### Ombre

| Emplacement | Base | Alternative |
| --- | --- | --- |
| Clic gauche | Dagues jumelles : 70/70/110 %, critique dans le dos | Poignard lourd : 220 %, critiques ×3,5 |
| Clic droit | Dagues de lancer [4 s] : 3 × 100 % | Fumigène [8 s] : 150 % dans 6 m, désoriente 2 s |
| Utilitaire | Voile d'ombre [10 s] : invisible 3 s, +30 % de vitesse, 1re attaque +100 % | Pas de l'ombre [7 s] : téléportation dans le dos (20 m), critique garanti |
| Spéciale | Marque de mort [12 s] : +50 % de dégâts subis 6 s, 30 % renvoyés à 8 m | Danse des lames [25 s] : 1,5 s invulnérable, 8 × 70 % à 6 m |

## Objets et équipements

44 objets passifs qui se cumulent sans plafond (valeur entre parenthèses = gain par exemplaire supplémentaire), et 3 équipements actifs (touche F). Les dégâts d'objets et sur la durée ne redéclenchent jamais d'objets.

| Rareté | Nombre | Source principale |
| --- | --- | --- |
| Commun | 17 | Petits coffres, marchand |
| Inhabituel | 16 | Petits et grands coffres |
| Rare | 5 | Grands coffres, coffres à choix |
| Légendaire | 4 | Coffres rares, secrets |
| Boss | 2 | Récompense de boss |

### Communs

| Objet | Effet |
| --- | --- |
| Bottes du messager | +14 % (+14 %) de vitesse |
| Breuvage du coursier | +25 % (+25 %) de vitesse en sprint |
| Gantelet de célérité | +15 % (+15 %) de vitesse d'attaque |
| Œil de faucon | +10 % (+10 %) de chances de critique |
| Viande séchée | +25 (+25) PV max |
| Racine de vigueur | +1,5 (+1,5) PV/s |
| Pendentif sanguin | Chaque coup soigne 1 (+1) PV |
| Croc de loup | Chaque kill soigne 8 (+6) PV |
| Bouclier de chêne | −5 (−5) dégâts par coup reçu, minimum 1 |
| Pierre de première taille | 1er coup sur chaque ennemi : +60 % (+40 %) |
| Croc de boucher | +30 % (+30 %) de dégâts aux ennemis à moins de 4 m |
| Pierre à feu | 10 % (+10 %) de chances d'enflammer : brûlure de 150 % en 3 s |
| Lame affûtée | +30 % (+30 %) de dégâts sur les coups critiques |
| Lame du bourreau | +40 % (+40 %) contre les ennemis sous 30 % de PV |
| Masse brise-garde | +40 % (+40 %) contre les ennemis au-dessus de 90 % de PV |
| Dague barbelée | 10 % (+10 %) de chances de saignement, 240 % en 3 s |
| Fiole d'adrénaline | Après un kill : +25 % (+15 %) de vitesse d'attaque 3 s |

### Inhabituels

| Objet | Effet |
| --- | --- |
| Sablier ancien | −12 % de rechargement (multiplicatif) |
| Plume de griffon | +1 (+1) saut |
| Orbe de tempête | 25 % : éclair sur 3 (+2) ennemis, 80 % |
| Carapace d'ambre | Chaque kill : 5 (+5) points de bouclier, max 20 % des PV max, perdus après 5 s sans kill |
| Résine collante | Chaque coup ralentit de 10 % (+10 %) pendant 2 s, max 50 % |
| Venin de vipère | +20 % (+20 %) de dégâts contre un ennemi qui saigne, brûle ou est empoisonné |
| Talisman de frénésie | Coup critique : +10 % de vitesse d'attaque 3 s, 3 (+2) cumuls max |
| Brise-armure | Chaque coup : +3 % de dégâts subis par la cible 4 s, 5 (+5) cumuls max |
| Cœur d'ambre | Chaque kill : −0,5 s (−0,5 s) sur les recharges restantes, sauf la principale |
| Carquois enchanté | 10 % : missile chercheur, 300 % (+300 %) |
| Pierre de résonance | Critiques : 50 % (+25 %) des dégâts dans 3 m |
| Huile incendiaire | Ennemis tués explosent : 150 % (+75 %) dans 6 m (+1,5 m) |
| Orbe de feu | Kill : brûle les ennemis à 4 m (+1,5 m), 200 % (+100 %) en 3 s |
| Poupée vaudou | Kill : maudit les ennemis à 4 m (+1 m) pendant 4 s |
| Bracelet d'épines | Touché : renvoie 400 % (+200 %), recharge 5 s (−0,5 s, min 3 s) |
| Cape du fuyard | Touché : +40 % de vitesses 2 s (+1 s), recharge 5 s |

### Rares, légendaires et boss

| Objet | Rareté | Effet |
| --- | --- | --- |
| Anneau de givre | Rare | 10 % (+5 %) : gèle 1,5 s et 250 % |
| Bannière de guerre | Rare | Alliés à 12 m (+2 m) : +20 % (+10 %) de dégâts |
| Cor de guerre | Rare | Kill : +15 % de vitesses, max 5 (+1) cumuls, perdus quand on est touché |
| Grimoire interdit | Rare | +1 (+1) charge aux compétences hors principale |
| Lame jumelle | Rare | 15 % (+10 %) de frapper une 2e fois (100 %) |
| Couronne des âmes | Légendaire | Kill : +1 % (+1 %) de dégâts jusqu'à la fin de l'étape, max +60 % |
| Larme de comète | Légendaire | 8 % (+4 %) : météore 500 % dans 5 m |
| Plume de phénix | Légendaire | Résurrection à 50 % PV, consommée |
| Œil du chaos | Légendaire | Chaque coup explose : 60 % dans 4 m (+2,5 m) |
| Os de titan | Boss | Toutes les 5 attaques : onde de 300 % (+150 %) |
| Écaille du colosse | Boss | −15 % de dégâts subis (dégressif) |

### Équipements

| Équipement | Rechargement | Effet |
| --- | --- | --- |
| Tome sombre | 30 s | Coûte 25 % PV ; maudit en cône (20 m) 8 s : −50 % de dégâts infligés, −10 % de vitesse, +30 % de dégâts subis |
| Pierre de foudre | 30 s | 3 éclairs de 500 % sur la zone visée |
| Corne de ralliement | 45 s | +40 % de vitesses 8 s, soi et alliés à 25 m |

## Ennemis

8 ennemis courants, 4 boss de téléporteur et 5 boss secrets. Les PV montent de 40 % et les dégâts de 20 % par niveau d'ennemi ; les boss ont +50 % de PV par joueur au-delà du premier ; les ennemis courants frappent ×1,15 plus fort et ×1,3 plus souvent.

### Ennemis courants

| Ennemi | PV | Vitesse (m/s) | Comportement | Attaque | Coût directeur | Or |
| --- | --- | --- | --- | --- | --- | --- |
| Araignée | 20 | 8,6 | Direct | Mêlée | 4 | 2 |
| Goule | 70 | 6,9 | Direct | Mêlée | 10 | 8 |
| Loup spectral | 40 | 8 | Contourne | Bond | 12 | 8 |
| Lame d'ombre | 50 | 8,6 | Contourne | Mêlée | 14 | 10 |
| Pyromancien | 55 | 5,8 | Garde ses distances | Tir en cloche | 16 | 12 |
| Squelette archer | 60 | 4,6 | Garde ses distances | Projectile | 15 | 14 |
| Ogre | 240 | 5,2 | Direct | Bond | 28 | 24 |
| Racine épineuse | 140 | 0 (fixe) | Immobile | Mêlée | — | 0 |

### Élites

×4 PV, ×2 dégâts, ×6 coût, ×4 or (sur un boss : ×1,5 PV, ×1,25 dégâts, ×1,5 or).

| Élite | Pouvoir |
| --- | --- |
| Ardente | Traînée de feu (3 dégâts/0,5 s, 3 s) + brûlure |
| Glaciale | Aura de 6 m : 2 dégâts/0,5 s, ralentit |
| Foudroyante | Toutes les 4 s : éclair de 12 dégâts annoncé 1 s avant (rayon 2,5 m) |
| Éthérée | Invulnérable 1 s toutes les 4 s, se téléporte toutes les 6 s |

### Boss

| Boss | PV | Attaques | Or |
| --- | --- | --- | --- |
| Reine araignée | 1 300 | Bond, projectiles, invoque des araignées | 150 |
| Colosse d'os | 1 400 | Bond, tirs en cloche, onde de choc | 150 |
| Drake de braise | 1 600 | Bond, souffle en cône, tirs en cloche | 150 |
| Avatar du chaos (final) | 4 000 | Météores, projectiles, invocations | 400 |
| Roi-liche (secret) | 1 300 | Hantise, projectiles, invocations | 150 |
| Tisseur du néant (secret) | 1 300 | Rayon, téléportation, vortex, projectiles | 150 |
| Wyverne des glaces (secret) | 1 400 | Bond, cône, éruptions | 150 |
| Sylvain corrompu (secret) | 1 500 | Tirs en cloche, éruptions, se régénère enraciné | 150 |
| Forgeron titanesque (secret) | 1 800 | Tirs en cloche, 2 types d'éruptions | 150 |

### Statuts

| Statut | Effet |
| --- | --- |
| Malédiction | −50 % de dégâts infligés, −10 % de vitesse, +30 % de dégâts subis |
| Marqué | +50 % de dégâts subis, 30 % renvoyés aux ennemis à 8 m |
| Entravé (bolas) | −50 % de vitesse (−25 % sur boss et élites) |
| Gelé | −30 % de vitesse |
| Enraciné | Immobile, peut attaquer |
| Sceau | 1 à 3 cumuls posés par le Paladin, éclatés par le Jugement |
| Frénésie | +10 % de vitesse d'attaque par cumul (Talisman de frénésie) |
| Armure brisée | +3 % de dégâts subis par cumul (Brise-armure) |
| Résine | −10 % de vitesse par exemplaire de Résine collante du joueur, max −50 % |
| Figé / étourdi | Ni déplacement ni attaque |
| Poison, saignement, brûlure | Dégâts sur la durée, voir ci-dessous |

### Dégâts sur la durée (DoT)

Chaque application ajoute **un cumul** indépendant, avec sa propre durée. Les cumuls d'un même effet posés par un même
joueur sur une même cible tiquent ensemble, toutes les 0,5 s.

| Effet | Dégâts par tick | Durée | Cumuls max | Au plafond | Sources |
| --- | --- | --- | --- | --- | --- |
| Saignement | 40 % du coup qui l'applique | 3 s | 20 | Le plus ancien cumul est remplacé | Fouet de liane, Dague barbelée |
| Brûlure | 25 % du coup | 3 s | 10 | Le plus ancien cumul est remplacé | Souffle enflammé, Orbe de feu, élite Ardente, Drake de braise |
| Poison | 0,4 % des PV max de la cible par seconde | 6 s | 10 | Tous les cumuls sont rafraîchis | Épines, Nuage toxique, Racines vénéneuses, Reine araignée (crachat) |

Sur toute sa durée, un cumul inflige donc 2,4 × le coup (saignement), 1,5 × le coup (brûlure) ou 2,4 % des PV max
(poison).

Règles :
- Saignement et brûlure **suivent les dégâts du coup** : stats, niveau, objets et critique comptent au moment de
  l'application. Les ticks eux-mêmes ne sont jamais critiques.
- Le poison **ignore les dégâts du lanceur** : il dépend des PV max de la cible, donc il reste efficace contre les gros
  ennemis. En contrepartie :
  - il est **divisé par 2 sur les boss et les élites** (2 %/s au plafond, contre 4 %/s sur un ennemi normal) ;
  - sur un joueur, il est limité à 0,5 % par seconde et 4 cumuls ;
  - chaque nouvelle application **rafraîchit tous les cumuls** : il faut continuer à toucher pour l'entretenir ;
  - **plancher** : au moins 40 % des dégâts de base du lanceur par seconde et par cumul, pour que le poison reste
    efficace contre les petits ennemis (hors joueurs).
- Les ticks ne déclenchent **aucun effet d'objet** (coefficient de déclenchement 0) ; ils passent par la réduction de
  dégâts normale de la cible.
- **Floraison fatale** consomme tous les poisons à 25 m et inflige d'un coup 150 % des dégâts qu'il leur restait.
- **Lisibilité** : la cible prend la teinte du DoT. Les icônes au-dessus des ennemis (masquées derrière les murs)
  affichent les DoT actifs, avec le nombre de cumuls sur les boss.

## Économie et progression

L'or achète les objets et nourrit l'expérience ; les défis débloquent classes, compétences et objets d'une partie à l'autre.

**Pendant la run**

- Or des ennemis ×0,75, multiplié par le coefficient de difficulté ; prix = prix de base × coefficient^1,4.
- Expérience = 0,6 par or gagné ; niveau 2 à 35 XP, puis ×1,65 par niveau ; +15 % de dégâts de base par niveau.
- Budget d'objets par carte : 220 crédits en Normal (+25 %/étape), 140 en Difficile (+20 %/étape), +30 % par joueur supplémentaire.

| Interactif | Coût en crédits | Poids de tirage |
| --- | --- | --- |
| Petit coffre | 15 | 6 |
| Grand coffre | 30 | 1,5 |
| Coffre à choix | 30 | 1,2 |
| Tonneau d'équipement | 10 | 1,5 |
| Autel de soin | 15 | 1 |
| Marchand | — | 1 garanti par carte |

**Méta-progression** (profil local) : 36 défis, dont 24 débloquent les compétences alternatives (4 par classe : atteindre l'étape 3, 300 kills, vaincre un boss, étape sans être mis à terre…).

| Déblocage | Condition |
| --- | --- |
| Archer, Guerrier | Dès le départ |
| Arcaniste | Atteindre l'étape 3 |
| Druide | Vaincre 3 boss de téléporteur |
| Paladin | Atteindre l'étape 5 |
| Ombre | Tuer 1 000 ennemis |
| Mode Difficile | Remporter une victoire |
| Œil du chaos | Vaincre l'Avatar du chaos |
| Carquois enchanté | Ouvrir 50 coffres |
| Lame du bourreau | Purifier 3 tombes |
| Sablier ancien | Finir une étape en moins de 5 min |
| Bouclier de chêne | Réussir 3 autels de défi |
| Plume de phénix | Être mis à terre 10 fois |
| Orbe de tempête | Tuer 50 élites |

Le journal (menu principal) suit les statistiques, les défis, les objets découverts et le bestiaire.

## Cartes, événements et multijoueur

5 cartes jouées dans un ordre fixe (une au choix aléatoire à l'étape 2), chacune avec son ambiance, ses événements et un secret menant à un boss caché.

| Événement de carte | Principe | Récompense |
| --- | --- | --- |
| Autel de défi | Gratuit ; 2 vagues d'ennemis (élites d'abord), budget lié à la difficulté | 1 objet par joueur |
| Coffre maudit | Payé en PV max (−1 % par cumul jusqu'à la fin de l'étape) | Objet Rare ou mieux |
| Tombe à purifier | Progresse si un joueur debout reste dans la zone, sous la pression des monstres | Zone de soin + 1 objet par joueur |
| Secret | Rapporter tous les fragments cachés à l'autel de la carte | Bonus d'étape (+30 % de dégâts) ou boss secret |

**Coopération** :

- 1 à 4 joueurs en hôte/client, via Steam (invitation d'amis) ou un code de partie ;
- salon partagé : choix de la classe, des compétences et de la difficulté par l'hôte ;
- un joueur à 0 PV est mis à terre 30 s ; un allié le relève en 3 s à moins de 3 m ;
- la difficulté et le budget d'objets augmentent de 30 % par joueur supplémentaire.

**Technique** :

- Unity 6, rendu URP avec post-process (bloom, étalonnage par carte) ;
- Netcode for GameObjects, serveur autoritaire sauf pour les déplacements ;
- menus en UI Toolkit, HUD en uGUI.

Question ouverte : l'étape 4 n'a pas encore de carte dans la rotation.
