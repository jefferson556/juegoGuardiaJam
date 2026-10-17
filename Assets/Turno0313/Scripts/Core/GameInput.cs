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

            foreach (var action in new[] { Move, LookMouse, LookStick, Run, Interact, Report, Flashlight, Pause })
                action.Enable();
        }
    }
}
