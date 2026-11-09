using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;

using AlekGames.Placer3.Profiles;

namespace AlekGames.Placer3.Editor
{
    [CustomEditor(typeof(prefabPalette))]
    public class prefabPaletteEditor : UnityEditor.Editor
    {
        private int copyFromIndex = 0;

        public override void OnInspectorGUI()
        {
            prefabPalette pp = (prefabPalette)target;

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(pp.prefabs)));
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("copy settings from index " + copyFromIndex + " to the rest of prefabs"))
            {
                for(int i = 0; i < pp.prefabs.Length; i++)
                {
                    if (i == copyFromIndex) continue;
                    pp.prefabs[i].snapChildren = pp.prefabs[copyFromIndex].snapChildren;
                    pp.prefabs[i].weight = pp.prefabs[copyFromIndex].weight;
                    pp.prefabs[i].minMaxScaleMltp = pp.prefabs[copyFromIndex].minMaxScaleMltp;
                    pp.prefabs[i].normalAllighn = pp.prefabs[copyFromIndex].normalAllighn;
                }

                serializedObject.Update();
                EditorUtility.SetDirty(pp);
            }
            copyFromIndex = Mathf.Clamp(EditorGUILayout.IntField(copyFromIndex, GUILayout.Width(60)), 0, pp.prefabs.Length);
            GUILayout.EndHorizontal();

            GUILayout.Label("Auto Assighn Prefabs (drag prefabs into the array below, to add to the one above)");
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(pp.objects)));
            if(pp.objects.Length > 0)
            {
                List<prefabPalette.objectSettings> os = pp.prefabs.ToList();
                os.AddRange(pp.objects.Select(t => new prefabPalette.objectSettings(t)));
                pp.prefabs = os.ToArray();
                pp.objects = new GameObject[0];
                EditorUtility.SetDirty(pp);
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }

            GUILayout.Space(10);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(pp.spawnAsPrefab)));

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(pp.minMaxScale)));
            if (pp.minMaxScale == Vector2.zero) GUILayout.Label("disabled");
            EditorGUILayout.EndHorizontal();
            if (pp.minMaxScale != Vector2.zero) EditorGUILayout.HelpBox("to disable overiding transform scale, set minMaxScale to v2.zero (0,0)", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(pp.randRotAdd)));

            GUILayout.Space(10);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(pp.maxNormal)));

            GUILayout.Space(10);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(pp.groundLayer)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(pp.avoidedLayer)));

            serializedObject.ApplyModifiedProperties();

            GUILayout.Space(30);
            GUILayout.Label("auto setup", EditorStyles.boldLabel);
            GUILayout.Space(5);

            if(GUILayout.Button("For Precision Spawning (buildins, etc.)"))
            {
                Undo.RegisterCompleteObjectUndo(pp, "auto setup for randomized");

                pp.randRotAdd = Vector3.zero;
                pp.minMaxScale = Vector2.zero; //Vector2.one;
                for(int i = 0; i < pp.prefabs.Length; i++) pp.prefabs[i].normalAllighn = 0;
                pp.maxNormal = 180;

                serializedObject.Update();
                EditorUtility.SetDirty(pp);
            }
            if (GUILayout.Button("For Randomized (mass) Spawning (forests, etc.)"))
            {
                Undo.RegisterCompleteObjectUndo(pp, "auto setup for randomized");

                pp.randRotAdd = new Vector3(0, 180, 0);
                pp.minMaxScale = new Vector2(0.9f, 1.1f);
                //for (int i = 0; i < pp.prefabs.Length; i++) pp.prefabs[i].normalAllighn = 0.5f;
                pp.maxNormal = 45;

                serializedObject.Update();
                EditorUtility.SetDirty(pp);
            }
        }
    }
}
