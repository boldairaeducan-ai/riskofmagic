using System.Collections.Generic;
using RiskOfMagic.Maps;
using RiskOfMagic.Run;
using RiskOfMagic.Run.Secrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Level design helpers for map secrets: adds a MapSecret to the open map with starter SpawnSpots
    /// (1 cyan sphere = fragment hiding place, magenta = altar candidate) scattered on the NavMesh, to move by hand.
    /// </summary>
    public static class SecretTools
    {
        private const string EnemiesFolder = "Assets/_Project/Configs/Enemies";

        // Placeholder secret bosses until each map gets its own (existing bosses, tougher).
        private static readonly Dictionary<string, (string card, string title)> DefaultBosses = new()
        {
            { "Map_GrimForest", ("SpawnCard_Boss_SpiderQueen", "Gardienne des ronces") },
            { "Map_Necropolis", ("SpawnCard_Boss_BoneColossus", "Roi des ossements") },
            { "Map_MountainPeak", ("SpawnCard_Boss_EmberDrake", "Drake des cimes") },
            { "Map_VolcanoTomb", ("SpawnCard_Boss_EmberDrake", "Gardien de la forge") },
            { "Map_ChaosSanctum", ("SpawnCard_Boss_BoneColossus", "Colosse du néant") },
        };

        [MenuItem("Tools/Risk of Magic/Secrets/Add Secret To Open Map")]
        public static void AddToOpenMap()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (Object.FindAnyObjectByType<MapSecret>() != null)
            {
                EditorUtility.DisplayDialog("Secrets", "Cette carte a déjà un MapSecret.", "OK");
                return;
            }
            var bounds = Object.FindAnyObjectByType<StageBounds>();

            var root = new GameObject("Secret");
            Undo.RegisterCreatedObjectUndo(root, "Add map secret");
            var secret = root.AddComponent<MapSecret>();
            if (DefaultBosses.TryGetValue(scene.name, out var boss))
            {
                secret.bossCard = AssetDatabase.LoadAssetAtPath<SpawnCard>($"{EnemiesFolder}/{boss.card}.asset");
                secret.bossTitle = boss.title;
            }

            AddSpots(root.transform, bounds, SpawnSpotType.SecretAltar, "AltarSpot", 3);
            AddSpots(root.transform, bounds, SpawnSpotType.SecretFragment, "FragmentSpot", 10);
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Risk of Magic] Secret added: move the spots (children of « Secret ») into hiding places, then save the scene.");
        }

        private static void AddSpots(Transform parent, StageBounds bounds, SpawnSpotType type, string name, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 position = parent.position + new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
                if (bounds != null && bounds.TryGetRandomPoint(out var point))
                    position = point;
                var go = new GameObject($"{name}{i + 1}");
                go.transform.SetParent(parent, true);
                go.transform.position = position;
                var spot = go.AddComponent<SpawnSpot>();
                var so = new SerializedObject(spot);
                so.FindProperty("type").intValue = (int)type;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
