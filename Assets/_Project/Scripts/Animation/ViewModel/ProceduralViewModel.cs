using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Player;
using RiskOfMagic.Skills;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskOfMagic.Animation
{
    /// <summary>
    /// Local player only: the hands / weapons in front of the camera, animated by code. Rest pose + walk bob,
    /// look sway, sprint, landing and hit reactions, the draw of charged skills, and one motion per skill.
    /// </summary>
    public class ProceduralViewModel : MonoBehaviour
    {
        private struct Key
        {
            public float T;
            public Vector3 Position;
            public Vector3 Euler;
            public Key(float t, float x, float y, float z, float rx, float ry, float rz)
            {
                T = t;
                Position = new Vector3(x, y, z);
                Euler = new Vector3(rx, ry, rz);
            }
        }

        private static readonly Key[] Still = { new(0f, 0, 0, 0, 0, 0, 0), new(1f, 0, 0, 0, 0, 0, 0) };

        // Offsets from the rest pose, right-hand version (x mirrored for the left hand). Position in meters, euler in degrees.
        private static readonly Key[] SwingKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.18f, 0.10f, 0.08f, -0.06f, -15, 40, -25), new(0.42f, -0.34f, -0.06f, 0.16f, 20, -75, 55),
            new(0.6f, -0.36f, -0.12f, 0.08f, 25, -80, 60), new(1f, 0, 0, 0, 0, 0, 0)
        };
        private static readonly Key[] OverheadKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.25f, -0.06f, 0.22f, -0.08f, -70, 0, 0), new(0.45f, -0.10f, -0.20f, 0.24f, 65, 0, 0),
            new(0.65f, -0.10f, -0.24f, 0.2f, 70, 0, 0), new(1f, 0, 0, 0, 0, 0, 0)
        };
        private static readonly Key[] SpinKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.15f, 0.14f, 0.04f, -0.05f, 0, 60, -30), new(0.5f, -0.5f, 0f, 0.1f, 0, -120, 70),
            new(0.75f, -0.3f, -0.3f, -0.1f, 20, -200, 80), new(1f, 0, 0, 0, 0, 0, 0)
        };
        private static readonly Key[] ThrustKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.2f, 0.02f, 0.02f, -0.14f, -8, 6, 0), new(0.4f, -0.12f, 0.03f, 0.34f, 4, -10, 0),
            new(0.55f, -0.12f, 0.03f, 0.3f, 4, -10, 0), new(1f, 0, 0, 0, 0, 0, 0)
        };
        private static readonly Key[] CastKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.2f, 0.02f, 0.06f, -0.08f, -25, 0, 0), new(0.4f, -0.10f, 0.12f, 0.26f, 30, -8, 0),
            new(0.6f, -0.10f, 0.10f, 0.22f, 25, -8, 0), new(1f, 0, 0, 0, 0, 0, 0)
        };
        private static readonly Key[] ThrowKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.25f, 0.06f, 0.2f, -0.2f, -80, 10, 0), new(0.42f, -0.12f, 0.06f, 0.36f, 50, -10, 0),
            new(0.7f, -0.04f, -0.3f, 0.1f, 30, 0, 0), new(1f, 0, 0, 0, 0, 0, 0)
        };
        private static readonly Key[] LowerKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.2f, 0.04f, -0.32f, -0.08f, 35, 0, -10), new(0.7f, 0.04f, -0.32f, -0.08f, 35, 0, -10),
            new(1f, 0, 0, 0, 0, 0, 0)
        };
        private static readonly Key[] SlamKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.3f, -0.08f, 0.26f, -0.06f, -75, 0, 0), new(0.48f, -0.14f, -0.26f, 0.2f, 70, 0, 0),
            new(0.7f, -0.14f, -0.3f, 0.16f, 75, 0, 0), new(1f, 0, 0, 0, 0, 0, 0)
        };
        private static readonly Key[] GuardKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.2f, -0.22f, 0.16f, 0.08f, 0, -20, 0), new(0.75f, -0.22f, 0.16f, 0.08f, 0, -20, 0),
            new(1f, 0, 0, 0, 0, 0, 0)
        };
        // Bow: the off hand brings the bow up to the center; the main hand draws back then lets go.
        private static readonly Key[] BowHoldKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.25f, -0.14f, 0.10f, 0.08f, 0, -8, 0), new(0.55f, -0.14f, 0.10f, 0.08f, 0, -8, 0),
            new(0.62f, -0.14f, 0.11f, 0.03f, -4, -8, 0), new(1f, 0, 0, 0, 0, 0, 0)
        };
        private static readonly Key[] BowDrawKeys =
        {
            new(0f, 0, 0, 0, 0, 0, 0), new(0.25f, -0.10f, 0.10f, 0.1f, 0, 0, 0), new(0.55f, -0.04f, 0.10f, -0.2f, 0, 0, 0),
            new(0.62f, -0.10f, 0.08f, 0.16f, 0, 0, 0), new(1f, 0, 0, 0, 0, 0, 0)
        };

        private ViewModelDefinition definition;
        private PlayerAvatar avatar;
        private Transform main, off;
        private GameObject mainWeapon;

        private ViewMotion motion = ViewMotion.None;
        private float motionTime = 1f, motionDuration = 0.4f;
        private bool mirror, offLeads;
        private int step;

        private Vector3 lastPosition, velocity;
        private Vector3 lastForward;
        private Vector2 sway;
        private float bobPhase, landKick, hitKick, draw, sprint;
        private bool wasGrounded = true;
        private int chargeSlot = 1;

        /// <summary>Builds the hands under the owner's camera.</summary>
        public static ProceduralViewModel Create(PlayerAvatar owner, ViewModelDefinition definition)
        {
            if (owner == null || definition == null || owner.Look == null || owner.Look.Camera == null)
                return null;
            var go = new GameObject("ProceduralViewModel");
            go.transform.SetParent(owner.Look.Camera.transform, false);
            var view = go.AddComponent<ProceduralViewModel>();
            view.avatar = owner;
            view.definition = definition;
            view.main = view.BuildHand("MainHand", definition.mainHand, out view.mainWeapon);
            view.off = view.BuildHand("OffHand", definition.offHand, out _);
            view.lastPosition = owner.transform.position;
            view.lastForward = owner.Look.Camera.transform.forward;
            owner.Health.Damaged += view.OnDamaged;
            return view;
        }

        private Transform BuildHand(string handName, ViewHand hand, out GameObject weapon)
        {
            weapon = null;
            var root = new GameObject(handName).transform;
            root.SetParent(transform, false);
            if (hand.prefab != null)
            {
                weapon = Instantiate(hand.prefab, root, false);
                weapon.transform.localPosition = Vector3.zero;
                weapon.transform.localRotation = Quaternion.identity;
                weapon.transform.localScale = Vector3.one * Mathf.Max(0.001f, hand.scale);
                foreach (var collider in weapon.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (var behaviour in weapon.GetComponentsInChildren<MonoBehaviour>(true))
                    behaviour.enabled = false;
            }
            if (hand.showGlove)
            {
                var glove = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glove.name = "Glove";
                Destroy(glove.GetComponent<Collider>());
                glove.transform.SetParent(root, false);
                glove.transform.localPosition = hand.gloveOffset;
                glove.transform.localScale = new Vector3(definition.gloveSize, definition.gloveSize * 1.15f, definition.gloveSize * 1.3f);
                var renderer = glove.GetComponent<Renderer>();
                var material = renderer.material;
                material.color = definition.gloveColor;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", definition.gloveColor);
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return root;
        }

        private void OnDestroy()
        {
            if (avatar != null && avatar.Health != null)
                avatar.Health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            if (!info.IgnoreModifiers)
                hitKick = 1f;
        }

        /// <summary>A skill fired (owner side, immediately).</summary>
        public void Play(SkillSlot slot, SkillDefinition skill, bool alternative, int comboStep)
        {
            var next = definition.GetMotion((int)slot, alternative);
            if (next == ViewMotion.None)
                return;
            if (next == motion && (next == ViewMotion.Swing || next == ViewMotion.ThrustAlternate))
            {
                mirror = !mirror;      // alternate the slash direction
                offLeads = !offLeads;  // alternate the hand
            }
            else
            {
                mirror = false;
                offLeads = false;
            }
            motion = next;
            step = comboStep;
            motionTime = 0f;
            float cast = skill != null ? skill.castDuration : 0.3f;
            motionDuration = Mathf.Clamp(cast * 1.5f, 0.22f, 0.9f) / Mathf.Max(0.1f, definition.motionSpeed);
            draw = 0f;
        }

        private void LateUpdate()
        {
            if (avatar == null || definition == null)
                return;
            bool visible = avatar.IsStanding;
            if (main.gameObject.activeSelf != visible)
            {
                main.gameObject.SetActive(visible);
                off.gameObject.SetActive(visible);
            }
            if (!visible)
                return;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            // Movement: bob while walking, lowered while sprinting, dip on landing.
            Vector3 position = avatar.transform.position;
            Vector3 instant = (position - lastPosition) / dt;
            lastPosition = position;
            if (instant.sqrMagnitude > 2500f)
                instant = Vector3.zero;
            velocity = Vector3.Lerp(velocity, instant, 1f - Mathf.Exp(-12f * dt));
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool grounded = avatar.Motor == null || avatar.Motor.IsGrounded;
            if (grounded && !wasGrounded)
                landKick = Mathf.Clamp01(-velocity.y / 12f + 0.35f);
            wasGrounded = grounded;
            landKick = Mathf.MoveTowards(landKick, 0f, 3.5f * dt);
            hitKick = Mathf.MoveTowards(hitKick, 0f, 5f * dt);
            bool sprinting = avatar.Motor != null && avatar.Motor.IsSprinting && speed > 1f;
            sprint = Mathf.MoveTowards(sprint, sprinting ? 1f : 0f, 5f * dt);
            if (grounded)
                bobPhase += speed * dt * 1.9f;
            float bobScale = grounded ? Mathf.Clamp01(speed / 6f) : 0f;
            Vector3 bob = new(Mathf.Cos(bobPhase) * definition.bobAmount * bobScale,
                -Mathf.Abs(Mathf.Sin(bobPhase)) * definition.bobAmount * 1.4f * bobScale, 0f);

            // Look sway: the hands lag behind the camera.
            Transform cam = transform.parent;
            Vector3 forward = cam.forward;
            Vector3 localDelta = cam.InverseTransformDirection(forward - lastForward);
            lastForward = forward;
            sway = Vector2.Lerp(sway, new Vector2(-localDelta.x, -localDelta.y) * (definition.swayAmount * 60f), 1f - Mathf.Exp(-10f * dt));
            sway = Vector2.ClampMagnitude(sway, 0.06f);

            Vector3 shared = bob + new Vector3(sway.x, sway.y, 0f)
                             + new Vector3(0f, -definition.sprintLower * sprint - 0.06f * landKick, -0.05f * hitKick)
                             + new Vector3(0f, Mathf.Clamp(-velocity.y * 0.004f, -0.03f, 0.03f), 0f);
            Vector3 sharedEuler = new(8f * landKick + 14f * sprint - 6f * hitKick, 0f, sway.x * 120f);

            // Charged skills: the main hand pulls back with the charge.
            var skills = avatar.Skills;
            bool charging = definition.drawWhileCharging && skills != null && skills.IsCharging;
            draw = Mathf.MoveTowards(draw, charging ? Mathf.Clamp01(skills.ChargeProgress) : 0f, (charging ? 3f : 8f) * dt);

            // Skill motion.
            Vector3 mainPos = Vector3.zero, mainEuler = Vector3.zero, offPos = Vector3.zero, offEuler = Vector3.zero;
            bool hideMain = false;
            if (motionTime < 1f)
            {
                motionTime = Mathf.Min(1f, motionTime + dt / motionDuration);
                Evaluate(motionTime, ref mainPos, ref mainEuler, ref offPos, ref offEuler, ref hideMain);
                float amplitude = definition.motionAmplitude;
                mainPos *= amplitude;
                offPos *= amplitude;
            }
            if (draw > 0f)
            {
                if (charging)
                    chargeSlot = (int)skills.ChargingSlot;
                bool bow = definition.GetMotion(chargeSlot, false) == ViewMotion.BowShot;
                mainPos += new Vector3(bow ? -0.06f : 0.04f, bow ? 0.1f : 0.06f, -0.22f) * draw
                           + (Vector3)(Random.insideUnitCircle * (0.004f * draw * draw)); // tension tremble at full draw
                if (bow)
                    offPos += new Vector3(0.14f, 0.1f, 0.08f) * draw; // x is mirrored below: towards the center
                else
                    mainEuler += new Vector3(-30f, 0f, 0f) * draw;
            }

            Apply(main, definition.mainHand, mainPos + shared, mainEuler + sharedEuler, false);
            Apply(off, definition.offHand, offPos + shared, offEuler + sharedEuler, true);
            if (mainWeapon != null && mainWeapon.activeSelf == hideMain)
                mainWeapon.SetActive(!hideMain);
        }

        private void Evaluate(float t, ref Vector3 mainPos, ref Vector3 mainEuler, ref Vector3 offPos, ref Vector3 offEuler, ref bool hideMain)
        {
            switch (motion)
            {
                case ViewMotion.Swing:
                    Sample(step >= 2 ? OverheadKeys : SwingKeys, t, out mainPos, out mainEuler);
                    if (mirror && step < 2)
                    {
                        // Backhand: same arc the other way round.
                        mainPos.x = -mainPos.x - 0.3f * Mathf.Sin(t * Mathf.PI);
                        mainEuler.y = -mainEuler.y;
                        mainEuler.z = -mainEuler.z;
                    }
                    break;
                case ViewMotion.Overhead:
                    Sample(OverheadKeys, t, out mainPos, out mainEuler);
                    break;
                case ViewMotion.Spin:
                    Sample(SpinKeys, t, out mainPos, out mainEuler);
                    Sample(LowerKeys, t, out offPos, out offEuler);
                    break;
                case ViewMotion.Thrust:
                    Sample(ThrustKeys, t, out mainPos, out mainEuler);
                    break;
                case ViewMotion.ThrustAlternate:
                    if (offLeads)
                        Sample(ThrustKeys, t, out offPos, out offEuler);
                    else
                        Sample(ThrustKeys, t, out mainPos, out mainEuler);
                    break;
                case ViewMotion.BowShot:
                    Sample(BowDrawKeys, t, out mainPos, out mainEuler);
                    Sample(BowHoldKeys, t, out offPos, out offEuler);
                    break;
                case ViewMotion.Cast:
                    Sample(CastKeys, t, out mainPos, out mainEuler);
                    break;
                case ViewMotion.CastBoth:
                    Sample(CastKeys, t, out mainPos, out mainEuler);
                    Sample(CastKeys, t, out offPos, out offEuler);
                    break;
                case ViewMotion.Throw:
                    Sample(ThrowKeys, t, out mainPos, out mainEuler);
                    hideMain = t > 0.42f && t < 0.9f; // out of the hand while it flies
                    break;
                case ViewMotion.Guard:
                    Sample(GuardKeys, t, out offPos, out offEuler);
                    Sample(LowerKeys, Mathf.Min(t, 0.5f) * 0.4f, out mainPos, out mainEuler);
                    break;
                case ViewMotion.Lower:
                    Sample(LowerKeys, t, out mainPos, out mainEuler);
                    Sample(LowerKeys, t, out offPos, out offEuler);
                    break;
                case ViewMotion.Slam:
                    Sample(SlamKeys, t, out mainPos, out mainEuler);
                    Sample(SlamKeys, t, out offPos, out offEuler);
                    break;
                default:
                    Sample(Still, t, out mainPos, out mainEuler);
                    break;
            }
        }

        /// <summary>Offsets are authored for the right hand: mirrored on x (and yaw/roll) for the left one.</summary>
        private static void Apply(Transform hand, ViewHand settings, Vector3 offset, Vector3 euler, bool left)
        {
            if (left)
            {
                offset.x = -offset.x;
                euler.y = -euler.y;
                euler.z = -euler.z;
            }
            hand.localPosition = settings.restPosition + offset;
            hand.localRotation = Quaternion.Euler(euler) * Quaternion.Euler(settings.restEuler);
        }

        private static void Sample(Key[] keys, float t, out Vector3 position, out Vector3 euler)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].T)
                    continue;
                float span = keys[i].T - keys[i - 1].T;
                float k = span > 0f ? Mathf.SmoothStep(0f, 1f, (t - keys[i - 1].T) / span) : 1f;
                position = Vector3.Lerp(keys[i - 1].Position, keys[i].Position, k);
                euler = Vector3.Lerp(keys[i - 1].Euler, keys[i].Euler, k);
                return;
            }
            position = keys[keys.Length - 1].Position;
            euler = keys[keys.Length - 1].Euler;
        }
    }
}
