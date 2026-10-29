using UnityEngine;

namespace RiskOfMagic.Skills
{
    /// <summary>Runtime state of one equipped skill (stocks, recharge, combo step).</summary>
    public sealed class SkillSlotState
    {
        private float rechargeTimer;

        public SkillSlotState(SkillDefinition definition)
        {
            Definition = definition;
            Stock = definition.maxStock;
        }

        public SkillDefinition Definition { get; }
        public int Stock { get; private set; }
        public int MaxStock => Definition.maxStock;
        public bool HasStock => Stock > 0;
        public int Step { get; set; }
        public float LastActivationTime { get; set; } = float.NegativeInfinity;

        public float GetCooldown(float multiplier) => Definition.cooldown * multiplier;

        public float GetCooldownRemaining(float multiplier)
        {
            if (Stock >= MaxStock)
                return 0f;
            return Mathf.Max(0f, GetCooldown(multiplier) - rechargeTimer);
        }

        public float GetRechargeProgress(float multiplier)
        {
            float cooldown = GetCooldown(multiplier);
            return Stock >= MaxStock || cooldown <= 0f ? 1f : rechargeTimer / cooldown;
        }

        public void Tick(float deltaTime, float multiplier)
        {
            if (Stock >= MaxStock)
            {
                rechargeTimer = 0f;
                return;
            }

            float cooldown = GetCooldown(multiplier);
            if (cooldown <= 0f)
            {
                Stock = MaxStock;
                return;
            }

            rechargeTimer += deltaTime;
            while (rechargeTimer >= cooldown && Stock < MaxStock)
            {
                rechargeTimer -= cooldown;
                Stock++;
            }
        }

        public void Consume()
        {
            if (Stock > 0)
                Stock--;
        }

        /// <summary>Server-side consume accepting casts slightly early (latency).</summary>
        public bool TryConsume(float tolerance, float multiplier)
        {
            if (Stock > 0)
            {
                Stock--;
                return true;
            }
            if (GetCooldownRemaining(multiplier) > tolerance)
                return false;
            rechargeTimer = 0f;
            return true;
        }
    }
}
