using UnityEngine;

namespace Turno0313
{
    // Anything the player can use with E. The prompt is shown next to the crosshair.
    public abstract class Interactable : MonoBehaviour
    {
        public abstract string Prompt { get; }
        public virtual bool CanInteract => true;
        public abstract void Interact(PlayerController player);
    }
}
