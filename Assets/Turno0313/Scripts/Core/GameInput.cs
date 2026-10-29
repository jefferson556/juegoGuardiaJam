using UnityEngine.InputSystem;

namespace Turno0313
{
    // Every player binding in one place: keyboard/mouse and gamepad.
    public static class GameInput
    {
        public static readonly InputAction Move = new InputAction("Move", InputActionType.Value);
        public static readonly InputAction LookMouse = new InputAction("LookMouse", InputActionType.Value);
        public static readonly InputAction LookStick = new InputAction("LookStick", InputActionType.Value);
        public static readonly InputAction Run = new InputAction("Run", InputActionType.Button);
        public static readonly InputAction Interact = new InputAction("Interact", InputActionType.Button);
        public static readonly InputAction Report = new InputAction("Report", InputActionType.Button);
        public static readonly InputAction Flashlight = new InputAction("Flashlight", InputActionType.Button);
        public static readonly InputAction Pause = new InputAction("Pause", InputActionType.Button);
        public static readonly InputAction Notes = new InputAction("Notes", InputActionType.Button);

        public static readonly InputAction Cameras = new InputAction("Cameras", InputActionType.Button);
        public static readonly InputAction Crouch = new InputAction("Crouch", InputActionType.Button);
        public static InputAction[] Actions => new[] { Move, LookMouse, LookStick, Run, Interact, Report, Flashlight, Pause, Notes, Cameras, Crouch };
        static bool ready;

        public static void Enable()
        {
            if (ready)
                return;
            ready = true;

            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            LookMouse.AddBinding("<Mouse>/delta");
            LookStick.AddBinding("<Gamepad>/rightStick");

            Run.AddBinding("<Keyboard>/leftShift");
            Run.AddBinding("<Gamepad>/leftStickPress");

            Interact.AddBinding("<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonSouth");

            Report.AddBinding("<Keyboard>/r");
            Report.AddBinding("<Mouse>/rightButton");
            Report.AddBinding("<Gamepad>/buttonNorth");

            Flashlight.AddBinding("<Keyboard>/f");
            Flashlight.AddBinding("<Gamepad>/rightShoulder");

            Pause.AddBinding("<Keyboard>/escape");
            Pause.AddBinding("<Gamepad>/start");

            Notes.AddBinding("<Keyboard>/tab");
            Notes.AddBinding("<Gamepad>/select");

            Cameras.AddBinding("<Keyboard>/v"); Cameras.AddBinding("<Gamepad>/buttonWest");
            Crouch.AddBinding("<Keyboard>/c"); Crouch.AddBinding("<Gamepad>/rightStickPress");
            Run.AddBinding("<Keyboard>/rightShift");
            foreach (var saved in DemoSettings.Current.bindings)
            {
                if (saved == null) continue;
                foreach (var action in Actions)
                    if (action.name == saved.action && saved.index >= 0 && saved.index < action.bindings.Count && !action.bindings[saved.index].isComposite)
                        action.ApplyBindingOverride(saved.index, saved.path);
            }
            foreach (var action in Actions)
                action.Enable();
        }
    }
}
