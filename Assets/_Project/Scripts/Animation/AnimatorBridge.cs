using System.Collections.Generic;
using UnityEngine;

namespace RiskOfMagic.Animation
{
    /// <summary>
    /// Standard Animator parameters driven by the game. Create any subset of them in your Animator Controller
    /// (same name and type); the others are simply ignored.
    /// </summary>
    public static class AnimParams
    {
        // Floats
        public static readonly int Speed = Animator.StringToHash("Speed");            // horizontal m/s
        public static readonly int MoveX = Animator.StringToHash("MoveX");            // local right m/s
        public static readonly int MoveY = Animator.StringToHash("MoveY");            // local forward m/s
        public static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        public static readonly int AimPitch = Animator.StringToHash("AimPitch");      // degrees, owner only
        // Bools
        public static readonly int Grounded = Animator.StringToHash("Grounded");
        public static readonly int Sprinting = Animator.StringToHash("Sprinting");    // owner only
        public static readonly int Downed = Animator.StringToHash("Downed");
        public static readonly int Dead = Animator.StringToHash("Dead");
        public static readonly int Frozen = Animator.StringToHash("Frozen");
        // Ints
        public static readonly int SkillSlot = Animator.StringToHash("SkillSlot");    // 0 primary .. 3 special
        public static readonly int AttackIndex = Animator.StringToHash("AttackIndex");// enemy EnemyAttack order
        // Triggers
        public static readonly int Jump = Animator.StringToHash("Jump");
        public static readonly int Land = Animator.StringToHash("Land");
        public static readonly int Hit = Animator.StringToHash("Hit");
        public static readonly int UseEquipment = Animator.StringToHash("UseEquipment");
        public static readonly int Revive = Animator.StringToHash("Revive");
        public static readonly int Spawn = Animator.StringToHash("Spawn");
        public static readonly int Dodge = Animator.StringToHash("Dodge");
        public static readonly int Leap = Animator.StringToHash("Leap");
    }

    /// <summary>Animator wrapper that only sets the parameters the controller actually has (no console warnings).</summary>
    public sealed class AnimatorBridge
    {
        private readonly HashSet<int> parameters = new();
        private RuntimeAnimatorController cachedController;

        public Animator Animator { get; }

        public AnimatorBridge(Animator animator)
        {
            Animator = animator;
            animator.applyRootMotion = false; // movement is gameplay-driven
        }

        public bool IsValid => Animator != null && Animator.isActiveAndEnabled && Animator.runtimeAnimatorController != null;

        public bool Has(int hash)
        {
            if (!IsValid)
                return false;
            if (Animator.runtimeAnimatorController != cachedController)
            {
                cachedController = Animator.runtimeAnimatorController;
                parameters.Clear();
                foreach (var parameter in Animator.parameters)
                    parameters.Add(parameter.nameHash);
            }
            return parameters.Contains(hash);
        }

        public void SetFloat(int hash, float value, float dampTime = 0f)
        {
            if (!Has(hash))
                return;
            if (dampTime > 0f)
                Animator.SetFloat(hash, value, dampTime, Time.deltaTime);
            else
                Animator.SetFloat(hash, value);
        }

        public void SetBool(int hash, bool value)
        {
            if (Has(hash))
                Animator.SetBool(hash, value);
        }

        public void SetInteger(int hash, int value)
        {
            if (Has(hash))
                Animator.SetInteger(hash, value);
        }

        public void SetTrigger(int hash)
        {
            if (Has(hash))
                Animator.SetTrigger(hash);
        }

        public void SetTrigger(string name)
        {
            if (!string.IsNullOrEmpty(name))
                SetTrigger(Animator.StringToHash(name));
        }
    }

    /// <summary>Several animators driven together (third-person body + first-person arms).</summary>
    public sealed class AnimatorGroup
    {
        private readonly List<AnimatorBridge> bridges = new();

        public int Count => bridges.Count;

        public void Clear() => bridges.Clear();

        public void AddFrom(GameObject root)
        {
            if (root == null)
                return;
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                bridges.Add(new AnimatorBridge(animator));
        }

        public void SetFloat(int hash, float value, float damp = 0f) { foreach (var b in bridges) b.SetFloat(hash, value, damp); }
        public void SetBool(int hash, bool value) { foreach (var b in bridges) b.SetBool(hash, value); }
        public void SetInteger(int hash, int value) { foreach (var b in bridges) b.SetInteger(hash, value); }
        public void SetTrigger(int hash) { foreach (var b in bridges) b.SetTrigger(hash); }
        public void SetTrigger(string name) { foreach (var b in bridges) b.SetTrigger(name); }

        public void SetSpeed(float speed)
        {
            foreach (var b in bridges)
                if (b.Animator != null)
                    b.Animator.speed = speed;
        }
    }
}
