# Interface — menus en UI Toolkit

Les **menus** (accueil, multijoueur, salon, options, journal, pause, écran de chargement) sont en **UI Toolkit** :
la mise en page est dans des fichiers **UXML**, l'apparence dans des feuilles **USS**, et le code C# ne fait que
retrouver les éléments par leur `name` et y injecter les données du jeu.

Le **HUD en jeu** (barres, compétences, inventaire, chiffres de dégâts, marqueurs) ainsi que les menus en jeu
**coffre à choix, marchand, fin de partie et le panneau de connexion debug** restent en **uGUI** (`UI/HUD.prefab`,
généré par `Editor/HudBuilder.cs`, art dans `UI/HudTheme.asset`).

---

## 1. Ce qui a changé

| Avant (uGUI) | Maintenant (UI Toolkit) |
|---|---|
| Menus construits par code dans `MainMenuBuilder` / `HudBuilder` (toute retouche écrasée au rebuild) | Mise en page dans `UI/Menus/*.uxml`, écrits à la main, **jamais régénérés** |
| Couleurs et polices dans `HudTheme.asset` | Tokens dans `UI/Menus/Styles/Theme.uss`, composants dans `Menus.uss` |
| `OptionsMenu`, `JournalMenu`, `OptionRow`, `OptionSliderRow`, `OptionToggleRow`, `OptionKeyRow`, `OptionHint`, `MenuButton`, `FadeInOnEnable`, `LobbyCard` | `OptionsScreen`, `JournalScreen`, `LobbyCardView` (classes C# simples) + templates `UI/Menus/Templates/*.uxml` + classes USS (survol, focus, transitions) |
| `MainMenuController`, `PauseMenu`, `LoadingScreen` en uGUI | Même rôle, mêmes noms, sur un `UIDocument` |
| `CharacterPreview` affiché par une `RawImage` | Rendu dans une RenderTexture affichée en fond de l'élément `characterPreview` |
| Sons des boutons : `UiSoundTrigger` (uGUI) | `MenuSounds` (même `AudioConfig.uiHover` / `uiClick`) |

Les générateurs ne créent plus que le strict nécessaire :

- `MainMenuBuilder` : un objet `MainMenu` avec un `UIDocument` et le `MainMenuController`.
- `HudBuilder` : un objet `PauseMenu` (`UIDocument` + `PauseMenu`) dans le HUD.
- `MenuUiBuilder` (nouveau) crée **une seule fois** `UI/Menus/MenuPanelSettings.asset`.
- `MenuUiBuilder` remplit les cases vides de `Resources/MenuUI.asset`, le registre des écrans et des templates.
- Menu : *Tools → Risk of Magic → Refresh Menu UI Assets*.

**Après mise à jour du dépôt** : *Build Prototype Scene* (scène MainMenu), puis *Rebuild HUD Prefab* (menu pause).

---

## 2. Où sont les fichiers

```
Assets/_Project/UI/Menus/
├── MenuTheme.tss            thème du PanelSettings (importe Theme.uss)
├── MenuPanelSettings.asset  échelle 1920×1080, ordre d'affichage (créé une fois, à toi de régler)
├── Styles/
│   ├── Theme.uss            TOKENS (couleurs, tailles, espacements, police) + look des contrôles de base
│   └── Menus.uss            mises en page et composants (panneaux, boutons de menu, cartes, lignes d'option…)
├── MainMenu.uxml            accueil + panneau multijoueur + salon (inclut Options et Journal)
├── Options.uxml             écran d'options (onglets et lignes ajoutés par le code)
├── Journal.uxml             journal (4 onglets, texte enrichi)
├── PauseMenu.uxml           pause + confirmation (inclut Options)
├── LoadingScreen.uxml       écran de chargement
└── Templates/               éléments clonés par le code
    ├── LobbyCard.uxml       carte personnage / compétence / difficulté
    ├── OptionChoice.uxml    ligne  « libellé   ‹ valeur › »
    ├── OptionSlider.uxml    ligne  « libellé   ━━●━━ 100% »
    ├── OptionToggle.uxml    ligne  « libellé   Activé (●) »
    └── OptionKey.uxml       ligne  « action   catégorie [touche] »

Assets/_Project/Resources/MenuUI.asset     registre (MenuUiAssets) : PanelSettings, écrans, templates
Assets/_Project/Scripts/UI/
├── MainMenuController.cs  PauseMenu.cs  LoadingScreen.cs
└── Toolkit/  MenuUi (helpers) · MenuSounds · MenuUiAssets · OptionsScreen · JournalScreen · LobbyCardView · CharacterPreview
```

---

## 3. Travailler dans l'UI Builder (sans toucher au code)

1. Double-clic sur un `.uxml` → l'**UI Builder** s'ouvre.
2. En haut du canvas, choisir le thème **MenuTheme** (sinon les contrôles de base n'ont pas de style dans l'aperçu).
3. Déplacer, réordonner, ajouter des éléments décoratifs (images, cadres, textes fixes) et modifier les classes.
4. Sauvegarder : en **Play**, le menu est mis à jour sans rebuild.

### Règle d'or : ne pas renommer les `name` utilisés par le code

Le code retrouve les éléments par leur `name`. Si l'un d'eux disparaît, la console affiche :
`[Risk of Magic] UI element 'xxx' (Type) not found…`. Tu peux les **déplacer** et les **restyler** librement.

| Écran | Éléments nommés (type) |
|---|---|
| MainMenu — accueil | `home`, `background`, `gameTitle`, `logo`, `soloButton`, `multiplayerButton`, `journalButton`, `optionsButton`, `quitButton`, `multiplayerPanel`, `hostButton`, `joinCodeField` (TextField), `joinButton`, `statusLabel`, `profileLabel`, `versionLabel` |
| MainMenu — salon | `lobby`, `codeLabel`, `copyCodeButton`, `inviteButton`, `playersLabel`, `difficultyCards`, `difficultyDescription`, `characterPreview`, `characterDescription`, `characterGrid`, `skillGrid`, `mainButton`, `leaveButton`, `options` (Instance), `journal` (Instance) |
| Options | `tabs`, `pages` (ScrollView), `hint`, `resetButton`, `backButton` |
| Journal | `statsTab`, `challengesTab`, `itemsTab`, `bestiaryTab`, `scroll` (ScrollView), `content` (Label), `backButton` |
| Pause | `pauseScreen`, `main`, `subtitleLabel`, `resumeButton`, `optionsButton`, `abandonButton`, `menuButton`, `quitButton`, `forceWaveButton`, `confirm`, `confirmLabel`, `confirmYesButton`, `confirmNoButton`, `options` (Instance) |
| Chargement | `loadingScreen`, `background`, `label` |
| Écran d'infos (Tab maintenu) | `infoScreen`, `portrait`, `characterName`, `levelLabel`, `stats`, `runStats`, `itemGrid`, `equipmentIcon`, `equipmentLabel`, `itemTooltip`, `tooltipName`, `tooltipRarity`, `tooltipDescription`, `runLabel`, `playerList` |
| InfoItem / InfoStat / InfoPlayerRow | `icon`, `count` / `label`, `value` / `name`, `details`, `score`, `ping`, `bar1`…`bar4`, `pingLabel` |
| LobbyCard | racine **Button**, `icon`, `iconLetter`, `title`, `body`, `lockLabel` |
| OptionChoice | `label`, `previous`, `value`, `next` |
| OptionSlider | `label`, `slider` (Slider), `value` |
| OptionToggle | `label`, `state`, `switch` (Button) |
| OptionKey | `label`, `category`, `key` (Button) |

### Classes USS pilotées par le code

Elles peuvent être restylées, mais leurs noms doivent rester.

| Classe | Rôle |
|---|---|
| `hidden` | masque l'élément (`display: none`) |
| `screen--enter` | posée un instant à l'apparition d'un écran : fondu + glissement (transition de `.screen`) |
| `tab--active` | onglet courant (options, journal) |
| `card--selected` / `card--locked` | carte choisie / verrouillée (le voile `card__lock` montre le défi) |
| `card--character`, `card--skill`, `card--difficulty` | variante de carte (taille) |
| `option-row--focused` | ligne d'option dont un contrôle a le focus clavier/manette |
| `switch--on` | interrupteur activé |
| `key-button--listening` | touche en attente d'une nouvelle assignation |
| `loading--visible` | écran de chargement affiché (fondu par transition) |
| `silent` | (à ajouter soi-même) coupe les sons de survol/clic d'un bouton |
| `info-ping--good/ok/bad/terrible`, `info-ping-bar--on` | qualité du ping et barres allumées (écran d'infos) |

---

## 4. Changer l'apparence

- **Couleurs, tailles, espacements, police** : variables `--…` en tête de `Styles/Theme.uss`.
  Exemple : `--color-accent` change l'or de tous les menus.
- **Police** : `-unity-font` dans `:root` (Theme.uss). Mettre un `.ttf`/`.otf` du projet, ou utiliser
  `-unity-font-definition` avec un *Font Asset* TextCore.
- **Cadres et panneaux en image** : dans `Menus.uss`, sur `.panel` ou `.unity-button`, ajouter le code suivant.
  L'image est étirée en 9-slice.
  ```
  background-image: url("project://database/Assets/…/cadre.png");
  -unity-slice-left: 12; -unity-slice-right: 12; -unity-slice-top: 12; -unity-slice-bottom: 12;
  ```
- **Fond, logo, image de chargement** : toujours dans `Resources/ArtLibrary` (`menuBackground`, `logo`,
  `loadingBackground`).
- **Échelle / ordre d'affichage** : `MenuPanelSettings.asset`.
  - Référence 1920×1080.
  - `Sort Order` = 100 : au-dessus du HUD uGUI.
  - L'écran de chargement a un ordre de 1000 dans le panel : il passe devant le menu pause.

Règles USS (sous-ensemble de CSS) à respecter :

- **Interdit** : `gap`, `z-index`, `box-shadow`, `filter`, `outline`, les dégradés, `:first-child` / `:nth-child`, les sélecteurs `[attr]`.
- **À la place** :
  - des marges sur les enfants ;
  - l'ordre des éléments dans l'UXML ;
  - des classes explicites.
- Pas de `style="…"` dans les UXML : tout passe par les feuilles USS.
- Une transition se déclare sur la classe de base, pas sur `:hover`.

---

## 5. Ajouter des fonctionnalités

### Un bouton au menu principal
1. `MainMenu.uxml` : ajouter `<ui:Button name="creditsButton" text="Crédits" class="menu-entry menu-entry--large" />` dans `homeMenu`.
2. `MainMenuController.Bind` : `Listen(root, "creditsButton", OpenCredits);`. Le son et la navigation manette sont automatiques.

### Une option
Une ligne dans `OptionsScreen.Build()`, dans l'onglet voulu. La valeur est appliquée tout de suite
(`GameSettings.Apply`) et sauvegardée à la fermeture.
```csharp
AddToggle("Afficher les FPS", "Compteur d'images par seconde en haut à droite.",
    () => GameSettings.ShowFps, v => GameSettings.ShowFps = v);
AddSlider("Taille de l'interface", "…", 0.8f, 1.2f, 0.05f, () => GameSettings.UiScale, v => GameSettings.UiScale = v, v => $"×{v:0.00}");
AddChoice("Langue", "…", new[] { "Français", "English" }, () => GameSettings.Language, i => GameSettings.Language = i);
```
Nouvel onglet : `AddTab("Accessibilité");` avant ses lignes. Nouveau type de ligne :
1. créer un template `Templates/OptionXxx.uxml` ;
2. ajouter un champ dans `MenuUiAssets` ;
3. l'ajouter à `MenuUiBuilder.EnsureAssets` ;
4. écrire une méthode `AddXxx` sur le modèle de `AddToggle`.

### Une page du journal
1. `Journal.uxml` : un `<ui:Button name="secretsTab" text="Secrets" class="tab" />` dans la barre d'onglets.
2. `JournalScreen` :
   - ajouter `Secrets` à l'enum `Tab` ;
   - ajouter le nom `"secretsTab"` dans le tableau du constructeur, au même rang que dans l'enum ;
   - ajouter un cas `Tab.Secrets => SecretsPage()` dans `Show` ;
   - écrire la page sous forme de texte enrichi : `<b>`, `<i>`, `<color=#hex>`, `<size=120%>`, `<alpha=#80>`.

### Un nouvel écran (ex. crédits, sélection de carte)
1. Créer `UI/Menus/Credits.uxml`, avec `<ui:Style src="Styles/Menus.uss" />` et un racine `class="modal-screen overlay"`.
   Réutiliser `.panel`, `.panel__title`, `.menu-entry`.
2. L'inclure dans `MainMenu.uxml` :
   `<ui:Template name="Credits" src="Credits.uxml" />`, puis `<ui:Instance template="Credits" name="credits" class="screen hidden" />`.
3. Écrire une classe sur le modèle de `JournalScreen` :
   - un constructeur qui reçoit l'élément ;
   - `MenuUi.Require<…>(root, "nom")` pour chaque élément utile ;
   - `Open(Action closed)` / `Close()` via `MenuUi.Show` / `MenuUi.Hide` ;
   - `MenuSounds.Register(root)`.
4. Dans `MainMenuController`, la créer dans `Bind` et l'ouvrir depuis un bouton :
   `ShowHome(false); credits.Open(() => ShowHome(true, creditsButton));`.
   Gérer `Échap` dans `Update`, comme pour le journal.

Pour un écran affiché **en jeu** (au-dessus du HUD), deux options :
- l'ajouter comme Instance dans `PauseMenu.uxml` ;
- ou créer un nouveau `UIDocument` sur `MenuPanelSettings` avec un `HudWidget` dédié (modèle : `PauseMenu` + `HudBuilder.BuildPauseMenu`).

### Un élément répété (liste de joueurs, objets…)
1. Créer un template `Templates/Xxx.uxml` dont la racine unique porte les classes.
2. Ajouter le champ correspondant dans `MenuUiAssets` et `MenuUiBuilder`.
3. Cloner avec `MenuUi.Clone(assets.xxx, parent)` : renvoie la racine, déjà câblée pour les sons.
4. Encapsuler l'accès aux sous-éléments dans une petite classe vue (modèle : `LobbyCardView`).

### Images venant des données du jeu
`MenuUi.SetImage(element, sprite)` pour les icônes, portraits et l'art de l'ArtLibrary. C'est le seul cas où le code
écrit un style en ligne, avec la couleur de repli des cartes. Tout le reste passe par des classes USS
(`EnableInClassList`).

---

## 6. Comportements à connaître

- **Navigation clavier / manette** :
  - les flèches et le stick déplacent le focus ;
  - Entrée / A valide ;
  - Échap / B revient en arrière (`MenuUi.BackPressed`) ;
  - Tab / LB-RB changent d'onglet dans les options ;
  - chaque écran met le focus sur son premier bouton à l'ouverture (`MenuUi.Focus` / `FocusFirst`).
- **Sons** : tous les `Button` enregistrés via `MenuSounds.Register` / `MenuUi.Clone` jouent `uiHover` au survol ou au
  focus, et `uiClick` au clic.
- **Pause** :
  - en solo, `timeScale = 0` ; les transitions UI Toolkit utilisent le temps réel et restent animées ;
  - Échap pendant une réassignation de touche ne ferme pas le menu (`GameInput.MenuKeysBlocked`).
- **Aperçu 3D du personnage** : un studio (caméra, lumières, socle) est créé loin sous la scène.
  - Il ne rend que pendant que le salon est affiché.
  - Ses réglages sont sur le composant `CharacterPreview`, créé sous l'objet `MainMenu` au lancement.
- **Saisie et uGUI** : l'`EventSystem` de la scène sert aux deux systèmes. Un élément visible et cliquable de UI
  Toolkit capte la souris avant le HUD uGUI : un écran caché doit porter `hidden`, et les décors non interactifs
  `picking-mode="Ignore"`.
