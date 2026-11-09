using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Economy
{
    public enum GoldDistribution : byte
    {
        KillerOnly,
        SplitBetweenPlayers,
        EveryPlayerFull
    }

    /// <summary>Server-side: converts enemy kills into gold.</summary>
    public class GoldRewarder : MonoBehaviour
    {
        [SerializeField] private GoldDistribution distribution = GoldDistribution.EveryPlayerFull;

        private static float bonusMultiplier = 1f;
        private static float bonusUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            bonusMultiplier = 1f;
            bonusUntil = 0f;
        }

        /// <summary>Server: kill gold × multiplier for a while (map secret reward).</summary>
        public static void SetBonus(float multiplier, float duration)
        {
            bonusMultiplier = Mathf.Max(1f, multiplier);
            bonusUntil = Time.time + duration;
        }

        public static bool BonusActive => Time.time < bonusUntil;

        private void OnEnable() => EnemyActor.Killed += OnEnemyKilled;
        private void OnDisable() => EnemyActor.Killed -= OnEnemyKilled;

        private void OnEnemyKilled(EnemyActor enemy, DamageInfo killingBlow)
        {
            uint amount = enemy.GoldReward;
            if (BonusActive)
                amount = (uint)Mathf.RoundToInt(amount * bonusMultiplier);
            if (amount == 0)
                return;

            var players = PlayerAvatar.All;
            switch (distribution)
            {
                case GoldDistribution.KillerOnly:
                    if (killingBlow.TryGetSource(out var source) && source.TryGetComponent(out PlayerWallet wallet))
                        wallet.Add(amount);
                    break;

                case GoldDistribution.SplitBetweenPlayers:
                    if (players.Count == 0)
                        return;
                    uint share = (uint)Mathf.CeilToInt(amount / (float)players.Count);
                    foreach (var player in players)
                        player.Wallet.Add(share);
                    break;

                default:
                    foreach (var player in players)
                        player.Wallet.Add(amount);
                    break;
            }
        }
    }
}
