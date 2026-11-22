using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("Combat.PlayModeTests")]

namespace Combat.Unity
{
    internal static class CombatControls
    {
        public const KeyCode Pause = KeyCode.Escape;
        public const KeyCode Reload = KeyCode.F5;
        public const KeyCode Step = KeyCode.Period;
        public const int MainSkillCount = 4;

        public readonly struct Binding
        {
            public readonly string Action, Label, AlternateLabel;
            public readonly KeyCode Primary, Alternate;
            public Binding(string action, string label, KeyCode primary, KeyCode alternate = KeyCode.None, string alternateLabel = null)
            {
                Action = action;
                Label = label;
                Primary = primary;
                Alternate = alternate;
                AlternateLabel = alternateLabel;
            }
        }

        static readonly Binding[] Skills =
        {
            new Binding("Fire1", "LMB", KeyCode.Mouse0, KeyCode.Keypad1, "Num 1"),
            new Binding("Fire2", "Q", KeyCode.Q, KeyCode.Keypad2, "Num 2"),
            new Binding("Fire3", "E", KeyCode.E, KeyCode.Keypad3, "Num 3"),
            new Binding("Fire4", "R", KeyCode.R, KeyCode.Keypad4, "Num 4"),
            new Binding("Roll", "Space", KeyCode.Space),
            new Binding("Fire5", "F", KeyCode.F),
            new Binding("Homing", "C", KeyCode.C),
            new Binding("Monkey", "X", KeyCode.X)
        };

        public static Binding At(int index) => Skills[index];
        public static int Count => Skills.Length;
        public static bool OverPanel(Rect panel) => panel.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

        public readonly struct ButtonFrame
        {
            public readonly bool Pressed, Held, Released;
            public ButtonFrame(bool pressed, bool held, bool released)
            { Pressed = pressed; Held = held; Released = released; }
        }

        public sealed class Sampler
        {
            readonly ButtonState[] _buttons = new ButtonState[Skills.Length];

            public ButtonFrame Read(int index, bool allowMousePress)
            {
                var binding = Skills[index];
                bool primaryDown = (binding.Primary != KeyCode.Mouse0 || allowMousePress) && Input.GetKeyDown(binding.Primary);
                bool hasAlternate = binding.Alternate != KeyCode.None;
                return _buttons[index].Sample(
                    primaryDown, Input.GetKey(binding.Primary),
                    hasAlternate && Input.GetKeyDown(binding.Alternate),
                    hasAlternate && Input.GetKey(binding.Alternate));
            }

            public void Cancel() => System.Array.Clear(_buttons, 0, _buttons.Length);
        }

        internal struct ButtonState
        {
            bool _primaryHeld, _alternateHeld;

            // A key joins the action only on an admitted down edge. This keeps a
            // mouse click that began over the HUD from turning into a field cast.
            public ButtonFrame Sample(bool primaryDown, bool primaryHeld, bool alternateDown, bool alternateHeld)
            {
                bool wasHeld = _primaryHeld || _alternateHeld;
                _primaryHeld = (_primaryHeld || primaryDown) && primaryHeld;
                _alternateHeld = (_alternateHeld || alternateDown) && alternateHeld;
                bool pressed = !wasHeld && (primaryDown || alternateDown);
                bool held = _primaryHeld || _alternateHeld;
                return new ButtonFrame(pressed, held, (wasHeld || pressed) && !held);
            }
        }
    }
}
