using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AlekGames.Placer3.Profiles;

namespace AlekGames.Placer3.Shared
{
    public interface IPrefabPalleteUser
    {
        void setPalette(prefabPalette pallete);
        prefabPalette getPalette();

        void setSpecificIndex(int index);
        int getSpecificIndex();
    }
}
