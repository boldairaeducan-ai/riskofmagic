using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

using AlekGames.Placer3.Systems.Main;
using AlekGames.Placer3.Systems.Addons;
using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Editor
{
    [CustomEditor(typeof(PoissonSamplingSpawner))]
    public class PoissonSamplingSpawnerEditor : UnityEditor.Editor
    {


        public override void OnInspectorGUI()
        {
            PoissonSamplingSpawner p = (PoissonSamplingSpawner)target;

            if (GUILayout.Button("open palette selector"))
                prefabPalleteSelectorEditorWindow.showWindow(p);

            DrawDefaultInspector();

            GUILayout.Space(20);

            PlacerEditorHelper.spawnerHeightWarning();

            GUILayout.Space(10);

            if (GUILayout.Button("Spawn")) p.spawn();

            GUILayout.Space(20);

            PlacerEditorHelper.childRemoveField(p.transform);
        }
    }
}
