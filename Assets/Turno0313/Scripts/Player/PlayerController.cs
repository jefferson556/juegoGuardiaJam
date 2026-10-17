using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turno0313
{
    // First person movement and look. Other systems (alarm panel, camera monitors, menus) can take
    // control away with SetControl(false) and give it back afterwards.
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] Camera view;
        [Header("Movimiento")]
        [SerializeField] float walkSpeed = 2.2f;
        [SerializeField] float runSpeed = 3.8f;
        [SerializeField] float gravity = 20f;
        [SerializeField] float stepLength = .75f;
        [SerializeField] float headBob = .03f;
        [Header("Cámara")]
        [SerializeField] float mouseSensitivity = .1f;
        [SerializeField] float stickSensitivity = 150f;
        [SerializeField] float maxPitch = 85f;

        public static PlayerController Instance { get; private set; }
        public Camera View => view;
        // True while the player is actually steering (cursor captured and nobody took control away).
        public bool HasControl => controlEnabled && Cursor.lockState == CursorLockMode.Locked;
        public bool IsRunning { get; private set; }
        public event Action<bool> Step;

        CharacterController body;
        bool controlEnabled = true;
        float yaw, pitch, verticalSpeed, stepDistance, bobPhase, bobWeight;
        Vector3 cameraRest;

        void Awake()
        {
            Instance = this;
            body = GetComponent<CharacterController>();
            if (!view)
                view = GetComponentInChildren<Camera>();
            cameraRest = view.transform.localPosition;
            yaw = transform.eulerAngles.y;
            GameInput.Enable();
        }

        void OnEnable() => CaptureCursor(controlEnabled);

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (controlEnabled)
            {
                if (GameInput.Pause.WasPressedThisFrame())
                    CaptureCursor(Cursor.lockState != CursorLockMode.Locked);
                else if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    CaptureCursor(true);
            }

            if (HasControl)
            {
                var mouse = GameInput.LookMouse.ReadValue<Vector2>() * mouseSensitivity;
                var stick = GameInput.LookStick.ReadValue<Vector2>() * (stickSensitivity * dt);
                yaw += mouse.x + stick.x;
                pitch = Mathf.Clamp(pitch - mouse.y - stick.y, -maxPitch, maxPitch);
            }

            transform.rotation = Quaternion.Euler(0, yaw, 0);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);

            var input = HasControl ? Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1) : Vector2.zero;
            IsRunning = HasControl && GameInput.Run.IsPressed() && input.y > .1f;
            var planar = (transform.right * input.x + transform.forward * input.y) * (IsRunning ? runSpeed : walkSpeed);
            if (body.isGrounded && verticalSpeed < 0)
                verticalSpeed = -2f;
            verticalSpeed -= gravity * dt;

            var before = transform.position;
            body.Move((planar + Vector3.up * verticalSpeed) * dt);
            var moved = transform.position - before;
            moved.y = 0;
            float travelled = moved.magnitude;

            stepDistance += travelled;
            float length = IsRunning ? stepLength * 1.25f : stepLength;
            if (stepDistance >= length)
            {
                stepDistance = 0;
                Step?.Invoke(IsRunning);
            }

            bobWeight = Mathf.MoveTowards(bobWeight, travelled > .0005f ? 1 : 0, dt * 4);
            bobPhase += travelled / length * Mathf.PI;
            view.transform.localPosition = cameraRest + Vector3.up * (Mathf.Sin(bobPhase * 2) * headBob * bobWeight);
        }

        // Lets other systems (panel, monitors, menus) take the controls and the cursor.
        public void SetControl(bool enabled)
        {
            controlEnabled = enabled;
            CaptureCursor(enabled);
        }

        // For night resets and scripted moments: move without fighting the CharacterController.
        public void Teleport(Vector3 position, float newYaw, float newPitch = 0)
        {
            body.enabled = false;
            transform.position = position;
            body.enabled = true;
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        public void SetLook(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, -maxPitch, maxPitch);
        }

        static void CaptureCursor(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
