using System.Collections.Generic;
using System.Linq;
using System.Text;
using RiskOfMagic.Maps;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using RiskOfMagic.Run.Secrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Checks every map scene: StageBounds, player spawns, NavMesh, and each important SpawnSpot (on the NavMesh,
    /// free space, reachable from the spawn). Report in the console + a summary dialog.
    /// </summary>
    public static class MapValidator
    {
        private const float TeleporterHalfWidth = 2.2f;
        private const float TeleporterHeight = 3f;

        [MenuItem("Tools/Risk of Magic/Validate Maps")]
        public static void ValidateAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            string current = EditorSceneManager.GetActiveScene().path;
            var paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes/Maps" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            int errors = 0, warnings = 0;
            var summary = new StringBuilder();
            foreach (string path in paths)
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var report = new StringBuilder();
                int e = 0, w = 0;
                Validate(report, ref e, ref w);
                errors += e;
                warnings += w;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                summary.AppendLine($"{name} : {(e == 0 ? "OK" : $"{e} erreur(s)")}{(w > 0 ? $", {w} avertissement(s)" : string.Empty)}");
                string text = $"[Risk of Magic] Map check — {name}\n{report}";
                if (e > 0)
                    Debug.LogError(text);
                else if (w > 0)
                    Debug.LogWarning(text);
                else
                    Debug.Log(text);
            }
            if (!string.IsNullOrEmpty(current))
                EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
            EditorUtility.DisplayDialog("Validation des cartes",
                summary + (errors + warnings > 0 ? "\nDétails dans la console." : string.Empty), "OK");
        }

        private static void Validate(StringBuilder report, ref int errors, ref int warnings)
        {
            void Error(string message) { report.AppendLine("  ✖ " + message); }
            void Warn(string message) { report.AppendLine("  ⚠ " + message); }

            if (Object.FindAnyObjectByType<StageBounds>() == null)
            {
                Error("Pas de StageBounds.");
                errors++;
            }
            var spawns = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude);
            if (spawns.Length == 0)
            {
                Error("Pas de PlayerSpawnPoint.");
                errors++;
            }
            else if (!NavMesh.SamplePosition(spawns[0].transform.position, out _, 4f, NavMesh.AllAreas))
            {
                Error("Le spawn joueur n'est pas sur le NavMesh (NavMesh non construit ?).");
                errors++;
                return;
            }

            // Layout variants are all active in the editor: report against every spot.
            var spots = Object.FindObjectsByType<SpawnSpot>(FindObjectsInactive.Exclude);
            int validTeleporters = 0;
            foreach (var spot in spots.Where(s => s.Type == SpawnSpotType.Teleporter))
            {
                string problem = Check(spot.transform.position, true, true);
                if (problem == null)
                    validTeleporters++;
                else
                {
                    Warn($"Téléporteur « {spot.name} » {spot.transform.position} : {problem}.");
                    warnings++;
                }
            }
            if (validTeleporters == 0)
            {
                Error("Aucun emplacement de téléporteur valide (placement aléatoire de secours utilisé).");
                errors++;
            }
            else
                report.AppendLine($"  ✔ {validTeleporters} emplacement(s) de téléporteur valide(s).");

            foreach (var spot in spots.Where(s => s.Type is SpawnSpotType.Merchant or SpawnSpotType.Interactable or SpawnSpotType.SecretAltar))
            {
                string problem = Check(spot.transform.position, spot.Type == SpawnSpotType.SecretAltar, false);
                if (problem == null)
                    continue;
                Warn($"{spot.Type} « {spot.name} » {spot.transform.position} : {problem}.");
                warnings++;
            }
            if (!spots.Any(s => s.Type == SpawnSpotType.Merchant))
            {
                Warn("Pas d'emplacement de marchand (position aléatoire).");
                warnings++;
            }

            var secret = Object.FindAnyObjectByType<MapSecret>();
            if (secret == null)
                report.AppendLine("  · Pas de secret (MapSecret).");
            else
            {
                int fragments = spots.Count(s => s.Type == SpawnSpotType.SecretFragment);
                int altars = spots.Count(s => s.Type == SpawnSpotType.SecretAltar);
                if (fragments < secret.fragmentCount || altars == 0)
                {
                    Error($"Secret : {fragments} cachette(s) de fragment pour {secret.fragmentCount} fragments, {altars} autel(s).");
                    errors++;
                }
                if (secret.bossCard == null)
                {
                    Warn("Secret sans boss (toujours un bonus).");
                    warnings++;
                }
            }
        }

        /// <summary>Null when fine, else the reason.</summary>
        private static string Check(Vector3 position, bool needsClearance, bool teleporter)
        {
            if (!NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas))
                return "hors du NavMesh";
            if (needsClearance && !StageBounds.HasClearance(hit.position, teleporter ? TeleporterHalfWidth : 1.2f, teleporter ? TeleporterHeight : 2.5f))
                return "encastré dans le décor (pas assez d'espace libre)";
            if (!StageBounds.IsReachableFromSpawn(hit.position))
                return "inaccessible à pied depuis le spawn";
            return null;
        }
    }
}
