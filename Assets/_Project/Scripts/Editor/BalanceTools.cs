using System.Collections.Generic;
using System.IO;
using System.Linq;
using RiskOfMagic.Core;
using UnityEditor;
using UnityEngine;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Configs are seeded once by the builder and then belong to whoever tunes them. These tools bring the builder's
    /// default values back on demand, and create Resources/GameBalance.asset.
    /// </summary>
    public static class BalanceTools
    {
        public const string GameBalancePath = "Assets/_Project/Resources/GameBalance.asset";
        private const string ResetPathsKey = "RiskOfMagic.ResetConfigPaths";
        private const string ResetAllKey = "RiskOfMagic.ResetAllConfigs";

        [MenuItem("Tools/Risk of Magic/Balance/Select Game Balance")]
        private static void SelectGameBalance()
        {
            Selection.activeObject = EnsureGameBalance();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Tools/Risk of Magic/Balance/Reset Selected Configs To Builder Defaults")]
        private static void ResetSelected()
        {
            var paths = Selection.objects.OfType<ScriptableObject>().Select(AssetDatabase.GetAssetPath).Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("Équilibrage", "Sélectionne un ou plusieurs assets de configuration (compétence, ennemi, attaque…).", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Équilibrage", $"Remettre {paths.Count} asset(s) aux valeurs du générateur ?\nTes réglages sur ces assets seront perdus.", "Remettre et générer", "Annuler"))
                return;
            var marked = ReadMarks();
            marked.UnionWith(paths);
            EditorPrefs.SetString(ResetPathsKey, string.Join("|", marked));
            PrototypeSceneBuilder.Build();
        }

        [MenuItem("Tools/Risk of Magic/Balance/Rebuild With Builder Defaults (all configs)")]
        private static void ResetAll()
        {
            if (!EditorUtility.DisplayDialog("Équilibrage", "Remettre TOUS les assets de configuration aux valeurs du générateur, puis générer ?\nTous tes réglages (compétences, objets, ennemis, attaques…) seront perdus. GameBalance n'est pas touché.", "Tout remettre", "Annuler"))
                return;
            SessionState.SetBool(ResetAllKey, true);
            PrototypeSceneBuilder.Build();
        }

        public static bool IsMarkedForReset(string path) => SessionState.GetBool(ResetAllKey, false) || ReadMarks().Contains(path);

        public static void ClearResetMarks()
        {
            SessionState.SetBool(ResetAllKey, false);
            EditorPrefs.DeleteKey(ResetPathsKey);
        }

        private static HashSet<string> ReadMarks()
        {
            string value = EditorPrefs.GetString(ResetPathsKey, string.Empty);
            return new HashSet<string>(value.Split('|').Where(p => !string.IsNullOrEmpty(p)));
        }

        /// <summary>Created once with the code defaults, never overwritten.</summary>
        public static GameBalance EnsureGameBalance()
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalance>(GameBalancePath);
            if (balance != null)
                return balance;
            Directory.CreateDirectory(Path.GetDirectoryName(GameBalancePath));
            balance = ScriptableObject.CreateInstance<GameBalance>();
            AssetDatabase.CreateAsset(balance, GameBalancePath);
            AssetDatabase.SaveAssets();
            return balance;
        }
    }
}
