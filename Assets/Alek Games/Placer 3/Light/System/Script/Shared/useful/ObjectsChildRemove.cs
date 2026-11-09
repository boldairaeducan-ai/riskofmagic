using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ObjectsChildRemove
{
    /// <summary>
    /// abortion for gameObjects
    /// </summary>
    /// <param name="t">the poor parent</param>
    public static void removeChildren(Transform t)
    {
        while (t.childCount > 0)
        {
            GameObject.DestroyImmediate(t.GetChild(0).gameObject);
        }
    }
}
