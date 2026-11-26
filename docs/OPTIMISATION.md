# Optimisation des cartes : occlusion culling et décor statique

À faire **sur chaque carte `Map_*`**, une fois le décor à peu près figé (à refaire après de gros changements).
Gain : ce qui est caché derrière un mur, un rocher ou un bâtiment n'est plus dessiné, ennemis compris. Gros effet sur
les cartes fermées (Nécropole, Tombe volcanique), plus faible sur les cartes ouvertes.

Rien ne change côté gameplay : l'IA des ennemis tourne sur le serveur et ne dépend pas de ce qui est dessiné.

## 1. Marquer le décor « Static »

1. Ouvre la carte. Dans la *Hierarchy*, sélectionne les objets **qui ne bougent jamais** : sol, terrain, murs, rochers,
   bâtiments, arbres, gros accessoires. Le plus simple : l'objet parent du décor (souvent `Level`).
2. En haut à droite de l'*Inspector*, flèche à côté de la case **Static** → coche :
   - **Occluder Static** : l'objet **cache** ce qu'il y a derrière (murs, rochers, bâtiments, terrain) ;
   - **Occludee Static** : l'objet **peut être caché** (presque tout le décor) ;
   - **Batching Static** : Unity regroupe les objets qui partagent un matériau (moins d'appels de dessin) ;
   - *Contributes GI* / *Navigation* : laisse comme ils sont.
   Unity demande s'il faut l'appliquer aux enfants : **Yes, change children**.
3. **Ne coche jamais Static** sur : le téléporteur, les coffres, les tonneaux, les autels, les points d'apparition,
   tout ce qui a un `NetworkObject`, une animation ou un script qui le déplace, ni sur les effets.
4. Petits objets (herbe, cailloux, petites caisses) : *Occludee Static* oui, *Occluder Static* **non** (trop petits
   pour cacher quoi que ce soit, ils alourdiraient le calcul).
5. Objets transparents ou à trous (feuillages en alpha, grilles, vitres) : **pas Occluder** (on voit à travers).

## 2. Calculer l'occlusion

1. *Window → Rendering → Occlusion Culling*.
2. Onglet **Bake** :
   - **Smallest Occluder** : 3 (m). Plus petit = plus précis mais plus lourd. 3 à 5 pour nos cartes.
   - **Smallest Hole** : 0,25. Le plus petit trou à travers lequel on doit voir (fenêtres, interstices).
   - **Backface Threshold** : 100 (laisse).
3. Clique sur **Bake**. Quelques secondes à quelques minutes selon la carte. Un fichier `OcclusionCullingData` est
   créé à côté de la scène : **commite-le** avec la scène.

## 3. Vérifier

1. Onglet **Visualization** de la fenêtre *Occlusion Culling*, sélectionne la *Main Camera* (ou place la *Scene view*
   en jeu) : dans la *Scene view*, les objets que la caméra ne voit pas disparaissent quand tu te déplaces derrière un
   mur.
2. En Play : **Options → Debug → Afficher les FPS**, et dans le *Game view* le bouton **Stats** : *Batches* et
   *Tris* doivent baisser quand un gros mur cache une partie de la carte.
3. Si un objet disparaît alors qu'il devrait être visible (« popping ») : il est probablement *Occluder* alors qu'on
   voit à travers (feuillage, grille) → décoche *Occluder Static* et refais le *Bake*.

## 4. LOD sur les gros éléments (facultatif)

Pour un élément très détaillé et vu de loin (arbre, statue) : *Add Component → LOD Group*, glisse le modèle détaillé
dans *LOD 0*, une version simplifiée dans *LOD 1* (si le pack en fournit), et laisse *Culled* à ~2 % : l'objet n'est
plus dessiné quand il est minuscule à l'écran.

## Ce que le code fait déjà (aucune action)

- Les ennemis hors écran ne calculent plus leurs animations (os), leurs mouvements procéduraux ni leurs réactions
  aux coups ; ils reprennent dès qu'une caméra les voit (ombres comprises).
- Les effets et projectiles de l'ArtLibrary ne projettent pas d'ombre.
- Réglages URP déjà légers : ombres à 50 m, 1 cascade, pas d'ombres pour les lumières secondaires, pas d'ombres
  douces, SRP Batcher actif.
