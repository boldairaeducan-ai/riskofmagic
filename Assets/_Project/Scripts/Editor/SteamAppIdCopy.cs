using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Development / test App ID (480): copies steam_appid.txt next to the built executable so the build talks to Steam
    /// without being launched from Steam. Never copied for a real App ID in a release build (Steam provides it).
    /// </summary>
    public sealed class SteamAppIdCopy : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneLinux64 && target != BuildTarget.StandaloneOSX)
                return;
            string source = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
            if (!File.Exists(source))
                return;
            bool testAppId = File.ReadAllText(source).Trim() == "480";
            bool development = (report.summary.options & BuildOptions.Development) != 0;
            if (!testAppId && !development)
                return;
            string folder = target == BuildTarget.StandaloneOSX ? report.summary.outputPath : Path.GetDirectoryName(report.summary.outputPath);
            if (string.IsNullOrEmpty(folder))
                return;
            File.Copy(source, Path.Combine(folder, "steam_appid.txt"), true);
            Debug.Log($"[Steam] steam_appid.txt copied to {folder}.");
        }
    }
}
