using System.IO;
using RiskOfMagic.Animation;
using UnityEditor;
using UnityEngine;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Creates each character's procedural first-person view once (Configs/ViewModels, never overwritten: poses and
    /// motions are tuned there, live in Play mode). Weapons come from the Medieval Fantasy Weapon Pack when present.
    /// </summary>
    public static class ViewModelBuilder
    {
        private const string Folder = "Assets/_Project/Configs/ViewModels";
        private const string Weapons = "Assets/GameLair/MedievalFantasyWeaponPack/Prefabs/";

        public static ViewModelDefinition GetOrCreate(string characterFile)
        {
            string name = characterFile.Replace("Character_", string.Empty);
            string path = $"{Folder}/ViewModel_{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ViewModelDefinition>(path);
            if (existing != null)
                return existing;
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var view = ScriptableObject.CreateInstance<ViewModelDefinition>();
            Fill(view, name);
            AssetDatabase.CreateAsset(view, path);
            return view;
        }

        private static GameObject Weapon(string prefab) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Weapons}{prefab}.prefab");

        private static void Hand(ViewHand hand, string prefab, Vector3 position, Vector3 euler, float scale = 1f)
        {
            hand.prefab = prefab != null ? Weapon(prefab) : null;
            hand.restPosition = position;
            hand.restEuler = euler;
            hand.scale = scale;
        }

        private static void Fill(ViewModelDefinition view, string character)
        {
            // Measured on the Medieval Fantasy Weapon Pack: grip at the origin, blade / handle along +Y, real-world size
            // (sword 1.2 m, bow 1.46 m with its belly on +X, arrow 0.78 m, shields 0.6-0.8 m thin on Z).
            // Euler (x, y, z): +x tips the weapon forward, +z leans it towards the screen center for the right hand.
            var right = new Vector3(0.26f, -0.3f, 0.5f);
            var left = new Vector3(-0.26f, -0.3f, 0.5f);
            switch (character)
            {
                case "Archer":
                    // Bow upright in the left hand, belly turned away (+X → forward), string towards the player.
                    Hand(view.offHand, "BowOne", new Vector3(-0.2f, -0.22f, 0.6f), new Vector3(0f, -90f, -12f), 0.55f);
                    // Arrow pointing forward from the right hand (+Y → forward).
                    Hand(view.mainHand, "ArrowOne", new Vector3(0.12f, -0.2f, 0.42f), new Vector3(90f, -6f, 0f), 0.7f);
                    view.motions = new[] { ViewMotion.BowShot, ViewMotion.BowShot, ViewMotion.Lower, ViewMotion.BowShot };
                    view.alternativeMotions = new[] { ViewMotion.BowShot, ViewMotion.BowShot, ViewMotion.Lower, ViewMotion.BowShot };
                    view.gloveColor = new Color(0.3f, 0.4f, 0.22f);
                    break;
                case "Warrior":
                    Hand(view.mainHand, "SwordOne", right, new Vector3(50f, -10f, 18f), 0.6f);
                    Hand(view.offHand, "ShieldOne", new Vector3(-0.36f, -0.34f, 0.55f), new Vector3(0f, 20f, 0f), 0.55f);
                    view.motions = new[] { ViewMotion.Swing, ViewMotion.Spin, ViewMotion.Guard, ViewMotion.Slam };
                    view.alternativeMotions = new[] { ViewMotion.Overhead, ViewMotion.Throw, ViewMotion.Guard, ViewMotion.CastBoth };
                    view.gloveColor = new Color(0.45f, 0.42f, 0.4f);
                    break;
                case "Arcanist":
                    Hand(view.mainHand, null, right, Vector3.zero);
                    Hand(view.offHand, null, left, Vector3.zero);
                    view.motions = new[] { ViewMotion.Cast, ViewMotion.CastBoth, ViewMotion.Guard, ViewMotion.CastBoth };
                    view.alternativeMotions = new[] { ViewMotion.Cast, ViewMotion.Cast, ViewMotion.Lower, ViewMotion.CastBoth };
                    view.gloveColor = new Color(0.4f, 0.25f, 0.6f);
                    break;
                case "Druid":
                    Hand(view.mainHand, null, right, Vector3.zero);
                    Hand(view.offHand, null, left, Vector3.zero);
                    view.motions = new[] { ViewMotion.ThrustAlternate, ViewMotion.Cast, ViewMotion.Lower, ViewMotion.Slam };
                    view.alternativeMotions = new[] { ViewMotion.Cast, ViewMotion.Cast, ViewMotion.Slam, ViewMotion.CastBoth };
                    view.gloveColor = new Color(0.25f, 0.45f, 0.2f);
                    break;
                case "Paladin":
                    Hand(view.mainHand, "BluntOne", right, new Vector3(45f, -10f, 15f), 0.7f);
                    Hand(view.offHand, "ShieldTwo", new Vector3(-0.38f, -0.36f, 0.58f), new Vector3(0f, 20f, 0f), 0.45f);
                    view.motions = new[] { ViewMotion.Swing, ViewMotion.Cast, ViewMotion.Guard, ViewMotion.Slam };
                    view.alternativeMotions = new[] { ViewMotion.Swing, ViewMotion.Guard, ViewMotion.Guard, ViewMotion.CastBoth };
                    view.gloveColor = new Color(0.8f, 0.7f, 0.4f);
                    break;
                case "Shadow":
                    // Daggers: the slimmest sword, scaled to ~40 cm, held point forward.
                    Hand(view.mainHand, "SwordThree", right, new Vector3(70f, -8f, 10f), 0.35f);
                    Hand(view.offHand, "SwordThree", left, new Vector3(70f, 8f, -10f), 0.35f);
                    view.motions = new[] { ViewMotion.ThrustAlternate, ViewMotion.Throw, ViewMotion.Lower, ViewMotion.Thrust };
                    view.alternativeMotions = new[] { ViewMotion.ThrustAlternate, ViewMotion.Throw, ViewMotion.Lower, ViewMotion.Spin };
                    view.gloveColor = new Color(0.15f, 0.15f, 0.2f);
                    break;
            }
        }
    }
}
