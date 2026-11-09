using AlekGames.Placer3.Profiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlekGames.Placer3.Shared
{
    public static class ColorSpawnerValues
    {
        [System.Serializable]
        public struct cValues
        {
            public colorComparisonHold[] colors;
            public int xLen;
            public int zLen;
            public bool temporarlyResetLights;
            [Range(0, 100)]
            public float pointKeepChance;
            [Min(1)]
            public int pixelSkips;
            public float offset;
            public LayerMask cullMasks;
            public prefabPalette palette;
            public int specificIndex;

            public cValues(colorComparisonHold[] colors)
            {
                this.colors = colors;
                xLen = 300;
                zLen = 300;
                temporarlyResetLights = true;
                cullMasks = ~0;
                palette = null;
                specificIndex = -1;
                pointKeepChance = 100;
                pixelSkips = 5;
                offset = 1;
            }
        }


        [System.Serializable]
        public struct colorComparisonHold
        {
            public Color color;
            [Range(0, 3)]
            public float colorDifAllowence;

            public colorComparisonHold(Color c, int dif)
            {
                color = c;
                colorDifAllowence = dif;
            }
        }
    }
}