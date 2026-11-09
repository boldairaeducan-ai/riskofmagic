using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

using AlekGames.Placer3.Profiles;
using AlekGames.Placer3.Systems.Main;

namespace AlekGames.Placer3.Shared
{
    [CreateAssetMenu(menuName = "Alek Games/" + productName + "/Data/Placer Deafults", fileName = "new Placer Deafults")]
    public class PlacerDeafultsData : ScriptableObject
    {
        public const string productName = "Placer 3";

        [System.Serializable]
        public enum selectM { Selected, DragIn};

        public string SavePath = deafultSavePath;
        [Space]
        public Color WorkColor = deafultWorkColor;
        public selectM selectMethod = deafultSelectMethod;
        public Rect WindowPos = deafultWindowPos;
        [Space]
        public bool debugInfoMessages = false;
        public bool closeManagerAfterSelectingWindow = true;
        [Space]
        public LayerMask GroundLayerMask = deafultGroundLayerMask;
        public LayerMask AvoidedLayerMask = deafultAvoidedLayerMask;
        [Space]
        public prefabPalette PrefabPalette = null;
        public textFont TextFont = null;

        [Space]
        public List<pValues> painterVaues = new List<pValues>();
        public List<ppValues> physicsPainterValues = new List<ppValues>();
        public List<bcValues> bezierCurvesValues = new List<bcValues>();
        public List<tdpValues> terrainDetailPopulatorValues = new List<tdpValues>();
        public List<tsValues> textSpawnerValues = new List<tsValues>();
        public List<nsValues> noiseSpawnerValues = new List<nsValues>();
        public List<csValues> colorSpawnerValues = new List<csValues>();

        [Space(20)]
        public bool showWelcomeScreen = true;
        [HideInInspector]
        public bool projectOpened;

        [SerializeField]
        [HideInInspector]
        private int applicationClosesSinceAdValue;
        public int applicationClosesSinceAd 
        { 
            get { return applicationClosesSinceAdValue; }
            set 
            {
                if (!projectOpened)
                {
                    applicationClosesSinceAdValue = value;
                }
            }
        }
         


        [ContextMenu("apply Deafult settings (doesnt change arrays)")]
        public void applyDeafultSettings()
        {
            SavePath = deafultSavePath;
            WorkColor = deafultWorkColor;
            GroundLayerMask = deafultGroundLayerMask;
            AvoidedLayerMask = deafultAvoidedLayerMask;
            showWelcomeScreen = true;
            selectMethod = deafultSelectMethod;
            debugInfoMessages = false;
        }

        public static string deafultSavePath = "Assets/Alek Games/" + productName + "/User Data/Profiles/";
        public static Color deafultWorkColor = Color.green;
        public static LayerMask deafultGroundLayerMask = 1 << 0;
        public static LayerMask deafultAvoidedLayerMask = 0;
        public static selectM deafultSelectMethod = selectM.Selected;
        public static Rect deafultWindowPos = new Rect(0, 0, 400, 600);

        public static PlacerDeafultsData instance;
        //{ 
        //    get 
        //    {
        //        if (instance == null) Debug.LogError("Placer Deafults instance not assighned. please report this (the hole error message) to me (Alek Games)");
        //        return instance;
        //    }
        //    private set 
        //    {
        //        instance = value; 
        //    } 
        //}

        /// <summary>
        /// gets data from previous serches, if none were made, first searches for one, then returns the result
        /// </summary>
        /// <returns></returns>
        public static PlacerDeafultsData getDataInProject(bool ignoreNotFoundError = false)
        {
            if(instance == null) return findDataInProject(ignoreNotFoundError);

            return instance;
        }

        public static PlacerDeafultsData findDataInProject(bool ignoreNotFoundError = false)
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(PlacerDeafultsData).Name);
            PlacerDeafultsData[] datas = new PlacerDeafultsData[guids.Length];

            if (datas.Length == 0)
            {
                if(!ignoreNotFoundError) Debug.LogError("no Placer Defults found in project");
                return null;
            }
            else if (datas.Length > 1) Debug.LogWarning("multiple PlacerStaticData's Found. only one will be used, so you can deleate the rest");         

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            PlacerDeafultsData data = AssetDatabase.LoadAssetAtPath<PlacerDeafultsData>(path);
            instance = data;
            return data;
#else
            return new PlacerDeafultsData();
#endif
        }

        public interface valueHold
        {
            string getName();
        }


        [System.Serializable]
        public struct pValues : valueHold
        {
            public string name;
            public PainterValues values;
            public string getName() { return name; }
        }
        [System.Serializable]
        public struct ppValues : valueHold
        {
            public string name;
            public PhysicsPainterValues values;
            public string getName() { return name; }
        }

        [System.Serializable]
        public struct bcValues : valueHold
        {
            public string name;
            public BezierCurveValues.values values;
            public BezierCurveValues.editValues editValues;
            public string getName() { return name; }
        }
        [System.Serializable]
        public struct tdpValues : valueHold
        {
            public string name;
            public TerrainDetailPopulator.values values;
            public string getName() { return name; }
        }
        [System.Serializable]
        public struct tsValues : valueHold
        {
            public string name;
            public TextSpawnerValues.values values;
            public string getName() { return name; }
        }
        [System.Serializable]
        public struct nsValues : valueHold
        {
            public string name;
            public NoiseSpawnerValues.values values;
            public NoiseSpawnerValues.noiseSettings noiseSettings;
            public string getName() { return name; }
        }
        [System.Serializable]
        public struct csValues : valueHold
        {
            public string name;
            public ColorSpawnerValues.cValues values;
            public string getName() { return name; }
        }
    }
}
