using System.Collections.Generic;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Flow
{
    /// <summary>Choices made in the lobby, read by the server when the game scene starts.</summary>
    public static class RunSettings
    {
        private static readonly Dictionary<ulong, int> characters = new();

        /// <summary>Null = use the scene's default (direct play from the game scene).</summary>
        public static DifficultyLevel? Difficulty { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            characters.Clear();
            Difficulty = null;
        }

        public static void SetCharacter(ulong clientId, int characterIndex) => characters[clientId] = characterIndex;
        public static bool TryGetCharacter(ulong clientId, out int characterIndex) => characters.TryGetValue(clientId, out characterIndex);
    }
}
