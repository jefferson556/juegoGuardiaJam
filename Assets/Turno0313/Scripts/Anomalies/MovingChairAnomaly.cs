using UnityEngine;

namespace Turno0313
{
    // "Una silla cambia de sitio cada vez que te das la vuelta": while active, every time the chair
    // leaves the player's view it jumps to the next spot. Reporting it puts it back in its place.
    public sealed class MovingChairAnomaly : Anomaly
    {
        [SerializeField] Transform chair;
        [SerializeField] Transform[] spots;
        [SerializeField] float lookSeconds = .3f;
        [Tooltip("No salta a un sitio a menos de esta distancia del jugador.")]
        [SerializeField] float keepAway = 1.3f;

        Vector3 homePosition;
        Quaternion homeRotation;
        int next;
        float seen;
        bool moved;

        protected override bool Manifested => moved;

        protected override void Awake()
        {
            base.Awake();
            homePosition = chair.position;
            homeRotation = chair.rotation;
        }

        protected override void ShowNormal()
        {
            chair.SetPositionAndRotation(homePosition, homeRotation);
            seen = 0;
            next = 0;
            moved = false;
        }

        // Activation only happens while the player can't see the chair, so it can jump right away.
        protected override void ShowAnomalous()
        {
            seen = 0;
            next = 0;
            moved = false;
            Jump();
        }

        protected internal override void Tick(bool visible, float dt)
        {
            if (!IsActive)
                return;
            if (visible)
                seen += dt;
            else if (seen >= lookSeconds)
            {
                seen = 0;
                Jump();
            }
        }

        void Jump()
        {
            if (spots == null || spots.Length == 0)
                return;
            var cam = Camera.main;
            for (int tries = 0; tries < spots.Length; tries++)
            {
                var spot = spots[next % spots.Length];
                next++;
                if (!spot || (cam && Vector3.Distance(Flat(spot.position), Flat(cam.transform.position)) < keepAway))
                    continue;
                chair.SetPositionAndRotation(spot.position, spot.rotation);
                moved = true;
                return;
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
