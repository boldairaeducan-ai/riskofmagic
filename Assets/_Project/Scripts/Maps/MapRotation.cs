using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RiskOfMagic.Maps
{
    /// <summary>
    /// Which map scenes can be played at each stage (RoR2-style pools). Stage N uses stages[N % count];
    /// one scene of its pool is picked at random. Scenes must be in the build settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Map Rotation", fileName = "MapRotation")]
    public class MapRotation : ScriptableObject
    {
        [Serializable]
        public class StagePool
        {
            public List<string> scenes = new();
        }

        public List<StagePool> stages = new();

        /// <param name="stageIndex">0-based.</param>
        public string PickScene(int stageIndex)
        {
            if (stages.Count == 0)
                return null;
            var pool = stages[Mathf.Abs(stageIndex) % stages.Count].scenes;
            var valid = pool.FindAll(scene => !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene));
            return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
        }
    }
}
