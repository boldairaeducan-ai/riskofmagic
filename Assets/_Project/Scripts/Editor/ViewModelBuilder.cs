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
            // Rest poses assume weapons modelled upright (+Y): adjust restEuler in the asset if a model lies differently.
            var right = new Vector3(0.3f, -0.3f, 0.5f);
            var left = new Vector3(-0.3f, -0.3f, 0.5f);
            switch (character)
            {
                case "Archer":
                    Hand(view.offHand, "BowOne", new Vector3(-0.24f, -0.2f, 0.55f), new Vector3(0f, 0f, 8f));
                    Hand(view.mainHand, "ArrowOne", new Vector3(0.2f, -0.24f, 0.45f), new Vector3(85f, -5f, 0f));
                    view.motions = new[] { ViewMotion.BowShot, ViewMotion.BowShot, ViewMotion.Lower, ViewMotion.BowShot };
                    view.alternativeMotions = new[] { ViewMotion.BowShot, ViewMotion.BowShot, ViewMotion.Lower, ViewMotion.BowShot };
                    view.gloveColor = new Color(0.3f, 0.4f, 0.22f);
                    break;
                case "Warrior":
                    Hand(view.mainHand, "SwordOne", right, new Vector3(20f, -10f, -6f));
                    Hand(view.offHand, "ShieldOne", new Vector3(-0.32f, -0.28f, 0.48f), new Vector3(0f, 25f, 0f));
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
                    Hand(view.mainHand, "BluntOne", right, new Vector3(20f, -10f, -6f));
                    Hand(view.offHand, "ShieldTwo", new Vector3(-0.32f, -0.28f, 0.48f), new Vector3(0f, 25f, 0f));
                    view.motions = new[] { ViewMotion.Swing, ViewMotion.Cast, ViewMotion.Guard, ViewMotion.Slam };
                    view.alternativeMotions = new[] { ViewMotion.Swing, ViewMotion.Guard, ViewMotion.Guard, ViewMotion.CastBoth };
                    view.gloveColor = new Color(0.8f, 0.7f, 0.4f);
                    break;
                case "Shadow":
                    Hand(view.mainHand, "SwordThree", right, new Vector3(35f, -15f, -10f), 0.6f);
                    Hand(view.offHand, "SwordThree", left, new Vector3(35f, 15f, 10f), 0.6f);
                    view.motions = new[] { ViewMotion.ThrustAlternate, ViewMotion.Throw, ViewMotion.Lower, ViewMotion.Thrust };
                    view.alternativeMotions = new[] { ViewMotion.ThrustAlternate, ViewMotion.Throw, ViewMotion.Lower, ViewMotion.Spin };
                    view.gloveColor = new Color(0.15f, 0.15f, 0.2f);
                    break;
            }
        }
    }
}
