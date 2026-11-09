using AlekGames.Placer3.Profiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlekGames.Placer3.Shared
{
    public static class TextSpawnerValues
    {

        [System.Serializable]
        public struct values
        {
            public textFont font;

            [TextArea()]
            public string text;

            public bool autoUpdate;

            public bool asPrefab;

            [Tooltip("if should add colliders when ' ' is wrote. good if you use text on text ground snapping")]
            public bool addSpaceColliders;

            [Tooltip("size of a letter ")]
            public float betweenLettersSpace;
            [Tooltip("size of 'space'. when you press spacebar ")]
            public float spaceLengh;
            [Tooltip("space down when tyrped 'enter'")]
            public float enterLengh;

            public Vector3 randPosAdd;
            public Vector3 randRotAdd;
            public Vector2 minMaxScale;

            public bool snapToGround;
            [Range(0, 1)]
            public float normalAllighn;
            public LayerMask groundLayer;
            public float snapHeightOffset;
            public Vector3 snapRayDir;

            public values(textFont font)
            {
                this.font = font;
                text = "how about leaving a review on this asset\nit would surely help a lot";
                autoUpdate = true;
                asPrefab = true;
                addSpaceColliders = false;
                betweenLettersSpace = 0.2f;
                spaceLengh = 0.4f;
                enterLengh = 0.4f;
                randPosAdd = Vector3.zero;
                randRotAdd = Vector3.zero;
                minMaxScale = Vector2.one;
                snapToGround = false;
                normalAllighn = 0.2f;
                groundLayer = 0;
                snapHeightOffset = 0.2f;
                snapRayDir = Vector3.down;
            }
        }
    }
}