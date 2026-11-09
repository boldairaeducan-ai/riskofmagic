using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlekGames.Placer3.Shared
{
    
    public static class ObjectGrouper // not used anymore, just thought it would be nice to keep it here
    {
        public static void addObjectToGroup(GameObject obj)
        {
            string groupName = obj.name + " GROUP";

            Transform parent = obj.transform.parent;

            bool grouped = false;
            for(int i = 0; i < parent.childCount; i++)
            {
                Transform group = parent.GetChild(i);
                if (group.name == groupName)
                {
                    obj.name += " (" + group.childCount + ")";
                    obj.transform.parent =group;
                    grouped = true;
                }
            }

            if(!grouped) //create group
            {
                GameObject group = new GameObject(groupName);
                group.transform.parent = parent;
                obj.name += " (0)";
                obj.transform.parent = group.transform;
            }
        }

        public static void groupChildren(Transform t)
        {
            Transform[] startChildren = new Transform[t.childCount];

            for (int i = 0; i < t.childCount; i++)
                startChildren[i] = t.GetChild(i);

            for (int i = 0; i < startChildren.Length; i++)
                addObjectToGroup(startChildren[i].gameObject);
        }
    }
}
