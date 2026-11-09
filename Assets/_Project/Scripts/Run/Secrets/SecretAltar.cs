using System;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Economy;
using RiskOfMagic.Enemies;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using RiskOfMagic.Run.MapEvents;
using RiskOfMagic.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RiskOfMagic.Run.Secrets
{
    /// <summary>
    /// Map secret altar (spawned by InteractableSpawner on a SecretAltar spot when the map has a MapSecret).
    /// Fragments found anywhere count for the team; once all are found, interacting rolls the reward:
    /// a stage bonus, or the map's secret boss (rare + boss item per player when it dies).
    /// </summary>
    public class SecretAltar : MapEvent
    {
        public enum AltarState : byte { Dormant, BossFight, Done }

        public enum Bonus : byte { Blessing, GoldRush, RareItems }

        [SerializeField] private Renderer[] runeRenderers = Array.Empty<Renderer>();
        [SerializeField] private Color dormantColor = new(0.15f, 0.15f, 0.2f);
        [SerializeField] private Color awakeColor = new(0.4f, 0.85f, 1f);
        [SerializeField] private Color doneColor = new(0.25f, 0.25f, 0.25f);
        [SerializeField] private float goldMultiplier = 3f;
        [SerializeField] private float goldDuration = 120f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color SecretColor = new(0.45f, 0.85f, 1f);

        private readonly NetworkVariable<AltarState> state = new();
        private readonly NetworkVariable<int> fragments = new();
        private readonly NetworkVariable<int> required = new(3);
        private readonly NetworkVariable<FixedString64Bytes> bossName = new();
        private MaterialPropertyBlock block;
        private EnemyActor boss;

        public static SecretAltar Instance { get; private set; }

        /// <summary>Server: a secret was solved (bonus) or its boss killed.</summary>
        public static event Action<SecretAltar> Solved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSecretStatics()
        {
            Instance = null;
            Solved = null;
        }

        private bool Complete => fragments.Value >= required.Value;

        /// <summary>Server, before Spawn().</summary>
        public void Setup(int fragmentCount) => required.Value = Mathf.Max(1, fragmentCount);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Instance = this;
            state.OnValueChanged += OnChanged;
            fragments.OnValueChanged += OnFragmentsChanged;
            ApplyVisuals();
            if (IsServer)
                EnemyActor.Killed += OnEnemyKilled;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (Instance == this)
                Instance = null;
            state.OnValueChanged -= OnChanged;
            fragments.OnValueChanged -= OnFragmentsChanged;
            EnemyActor.Killed -= OnEnemyKilled;
        }

        // ---------------------------------------------------------------- Interaction

        public override bool CanInteract(PlayerAvatar player) => player.IsStanding && state.Value == AltarState.Dormant && Complete;

        public override string GetPrompt(PlayerAvatar player)
        {
            if (state.Value != AltarState.Dormant)
                return string.Empty;
            return Complete ? "[E] Réveiller l'autel runique" : $"Autel runique : fragments {fragments.Value}/{required.Value}";
        }

        public override string GetObjective(PlayerAvatar player) => state.Value switch
        {
            AltarState.Dormant when fragments.Value > 0 => Complete
                ? "Secret : rapportez les fragments à l'autel runique"
                : $"Secret : fragments de rune {fragments.Value}/{required.Value}",
            AltarState.BossFight => $"Secret : vaincre {bossName.Value}",
            _ => string.Empty
        };

        /// <summary>Server (SecretFragment).</summary>
        public void AddFragment()
        {
            if (!IsServer || state.Value != AltarState.Dormant)
                return;
            fragments.Value = Mathf.Min(required.Value, fragments.Value + 1);
        }

        public override void Interact(PlayerAvatar player)
        {
            if (!IsServer || !CanInteract(player))
                return;
            var secret = MapSecret.Current;
            bool bossFight = secret != null && secret.bossCard != null && Random.value < secret.bossChance;
            if (bossFight && SummonBoss(secret))
                return;
            GrantBonus((Bonus)Random.Range(0, 3));
        }

        // ---------------------------------------------------------------- Rewards (server)

        private void GrantBonus(Bonus bonus)
        {
            switch (bonus)
            {
                case Bonus.Blessing:
                    foreach (var player in PlayerAvatar.All)
                        if (player.Status != null)
                            player.Status.Apply(StatusType.RuneBlessing, 0f); // until the end of the stage
                    AnnounceRpc("Bénédiction runique", "+30% de dégâts jusqu'à la fin de l'étape.");
                    break;
                case Bonus.GoldRush:
                    GoldRewarder.SetBonus(goldMultiplier, goldDuration);
                    AnnounceRpc("Fièvre de l'or", $"Or des ennemis ×{goldMultiplier:0} pendant {goldDuration / 60f:0} minutes.");
                    break;
                default:
                    GiveEach(ItemRarity.Rare);
                    AnnounceRpc("Trésor oublié", "Chaque joueur reçoit un objet rare.");
                    break;
            }
            state.Value = AltarState.Done;
            Solved?.Invoke(this);
        }

        private bool SummonBoss(MapSecret secret)
        {
            boss = SpawnEnemy(new Vector2(6f, 10f), null, secret.bossCard);
            if (boss == null)
                return false;
            boss.Health.SetMaxHealth(boss.Health.Max * secret.bossHealthMultiplier, keepRatio: true);
            string title = string.IsNullOrEmpty(secret.bossTitle) ? boss.DisplayName : secret.bossTitle;
            bossName.Value = new FixedString64Bytes(title);
            state.Value = AltarState.BossFight;
            AnnounceRpc(title, "Un gardien secret se réveille !");
            return true;
        }

        private void OnEnemyKilled(EnemyActor enemy, DamageInfo killingBlow)
        {
            if (enemy != boss || state.Value != AltarState.BossFight)
                return;
            boss = null;
            GiveEach(ItemRarity.Rare);
            GiveEach(ItemRarity.Boss);
            AnnounceRpc("Gardien vaincu", "Chaque joueur reçoit un objet rare et un objet de boss.");
            state.Value = AltarState.Done;
            Solved?.Invoke(this);
        }

        private static void GiveEach(ItemRarity rarity)
        {
            var catalog = GameCatalog.Instance;
            if (catalog == null)
                return;
            foreach (var player in PlayerAvatar.All)
            {
                var item = catalog.RollItem(rarity);
                if (item != null)
                    player.Inventory.AddItem(item);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void AnnounceRpc(FixedString128Bytes title, FixedString512Bytes description)
            => HudAnnouncements.Show(title.ToString(), description.ToString(), SecretColor);

        // ---------------------------------------------------------------- Visuals

        private void OnChanged(AltarState previous, AltarState current) => ApplyVisuals();

        private void OnFragmentsChanged(int previous, int current)
        {
            ApplyVisuals();
            if (current > previous)
                HudAnnouncements.Show("Fragment de rune", current >= required.Value
                    ? "Tous les fragments sont réunis : trouvez l'autel runique."
                    : $"{current}/{required.Value} fragments trouvés.", SecretColor);
        }

        private void ApplyVisuals()
        {
            Color color = state.Value switch
            {
                AltarState.Done => doneColor,
                _ => Color.Lerp(dormantColor, awakeColor, required.Value > 0 ? fragments.Value / (float)required.Value : 0f)
            };
            block ??= new MaterialPropertyBlock();
            foreach (var r in runeRenderers)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(block);
                block.SetColor(ColorId, color);
                block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
