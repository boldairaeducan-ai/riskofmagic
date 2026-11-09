using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace AlekGames.Placer3.Shared
{
    public static class ToolAdder
    {

        public static void addTool<T>() where T : MonoBehaviour
        {
            string name = typeof(T).Name;
            GameObject gs = new GameObject(name+ " Tool");
            gs.AddComponent<T>();

#if UNITY_EDITOR
            EditorUtility.SetDirty(gs);
            Undo.RegisterCreatedObjectUndo(gs, "added " + name + " Tool");
            Selection.objects = new Object[1] { gs };
#endif
        }

    }
}
