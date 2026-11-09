using System.Collections;
using System.Collections.Generic;
using System;

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using AlekGames.Placer3.Shared;
using AlekGames.Placer3.Systems.Main;
using AlekGames.Placer3.Systems.Addons;
using AlekGames.Placer3.Systems.Useful;
using AlekGames.Placer3.Profiles;


namespace AlekGames.Placer3.Editor
{

    public static class PlacerEditorHelper
    {

        #region info

        public const string proFolderPath = "Assets/Alek Games/" + PlacerDeafultsData.productName + "/Pro";
        public const string lightFolderPath = "Assets/Alek Games/" + PlacerDeafultsData.productName + "/Light";
        public const string docksFilePath = lightFolderPath + "/Documentation/Placer Documentation.txt";

        public const string AlekGamesToolEntryMenuItem = "Tools/Alek Games/" + PlacerDeafultsData.productName + "/";
        public const string runtimeToolMenuItem = AlekGamesToolEntryMenuItem + "Runtime/";
        public const string EditorToolMenuItem = AlekGamesToolEntryMenuItem + "Editor/";
        public const string lighturl = "https://assetstore.unity.com/packages/slug/284158";
        public const string prourl = "https://assetstore.unity.com/packages/slug/283376";

        #endregion



        public static void openPlacerInBrowser(bool reviewTab)
        {
            Application.OpenURL((placerProVersionDetection.isProVersion? prourl : lighturl) + (reviewTab ? "#reviews" : ""));
        }

        #region field drawers

