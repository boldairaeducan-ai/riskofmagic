# Checklist de playtest

Objectif : un premier test par 3 à 5 personnes, idéalement en les regardant jouer (partage d'écran).
Coche chaque point avant d'envoyer le build.

## 1. Stabilité

- [ ] Console **sans erreur** sur une partie complète en solo : 5 étapes, boss de chaque étape, boss final, écran de fin.
- [ ] Idem à **2 joueurs**, puis à **4** si possible (Multiplayer Play Mode, puis deux PC via Steam).
- [ ] Mise à terre, relève par un allié, hémorragie, défaite, **Recommencer** et **retour au menu** : aucune erreur.
- [ ] Chaque classe jouée au moins une étape, avec ses compétences de base **et** alternatives.
- [ ] Coffres, coffre à choix, marchand, tonneaux, autels, tombe, coffre maudit, secret de carte : tous testés une fois.
- [ ] Aucun blocage : téléporteur introuvable, ennemi coincé, joueur bloqué dans le décor, chute hors carte.

## 2. Build

- [ ] **Build Windows** qui se lance hors de l'éditeur (*File → Build Profiles*), scènes : `MainMenu` en premier,
      `Prototype` et les `Map_*` de la rotation.
- [ ] `steam_appid.txt` (480) copié à côté de l'exécutable (fait par `SteamAppIdCopy`), Steam lancé.
- [ ] Invitation d'un ami Steam, code de partie, salon : un testeur qui n'a jamais vu le jeu arrive à rejoindre.
- [ ] *Player Settings* : `Company Name` et `Product Name` renseignés (le dossier des journaux en dépend), version
      (`Version`) incrémentée à chaque build envoyé.

## 3. Performances

- [ ] 60 FPS sur une machine moyenne avec 22 ennemis, élites et effets à l'écran (Profiler ou statistiques du Game View).
- [ ] Pas de saccade au changement d'étape ni à l'apparition du boss.

## 4. Prise en main (sans toi)

- [ ] Écran des contrôles accessible (Options → Contrôles) et affiché la première fois.
- [ ] Objectif clair au début : « Trouvez et activez le téléporteur ».
- [ ] Les règles particulières se comprennent : coffres scellés pendant le boss, essoufflement des vagues, frise de
      difficulté, écran Tab.
- [ ] Textes en français partout, pas de texte de debug visible.

## 5. Contenu provisoire

- [ ] Aucune capsule sans modèle sur les ennemis courants, ni icône manquante sur les objets.
- [ ] Les icônes de statut provisoires (`skill_xxx`) sont acceptables ou remplacées.
- [ ] Sons présents sur les actions principales (tir, coup, ramassage, téléporteur).

## 6. Progression

- [ ] Le profil (défis, déblocages) se sauvegarde et se recharge.
- [ ] Prévoir une façon de **tout débloquer** pour les testeurs (ou leur donner un profil complet), sinon ils ne
      verront que l'Archer et le Guerrier.

## 7. Retours

- [ ] **Journal de session** : chaque partie écrit un fichier JSON (classe, étapes, durées, kills, mises à terre et leurs
      causes, positions). Dossier sur Windows :
      `%USERPROFILE%\AppData\LocalLow\<Company Name>\<Product Name>\Sessions`
      (actuellement `DefaultCompany\riskofmagic`). Le chemin exact est écrit dans le log au début de chaque partie.
- [ ] Demander aux testeurs d'envoyer **tout le dossier Sessions** après leurs parties.
- [ ] **Questionnaire court** (5 min) :
  1. Note globale (1 à 10) et envie de rejouer.
  2. Classe préférée / la moins agréable, et pourquoi.
  3. Difficulté : trop facile, bien, trop dur ? À quel moment ?
  4. Objet ou compétence qui t'a marqué (en bien ou en mal).
  5. Moment où tu t'es senti perdu ou bloqué.
  6. Bugs rencontrés (même petits).
- [ ] **Liste des problèmes connus** jointe au build, pour ne pas recevoir dix fois le même retour.

## 8. Analyse

- [ ] Copier les fichiers reçus dans un même dossier, ouvrir chaque `Map_*` puis
      **Tools → Risk of Magic → Heatmap** → choisir le dossier → **Charger** :
  - présence : cases bleues (peu) → rouges (beaucoup) ;
  - sphères orange (mise à terre) / rouges (mort) avec la cause ;
  - temps moyen par carte et causes les plus fréquentes.
- [ ] Repérer : zones jamais visitées (coffres inutiles ?), couloirs saturés, endroits où l'on meurt souvent,
      cartes où l'on reste trop longtemps (farm) ou trop peu.
