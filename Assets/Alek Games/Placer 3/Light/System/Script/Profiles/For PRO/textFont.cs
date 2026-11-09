using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Profiles
{
    [CreateAssetMenu(menuName = "Alek Games/" + PlacerDeafultsData.productName + "/Profiles/text Font", fileName = "new 3DText Font")]
    public class textFont : ScriptableObject
    {
        public List<letter> fontLetters = new List<letter>();

        public GameObject[] autoAssighnModelRepresentations;

        [ContextMenu("add numbers")]
        public void addNumbers()
        {
            addS("1234567890");
        }

        [ContextMenu("add full")]
        public void addFullAlphabet()
        {
            addS("aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyYzZ");
        }

        [ContextMenu("add deafult")]
        public void addSimplifiedAlphabet()
        {
            char[] alpha = "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyYzZ".ToCharArray();

            string[] l = alpha.Select(t => t.ToString()).ToArray();


            for (int i = 0; i < 26; i++)
            {
                string[] cl = new string[2] { l[i * 2], l[i * 2 + 1] };
                fontLetters.Add(new letter(cl, null));
            }
        }

        private void addS(string toAdd)
        {
            char[] c = toAdd.ToCharArray();

            string[] l = c.Select(t => t.ToString()).ToArray();

            for (int i = 0; i < c.Length; i++)
            {
                string[] cl = new string[1] { l[i] };
                fontLetters.Add(new letter(cl, null));
            }
        }

        [ContextMenu("clear")]
        public void clear() => fontLetters.Clear();


        public GameObject getRepresentation(string letter)
        {
            foreach (letter l in fontLetters)
                if (l.letters.Contains(letter))
                    return l.representation;

            return null;
        }


#if UNITY_EDITOR
        private void OnValidate()
        {
            if (autoAssighnModelRepresentations.Length > 0)
            {
                for (int i = 0; i < autoAssighnModelRepresentations.Length && i < fontLetters.Count; i++)
                {
                    fontLetters[i].representation = autoAssighnModelRepresentations[i];
                }

                autoAssighnModelRepresentations = new GameObject[0];

                Debug.Log("assighned models");
            }
        }
#endif
    }

    [System.Serializable]
    public class letter
    {
        public string[] letters = new string[0];

        public GameObject representation = null;

        public letter(string[] letters)
        {
            this.letters = letters;
        }

        public letter(string[] letters, GameObject representation)
        {
            this.letters = letters;
            this.representation = representation;
        }
    }
}
