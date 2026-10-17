using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;

#endif
namespace Piso13
{
    public sealed class P13Input
    {
        public readonly P13Settings Settings;
        readonly HashSet<string> blocked = new HashSet<string>();
        bool padNeutral;
        public static readonly string[] Actions = {"forward", "backward", "left", "right", "interact", "examine", "flashlight", "cctv", "map", "door", "light", "sprint", "crouch", "cancel"};
        public static readonly string[] Titles = {"Avanzar", "Retroceder", "Izquierda", "Derecha", "Interactuar", "Examinar pista", "Linterna", "CCTV", "Mapa", "Puerta", "Luz del puesto", "Correr", "Agacharse", "Cancelar tarea"};
        public static List<Binding> Defaults()
        {
            string[] keys = {"W", "S", "A", "D", "E", "R", "F", "V", "M", "Q", "L", "LeftShift", "C", "X"};
            string[] pad = {"", "", "", "", "A", "X", "RB", "Y", "UP", "LEFT", "RIGHT", "LT", "R3", "DOWN"};
            return Actions.Select((a, i) => new Binding(a, keys[i], pad[i])).ToList();
        }

        public P13Input(P13Settings s)
        {
            Settings = s;
            var saved = s.bindings ?? new List<Binding>();
            s.bindings = Defaults();
            foreach (var b in s.bindings)
            {
                var candidate = saved.FirstOrDefault(x => x != null && x.action == b.action);
                KeyCode key;
                if (candidate == null)
                    continue;
                if (!string.IsNullOrEmpty(candidate.key) && Enum.TryParse(candidate.key, true, out key) && Enum.IsDefined(typeof(KeyCode), key))
                    b.key = candidate.key;
                if (new[]{"", "A", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "UP", "DOWN", "LEFT", "RIGHT"}.Contains(candidate.pad))
                    b.pad = candidate.pad;
            }
        }

        public string Label(string action)
        {
            return Settings.bindings.First(x => x.action == action).key;
        }

        public void Remap(string action, string token, bool pad)
        {
            var b = Settings.bindings.First(x => x.action == action);
            var other = Settings.bindings.FirstOrDefault(x => x != b && (pad ? x.pad : x.key) == token);
            string old = pad ? b.pad : b.key;
            if (other != null)
            {
                if (pad)
                    other.pad = old;
                else
                    other.key = old;
            }

            if (pad)
                b.pad = token;
            else
                b.key = token;
        }

        public bool Held(string a)
        {
            var b = Settings.bindings.First(x => x.action == a);
            bool down = Key(b.key, false) || Pad(b.pad, false);
            if (!down)
                blocked.Remove(a);
            return down && !blocked.Contains(a);
        }

        public bool Pressed(string a)
        {
            var b = Settings.bindings.First(x => x.action == a);
            if (!Key(b.key, false) && !Pad(b.pad, false))
                blocked.Remove(a);
            return !blocked.Contains(a) && (Key(b.key, true) || Pad(b.pad, true));
        }

        public void Neutralize()
        {
            foreach (var a in Actions)
                blocked.Add(a);
            padNeutral = true;
        }

        public static bool Key(string name, bool edge)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null)
                return false;
            if (name == "LeftShift")
                return edge ? (k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame) : (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
            string n = name == "Return" ? "Enter" : name == "Esc" ? "Escape" : name;
            if (n.StartsWith("Alpha"))
                n = "Digit" + n.Substring(5);
            UnityEngine.InputSystem.Key key;
            if (Enum.TryParse(n, true, out key))
            {
                var control = k[key];
                return edge ? control.wasPressedThisFrame : control.isPressed;
            }

            return false;
#else
 KeyCode key;if(!Enum.TryParse(name,true,out key))return false;if(key==KeyCode.LeftShift)return edge?(UnityEngine.Input.GetKeyDown(key)||UnityEngine.Input.GetKeyDown(KeyCode.RightShift)):(UnityEngine.Input.GetKey(key)||UnityEngine.Input.GetKey(KeyCode.RightShift));return edge?UnityEngine.Input.GetKeyDown(key):UnityEngine.Input.GetKey(key);
#endif
        }

