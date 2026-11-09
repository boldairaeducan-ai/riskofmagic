using AlekGames.Placer3.Shared;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AlekGames.Placer3.Editor
{
    public static class placerProVersionDetection
    {
        public static bool isProVersion { get; private set; } = false;

        public static void Refresh()
        {
            isProVersion = System.IO.Directory.Exists(PlacerEditorHelper.proFolderPath);
            //Debug.Log(isProVersion);
        }
    }

}
