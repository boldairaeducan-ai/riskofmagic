using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RiskOfMagic.Editor.Models
{
    /// <summary>Builds a CharacterModelRecipe: prefab variant + Animator Controller, then assigns it.</summary>
    public static class CharacterModelBuilder
    {
        public static GameObject Build(CharacterModelRecipe recipe)
        {
            if (recipe.sourceModel == null)
            {
                Debug.LogError($"[Risk of Magic] {recipe.name}: no source model.", recipe);
                return null;
            }
            Directory.CreateDirectory(recipe.outputFolder);
            string prefabPath = $"{recipe.outputFolder}/{recipe.name}.prefab";
            string controllerPath = $"{recipe.outputFolder}/{recipe.name}.controller";

            var controller = recipe.HasClips ? BuildController(recipe, controllerPath) : null;
            if (controller == null && AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(recipe.sourceModel);
            try
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(recipe.rotation) * instance.transform.localRotation;
                FitHeight(instance, recipe.height);

                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    animator = instance.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                foreach (var attachment in recipe.attachments)
                    Attach(instance, animator, attachment, recipe);

                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Assign(recipe, prefab);
                Debug.Log($"[Risk of Magic] {recipe.name}: model built at {prefabPath}"
                          + (controller != null ? $" with {controllerPath}." : " (no idle clip: animated by code)."), prefab);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void FitHeight(GameObject instance, float height)
        {
            if (height <= 0f)
                return;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            if (bounds.size.y > 0.01f)
                instance.transform.localScale *= height / bounds.size.y;
        }

        private static void Attach(GameObject instance, Animator animator, CharacterModelRecipe.Attachment attachment, CharacterModelRecipe recipe)
        {
            if (attachment.prefab == null)
                return;
            Transform bone = animator.isHuman ? animator.GetBoneTransform(attachment.bone) : null;
            if (bone == null)
                bone = FindByName(instance.transform, attachment.bone.ToString());
            if (bone == null)
            {
                Debug.LogWarning($"[Risk of Magic] {recipe.name}: bone {attachment.bone} not found (is the Rig set to Humanoid?).", recipe);
                return;
            }
            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(attachment.prefab, bone);
            weapon.transform.localScale = Vector3.one;
            if (attachment.alignment == CharacterModelRecipe.AttachAlignment.HandGrip)
                PlaceInFist(instance.transform, animator, bone, attachment, weapon.transform);
            else
            {
                weapon.transform.localPosition = attachment.localPosition;
                weapon.transform.localRotation = Quaternion.Euler(attachment.localEulerAngles);
            }
            // Keep the weapon's own world size whatever the bone scale (the model was scaled to its height).
            Vector3 lossy = bone.lossyScale;
            weapon.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, lossy.x), 1f / Mathf.Max(0.0001f, lossy.y), 1f / Mathf.Max(0.0001f, lossy.z))
                                          * (attachment.scale > 0f ? attachment.scale : 1f); // 0 = list element added in the inspector
        }

        /// <summary>
        /// Grip frame computed from the bind pose: fingers = forearm → hand direction, thumb = perpendicular to the
        /// fingers and the down vector (palm down in a T / A pose). Weapon Y → thumb, weapon Z → fingers.
        /// </summary>
        private static void PlaceInFist(Transform model, Animator animator, Transform hand, CharacterModelRecipe.Attachment attachment, Transform weapon)
        {
            bool left = attachment.bone == HumanBodyBones.LeftHand;
            var lowerArm = animator.isHuman ? animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm) : null;
            if (lowerArm == null)
                lowerArm = hand.parent;
            Vector3 forearm = hand.position - lowerArm.position;
            Vector3 fingers = hand.childCount > 0 && (hand.GetChild(0).position - hand.position).sqrMagnitude > 0.0001f
                ? (hand.GetChild(0).position - hand.position).normalized
                : forearm.normalized;
            Vector3 thumb = Vector3.Cross(fingers, -model.up).normalized * (left ? 1f : -1f);
            if (thumb.sqrMagnitude < 0.01f)
                thumb = model.forward;
            var grip = Quaternion.LookRotation(fingers, thumb);
            float handLength = forearm.magnitude * 0.35f;
            weapon.position = hand.position + fingers * handLength + grip * attachment.localPosition;
            weapon.rotation = grip * Quaternion.Euler(attachment.localEulerAngles) * LongAxisToUp(weapon);
        }

        /// <summary>
        /// Rotation bringing the weapon's longest dimension (blade, limbs, shaft), on the side where its mesh extends, onto
        /// local +Y: weapons from any pack are then held the same way (Y = thumb direction). Identity for a bow already
        /// modelled along Y.
        /// </summary>
        private static Quaternion LongAxisToUp(Transform weapon)
        {
            var renderers = weapon.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return Quaternion.identity;
            // Bounds in the weapon's own space (independent of where it currently is).
            var toLocal = weapon.worldToLocalMatrix;
            bool any = false;
            var bounds = new Bounds();
            foreach (var r in renderers)
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 local = toLocal.MultiplyPoint3x4(corner);
                    if (!any)
                        bounds = new Bounds(local, Vector3.zero);
                    else
                        bounds.Encapsulate(local);
                    any = true;
                }
            }
            Vector3 size = bounds.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            float center = bounds.center[axis];
            float extent = Mathf.Max(0.0001f, size[axis] * 0.5f);
            float sign = Mathf.Abs(center) / extent < 0.2f ? 1f : Mathf.Sign(center); // grip in the middle (bow): keep +
            Vector3 longDir = Vector3.zero;
            longDir[axis] = sign;
            return Quaternion.FromToRotation(longDir, Vector3.up);
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

        private static void Assign(CharacterModelRecipe recipe, GameObject prefab)
        {
            if (recipe.targetCharacter != null)
            {
                recipe.targetCharacter.modelPrefab = prefab;
                EditorUtility.SetDirty(recipe.targetCharacter);
            }
            if (recipe.targetEnemy != null)
            {
                recipe.targetEnemy.modelPrefab = prefab;
                EditorUtility.SetDirty(recipe.targetEnemy);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- Animator Controller

        private static readonly string[] Floats = { "Speed", "MoveX", "MoveY", "VerticalSpeed", "AimPitch", "ActionSpeed" };
        private static readonly string[] Bools = { "Grounded", "Sprinting", "Downed", "Dead", "Frozen" };
        private static readonly string[] Ints = { "SkillSlot", "AttackIndex" };
        private static readonly string[] Triggers = { "Jump", "Land", "Hit", "UseEquipment", "Revive", "Spawn", "Dodge", "Leap", "Cast", "Attack" };

        private static AnimatorController BuildController(CharacterModelRecipe recipe, string path)
        {
            AssetDatabase.DeleteAsset(path); // generated: always rebuilt from the recipe
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var p in Floats)
                controller.AddParameter(p, AnimatorControllerParameterType.Float);
            foreach (var p in Bools)
                controller.AddParameter(p, AnimatorControllerParameterType.Bool);
            foreach (var p in Ints)
                controller.AddParameter(p, AnimatorControllerParameterType.Int);
            foreach (var p in Triggers)
                controller.AddParameter(p, AnimatorControllerParameterType.Trigger);
            // Grounded starts true so a model doesn't begin in the air.
            var parameters = controller.parameters;
            foreach (var p in parameters)
            {
                if (p.name == "Grounded")
                    p.defaultBool = true;
                if (p.name == "ActionSpeed")
                    p.defaultFloat = 1f;
            }
            controller.parameters = parameters;

            BuildBaseLayer(recipe, controller);
            BuildActionLayer(recipe, controller, path);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void BuildBaseLayer(CharacterModelRecipe recipe, AnimatorController controller)
        {
            var machine = controller.layers[0].stateMachine;

            // Locomotion: 1D blend on Speed (idle → walk → run).
            // Built by hand and saved inside the controller (children set before saving, so they are persisted).
            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };
            var children = new System.Collections.Generic.List<ChildMotion> { new() { motion = recipe.idle, threshold = 0f, timeScale = 1f } };
            if (recipe.walk != null)
                children.Add(new ChildMotion { motion = recipe.walk, threshold = recipe.walkSpeed, timeScale = 1f });
            if (recipe.run != null)
                children.Add(new ChildMotion { motion = recipe.run, threshold = Mathf.Max(recipe.runSpeed, recipe.walkSpeed + 0.1f), timeScale = 1f });
            tree.children = children.ToArray();
            AssetDatabase.AddObjectToAsset(tree, controller);
            EditorUtility.SetDirty(tree);
            var locomotion = machine.AddState("Locomotion");
            locomotion.motion = tree;
            machine.defaultState = locomotion;

            // Air: Jump → Fall (loop) → Land/Locomotion when grounded again.
            AnimatorState fall = null;
            if (recipe.fall != null)
            {
                fall = machine.AddState("Fall");
                fall.motion = recipe.fall;
                var toFall = locomotion.AddTransition(fall);
                toFall.hasExitTime = false;
                toFall.duration = 0.2f;
                toFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                ReturnWhen(fall, locomotion, recipe.land, machine, "Grounded", true);
            }
            if (recipe.jump != null)
            {
                var jump = machine.AddState("Jump");
                jump.motion = recipe.jump;
                var toJump = machine.AddAnyStateTransition(jump);
                toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
                toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                toJump.duration = 0.1f;
                toJump.canTransitionToSelf = false;
                if (fall != null)
                {
                    var next = jump.AddTransition(fall);
                    next.hasExitTime = true;
                    next.exitTime = 0.8f;
                    next.duration = 0.15f;
                }
                ReturnWhen(jump, locomotion, recipe.land, machine, "Grounded", true, requireExit: true);
            }

            // Reactions.
            if (recipe.hit != null)
            {
                var hit = machine.AddState("Hit");
                hit.motion = recipe.hit;
                var toHit = machine.AddAnyStateTransition(hit);
                toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Downed");
                toHit.duration = 0.05f;
                toHit.canTransitionToSelf = true;
                ExitTo(hit, locomotion);
            }
            if (recipe.downed != null)
            {
                var downed = machine.AddState("Downed");
                downed.motion = recipe.downed;
                var toDowned = machine.AddAnyStateTransition(downed);
                toDowned.AddCondition(AnimatorConditionMode.If, 0f, "Downed");
                toDowned.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                toDowned.duration = 0.2f;
                toDowned.canTransitionToSelf = false;
                AnimatorState revive = null;
                if (recipe.revive != null)
                {
                    revive = machine.AddState("Revive");
                    revive.motion = recipe.revive;
                    ExitTo(revive, locomotion);
                }
                var up = downed.AddTransition(revive != null ? revive : locomotion);
                up.hasExitTime = false;
                up.duration = 0.2f;
                up.AddCondition(AnimatorConditionMode.IfNot, 0f, "Downed");
            }
            if (recipe.death != null)
            {
                var death = machine.AddState("Death");
                death.motion = recipe.death;
                var toDeath = machine.AddAnyStateTransition(death);
                toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                toDeath.duration = 0.1f;
                toDeath.canTransitionToSelf = false;
            }
        }

        private static void BuildActionLayer(CharacterModelRecipe recipe, AnimatorController controller, string path)
        {
            bool any = recipe.useEquipment != null;
            foreach (var action in recipe.actions)
                any |= action.clip != null;
            if (!any)
                return;

            controller.AddLayer("Actions");
            var layers = controller.layers;
            var layer = layers[layers.Length - 1];
            layer.defaultWeight = 1f;
            if (recipe.actionsUpperBodyOnly)
            {
                var mask = new AvatarMask { name = "UpperBody" };
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                {
                    var part = (AvatarMaskBodyPart)i;
                    bool lower = part == AvatarMaskBodyPart.Root || part == AvatarMaskBodyPart.LeftLeg || part == AvatarMaskBodyPart.RightLeg
                                 || part == AvatarMaskBodyPart.LeftFootIK || part == AvatarMaskBodyPart.RightFootIK;
                    mask.SetHumanoidBodyPartActive(part, !lower);
                }
                AssetDatabase.AddObjectToAsset(mask, path);
                layer.avatarMask = mask;
            }
            controller.layers = layers;

            var machine = layer.stateMachine;
            // Write Defaults off on this layer: an empty humanoid state would otherwise force the upper body into the
            // default muscle pose (arms half bent, the « T-pose » look) instead of letting the base layer through.
            var empty = machine.AddState("Empty");
            empty.writeDefaultValues = false;
            machine.defaultState = empty;

            foreach (var action in recipe.actions)
            {
                if (action.clip == null || string.IsNullOrEmpty(action.trigger))
                    continue;
                var state = machine.AddState(action.index >= 0 ? $"{action.trigger}_{action.index}" : action.trigger);
                state.motion = action.clip;
                state.writeDefaultValues = false;
                if (action.matchCastDuration)
                {
                    // Clip normalized to 1 s, then scaled live by ActionSpeed (= 1 / action duration).
                    state.speed = action.clip.length * action.speed;
                    state.speedParameter = "ActionSpeed";
                    state.speedParameterActive = true;
                }
                else
                    state.speed = action.speed;
                var enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, action.trigger);
                if (action.index >= 0)
                    enter.AddCondition(AnimatorConditionMode.Equals, action.index, action.trigger == "Attack" && recipe.targetEnemy != null ? "AttackIndex" : "SkillSlot");
                enter.duration = 0.05f;
                enter.canTransitionToSelf = true;
                ExitTo(state, empty);
            }
            if (recipe.useEquipment != null)
            {
                var state = machine.AddState("UseEquipment");
                state.motion = recipe.useEquipment;
                state.writeDefaultValues = false;
                var enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "UseEquipment");
                enter.duration = 0.05f;
                ExitTo(state, empty);
            }
        }

        private static void ExitTo(AnimatorState from, AnimatorState to)
        {
            var back = from.AddTransition(to);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.15f;
        }

        private static void ReturnWhen(AnimatorState from, AnimatorState locomotion, AnimationClip landClip, AnimatorStateMachine machine,
            string parameter, bool value, bool requireExit = false)
        {
            AnimatorState target = locomotion;
            if (landClip != null)
            {
                var land = FindState(machine, "Land");
                if (land == null)
                {
                    land = machine.AddState("Land");
                    land.motion = landClip;
                    ExitTo(land, locomotion);
                }
                target = land;
            }
            var back = from.AddTransition(target);
            back.hasExitTime = requireExit;
            back.exitTime = 0.5f;
            back.duration = 0.1f;
            back.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (var child in machine.states)
                if (child.state.name == name)
                    return child.state;
            return null;
        }
    }
}
