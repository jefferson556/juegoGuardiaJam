using UnityEngine;

namespace Turno0313
{
    // E: interact with whatever Interactable is under the crosshair.
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] float range = 2.4f;

        public Interactable Current { get; private set; }
        PlayerController player;

        void Awake() => player = GetComponent<PlayerController>();

        void Update()
        {
            Current = null;
            var eye = player.View.transform;
            if (Physics.Raycast(eye.position, eye.forward, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<Interactable>();
                if (target && target.isActiveAndEnabled && target.CanInteract)
                    Current = target;
            }

            if (Current && player.HasControl && GameInput.Interact.WasPressedThisFrame())
                Current.Interact(player);
        }
    }
}
