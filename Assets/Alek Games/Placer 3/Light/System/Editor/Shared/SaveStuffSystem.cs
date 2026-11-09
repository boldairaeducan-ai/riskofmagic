using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Editor
{
    public class SaveStuffSystem
    {
        public static string pickFilePath(string fileName)
        {
            string path;
            string fullPath = EditorUtility.OpenFolderPanel("Select location to save", "Assets/", "");
            path = FileUtil.GetProjectRelativePath(fullPath);
            path += "/" + fileName + ".asset";

            return path;
        }


        public static T CreateScriptibleObject<T>(string fileName, bool focusOnCreated = false, bool chooseDestination = false) where T : ScriptableObject
        {
            T asset = ObjectFactory.CreateInstance<T>();

            string path;
            if (chooseDestination)
            {
                path = pickFilePath(fileName);
            }
            else path = PlacerDeafultsData.getDataInProject().SavePath;

            path += fileName + ".asset";

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (focusOnCreated)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;
            }

            return asset;
        }

        public static void selectObjectOnPath(string path)
        {
            Object asset = AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset));
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
    }
}
