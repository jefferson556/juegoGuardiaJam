using UnityEngine;

namespace Turno0313
{
    public static class Doors
    {
        // Every door of the scene closed at once (night reset, waking up, falling back into the dream).
        public static void CloseAll()
        {
            foreach (var door in Object.FindObjectsByType<Door>(FindObjectsInactive.Exclude))
                door.ResetClosed();
            foreach (var door in Object.FindObjectsByType<SlidingDoor>(FindObjectsInactive.Exclude))
                door.ResetClosed();
        }
    }
}
