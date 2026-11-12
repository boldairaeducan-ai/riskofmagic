using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// The game's single action map (built in code, physical key positions: WASD = ZQSD on AZERTY), shared by the local
    /// player and the menus. Keyboard / mouse bindings can be changed at runtime (Options → Raccourcis): overrides
    /// apply immediately to the live actions and are saved in PlayerPrefs.
    /// </summary>
    public static class GameInput
    {
        private const string PrefsKey = "RiskOfMagic.Bindings";

        /// <summary>One rebindable keyboard / mouse binding.</summary>
        public sealed class Rebindable
        {
            public string Label;
            public string Category;
            public InputAction Action;
            public int BindingIndex;

            public string Path => Action.bindings[BindingIndex].effectivePath;
            public string Display => DisplayName(Path);
        }

        private static InputActionMap map;
        private static readonly List<Rebindable> rebindables = new();
        private static InputActionRebindingExtensions.RebindingOperation rebinding;

        public static InputAction Move, LookPointer, LookStick, Jump, Sprint, ToggleCursor, Interact, Equipment;
        public static InputAction[] Skills;

        /// <summary>Raised whenever a binding changes (HUD key labels, options rows).</summary>
        public static event Action BindingsChanged;

        public static bool IsRebinding => rebinding != null;
        private static int lastRebindFrame = -10;

        /// <summary>A rebind is running or just ended (the key pressed must not also act: Escape, Tab...).</summary>
        public static bool MenuKeysBlocked => rebinding != null || Time.frameCount - lastRebindFrame <= 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            rebinding?.Dispose();
            rebinding = null;
            map?.Dispose();
            map = null;
            rebindables.Clear();
            BindingsChanged = null;
        }

        public static InputActionMap Map
        {
            get
            {
                if (map == null)
                    Build();
                return map;
            }
        }

        public static IReadOnlyList<Rebindable> Rebindables
        {
            get
            {
                if (map == null)
                    Build();
                return rebindables;
            }
        }

        private static void Build()
        {
            map = new InputActionMap("Player");

            Move = map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");

            LookPointer = map.AddAction("LookPointer", InputActionType.Value, "<Mouse>/delta");
            LookStick = map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            Jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            ToggleCursor = Button("ToggleCursor", "<Keyboard>/escape", "<Gamepad>/start");
            Interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            Equipment = Button("Equipment", "<Keyboard>/f", "<Gamepad>/dpad/up");
            Skills = new[]
            {
                Button("Primary", "<Mouse>/leftButton", "<Gamepad>/rightTrigger"),
                Button("Secondary", "<Mouse>/rightButton", "<Gamepad>/leftTrigger"),
                Button("Utility", "<Keyboard>/q", "<Gamepad>/leftShoulder"), // physical key: A on AZERTY
                Button("Special", "<Keyboard>/r", "<Gamepad>/rightShoulder")
            };

            rebindables.Clear();
            Add("Avancer", "Déplacement", Move, 1);
            Add("Reculer", "Déplacement", Move, 2);
            Add("Gauche", "Déplacement", Move, 3);
            Add("Droite", "Déplacement", Move, 4);
            Add("Sauter", "Déplacement", Jump, 0);
            Add("Sprint", "Déplacement", Sprint, 0);
            Add("Compétence principale", "Combat", Skills[0], 0);
            Add("Compétence secondaire", "Combat", Skills[1], 0);
            Add("Compétence utilitaire", "Combat", Skills[2], 0);
            Add("Compétence spéciale", "Combat", Skills[3], 0);
            Add("Équipement", "Combat", Equipment, 0);
            Add("Interagir", "Interaction", Interact, 0);

            string saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
            {
                try
                {
                    map.LoadBindingOverridesFromJson(saved);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[GameInput] Saved bindings ignored: {exception.Message}");
                }
            }
        }

        private static InputAction Button(string name, params string[] bindings)
        {
            var action = map.AddAction(name, InputActionType.Button);
            foreach (string binding in bindings)
                action.AddBinding(binding);
            return action;
        }

        private static void Add(string label, string category, InputAction action, int index)
            => rebindables.Add(new Rebindable { Label = label, Category = category, Action = action, BindingIndex = index });

        // ---------------------------------------------------------------- Rebinding

        /// <summary>
        /// Waits for the next key or mouse button and assigns it. A key already used by another action is swapped with
        /// this one's previous key. Escape cancels. <paramref name="done"/> runs on completion and on cancel.
        /// </summary>
        public static void StartRebind(Rebindable target, Action done)
        {
            CancelRebind();
            string previous = target.Path;
            bool wasEnabled = map.enabled;
            map.Disable(); // actions must be disabled while rebinding

            rebinding = target.Action.PerformInteractiveRebinding(target.BindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.08f)
                .OnComplete(operation =>
                {
                    SwapConflict(target, previous);
                    Finish(wasEnabled, done);
                })
                .OnCancel(operation => Finish(wasEnabled, done))
                .Start();
        }

        public static void CancelRebind()
        {
            if (rebinding == null)
                return;
            rebinding.Cancel();
        }

        private static void Finish(bool reenable, Action done)
        {
            rebinding?.Dispose();
            rebinding = null;
            lastRebindFrame = Time.frameCount;
            if (reenable)
                map.Enable();
            Save();
            done?.Invoke();
        }

        private static void SwapConflict(Rebindable changed, string previous)
        {
            string path = changed.Path;
            foreach (var other in rebindables)
            {
                if (other == changed || !string.Equals(other.Path, path, StringComparison.OrdinalIgnoreCase))
                    continue;
                other.Action.ApplyBindingOverride(other.BindingIndex, previous);
            }
        }

        public static void ResetAll()
        {
            CancelRebind();
            map?.RemoveAllBindingOverrides();
            Save();
        }

        private static void Save()
        {
            if (map == null)
                return;
            PlayerPrefs.SetString(PrefsKey, map.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }

        // ---------------------------------------------------------------- Display

        /// <summary>Key name in the player's keyboard layout ("A" for the physical Q on AZERTY), mouse buttons in French.</summary>
        public static string DisplayName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "—";
            string lower = path.ToLowerInvariant();
            if (lower.Contains("<mouse>/leftbutton"))
                return "Clic G";
            if (lower.Contains("<mouse>/rightbutton"))
                return "Clic D";
            if (lower.Contains("<mouse>/middlebutton"))
                return "Clic molette";
            if (lower.Contains("<mouse>/backbutton"))
                return "Souris 4";
            if (lower.Contains("<mouse>/forwardbutton"))
                return "Souris 5";
            var control = InputSystem.FindControl(path);
            string name = control != null ? control.displayName : InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return name switch
            {
                "Left Shift" => "Maj gauche",
                "Right Shift" => "Maj droite",
                "Left Control" => "Ctrl gauche",
                "Right Control" => "Ctrl droite",
                "Left Alt" => "Alt gauche",
                "Right Alt" => "Alt Gr",
                "Space" => "Espace",
                "Tab" => "Tab",
                "Enter" => "Entrée",
                "Backspace" => "Retour arrière",
                _ => name.Length == 1 ? name.ToUpperInvariant() : name
            };
        }

        /// <summary>Label of a skill slot's key (HUD, lobby).</summary>
        public static string SkillKey(int slot)
        {
            _ = Map;
            return slot >= 0 && slot < Skills.Length ? DisplayName(Skills[slot].bindings[0].effectivePath) : string.Empty;
        }
    }
}
