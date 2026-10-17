using UnityEngine;

namespace Turno0313
{
    // "El retrato cambia de pose si lo miras dos veces": once active, the portrait still looks normal;
    // the pose changes the first time the player looks away after seeing it, so the second look is wrong.
    public sealed class PortraitAnomaly : Anomaly
    {
        [SerializeField] GameObject normalPose;
        [SerializeField] GameObject anomalousPose;
        [SerializeField] float lookSeconds = .4f;

        float seen;
        bool changed;

        protected override bool Manifested => changed;

        protected override void ShowNormal()
        {
            seen = 0;
            changed = false;
            ShowPose(false);
        }

        protected override void ShowAnomalous()
        {
            seen = 0;
            changed = false;
            ShowPose(false);
        }

        protected internal override void Tick(bool visible, float dt)
        {
            if (!IsActive || changed)
                return;
            if (visible)
                seen += dt;
            else if (seen >= lookSeconds)
            {
                changed = true;
                ShowPose(true);
            }
        }

        void ShowPose(bool anomalous)
        {
            if (normalPose)
                normalPose.SetActive(!anomalous);
            if (anomalousPose)
                anomalousPose.SetActive(anomalous);
        }
    }
}
