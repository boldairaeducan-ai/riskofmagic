using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

using AlekGames.Placer3.Shared;
using AlekGames.Placer3.Editor;

namespace AlekGames.Placer3.Systems.Addons
{
    public class TerrainDetailPopulatorEditorWindow : EditorWindow
    {

        private void OnEnable()
        {
            init();
        }

        public void init()
        {
            deafults = PlacerDeafultsData.getDataInProject();
            if (deafults.terrainDetailPopulatorValues.Count > 0) pValues = deafults.terrainDetailPopulatorValues[0].values;
            else pValues = new TerrainDetailPopulator.values(2);

        }

        private PlacerDeafultsData deafults;

        public TerrainDetailPopulator.values pValues;

        private Terrain terrain;


        private string valuesName = "values";
        private Vector2 scrollPos;

        private void OnGUI()
        {
            if (!PlacerEditorHelper.autoObjectSelectionField(ref terrain)) return;

            if(terrain.terrainData == null)
            {
                EditorGUILayout.HelpBox("no terrain data assighned to the terrain. assighn it to continue", MessageType.Error);
                return;
            }

            if(terrain.terrainData.detailPrototypes.Length == 0)
            {
                EditorGUILayout.HelpBox("no detail prototypes (terrain detail) detected. add a detail prototype to the terrain, to continue", MessageType.Error);
                return;
            }

            TerrainDetailPopulator.values lastV = pValues;

            lastV.detailILayer = EditorGUILayout.IntSlider(new GUIContent("Detail Layer", "index of a detail on details of terrain"), lastV.detailILayer, 0, terrain.terrainData.detailPrototypes.Length - 1);

            Texture2D texDl = terrain.terrainData.detailPrototypes[lastV.detailILayer].prototypeTexture;
            if (texDl != null) GUILayout.Label(new GUIContent(texDl));

            GUILayout.Space(10);

            lastV.density = EditorGUILayout.IntSlider(new GUIContent("Density", ""), lastV.density, 1, 15);

            lastV.zeroChance = EditorGUILayout.Slider(new GUIContent("Zero Chance", ""), lastV.zeroChance, 0, 100);

            lastV.maxNormal = EditorGUILayout.Slider(new GUIContent("Max Normal", ""), lastV.maxNormal, 0, 90);

            GUILayout.Space(10);

            lastV.minmaxHeight = EditorGUILayout.Vector2Field(new GUIContent("Min Max Height", ""), lastV.minmaxHeight);

            PlacerEditorHelper.LayerMaskField(ref lastV.obstacles, new GUIContent("Obstacles"));


            GUILayout.Space(10);

            TerrainLayer[] st = terrain.terrainData.terrainLayers;

            if (st.Length == 0)
            {
                EditorGUILayout.HelpBox("No terrain Layers found", MessageType.Error);
                return;
            }

            GUILayout.Label("Avoided Terrain Layers" + (lastV.avoidedTerrainLayers.Length == 0 ? " (click the '+' button to add)" : ""));
            for (int i = 0; i < lastV.avoidedTerrainLayers.Length; i++)
            {
                lastV.avoidedTerrainLayers[i].avoidedWeightOver = EditorGUILayout.Slider(new GUIContent("Avoid Weight Over Value"), lastV.avoidedTerrainLayers[i].avoidedWeightOver, 0, 1);
                lastV.avoidedTerrainLayers[i].layerIndex = EditorGUILayout.IntSlider(new GUIContent("Layer Index"), lastV.avoidedTerrainLayers[i].layerIndex, 0, st.Length - 1);
                Texture2D texAL = st[lastV.avoidedTerrainLayers[i].layerIndex].diffuseTexture;
                if (texAL != null) GUILayout.Label(new GUIContent(texAL), GUILayout.Width(64), GUILayout.Height(64));

                if (GUILayout.Button("-", GUILayout.Width(60)))
                {
                    List<TerrainDetailPopulator.avoidedTerrainLayer> newStuff = lastV.avoidedTerrainLayers.ToList();
                    newStuff.RemoveAt(i);
                    i--;
                    lastV.avoidedTerrainLayers = newStuff.ToArray();
                }
            }

            if (GUILayout.Button("+", GUILayout.Width(120)))
            {
                List<TerrainDetailPopulator.avoidedTerrainLayer> newStuff = lastV.avoidedTerrainLayers.ToList();
                newStuff.Add(new TerrainDetailPopulator.avoidedTerrainLayer());
                lastV.avoidedTerrainLayers = newStuff.ToArray();
            }

            GUILayout.Space(20);


            if (GUILayout.Button("Populate Layer " + lastV.detailILayer))
            {
                Undo.RegisterCompleteObjectUndo(terrain.terrainData, "terrain data grass layer "  + lastV.detailILayer +  " fill");
                TerrainDetailPopulator.populate(terrain, lastV);
                EditorUtility.SetDirty(terrain.terrainData);
            }

            if (GUILayout.Button("Clear Layer " + lastV.detailILayer))
            {
                Undo.RegisterCompleteObjectUndo(terrain.terrainData, "terrain data grass layer " + lastV.detailILayer + " clear");
                TerrainDetailPopulator.clearTerrainLayerDetail(terrain, lastV);
                EditorUtility.SetDirty(terrain.terrainData);
            }

            PlacerDeafultsData.tdpValues v = new PlacerDeafultsData.tdpValues() { name = valuesName, values = lastV };
            PlacerEditorHelper.deafultPresetsField(ref deafults.terrainDetailPopulatorValues, ref v, ref valuesName, ref scrollPos);
            lastV = v.values;

            if (!pValues.Equals(lastV))
            {
                Undo.RegisterCompleteObjectUndo(this, "terrain detail populator values change");
                pValues = lastV;
            }

        }
    }
}
