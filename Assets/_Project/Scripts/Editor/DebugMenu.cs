using RiskOfMagic.Meta;
using UnityEditor;

namespace RiskOfMagic.Editor
{
    /// <summary>Testing toggles (stored in PlayerPrefs, read in Play mode).</summary>
    public static class DebugMenu
    {
        private const string UnlockAllPath = "Tools/Risk of Magic/Debug/Unlock Everything";

        [MenuItem(UnlockAllPath)]
        private static void ToggleUnlockAll() => MetaProgression.DebugUnlockAll = !MetaProgression.DebugUnlockAll;

        [MenuItem(UnlockAllPath, true)]
        private static bool ToggleUnlockAllValidate()
        {
            Menu.SetChecked(UnlockAllPath, MetaProgression.DebugUnlockAll);
            return true;
        }
    }
}
