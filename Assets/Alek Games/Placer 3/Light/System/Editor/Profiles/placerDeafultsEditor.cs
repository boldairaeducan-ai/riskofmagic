using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AlekGames.Placer3.Profiles;
using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Editor
{
    [CustomEditor(typeof(PlacerDeafultsData))]
    public class placerDeafultsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.Space(20);

            bool pro = placerProVersionDetection.isProVersion;
            EditorGUILayout.Toggle("detected PRO version", pro);

            if(pro)
            {
                GUILayout.Label("thanks a lot for buying the pro version, hope you find it helpful\nif you have any suggestions for new placing methods, let me know");
            }
            else
            {
                PlacerDeafultsData data = (PlacerDeafultsData)target;
                GUILayout.Label("PRO version of placer has not been detected\nthere will be an ad once in a while about the PRO version\nhope it wont be a problem, have fun using placer :)");
                EditorGUILayout.IntField("project opens since PRO ad", data.applicationClosesSinceAd);
            }
        }
    }
}
