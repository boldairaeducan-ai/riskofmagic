using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Systems.Useful
{
    public class UsefulLogEditorWindow : EditorWindow
    {
        [MenuItem("Window/Alek Games/" + PlacerDeafultsData.productName + "/Useful Logs #&l")]
        public static void showWindow()
        {
            GetWindow<UsefulLogEditorWindow>("Useful Logs");
        }

        string[] tabs = new string[] { "distance"};

        static int selectedtab = 0;

        private void OnGUI()
        {
            GUILayout.Space(10);

            selectedtab = GUILayout.SelectionGrid(selectedtab, tabs, 1);

            switch (selectedtab)
            {
                case 0:
                    drawDistance();
                    break;
            }

        }

        private Transform t1;
        private Transform t2;

        private void drawDistance()
        {
            t1 = EditorGUILayout.ObjectField("transform 1: ", t1, typeof(Transform), true) as Transform;
            if(t1 == null)
            {
                if (Selection.gameObjects.Length > 0) t1 = Selection.gameObjects[0].transform;
            }
            t2 = EditorGUILayout.ObjectField("transform 2: ", t2, typeof(Transform), true) as Transform;
            if (t2 == null)
            {
                if (Selection.gameObjects.Length > 1) t2 = Selection.gameObjects[1].transform;
            }

            GUILayout.Space(20);

            if (t1 != null && t2 != null)
            {
                GUILayout.Label("distance between these transforms is: " + Vector3.Distance(t1.position, t2.position));
                GUILayout.Space(10);
                if (GUILayout.Button("make transforms null")) t1 = t2 = null;
            }
            else EditorGUILayout.HelpBox("you have to select 2 points to continue", MessageType.Error);
        }
    }
}
