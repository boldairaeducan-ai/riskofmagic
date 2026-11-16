using UnityEditor;
using UnityEngine;

namespace RiskOfMagic.Editor.Models
{
    [CustomEditor(typeof(CharacterModelRecipe))]
    public class CharacterModelRecipeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var recipe = (CharacterModelRecipe)target;
            EditorGUILayout.HelpBox(
                recipe.HasClips
                    ? "Construit le prefab du modèle (armes, taille) et son Animator Controller, puis le branche sur la cible."
                    : "Pas de clip Idle : le modèle sera animé par code (marche, attaque, mort). Ajoute des clips puis reconstruis pour un vrai Animator.",
                MessageType.Info);
            if (GUILayout.Button("Construire le modèle", GUILayout.Height(32)))
            {
                serializedObject.ApplyModifiedProperties();
                var prefab = CharacterModelBuilder.Build(recipe);
                if (prefab != null)
                    EditorGUIUtility.PingObject(prefab);
            }
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
