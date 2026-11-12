using UnityEditor;
using UnityEngine;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Blockout palette: one click drops a piece at the center of the Scene view, snapped to the 1 m grid, under the
    /// selected object (or the map's "Level", whose NavMesh only collects its children). Rotate with the buttons.
    /// </summary>
    public sealed class BlockoutPalette : EditorWindow
    {
        private Vector2 scroll;
        private float snap = 1f;

        [MenuItem("Tools/Risk of Magic/Blockout/Palette")]
        private static void Open() => GetWindow<BlockoutPalette>("Blockout");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Clic = pièce au centre de la vue Scène, alignée sur la grille, rangée sous « Level » (ou sous la sélection).\n"
                                    + "Repères : saut 1,6 m · double saut ~3,2 m · trou franchissable ≤ 4 m (6 m en sprint) · joueur 1,8 m.", MessageType.Info);
            snap = EditorGUILayout.Slider("Grille (m)", snap, 0.25f, 4f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tourner 90°"))
                foreach (var t in Selection.transforms)
                {
                    Undo.RecordObject(t, "Rotate blockout");
                    t.Rotate(0f, 90f, 0f, Space.World);
                }
            if (GUILayout.Button("Aligner sur la grille"))
                foreach (var t in Selection.transforms)
                {
                    Undo.RecordObject(t, "Snap blockout");
                    t.position = Snap(t.position);
                }
            if (GUILayout.Button("Silhouette joueur"))
                Place("Scale_Player", null);
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            BlockoutKit.Kind? current = null;
            foreach (var piece in BlockoutKit.Pieces)
            {
                if (current != piece.Kind)
                {
                    current = piece.Kind;
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(Title(piece.Kind), EditorStyles.boldLabel);
                }
                if (GUILayout.Button(new GUIContent(piece.Name, piece.Description), GUILayout.Height(22f)))
                    Place(piece.Name, piece.Description);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Reconstruire le NavMesh de la carte"))
                MapTools.RebakeOpenMap();
        }

        private static string Title(BlockoutKit.Kind kind) => kind switch
        {
            BlockoutKit.Kind.Floor => "Sols",
            BlockoutKit.Kind.Wall => "Murs",
            BlockoutKit.Kind.Cover => "Couvertures",
            BlockoutKit.Kind.Ramp => "Rampes et escaliers",
            BlockoutKit.Kind.Platform => "Plateformes et ponts",
            _ => "Obstacles"
        };

        private Vector3 Snap(Vector3 p) => new(Mathf.Round(p.x / snap) * snap, Mathf.Round(p.y / snap) * snap, Mathf.Round(p.z / snap) * snap);

        private void Place(string name, string description)
        {
            var prefab = BlockoutKit.Load(name);
            if (prefab == null)
                return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place blockout piece");

            Transform parent = Selection.activeTransform;
            if (parent != null && PrefabUtility.GetCorrespondingObjectFromSource(parent.gameObject) != null && parent.parent != null)
                parent = parent.parent; // a piece is selected: drop next to it, in the same group
            if (parent == null)
            {
                var level = GameObject.Find("Level");
                parent = level != null ? level.transform : null;
            }
            if (parent != null)
                instance.transform.SetParent(parent, true);

            var view = SceneView.lastActiveSceneView;
            Vector3 position = view != null ? view.pivot : Vector3.zero;
            if (view != null && Physics.Raycast(view.camera.transform.position, view.camera.transform.forward, out var hit, 500f))
                position = hit.point; // on whatever is in front of the camera
            instance.transform.position = Snap(position);
            Selection.activeGameObject = instance;
            if (description != null)
                Debug.Log($"[Blockout] {name} : {description}");
        }
    }
}
