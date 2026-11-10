using System.Collections;
using UnityEngine;

namespace RiskOfMagic.Interaction
{
    /// <summary>
    /// Imported animated models (chests, barrels) usually loop their animation on their own: held on the first frame
    /// (closed) until <see cref="SetOpen"/>, then played once and frozen on the last frame (open).
    /// Works with an Animator (an "Open" trigger is used when the controller has one) or a legacy Animation.
    /// </summary>
    public class OpenAnimation : MonoBehaviour
    {
        private static readonly int OpenTrigger = Animator.StringToHash("Open");

        private Animator[] animators;
        private Animation[] legacy;
        private bool opened;
        private Coroutine freeze;

        /// <summary>Every peer: closed (true) / opened (false), from the interactable's replicated state.</summary>
        public static void Apply(Component owner, bool available)
        {
            var gate = owner.GetComponent<OpenAnimation>();
            if (gate == null)
                gate = owner.gameObject.AddComponent<OpenAnimation>();
            gate.SetOpen(!available);
        }

        private void Collect()
        {
            if (animators != null)
                return;
            animators = GetComponentsInChildren<Animator>(true);
            legacy = GetComponentsInChildren<Animation>(true);
            foreach (var animation in legacy)
                animation.playAutomatically = false;
        }

        public void SetOpen(bool open)
        {
            Collect();
            if (open == opened && (open || freeze != null || animators.Length + legacy.Length == 0))
                return;
            opened = open;
            if (freeze != null)
                StopCoroutine(freeze);
            freeze = null;

            foreach (var animator in animators)
            {
                if (animator == null || animator.runtimeAnimatorController == null)
                    continue;
                if (!open)
                {
                    animator.Play(0, 0, 0f); // default state, first frame = closed
                    animator.Update(0f);
                    animator.speed = 0f;
                    continue;
                }
                animator.speed = 1f;
                bool hasTrigger = false;
                foreach (var parameter in animator.parameters)
                    if (parameter.nameHash == OpenTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
                        hasTrigger = true;
                if (hasTrigger)
                    animator.SetTrigger(OpenTrigger);
                else
                    animator.Play(0, 0, 0f);
            }
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
            if (open)
                freeze = StartCoroutine(FreezeAtEnd());
            else
                freeze = StartCoroutine(HoldClosed());
        }

        /// <summary>Looping clips: stop on the last frame once played through.</summary>
        private IEnumerator FreezeAtEnd()
        {
            yield return null;
            for (float t = 0f; t < 10f; t += Time.deltaTime)
            {
                bool done = true;
                foreach (var animator in animators)
                {
                    if (animator == null || animator.runtimeAnimatorController == null || animator.speed == 0f)
                        continue;
                    var info = animator.GetCurrentAnimatorStateInfo(0);
                    if (animator.IsInTransition(0) || info.normalizedTime < 0.98f)
                        done = false;
                    else
                        animator.speed = 0f;
                }
                if (done)
                    break;
                yield return null;
            }
            freeze = null;
        }

        /// <summary>Some controllers start playing again on enable: keep them on the first frame.</summary>
        private IEnumerator HoldClosed()
        {
            yield return null;
            foreach (var animator in animators)
                if (animator != null && animator.runtimeAnimatorController != null && !opened)
                {
                    animator.Play(0, 0, 0f);
                    animator.Update(0f);
                    animator.speed = 0f;
                }
        }
    }
}
