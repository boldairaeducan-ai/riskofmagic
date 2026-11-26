using UnityEngine;

namespace RiskOfMagic.Animation
{
    /// <summary>
    /// Code-driven animation for a humanoid model that ships without clips (Animator without controller): idle breathing,
    /// walk / run cycle from the movement speed, attack (windup above the head, strike, recovery; alternates hands),
    /// hit flinch, fall on death, freeze. Added by EnemyAnimationDriver; bones come from the humanoid avatar, else from
    /// transforms named like HumanBodyBones (Hips, LeftUpperLeg…). Every angle is applied around the model's own axes
    /// (right = pitch, forward = roll, up = yaw), whatever the bone orientations of the rig.
    /// A real Animator Controller on the model always takes over (this component is not added then).
    /// </summary>
    public class ProceduralHumanoidMotion : MonoBehaviour
    {
        public enum AttackStyle
        {
            /// <summary>One arm raised above the head, then a downward blow (alternates hands).</summary>
            Swing,
            /// <summary>Bow: left arm forward, right arm draws then releases.</summary>
            Shoot
        }

        [Header("Locomotion")]
        [Tooltip("Meters per full step cycle (two steps).")] public float strideLength = 1.6f;
        public float legSwing = 32f;
        public float kneeBend = 45f;
        public float armSwing = 22f;
        [Tooltip("Forward lean at full speed (hunched gait).")] public float runLean = 14f;
        [Tooltip("Base forward lean (posture).")] public float posture = 8f;
        public float bobHeight = 0.04f;

        [Header("Arms at rest")]
        [Tooltip("Arms lowered from the bind pose (T-pose) to hang slightly outward.")] public float armOutward = 0.3f;
        public float elbowBend = 20f;

        [Header("Attack")]
        public float windupRaise = 150f;
        public float strikeAngle = 20f;
        public float strikeLean = 18f;
        public float strikeDuration = 0.14f;

        private enum Bone
        {
            Hips, Spine, Chest, Head,
            LeftUpperLeg, LeftLowerLeg, RightUpperLeg, RightLowerLeg,
            LeftUpperArm, LeftLowerArm, RightUpperArm, RightLowerArm,
            Count
        }

        private static readonly HumanBodyBones[] HumanBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm
        };

        private readonly Transform[] bones = new Transform[(int)Bone.Count];
        private readonly Quaternion[] restLocal = new Quaternion[(int)Bone.Count];
        private Vector3 hipsRestPosition;
        private Quaternion leftArmDown = Quaternion.identity, rightArmDown = Quaternion.identity; // model space
        private Quaternion restModelRotation;
        private bool ready;

        // Inputs (set by the driver every frame / on events).
        private float speed;
        private float referenceSpeed = 5f;
        private bool frozen;
        private bool dead;

        private float phase;
        private float moveWeight;
        private float time;
        private float attackStart = -10f, attackWindup, attackRecovery;
        private bool attackRight = true;
        private AttackStyle attackStyle;
        private float hitTime = -10f;
        private float deathTime = -1f;

        public bool IsReady => ready;

        /// <summary>True when the model has an Animator without controller and recognizable humanoid bones.</summary>
        public static bool CanDrive(GameObject model)
        {
            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
                return false;
            return FindBone(model.transform, animator, HumanBodyBones.Hips) != null
                   && FindBone(model.transform, animator, HumanBodyBones.LeftUpperLeg) != null
                   && FindBone(model.transform, animator, HumanBodyBones.RightUpperLeg) != null;
        }

        private void Awake()
        {
            var animator = GetComponentInChildren<Animator>(true);
            for (int i = 0; i < (int)Bone.Count; i++)
            {
                bones[i] = FindBone(transform, animator, HumanBones[i]);
                if (bones[i] != null)
                    restLocal[i] = bones[i].localRotation;
            }
            if (animator != null)
                animator.enabled = false; // no controller: the bind pose is ours to animate
            if (bones[(int)Bone.Hips] == null)
                return;
            hipsRestPosition = bones[(int)Bone.Hips].localPosition;
            restModelRotation = transform.localRotation;
            leftArmDown = ArmDownRotation(Bone.LeftUpperArm, Bone.LeftLowerArm, -1f);
            rightArmDown = ArmDownRotation(Bone.RightUpperArm, Bone.RightLowerArm, 1f);
            ready = true;
        }

