using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using AlekGames.Placer3.Systems.Main;
using AlekGames.Placer3.Profiles;
using AlekGames.Placer3.Systems.Addons;
using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Editor
{
    [CustomEditor(typeof(GridSpawner))]
    public class GridSpawnerEditor : UnityEditor.Editor
    {


        public override void OnInspectorGUI()
        {
            GridSpawner g = (GridSpawner)target;

            if (GUILayout.Button("open palette selector"))
                prefabPalleteSelectorEditorWindow.showWindow(g);

            DrawDefaultInspector();

            GUILayout.Space(20);

            EditorGUI.BeginChangeCheck();

            g.drawGizmos = EditorGUILayout.Toggle("draw gizmos", g.drawGizmos);
            g.gizmoDistance = EditorGUILayout.FloatField("renderer distance", g.gizmoDistance);


            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(g);

            GUILayout.Space(10);

            PlacerEditorHelper.spawnerHeightWarning();

            GUILayout.Space(10);

            if (GUILayout.Button("Spawn")) g.spawn();

            GUILayout.Space(20);

            PlacerEditorHelper.childRemoveField(g.transform);
        }
    }
}
