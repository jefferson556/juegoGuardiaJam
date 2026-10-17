using UnityEngine;

namespace Turno0313
{
    // Marks a normal prop the player can point at and report. Reporting it counts as a wrong report,
    // so the player can't learn which objects are special just by aiming at them.
    public sealed class Reportable : MonoBehaviour
    {
        [SerializeField] string displayName;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    }
}
