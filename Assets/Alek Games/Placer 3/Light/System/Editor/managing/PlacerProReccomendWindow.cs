using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Editor
{
    public class PlacerProReccomendWindow : EditorWindow
    {
        int r = -1;


        private void OnGUI()
        {
            if(r < 0) r = Random.Range(0, 3);

            if (r == 0)
            {
                GUILayout.Label("hi there, if you like " + PlacerDeafultsData.productName + ", how about giving the PRO version a go");
            }
            else if(r == 1)
            {
                GUILayout.Label("hello fellow dev, if you enjoy using " + PlacerDeafultsData.productName + ", i am quite ceirtain you would like the PRO version.");
            }
            else
            {
                GUILayout.Label("dear dev, if you think " + PlacerDeafultsData.productName + " is a nice tool how about upgrading it to the PRO version? ");
            }

            GUILayout.Space(10);

            GUILayout.Label("Why is " + PlacerDeafultsData.productName + " PRO worth it?");

            GUILayout.Label("It is quite simple really, it is just more and better!\n" +
                "with it you can place with splines physics noise color and so much more!");

            if (GUILayout.Button("Click this to read more")) Application.OpenURL(PlacerEditorHelper.prourl);
        }
    }
}