        public static void deafultPresetsField<T>(ref List<T> values, ref T current, ref string currentAdd, ref Vector2 scrollPos) where T : PlacerDeafultsData.valueHold
        {
            PlacerDeafultsData deafults = PlacerDeafultsData.getDataInProject();
            GUILayout.Space(20);
            GUILayout.Label("Placer Deafuts values presets (click to copy values. add a preset at the bottom of the window): ", EditorStyles.boldLabel);

            if (values.Count > 0)
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                for (int i = 0; i < values.Count; i++)
                {
                    GUILayout.Space(5);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(values[i].getName()))
                    {
                        current = values[i];
                        Debug.Log("copied values from: " + i);
                    }

                    if (GUILayout.Button("Overright", GUILayout.Width(80)))
                    {
                        Undo.RegisterCompleteObjectUndo(deafults, "painter values change");
                        values[i] = current;
                        EditorUtility.SetDirty(deafults);
                        Debug.Log("values: " + i + " in the Placer Deafults have been overritten");
                    }

                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
            else GUILayout.Label("no deafults found. click the button at the bottom of the window to add one");

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Values to Placer Deafults List"))
            {
                values.Add(current);
                EditorUtility.SetDirty(deafults);
            }
            currentAdd = EditorGUILayout.TextField(currentAdd);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        public static void childRemoveField(Transform t)
        {
            GUILayout.Space(15);

            if (GUILayout.Button("revert (deleate children)"))
            {
                ObjectsChildRemove.removeChildren(t);
            }
        }

        public static void prefabPaletteField(ref prefabPalette palette, ref int specificIndex, IPrefabPalleteUser user, prefabPalleteSelectorEditorWindow.call onChange)
        {
            prefabPaletteField(ref palette, user, onChange);

            specificIndex = EditorGUILayout.IntField(new GUIContent("Specific Index", ""), specificIndex);
            if (specificIndex >= palette.prefabs.Length) specificIndex = palette.prefabs.Length - 1;
        }

        public static void prefabPaletteField(ref prefabPalette palette, IPrefabPalleteUser user, prefabPalleteSelectorEditorWindow.call onChange)
        {
            if (GUILayout.Button("open palette selector"))
            {
                prefabPalleteSelectorEditorWindow w = prefabPalleteSelectorEditorWindow.showWindow(user);
                w.addToOnChange(onChange);
            }

            palette = EditorGUILayout.ObjectField(new GUIContent("Palette"), palette, typeof(prefabPalette), false) as prefabPalette;

            if (palette == null)
            {
                EditorGUILayout.HelpBox("Pallete not found", MessageType.Error);
                return;
            }
        }

        public static void LayerMaskField(ref LayerMask layerMask, GUIContent content)
        {
            LayerMask tempMask = EditorGUILayout.MaskField(content, InternalEditorUtility.LayerMaskToConcatenatedLayersMask(layerMask), InternalEditorUtility.layers);
            layerMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
        }

        /// <summary>
        /// returns if have something selected
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool autoObjectSelectionField<T>(ref T t) where T : UnityEngine.Object
        {
            PlacerDeafultsData data = PlacerDeafultsData.findDataInProject();

            switch (data.selectMethod)
            {
                case PlacerDeafultsData.selectM.Selected:
                    return autoObjectSelectionFieldMSelected(ref t);
                case PlacerDeafultsData.selectM.DragIn:
                    return autoObjectSelectionFieldMDragIn(ref t);
            }

            return false;
        }

        public static bool autoObjectSelectionFieldMSelected<T>(ref T t) where T : UnityEngine.Object
        {
            GUI.enabled = false;
            EditorGUILayout.ObjectField(new GUIContent("selected: "), t, typeof(T), true);
            GameObject selected = Selection.activeGameObject;
            GUI.enabled = true;

            bool selectingTransform = typeof(T) == typeof(Transform);

            if (selected != null)
            {
                if (selectingTransform)
                    t = selected.transform as T;
                else
                {
                    T a = selected.GetComponent<T>();
                    if (a != null) t = a;
                }
            }
            else if (t == null)
            {
                if (selectingTransform)
                {
                    EditorGUILayout.HelpBox("Select any object to act as parent (Placed Object Holder for spawned objects, or click on the button below", MessageType.Error);
                    if (GUILayout.Button("create new gameObject as parent"))
                    {
                        GameObject created = new GameObject("Placer Object Holder");
                        t = created.transform as T;
                        Selection.activeGameObject = created;
                    }
                }
                else EditorGUILayout.HelpBox("Select an object with a component on it of type: " + typeof(T).Name + " to continue", MessageType.Error);
            }


            return t != null;
        }

        public static bool autoObjectSelectionFieldMDragIn<T>(ref T t) where T : UnityEngine.Object
        {
            t = EditorGUILayout.ObjectField(new GUIContent("selected: "), t, typeof(T), true) as T;

            bool selectingTransform = typeof(T) == typeof(Transform);
            if (t == null)
            {
                if (selectingTransform)
                {
                    EditorGUILayout.HelpBox("Select any object to act as parent (Placed Object Holder for spawned objects, or click on the button below", MessageType.Error);
                    if (GUILayout.Button("create new gameObject as parent"))
                    {
                        GameObject created = new GameObject("Placer Object Holder");
                        t = created.transform as T;
                        Selection.activeGameObject = created;
                    }
                }
                else EditorGUILayout.HelpBox("drag  an object with a component on it of type: " + typeof(T).Name + " into the field above to continue", MessageType.Error);
            }

            return t != null;
        }

        #endregion

        #region func

        public static void doARepaint(EditorWindow w)
        {
            w.Repaint();
            SceneView.RepaintAll();
        }

        public static void spawnerHeightWarning()
        {
            EditorGUILayout.HelpBox("if spawning on a surface, make sure the spawner is at least 0.1 unit of distance above the targeted surface (under the gizmos)", MessageType.Warning);
        }
        #endregion

        #region open windows

        public static void OpenWindow<T>() where T : EditorWindow
        {
            EditorWindow ew = EditorWindow.GetWindow<T>(typeof(T).Name);

            PlacerDeafultsData data = PlacerDeafultsData.findDataInProject();

            Rect pos = data.WindowPos;
            ew.position = pos;
        }

        [MenuItem(EditorToolMenuItem + "Painter")]
        private static void openPainter() => OpenWindow<PainterEditorWindow>();

        [MenuItem(EditorToolMenuItem + "Terrain Details Populator")]
        private static void openTerrainDetail() => OpenWindow<TerrainDetailPopulatorEditorWindow>();

        [MenuItem(runtimeToolMenuItem + "Grid Spawner")]
        public static void addGrid() => ToolAdder.addTool<GridSpawner>();

        [MenuItem(runtimeToolMenuItem + "Poisson Sampling Spawner")]
        public static void addPossion() => ToolAdder.addTool<PoissonSamplingSpawner>();

        #endregion
    }
}