        private static Transform FindBone(Transform root, Animator animator, HumanBodyBones bone)
        {
            if (animator != null && animator.isHuman && animator.avatar != null)
            {
                var found = animator.GetBoneTransform(bone);
                if (found != null)
                    return found;
            }
            return FindByName(root, bone.ToString());
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                var found = FindByName(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>Model-space rotation bringing the arm from its bind pose (often a T-pose) to hanging down.</summary>
        private Quaternion ArmDownRotation(Bone upper, Bone lower, float side)
        {
            var a = bones[(int)upper];
            var b = bones[(int)lower];
            if (a == null || b == null)
                return Quaternion.identity;
            Vector3 current = transform.InverseTransformDirection(b.position - a.position).normalized;
            Vector3 target = new Vector3(side * armOutward, -1f, 0.05f).normalized;
            return Quaternion.FromToRotation(current, target);
        }

        // ---------------------------------------------------------------- Inputs

        public void SetLocomotion(float horizontalSpeed, float maxSpeed)
        {
            speed = horizontalSpeed;
            referenceSpeed = Mathf.Max(0.5f, maxSpeed);
        }

        public void SetFrozen(bool value) => frozen = value;

        public void SetDead(bool value)
        {
            if (value && !dead)
                deathTime = time;
            dead = value;
        }

        public void PlayAttack(float windup, float recovery, AttackStyle style = AttackStyle.Swing)
        {
            attackStart = time;
            attackWindup = Mathf.Max(0.1f, windup);
            attackRecovery = Mathf.Max(0.1f, recovery);
            attackStyle = style;
            if (style == AttackStyle.Swing)
                attackRight = !attackRight;
        }

        public void PlayHit() => hitTime = time;

        // ---------------------------------------------------------------- Pose

        private Renderer[] visibilityRenderers;

        private void LateUpdate()
        {
            if (!ready || frozen)
                return; // frozen: keep the last pose
            visibilityRenderers ??= GetComponentsInChildren<Renderer>(true);
            if (!RendererVisibility.AnyVisible(visibilityRenderers))
                return; // off-screen: no bone work
            float dt = Time.deltaTime;
            time += dt;

            // Rest pose every frame, then layered offsets (parents first).
            for (int i = 0; i < (int)Bone.Count; i++)
                if (bones[i] != null)
                    bones[i].localRotation = restLocal[i];
            bones[(int)Bone.Hips].localPosition = hipsRestPosition;

            if (dead)
            {
                ApplyDeath();
                return;
            }
            transform.localRotation = restModelRotation;

            float target = Mathf.Clamp01(speed / referenceSpeed);
            moveWeight = Mathf.MoveTowards(moveWeight, target, dt * 4f);
            phase = (phase + speed * dt / Mathf.Max(0.1f, strideLength) * Mathf.PI * 2f) % (Mathf.PI * 2f);

            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);
            float breathe = Mathf.Sin(time * 1.8f);

            // Hips bob on each step.
            var hips = bones[(int)Bone.Hips];
            hips.position += transform.up * (-Mathf.Abs(c) * bobHeight * moveWeight * transform.lossyScale.y + breathe * 0.004f);
            Yaw(Bone.Hips, s * 6f * moveWeight);

            // Torso: posture + run lean + breathing + hit flinch.
            float flinch = Envelope(time - hitTime, 0.05f, 0.25f);
            Pitch(Bone.Spine, posture + runLean * moveWeight + breathe * 1.5f * (1f - moveWeight) - flinch * 14f);
            Pitch(Bone.Chest, breathe * 1.5f);
            Roll(Bone.Spine, s * 3f * moveWeight);
            Pitch(Bone.Head, -posture * 0.6f - runLean * 0.5f * moveWeight + flinch * 6f);

            // Legs (forward swing = negative pitch), knees bend while the leg travels forward.
            float swing = legSwing * moveWeight;
            Pitch(Bone.LeftUpperLeg, -s * swing);
            Pitch(Bone.RightUpperLeg, s * swing);
            Pitch(Bone.LeftLowerLeg, Mathf.Max(0f, c) * kneeBend * moveWeight + 4f);
            Pitch(Bone.RightLowerLeg, Mathf.Max(0f, -c) * kneeBend * moveWeight + 4f);

            // Arms: hang, swing opposite to the legs, then the attack overrides one arm.
            if (attackStyle == AttackStyle.Shoot && ApplyShoot(breathe))
                return;
            ApplyArm(Bone.LeftUpperArm, Bone.LeftLowerArm, leftArmDown, s * armSwing * moveWeight, breathe, !attackRight);
            ApplyArm(Bone.RightUpperArm, Bone.RightLowerArm, rightArmDown, -s * armSwing * moveWeight, breathe, attackRight);
        }

        /// <summary>Bow shot: both arms raised forward, the right hand draws to the chest then releases.</summary>
        private bool ApplyShoot(float breathe)
        {
            float t = time - attackStart;
            float total = attackWindup + attackRecovery;
            if (t < 0f || t > total)
                return false;
            // Raise quickly, hold, lower at the end.
            float raise = Mathf.Min(Mathf.Clamp01(t / 0.08f), Mathf.Clamp01((total - t) / 0.15f));
            float draw = t < attackWindup ? Mathf.SmoothStep(0f, 1f, t / attackWindup) : 0f; // released at the end of the windup
            if (bones[(int)Bone.LeftUpperArm] != null)
            {
                RotateModelSpace(bones[(int)Bone.LeftUpperArm], leftArmDown);
                Pitch(Bone.LeftUpperArm, -85f * raise + breathe);
                Yaw(Bone.LeftUpperArm, 10f * raise);
            }
            if (bones[(int)Bone.RightUpperArm] != null)
            {
                RotateModelSpace(bones[(int)Bone.RightUpperArm], rightArmDown);
                Pitch(Bone.RightUpperArm, -80f * raise);
                Yaw(Bone.RightUpperArm, -25f * raise * draw);
                Pitch(Bone.RightLowerArm, -110f * raise * draw);
            }
            Yaw(Bone.Spine, -12f * raise);
            return true;
        }

        private void ApplyArm(Bone upper, Bone lower, Quaternion down, float swing, float breathe, bool attacking)
        {
            if (bones[(int)upper] == null)
                return;
            RotateModelSpace(bones[(int)upper], down);
            float pitch = -swing + breathe * 2f;
            float lean = 0f;
            if (attacking)
            {
                float t = time - attackStart;
                float strikeEnd = attackWindup + strikeDuration;
                float raise;
                if (t < attackWindup)
                    raise = Mathf.SmoothStep(0f, 1f, t / attackWindup) * -windupRaise;          // axe up, behind the head
                else if (t < strikeEnd)
                    raise = Mathf.Lerp(-windupRaise, -strikeAngle, Mathf.SmoothStep(0f, 1f, (t - attackWindup) / strikeDuration));
                else if (t < strikeEnd + attackRecovery)
                    raise = Mathf.Lerp(-strikeAngle, 0f, Mathf.SmoothStep(0f, 1f, (t - strikeEnd) / attackRecovery));
                else
                    raise = 0f;
                if (raise != 0f)
                {
                    pitch = raise;
                    // Body follows the blow: lean back during the windup, forward on the strike.
                    lean = t < attackWindup ? -6f * (t / attackWindup) : strikeLean * Envelope(t - attackWindup, 0.05f, attackRecovery);
                }
            }
            Pitch(upper, pitch);
            if (lean != 0f)
                Pitch(Bone.Spine, lean);
            Pitch(lower, -elbowBend);
        }

        private void ApplyDeath()
        {
            // Topple backwards around the feet, limbs going slack.
            float t = Mathf.Clamp01((time - deathTime) / 0.45f);
            float eased = t * t;
            transform.localRotation = restModelRotation * Quaternion.Euler(-85f * eased, 0f, 0f);
            Pitch(Bone.Spine, -10f * eased);
            if (bones[(int)Bone.LeftUpperArm] != null)
                RotateModelSpace(bones[(int)Bone.LeftUpperArm], Quaternion.Slerp(Quaternion.identity, leftArmDown, 0.5f));
            if (bones[(int)Bone.RightUpperArm] != null)
                RotateModelSpace(bones[(int)Bone.RightUpperArm], Quaternion.Slerp(Quaternion.identity, rightArmDown, 0.5f));
            Pitch(Bone.LeftUpperLeg, -20f * eased);
            Pitch(Bone.LeftLowerLeg, 30f * eased);
        }

        /// <summary>0 → 1 in <paramref name="rise"/> seconds, then back to 0 over <paramref name="fall"/> seconds.</summary>
        private static float Envelope(float t, float rise, float fall)
        {
            if (t < 0f)
                return 0f;
            if (t < rise)
                return t / rise;
            return Mathf.Clamp01(1f - (t - rise) / Mathf.Max(0.01f, fall));
        }

        private void Pitch(Bone bone, float degrees) => RotateAround(bone, Vector3.right, degrees);
        private void Roll(Bone bone, float degrees) => RotateAround(bone, Vector3.forward, degrees);
        private void Yaw(Bone bone, float degrees) => RotateAround(bone, Vector3.up, degrees);

        private void RotateAround(Bone bone, Vector3 modelAxis, float degrees)
        {
            var t = bones[(int)bone];
            if (t != null && degrees != 0f)
                t.rotation = Quaternion.AngleAxis(degrees, transform.TransformDirection(modelAxis)) * t.rotation;
        }

        private void RotateModelSpace(Transform bone, Quaternion modelDelta)
        {
            var model = transform.rotation;
            bone.rotation = model * modelDelta * Quaternion.Inverse(model) * bone.rotation;
        }
    }
}
