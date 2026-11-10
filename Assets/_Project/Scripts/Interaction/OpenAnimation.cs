using System.Collections;
using UnityEngine;

namespace RiskOfMagic.Interaction
{
    /// <summary>
    /// Imported animated models (chests, barrels) usually animate on their own (often an idle loop chaining into
    /// open/close): held closed until <see cref="SetOpen"/>, then the opening plays once and stays open.
    /// Animator: plays the "open" state (named by <see cref="stateName"/>, else the first clip whose name contains
    /// "open"), from its first frame (closed) up to <see cref="holdTime"/>; clips that open AND close are held
    /// at <see cref="openCloseHoldFraction"/> of their length (lid up). An "Open" trigger is used when the
    /// controller has one and no open state is found. Legacy Animation components are supported too.
    /// </summary>
    public class OpenAnimation : MonoBehaviour
    {
        private static readonly int OpenTrigger = Animator.StringToHash("Open");

        [Tooltip("Animator state to play when opened (empty = first clip named *open*).")] public string stateName;
        [Tooltip("Seconds into the state where the model stays open (< 0 = automatic).")] public float holdTime = -1f;
        [Range(0.1f, 1f), Tooltip("Automatic hold point of an open+close clip (0.45 = lid up).")] public float openCloseHoldFraction = 0.45f;

        private Animator[] animators;
        private Animation[] legacy;
        private bool opened;
        private bool initialized;
        private Coroutine routine;

        /// <summary>Every peer: closed (available) / opened, from the interactable's replicated state.</summary>
        public static void Apply(Component owner, bool available)
        {
            var gate = owner.GetComponent<OpenAnimation>();
            if (gate == null)
                gate = owner.gameObject.AddComponent<OpenAnimation>();
            gate.SetOpen(!available);
        }

        private struct Target
        {
            public int StateHash;
            public float Length;
            public float Hold; // seconds
        }

        /// <summary>The state to play and where to stop, or StateHash 0 when the controller has none to offer.</summary>
        private Target Resolve(Animator animator)
        {
            var controller = animator.runtimeAnimatorController;
            AnimationClip clip = null;
            foreach (var candidate in controller.animationClips)
            {
                if (candidate == null)
                    continue;
                bool named = !string.IsNullOrEmpty(stateName) ? candidate.name == stateName : candidate.name.ToLowerInvariant().Contains("open");
                if (named)
                {
                    clip = candidate;
                    break;
                }
            }
            string state = !string.IsNullOrEmpty(stateName) ? stateName : clip != null ? clip.name : null;
            if (state == null)
                return default;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash))
                return default;
            float length = clip != null ? clip.length : 1f;
            float hold = holdTime >= 0f ? holdTime
                : clip != null && clip.name.ToLowerInvariant().Contains("close") ? length * openCloseHoldFraction
                : length;
            return new Target { StateHash = hash, Length = Mathf.Max(0.01f, length), Hold = Mathf.Min(hold, length) };
        }

        public void SetOpen(bool open)
        {
            if (!initialized)
            {
                initialized = true;
                animators = GetComponentsInChildren<Animator>(true);
                legacy = GetComponentsInChildren<Animation>(true);
                foreach (var animation in legacy)
                    animation.playAutomatically = false;
            }
            else if (open == opened)
                return;
            opened = open;
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(open ? Open() : HoldClosed());
            ApplyLegacy(open);
        }

        private IEnumerator HoldClosed()
        {
            // Twice: some controllers restart on their first update after being enabled.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var animator in animators)
                {
                    if (animator == null || animator.runtimeAnimatorController == null)
                        continue;
                    var target = Resolve(animator);
                    animator.Play(target.StateHash, 0, 0f); // hash 0 = current (default) state
                    animator.Update(0f);
                    animator.speed = 0f;
                }
                yield return null;
            }
            routine = null;
        }

        private IEnumerator Open()
        {
            var targets = new Target[animators.Length];
            for (int i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null || animator.runtimeAnimatorController == null)
                    continue;
                targets[i] = Resolve(animator);
                animator.speed = 1f;
                if (targets[i].StateHash != 0)
                    animator.Play(targets[i].StateHash, 0, 0f);
                else if (HasOpenTrigger(animator))
                    animator.SetTrigger(OpenTrigger);
                else
                    animator.Play(0, 0, 0f);
            }

            float elapsed = 0f;
            bool running = true;
            while (running && elapsed < 15f)
            {
                yield return null;
                elapsed += Time.deltaTime;
                running = false;
                for (int i = 0; i < animators.Length; i++)
                {
                    var animator = animators[i];
                    if (animator == null || animator.runtimeAnimatorController == null || animator.speed == 0f)
                        continue;
                    var info = animator.GetCurrentAnimatorStateInfo(0);
                    float hold = targets[i].StateHash != 0 ? targets[i].Hold / targets[i].Length : 0.98f;
                    if (!animator.IsInTransition(0) && info.normalizedTime >= hold)
                    {
                        animator.Play(info.fullPathHash, 0, hold); // exact pose, then frozen
                        animator.Update(0f);
                        animator.speed = 0f;
                    }
                    else
                        running = true;
                }
            }
            routine = null;
        }

        private static bool HasOpenTrigger(Animator animator)
        {
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == OpenTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
                    return true;
            return false;
        }

        private void ApplyLegacy(bool open)
        {
            foreach (var animation in legacy)
            {
                if (animation == null || animation.clip == null)
                    continue;
                var state = animation[animation.clip.name];
                if (!open)
                {
                    animation.Stop();
                    state.time = 0f;
                    state.enabled = true;
                    animation.Sample();
                    state.enabled = false;
                    continue;
                }
                state.wrapMode = WrapMode.ClampForever;
                animation.Play(animation.clip.name);
            }
        }
    }
}