        public static bool Pad(string name, bool edge)
        {
            if (string.IsNullOrEmpty(name))
                return false;
#if ENABLE_INPUT_SYSTEM
            var p = Gamepad.current;
            if (p == null)
                return false;
            UnityEngine.InputSystem.Controls.ButtonControl c = null;
            switch (name)
            {
                case "A":
                    c = p.buttonSouth;
                    break;
                case "B":
                    c = p.buttonEast;
                    break;
                case "X":
                    c = p.buttonWest;
                    break;
                case "Y":
                    c = p.buttonNorth;
                    break;
                case "LB":
                    c = p.leftShoulder;
                    break;
                case "RB":
                    c = p.rightShoulder;
                    break;
                case "LT":
                    c = p.leftTrigger;
                    break;
                case "RT":
                    c = p.rightTrigger;
                    break;
                case "L3":
                    c = p.leftStickButton;
                    break;
                case "R3":
                    c = p.rightStickButton;
                    break;
                case "UP":
                    c = p.dpad.up;
                    break;
                case "DOWN":
                    c = p.dpad.down;
                    break;
                case "LEFT":
                    c = p.dpad.left;
                    break;
                case "RIGHT":
                    c = p.dpad.right;
                    break;
                case "MENU":
                    c = p.startButton;
                    break;
            }

            return c != null && (edge ? c.wasPressedThisFrame : c.isPressed);
#else
 string[] names={"A","B","X","Y","LB","RB","OPTIONS","MENU","L3","R3"};int i=Array.IndexOf(names,name);if(i<0)return false;KeyCode key=(KeyCode)((int)KeyCode.JoystickButton0+i);return edge?UnityEngine.Input.GetKeyDown(key):UnityEngine.Input.GetKey(key);
#endif
        }

        public Vector2 Movement()
        {
            var v = new Vector2((Held("right") ? 1 : 0) - (Held("left") ? 1 : 0), (Held("forward") ? 1 : 0) - (Held("backward") ? 1 : 0));
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null)
            {
                var stick = Gamepad.current.leftStick.ReadValue();
                if (stick.magnitude < .15f)
                    padNeutral = false;
                if (!padNeutral)
                    v += stick;
            }

#endif
            return Vector2.ClampMagnitude(v, 1);
        }

        public Vector2 Look(float dt)
        {
            Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                v = Mouse.current.delta.ReadValue() * .12f;
            if (Gamepad.current != null)
                v += Gamepad.current.rightStick.ReadValue() * 120 * dt;
#else
 v=new Vector2(UnityEngine.Input.GetAxisRaw("Mouse X"),UnityEngine.Input.GetAxisRaw("Mouse Y"))*2;
#endif
            return v * Settings.sensitivity;
        }

        public static bool Back => Key("Escape", true) || Pad("B", true) || Pad("MENU", true);
        public static bool Confirm => Key("Return", true) || Key("Space", true) || Pad("A", true);
        public static int Vertical => Key("UpArrow", true) || Pad("UP", true) ? -1 : Key("DownArrow", true) || Key("Tab", true) || Pad("DOWN", true) ? 1 : 0;
        public static int Horizontal => Key("LeftArrow", true) || Pad("LEFT", true) ? -1 : Key("RightArrow", true) || Pad("RIGHT", true) ? 1 : 0;
        public static string CaptureKey()
        {
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if ((k >= KeyCode.A && k <= KeyCode.Z) || (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) || k == KeyCode.Space || k == KeyCode.LeftShift)
                    if (Key(k.ToString(), true))
                        return k.ToString();
            }

            return null;
        }

        public static string CapturePad()
        {
            foreach (var k in new[]{"A", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "UP", "DOWN", "LEFT", "RIGHT"})
                if (Pad(k, true))
                    return k;
            return null;
        }
    }
}
