using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using UnityEngine;
using UnityEditor;

using AlekGames.Placer3.Profiles;
using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Systems.Addons
{
    public class prefabPalleteSelectorEditorWindow : EditorWindow
    {
        private paletteInfo[] palettes;

        private IPrefabPalleteUser user;

        public static prefabPalleteSelectorEditorWindow showWindow(IPrefabPalleteUser user)
        {
            prefabPalleteSelectorEditorWindow w = GetWindow<prefabPalleteSelectorEditorWindow>("pallete prefab selector");
            w.minSize = new Vector2(230, 250);
            w.init(user);
            w.refreshSoon();
            return w;
        }

        private async void refreshSoon()
        {
            await Task.Yield();
            await Task.Yield();
            await Task.Yield();

            if (this != null)
                refreshPalletes();
        }

        private void init(IPrefabPalleteUser user)
        {
            if (user == null) Debug.LogError("provided null user in the prefab palette selector");
            this.user = user;
            refreshPalletes();
        }

        public void addToOnChange(call c) => onChange += c;

        Vector2 scrollPos;

        indexInfo[] indexPreviews;

        string[] options = new string[] { "palettes", "palette indexes" };
        int curSelected;

        public delegate void call();
        private call onChange;


        private void OnGUI()
        {
            GUI.enabled = false;
            if (user == null || user.Equals(null))
            {
                EditorGUILayout.HelpBox("no user found. \n reopen window to make it work", MessageType.Error);
                return;
            }
            GUI.enabled = true;

            GUILayout.Space(5);

            EditorGUI.BeginChangeCheck();

            if (GUILayout.Button("refreshPalettes")) refreshPalletes();

            GUILayout.Space(10);

            curSelected = GUILayout.SelectionGrid(curSelected, options, 2);

            GUILayout.Space(20);

            switch (curSelected)
            {
                case 0:
                    drawPalettes();
                    break;
                case 1:
                    drawIndexes();
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorWindow window = (user as EditorWindow);
                if (window != null) window.Repaint();
                onChange?.Invoke();
            }

        }

        private void drawPalettes()
        {
            if (palettes.Length == 0)
            {
                EditorGUILayout.HelpBox("no paletts found in project. \ncreate one by right clicking and creating a prefab palette (Create/Alek Games/Profiles/Prefab Palette)", MessageType.Error);

                return;
            }
            else
            {

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                int selected = GUILayout.SelectionGrid(-1, palettes.Select(t => new GUIContent(t.palette.name, t.thumbnail, "switch to this palette")).ToArray(), Mathf.Clamp((int)(position.width / 250), 1, palettes.Length));
                EditorGUILayout.EndScrollView();

                if (selected != -1)
                {
                    curSelected = 1; // set to indexes tab
                    prefabPalette p = palettes[selected].palette;
                    user.setPalette(p);
                    setIndexPreviews(user.getPalette());
                    refreshThumbnailsSoon();
                }

            }
        }

        private async void refreshThumbnailsSoon()
        {
            await Task.Yield();
            await Task.Delay(100);
            await Task.Yield();
            setIndexPreviews(user.getPalette());
        }

        private void drawIndexes()
        {
            if (indexPreviews == null || indexPreviews.Length == 0)
            {
                EditorGUILayout.HelpBox("no registered indexes. \n if you didnt fill in palette in painter, do so, or try refreshing palettes. \n to select a palette go to palette tab, and select one", MessageType.Error);
                if (GUILayout.Button("go to palette selection tab")) curSelected = 0;
                return;
            }

            if (GUILayout.Button("refresh indexes Thumbnails")) setIndexPreviews(user.getPalette());

            GUILayout.Space(10);

            if (GUILayout.Button(new GUIContent("un-index", "when spawning, will choose random, insted of a specific index"))) user.setSpecificIndex(-1);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            int selected = GUILayout.SelectionGrid(-1, indexPreviews.Select(t => new GUIContent(t.name, t.thumbnail, "switch to this index")).ToArray(), Mathf.Clamp((int)(position.width / 250), 1, indexPreviews.Length));
            EditorGUILayout.EndScrollView();

            if (selected != -1)
            {
                user.setSpecificIndex(selected);
            }

        }

        /// <summary>
        /// refreshes palettes available to choose from
        /// </summary>
        public void refreshPalletes()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(prefabPalette).Name);
            int len = guids.Length;
            List<paletteInfo> paletteList = new List<paletteInfo>();

            if (len == 0)
            {
                Debug.Log("no prefab palettes found in project");
            }

            for (int i = 0; i < len; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                prefabPalette palette = AssetDatabase.LoadAssetAtPath<prefabPalette>(path);

                if (palette == null) // in case another type is named the same in another namespace
                {
                    //Debug.LogError("palette on the path:" + path + " seems to be corrupted");
                    continue;
                }

                Texture t = null;

                if (palette.prefabs == null || palette.prefabs.Length == 0)
                {
                    Debug.LogWarning("no prefabs asighned in palette " + palette.name + " couldnt load any thumbnail, and this palette will not work");
                    continue;
                }
                else t = getPrefabPreview(palette.prefabs[0].prefab);

                paletteList.Add(new paletteInfo(palette, t));
            }

            palettes = paletteList.ToArray();

            setIndexPreviews(user.getPalette());
        }

        public void setIndexPreviews(prefabPalette p)
        {
            if (p == null)
            {
                Debug.LogWarning("couldnt refresh palette index previews, palette it is null");
                return;
            }

            indexPreviews = new indexInfo[p.prefabs.Length];

            for (int i = 0; i < indexPreviews.Length; i++)
            {
                GameObject prefab = p.prefabs[i].prefab;
                if (prefab == null)
                {
                    Debug.LogError("null prefab in a palette (index: " + i + ")");
                    continue;
                }

                indexPreviews[i] = new indexInfo(prefab.name, getPrefabPreview(prefab));
            }

        }

        private Texture2D getPrefabPreview(GameObject obj)
        {
            if (obj == null) return null;

            Texture2D t = AssetPreview.GetAssetPreview(obj);
            if (t == null) t = AssetPreview.GetMiniThumbnail(obj);
            return t;
        }

        public struct paletteInfo
        {
            public prefabPalette palette;
            public Texture thumbnail;

            public paletteInfo(prefabPalette palette, Texture thumbnail)
            {
                this.palette = palette;
                this.thumbnail = thumbnail;
            }
        }

        public struct indexInfo
        {
            public string name;
            public Texture2D thumbnail;

            public indexInfo(string name, Texture2D thumbnail)
            {
                this.name = name;
                this.thumbnail = thumbnail;
            }
        }
    }
}
