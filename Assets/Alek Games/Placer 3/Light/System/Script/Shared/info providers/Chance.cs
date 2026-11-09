using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlekGames.Placer3.Shared
{
    public static class Chance
    {
        public static bool giveChance(float chance)
        {
            //attempt
            if (chance < 100)
            {
                int randChance = Random.Range(0, 100);

                //Debug.Log(randChance + " try, chance " + chance);
                if (chance <= randChance) return false;

            }

            return true;
        }
    }
}
